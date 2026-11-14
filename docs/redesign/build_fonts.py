"""Czcionki aplikacji Dowózka.pl (E2-2): statyczne grubości Inter (z wersji zmiennej) i Poppins, odchudzone do
alfabetu łacińskiego z polskimi znakami. Wynik: mobile/assets/fonts/*.ttf."""
import os
import sys
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset

SRC = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1]
POPPINS_DIR = os.path.join(os.environ["LOCALAPPDATA"], "Microsoft", "Windows", "Fonts")
# Łacina podstawowa + Latin-1 (ó) + Latin Extended-A (ąćęłńśźż) + typografia (– — „ ” … · • ←→↑↓ − ✓ ✕ €).
UNICODES = "U+0020-007E,U+00A0-00FF,U+0100-017F,U+2010-2027,U+2030-203A,U+20AC,U+2122,U+2190-2193,U+2212,U+2713,U+2715"
WEIGHTS = {400: "Regular", 500: "Medium", 600: "SemiBold", 700: "Bold"}


def do_subset(font: TTFont, path: str) -> None:
    opts = subset.Options()
    opts.layout_features = ["kern", "liga", "calt", "tnum", "ccmp", "locl", "mark", "mkmk"]
    opts.name_IDs = ["*"]
    opts.notdef_outline = True
    opts.hinting = False
    s = subset.Subsetter(opts)
    s.populate(unicodes=subset.parse_unicodes(UNICODES))
    s.subset(font)
    font.save(path)


os.makedirs(OUT, exist_ok=True)
for wght, name in WEIGHTS.items():
    vf = TTFont(os.path.join(SRC, "Inter-VF.ttf"))
    static = instancer.instantiateVariableFont(vf, {"wght": wght, "opsz": 14}, updateFontNames=True)
    do_subset(static, os.path.join(OUT, f"Inter-{name}.ttf"))
    do_subset(TTFont(os.path.join(POPPINS_DIR, f"Poppins-{name}.ttf")), os.path.join(OUT, f"Poppins-{name}.ttf"))

for f in sorted(os.listdir(OUT)):
    print(f, os.path.getsize(os.path.join(OUT, f)))
