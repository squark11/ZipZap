// Macierz stanów ekranów (E1-3): 8 ekranów × ładowanie / pusty / błąd / brak internetu, z tekstami dla każdego ekranu.
// Wynik: StanyEkranow.dc.html (artboard płótna). Ilustracje z mobile/assets/svg/illustrations.
import { readFileSync, writeFileSync } from 'node:fs';
const [, , svgDir, out] = process.argv;
const ill = (n, w = 150) => readFileSync(`${svgDir}/${n}.svg`, 'utf8').trim()
  .replace(/<svg xmlns="http:\/\/www.w3.org\/2000\/svg" /, '<svg ').replace('role="img"', 'aria-hidden="true"')
  .replace(/<title>[^<]*<\/title>/, '').replace(/width="\d+" height="\d+"/, `width="${w}" height="${Math.round(w * 0.8)}"`);

const CW = 375, CH = 560, GAP = 40, LBL = 170;
const sk = (w, h, r = 8, extra = '') => `<div style="width: ${w}; height: ${h}px; border-radius: ${r}px; background: #E3EBEB; flex: none${extra}"></div>`;
const head = (t, sub) => `<div style="padding: 16px 18px 10px; display: flex; flex-direction: column; gap: 2px"><span style="font: 700 20px Poppins, sans-serif">${t}</span>${sub ? `<span style="font: 500 13px Inter, sans-serif; color: #5B6675">${sub}</span>` : ''}</div>`;
const offlineBar = `<div role="status" style="background: #1F2A37; color: #FFFFFF; padding: 10px 14px; display: flex; align-items: center; gap: 8px; font: 600 13px Inter, sans-serif"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#FFB703" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M3 3l18 18M5 9a12 12 0 0 1 4-2M19 9a12 12 0 0 0-6-2.9M8 13a6 6 0 0 1 3-1.5M12 18h.01"></path></svg>Brak internetu — pokazujemy ostatnio wczytane dane</div>`;
const btn = (t, kind = 'main', dis = false) => {
  const s = dis ? 'background: #E3EBEB; color: #5B6675; border: 0' : kind === 'main' ? 'background: #097778; color: #FFFFFF; border: 0' : 'background: #FFFFFF; color: #1F2A37; border: 1.5px solid #CFE3E3';
  return `<button type="button"${dis ? ' disabled' : ''} style="height: 48px; padding: 0 20px; border-radius: 999px; ${s}; font: 700 15px Inter, sans-serif">${t}</button>`;
};
const center = (art, title, text, action = '') => `<div style="flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; padding: 0 28px; text-align: center">${art}<span style="font: 700 18px Poppins, sans-serif">${title}</span><span style="font: 400 14px/1.5 Inter, sans-serif; color: #5B6675">${text}</span>${action}</div>`;
const card = (inner, extra = '') => `<div style="background: #FFFFFF; border-radius: 18px; padding: 12px 14px; display: flex; flex-direction: column; gap: 8px${extra}">${inner}</div>`;
const skCard = card(sk('70%', 14) + sk('45%', 11));
const note = t => `<div style="margin: 0 16px; padding: 10px 12px; border-radius: 12px; background: #E6F7F7; font: 500 12px/1.45 Inter, sans-serif; color: #097778">${t}</div>`;
const na = t => `<div style="flex: 1; display: flex; align-items: center; justify-content: center; padding: 0 30px; text-align: center; font: 500 14px/1.5 Inter, sans-serif; color: #5B6675; border: 2px dashed #D5E2E2; margin: 16px; border-radius: 18px">${t}</div>`;
const list = (rows) => `<div style="padding: 0 16px; display: flex; flex-direction: column; gap: 10px">${rows.join('')}</div>`;

const rows = [
  ['Start', {
    load: head('Co dziś kupujemy?') + list([sk('100%', 44, 999), sk('100%', 96, 22), skCard, skCard]) + note('Po 4 s bez odpowiedzi: „Budzimy serwer, to potrwa chwilę…”.'),
    empty: head('Sklepy w pobliżu', 'Dostawa do 31-042') + center(ill('state_no_stores'), 'Nikt tu jeszcze nie dowozi', 'Żaden sklep nie obsługuje kodu 31-042. Sprawdź inny adres.', btn('Zmień kod pocztowy')),
    error: head('Sklepy w pobliżu') + center(ill('state_error'), 'Nie udało się wczytać sklepów', 'Serwer nie odpowiedział. Sprawdź połączenie i spróbuj ponownie.', btn('Spróbuj ponownie')),
    off: offlineBar + head('Sklepy w pobliżu') + list([card('<span style="font: 600 15px Poppins, sans-serif">Lewiatan — Podwawelskie</span><span style="font: 500 13px Inter, sans-serif; color: #5B6675">min. 15,00 zł</span>'), card('<span style="font: 600 15px Poppins, sans-serif">Rapacz — Rynek</span><span style="font: 500 13px Inter, sans-serif; color: #5B6675">min. 20,00 zł</span>')]) + note('Lista z ostatniego wczytania. Godziny dostaw odświeżymy po powrocie internetu.'),
  }],
  ['Sklep', {
    load: sk('100%', 110, 0) + list([card(sk('60%', 16) + sk('40%', 11)), `<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px">${card(sk('100%', 80, 12) + sk('70%', 12)).repeat(4)}</div>`]),
    empty: head('Lewiatan — Podwawelskie', 'Kategoria: Nabiał') + center(ill('state_no_stores'), 'Brak produktów w tej kategorii', 'Sklep nie dodał jeszcze nabiału. Zajrzyj do innych kategorii.', btn('Pokaż wszystko', 'sec')),
    error: head('Lewiatan — Podwawelskie') + center(ill('state_error'), 'Nie udało się wczytać oferty', 'Serwer nie odpowiedział. Spróbuj ponownie za chwilę.', btn('Spróbuj ponownie')),
    off: offlineBar + head('Lewiatan — Podwawelskie') + list([card('<span style="font: 600 14px Inter, sans-serif">Jabłka · 4,99 zł / kg</span><span style="font: 500 13px Inter, sans-serif; color: #5B6675">Ceny z ostatniego wczytania</span>'), card('<span style="font: 600 14px Inter, sans-serif">Mleko 2% · 3,49 zł / szt.</span>')]) + note('Koszyk jest zapisany na serwerze — dodawanie produktów wróci po odzyskaniu połączenia (przyciski „+” wyłączone).'),
  }],
  ['Produkt', {
    load: list([sk('100%', 180, 22), sk('50%', 20), sk('35%', 13), `<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px">${sk('100%', 60, 14)}${sk('100%', 60, 14)}</div>`, sk('100%', 52, 999)]),
    empty: head('Szynka', 'Lewiatan — Podwawelskie') + center(ill('state_no_stores', 120), 'Chwilowo brak', 'Sklep oznaczył produkt jako niedostępny. Zajrzyj później albo wybierz inny.', btn('Niedostępny', 'main', true)),
    error: head('Jabłka') + list([card('<span style="font: 600 14px Inter, sans-serif">Jak kupujesz? na wagę · 4,99 zł / kg</span>')]) + `<div style="margin: 16px; padding: 12px 14px; border-radius: 16px; background: #FDECEC; color: #B91C1C; font: 600 14px/1.45 Inter, sans-serif">Nie udało się dodać do koszyka. Spróbuj ponownie.</div>` + `<div style="padding: 0 16px">${btn('Dodaj · 9,98 zł')}</div>`,
    off: offlineBar + head('Jabłka') + list([card('<span style="font: 600 14px Inter, sans-serif">4,99 zł / kg · 1,20 zł / szt.</span>')]) + `<div style="padding: 16px">${btn('Dodasz po odzyskaniu połączenia', 'main', true)}</div>`,
  }],
  ['Koszyk', {
    load: head('Twój koszyk') + list([card(sk('100%', 8, 999)), card(sk('80%', 14) + sk('50%', 11)), card(sk('80%', 14) + sk('50%', 11)), sk('100%', 52, 999)]),
    empty: head('Twój koszyk') + center(ill('state_empty_cart'), 'Koszyk czeka na zakupy', 'Wybierz sklep i dodaj produkty — zbierzemy je w jednej dostawie.', btn('Przeglądaj sklepy')),
    error: head('Twój koszyk') + `<div style="margin: 0 16px 10px; padding: 12px 14px; border-radius: 16px; background: #FDECEC; color: #B91C1C; font: 600 14px/1.45 Inter, sans-serif">Nie udało się zmienić ilości. Pokazujemy ostatni zapisany stan koszyka.</div>` + list([card('<span style="font: 600 14px Inter, sans-serif">Jabłka · 2 kg · 9,98 zł</span>')]) + `<div style="padding: 12px 16px">${btn('Spróbuj ponownie', 'sec')}</div>`,
    off: offlineBar + head('Twój koszyk') + list([card('<span style="font: 600 14px Inter, sans-serif">Jabłka · 2 kg · 9,98 zł</span><span style="font: 500 13px Inter, sans-serif; color: #5B6675">Zmiana ilości po powrocie internetu</span>')]) + `<div style="padding: 16px">${btn('Przejdź do zamówienia', 'main', true)}</div>`,
  }],
  ['Zamówienie (checkout)', {
    load: head('Kiedy dowieźć?', 'Krok 2 z 3') + list([sk('100%', 56, 18), `<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px">${sk('100%', 60, 16).repeat(4)}</div>`]),
    empty: head('Kiedy dowieźć?', 'Krok 2 z 3') + center(ill('state_no_orders', 120), 'Brak wolnych terminów', 'Sklep nie ma teraz wolnych terminów dostawy na ten adres. Wróć później albo zmień adres.', btn('Zmień adres', 'sec')),
    error: head('Sprawdź i zamów', 'Krok 3 z 3') + `<div style="margin: 0 16px; padding: 12px 14px; border-radius: 16px; background: #FEF3E2; color: #7A3A06; font: 600 14px/1.45 Inter, sans-serif">Ten termin właśnie się zapełnił. Wybierz inny — koszyk jest bez zmian.</div>` + `<div style="padding: 16px">${btn('Wybierz inny termin')}</div>` + note('Źródło: API zwraca „Wybrany slot dostawy jest już pełny.” / „Nie udało się zarezerwować terminu przy dużym ruchu”.'),
    off: offlineBar + head('Sprawdź i zamów', 'Krok 3 z 3') + `<div style="padding: 16px">${btn('Złożysz po odzyskaniu połączenia', 'main', true)}</div>` + note('Nic nie wysyłamy bez sieci — żadnego „zamówienia w kolejce”.'),
  }],
  ['Zamówienia i śledzenie', {
    load: head('Zamówienia') + list([skCard, skCard, skCard]),
    empty: head('Zamówienia') + center(ill('state_no_orders'), 'Nie masz jeszcze zamówień', 'Tu zobaczysz każde zamówienie i jego drogę do Twoich drzwi.', btn('Przeglądaj sklepy')),
    error: head('Zamówienie') + center(ill('state_error'), 'Nie udało się wczytać zamówienia', 'Twoje zamówienie jest bezpieczne — to tylko problem z połączeniem z serwerem.', btn('Spróbuj ponownie')),
    off: offlineBar + head('Zamówienie', 'Dziś, 17:00–19:00') + list([card('<span style="font: 700 15px Inter, sans-serif; color: #097778">Kompletowane</span><span style="font: 500 13px Inter, sans-serif; color: #5B6675">Stan z 15:02 — odświeżymy po powrocie internetu</span>')]),
  }],
  ['Konto', {
    load: list([`<div style="display: flex; gap: 12px; align-items: center; padding-top: 16px">${sk('52px', 52, 999)}<div style="flex: 1; display: flex; flex-direction: column; gap: 6px">${sk('60%', 16)}${sk('80%', 11)}</div></div>`, sk('100%', 220, 20)]),
    empty: head('Adresy dostawy') + center(ill('state_no_stores', 120), 'Nie masz zapisanych adresów', 'Dodaj adres, a pokażemy tylko sklepy, które pod niego dowożą.', btn('Dodaj adres')),
    error: head('Konto') + center(ill('state_error'), 'Nie udało się wczytać konta', 'Spróbuj ponownie. Jeśli problem wraca, zaloguj się jeszcze raz.', btn('Spróbuj ponownie')),
    off: offlineBar + head('Anna Nowak', 'anna@example.pl') + list([card('<span style="font: 500 14px Inter, sans-serif">Wygląd · Systemowy</span><span style="font: 500 13px Inter, sans-serif; color: #5B6675">Działa bez internetu</span>'), card('<span style="font: 500 14px Inter, sans-serif; color: #5B6675">Zmiana hasła i adresów — po powrocie internetu</span>')]),
  }],
  ['Logowanie', {
    load: head('Witaj ponownie') + list([sk('100%', 52, 12), sk('100%', 52, 12), `<button type="button" disabled style="height: 52px; border: 0; border-radius: 999px; background: #097778; color: #FFFFFF; font: 700 15px Inter, sans-serif; opacity: 0.85">Logowanie…</button>`]) + note('Przycisk zachowuje szerokość; pola zablokowane na czas żądania.'),
    empty: na('Brak stanu „pusty” — ekran logowania zawsze ma formularz.'),
    error: head('Witaj ponownie') + list([`<div style="display: flex; flex-direction: column; gap: 6px"><span style="font: 600 13px Inter, sans-serif">Hasło</span><div style="height: 52px; border-radius: 12px; border: 1.5px solid #B91C1C; background: #FFFFFF"></div><span style="font: 600 13px Inter, sans-serif; color: #B91C1C">Nieprawidłowy e-mail lub hasło.</span></div>`, btn('Zaloguj się'), '<span style="font: 600 14px Inter, sans-serif; color: #097778; text-align: center">Nie pamiętasz hasła?</span>']) + note('Tekst błędu z API (401). Przy limicie prób API zwraca kod 429 bez tekstu — proponowany komunikat: „Za dużo prób — spróbuj za minutę.”'),
    off: offlineBar + head('Witaj ponownie') + list([sk('100%', 52, 12), `<div style="display: flex">${btn('Zalogujesz się po odzyskaniu połączenia', 'main', true)}</div>`]),
  }],
];

const cols = [['load', 'Ładowanie'], ['empty', 'Pusty'], ['error', 'Błąd'], ['off', 'Brak internetu']];
const W = LBL + cols.length * CW + cols.length * GAP, H = 70 + rows.length * (CH + GAP);
const cell = (html) => `<div style="width: ${CW}px; height: ${CH}px; flex: none; background: #F3F8F8; border-radius: 22px; overflow: hidden; display: flex; flex-direction: column; box-shadow: 0 1px 2px rgba(15,40,40,0.06), 0 8px 24px rgba(15,40,40,0.06)">${html}</div>`;
const body = `<div style="width: ${W}px; height: ${H}px; box-sizing: border-box; padding: 0; background: #FFFFFF; display: flex; flex-direction: column; gap: ${GAP}px; font-family: Inter, sans-serif; color: #1F2A37">
<div style="display: flex; gap: ${GAP}px; height: 30px; align-items: flex-end"><div style="width: ${LBL - GAP}px; flex: none"></div>${cols.map(([, t]) => `<div style="width: ${CW}px; flex: none; font: 700 18px Poppins, sans-serif">${t}</div>`).join('')}</div>
${rows.map(([name, st]) => `<div style="display: flex; gap: ${GAP}px; align-items: flex-start"><div style="width: ${LBL - GAP}px; flex: none; font: 700 18px/1.3 Poppins, sans-serif; padding-top: 8px">${name}</div>${cols.map(([k]) => cell(st[k])).join('')}</div>`).join('\n')}
</div>`;
const html = `<!doctype html>
<html lang="pl">
<head>
<meta charset="utf-8">
<title>Stany każdego ekranu</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Poppins:wght@600;700&amp;family=Inter:wght@400;500;600;700&amp;display=swap">
<style>
body{margin:0;font-family:Inter,system-ui,sans-serif;color:#1F2A37}
a{color:#097778}a:hover{color:#065a5b}
</style>
</helmet>
${body}
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{"$preview":{"width":${W},"height":${H}}}'>
class Component extends DCLogic {
  renderVals() { return {}; }
}
</script>
</body>
</html>
`;
writeFileSync(out, html);
console.log('ok', W, 'x', H, html.length, 'B');
