"""Sprawdza, czy tekst aplikacji i danych sklepów mieści się w czcionkach z paczki (Inter, Poppins).

Znak spoza czcionek (np. emoji w nazwie produktu) sprawia, że Flutter web dociąga czcionkę zapasową
z fonts.gstatic.com — to jedyna pozostała droga do serwerów Google po Etapie 2.

Użycie (z katalogu repozytorium):
    python docs/redesign/check_glyphs.py [adres_API] [plik_wyniku.json]
Domyślnie API produkcyjne, tylko odczyt publicznych endpointów katalogu. Kod wyjścia 1, gdy są braki.
"""
import glob
import io
import json
import os
import sys
import urllib.request

from fontTools.ttLib import TTFont

API = (sys.argv[1] if len(sys.argv) > 1 else 'https://dowozka-api.onrender.com/api').rstrip('/')
OUT = sys.argv[2] if len(sys.argv) > 2 else None
FONTS = sorted(glob.glob('mobile/assets/fonts/*.ttf'))
cmaps = {os.path.basename(f): set(TTFont(f).getBestCmap()) for f in FONTS}
# Znak musi mieć glif w każdej wadze danej rodziny (tekst bywa pogrubiony); Inter = treść, Poppins = nagłówki.
inter = set.intersection(*[c for n, c in cmaps.items() if n.startswith('Inter')])
poppins = set.intersection(*[c for n, c in cmaps.items() if n.startswith('Poppins')])
IGNORED = {0x0A, 0x0D, 0x09, 0x200B, 0xFEFF}  # znaki sterujące, nie rysowane


def get(path):
    req = urllib.request.Request(API + path, headers={'Accept': 'application/json', 'User-Agent': 'glyph-check'})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read().decode('utf-8'))


def strings(o):
    if isinstance(o, str):
        yield o
    elif isinstance(o, dict):
        for v in o.values():
            yield from strings(v)
    elif isinstance(o, list):
        for v in o:
            yield from strings(v)


found = {}  # znak -> (źródło, przykład)


def scan(source, text):
    for ch in text:
        cp = ord(ch)
        if cp < 0x20 or cp in IGNORED:
            continue
        if cp not in inter or cp not in poppins:
            found.setdefault(ch, (source, text[:60]))


# 1) Teksty w kodzie aplikacji (bez komentarzy)
for p in glob.glob('mobile/lib/**/*.dart', recursive=True):
    for line in io.open(p, encoding='utf-8'):
        code = line.split('//')[0]
        scan(os.path.basename(p), code)

# 2) Dane publiczne katalogu: sklepy i ich produkty
stores = get('/catalog/stores')
counts = {'stores': len(stores), 'products': 0}
for s in stores:
    scan('sklep ' + s.get('name', '?'), ' '.join(strings(s)))
    products = get('/catalog/stores/%s/products' % s['id'])
    items = products if isinstance(products, list) else products.get('items', products.get('products', []))
    counts['products'] += len(items)
    for p in items:
        scan('produkt w ' + s.get('name', '?'), ' '.join(strings(p)))

result = {
    'api': API,
    'fonts': [os.path.basename(f) for f in FONTS],
    'checked': counts,
    'missing': [{'char': ch, 'codepoint': 'U+%04X' % ord(ch), 'source': src, 'sample': sample}
                for ch, (src, sample) in sorted(found.items())],
}
text = json.dumps(result, ensure_ascii=False, indent=2)
if OUT:
    io.open(OUT, 'w', encoding='utf-8').write(text + '\n')
sys.stdout.buffer.write((text + '\n').encode('utf-8'))
sys.exit(1 if result['missing'] else 0)
