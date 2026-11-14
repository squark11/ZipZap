// Generator ilustracji i ikon kategorii Dowózka.pl (Etap 1, E1-4). Płaskie SVG, bez filtrów, stylów i bitmap
// (zgodne z flutter_svg). Wynik: mobile/assets/svg/illustrations/*.svg i mobile/assets/svg/categories/*.svg.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = process.argv[2];
const C = { teal: '#14B9BA', strong: '#097778', tint: '#E6F7F7', sun: '#FFB703', sunLight: '#FFCE4A', ink: '#1F2A37',
  skin: '#F4C7A1', ground: '#14B9BA', white: '#FFFFFF', red: '#E5484D', green: '#22A35A', muted: '#9FB0B0' };

const svg = (w, h, title, body) =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" role="img"><title>${title}</title>${body}</svg>\n`;
// Tło-plama: półprzezroczysty turkus — czytelny i w trybie jasnym, i w ciemnym.
const blob = (cx, cy, r) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${C.teal}" fill-opacity="0.14"/>`;
const shadow = (cx, cy, rx) => `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="7" fill="${C.teal}" fill-opacity="0.22"/>`;

// Maskotka: dostawca na rowerze ze skrzynką zakupów. (x,y) = lewy-górny róg, skala s.
const courier = (x, y, s = 1) => `<g transform="translate(${x} ${y}) scale(${s})">
<circle cx="40" cy="120" r="20" fill="${C.ink}" stroke="${C.teal}" stroke-width="3"/><circle cx="40" cy="120" r="8" fill="${C.white}"/>
<circle cx="154" cy="120" r="20" fill="${C.ink}" stroke="${C.teal}" stroke-width="3"/><circle cx="154" cy="120" r="8" fill="${C.white}"/>
<path d="M40 120 L74 86 L136 86 L154 120" stroke="${C.strong}" stroke-width="8" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M136 86 L126 54" stroke="${C.strong}" stroke-width="8" stroke-linecap="round"/>
<rect x="114" y="46" width="32" height="9" rx="4.5" fill="${C.ink}"/>
<rect x="4" y="58" width="66" height="50" rx="10" fill="${C.sun}"/>
<path d="M14 74h46M14 88h30" stroke="${C.ink}" stroke-width="4" stroke-linecap="round" stroke-opacity="0.3"/>
<circle cx="22" cy="54" r="9" fill="${C.green}"/><rect x="34" y="40" width="10" height="20" rx="3" fill="${C.white}"/><circle cx="56" cy="52" r="8" fill="${C.red}"/>
<path d="M84 86 C88 60 98 46 112 42" stroke="${C.teal}" stroke-width="16" stroke-linecap="round" fill="none"/>
<path d="M96 82 l28 0" stroke="${C.ink}" stroke-width="10" stroke-linecap="round"/>
<circle cx="116" cy="26" r="16" fill="${C.skin}"/>
<path d="M98 22 a18 16 0 0 1 36 0 z" fill="${C.teal}"/><rect x="128" y="18" width="12" height="5" rx="2.5" fill="${C.strong}"/>
<path d="M110 46 L132 64" stroke="${C.teal}" stroke-width="9" stroke-linecap="round"/>
</g>`;

const cart = (x, y, s = 1, items = true) => `<g transform="translate(${x} ${y}) scale(${s})">
<path d="M0 6h14l12 52h58l12-38H22" stroke="${C.strong}" stroke-width="7" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
<circle cx="34" cy="74" r="8" fill="${C.ink}" stroke="${C.teal}" stroke-width="3"/><circle cx="78" cy="74" r="8" fill="${C.ink}" stroke="${C.teal}" stroke-width="3"/>
${items ? `<circle cx="44" cy="12" r="11" fill="${C.green}"/><rect x="58" y="-6" width="14" height="28" rx="4" fill="${C.white}" stroke="${C.teal}" stroke-width="3"/><circle cx="84" cy="12" r="10" fill="${C.red}"/>` : ''}
</g>`;

const ill = {
  mascot: svg(240, 180, 'Dostawca Dowózka.pl na rowerze z zakupami',
    blob(130, 86, 82) + shadow(124, 168, 96) + courier(26, 28, 1)),

  onboarding_1_sklepy: svg(240, 200, 'Lokalne sklepy z Twojej okolicy', blob(120, 100, 92) + shadow(120, 186, 100) +
    `<rect x="30" y="70" width="80" height="110" rx="8" fill="${C.white}"/><path d="M24 70 h92 l-8 -22 h-76 z" fill="${C.sun}"/>
<rect x="44" y="88" width="24" height="30" rx="3" fill="${C.tint}"/><rect x="74" y="88" width="24" height="30" rx="3" fill="${C.tint}"/><rect x="58" y="134" width="26" height="46" rx="3" fill="${C.strong}"/>
<rect x="124" y="92" width="90" height="88" rx="8" fill="${C.white}"/><path d="M118 92 h102 l-8 -20 h-86 z" fill="${C.teal}"/>
<rect x="136" y="108" width="66" height="26" rx="3" fill="${C.tint}"/><rect x="156" y="142" width="26" height="38" rx="3" fill="${C.strong}"/>
<circle cx="190" cy="40" r="16" fill="${C.sun}"/>`),

  onboarding_2_rundy: svg(240, 200, 'Zakupy robimy o stałych godzinach', blob(120, 100, 92) + shadow(120, 186, 90) +
    `<circle cx="104" cy="96" r="62" fill="${C.white}" stroke="${C.strong}" stroke-width="8"/>
<path d="M104 52v8M104 132v8M60 96h8M140 96h8" stroke="${C.ink}" stroke-width="5" stroke-linecap="round"/>
<path d="M104 96 L104 64 M104 96 L128 108" stroke="${C.ink}" stroke-width="7" stroke-linecap="round"/><circle cx="104" cy="96" r="6" fill="${C.sun}"/>` +
    cart(146, 112, 0.8)),

  onboarding_3_dostawa: svg(240, 200, 'Dostawa pod same drzwi', blob(120, 100, 92) + shadow(120, 186, 104) +
    `<rect x="150" y="60" width="64" height="120" rx="6" fill="${C.white}"/><rect x="164" y="96" width="36" height="84" rx="4" fill="${C.strong}"/><circle cx="192" cy="140" r="3.5" fill="${C.sun}"/>
<path d="M150 60 l32 -26 l32 26 z" fill="${C.sun}"/>` + courier(6, 64, 0.72)),

  state_empty_cart: svg(200, 160, 'Pusty koszyk', blob(100, 78, 70) + shadow(98, 146, 70) + cart(50, 50, 1, false) +
    `<path d="M150 30 q10 -12 22 -4" stroke="${C.sun}" stroke-width="5" fill="none" stroke-linecap="round"/><circle cx="160" cy="48" r="4" fill="${C.sun}"/>`),

  state_no_stores: svg(200, 160, 'Brak sklepów w okolicy', blob(100, 78, 70) + shadow(100, 146, 76) +
    `<rect x="44" y="64" width="112" height="78" rx="8" fill="${C.white}"/><path d="M36 64 h128 l-10 -26 h-108 z" fill="${C.teal}"/>
<rect x="60" y="80" width="80" height="24" rx="4" fill="${C.tint}"/><rect x="86" y="112" width="28" height="30" rx="3" fill="${C.muted}"/>
<rect x="64" y="84" width="72" height="16" rx="3" fill="${C.sun}"/><path d="M76 92h48" stroke="${C.ink}" stroke-width="4" stroke-linecap="round"/>`),

  state_no_orders: svg(200, 160, 'Brak zamówień', blob(100, 78, 70) + shadow(100, 146, 60) +
    `<path d="M62 24 h76 v118 l-12 -8 l-13 8 l-13 -8 l-13 8 l-13 -8 l-12 8 z" fill="${C.white}" stroke="${C.strong}" stroke-width="6" stroke-linejoin="round"/>
<path d="M78 52h44M78 72h44M78 92h26" stroke="${C.teal}" stroke-width="6" stroke-linecap="round"/><circle cx="140" cy="36" r="16" fill="${C.sun}"/><path d="M134 36h12M140 30v12" stroke="${C.ink}" stroke-width="4" stroke-linecap="round"/>`),

  state_error: svg(200, 160, 'Coś poszło nie tak', blob(100, 78, 70) + shadow(98, 146, 70) + cart(52, 52, 1, false) +
    `<g transform="rotate(-12 100 90)"><rect x="128" y="18" width="40" height="40" rx="20" fill="${C.sun}"/><path d="M148 28v14" stroke="${C.ink}" stroke-width="6" stroke-linecap="round"/><circle cx="148" cy="50" r="3.5" fill="${C.ink}"/></g>`),

  state_offline: svg(200, 160, 'Brak połączenia z internetem', blob(100, 78, 70) + shadow(100, 146, 60) +
    `<path d="M48 76 a76 76 0 0 1 104 0" stroke="${C.muted}" stroke-width="10" fill="none" stroke-linecap="round"/>
<path d="M66 96 a50 50 0 0 1 68 0" stroke="${C.muted}" stroke-width="10" fill="none" stroke-linecap="round"/>
<path d="M84 116 a24 24 0 0 1 32 0" stroke="${C.strong}" stroke-width="10" fill="none" stroke-linecap="round"/><circle cx="100" cy="134" r="8" fill="${C.strong}"/>
<path d="M54 40 L146 140" stroke="${C.sun}" stroke-width="10" stroke-linecap="round"/>`),

  state_success: svg(200, 160, 'Zamówienie przyjęte', blob(100, 78, 70) + shadow(100, 146, 56) +
    `<circle cx="100" cy="78" r="48" fill="${C.strong}"/><path d="M78 80 l16 16 l30 -34" stroke="${C.white}" stroke-width="11" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
<circle cx="44" cy="40" r="6" fill="${C.sun}"/><circle cx="160" cy="32" r="5" fill="${C.teal}"/><circle cx="164" cy="118" r="7" fill="${C.sun}"/><path d="M34 112 l10 4 M150 60 l12 -6" stroke="${C.teal}" stroke-width="4" stroke-linecap="round"/>`),
};

// Ikony kategorii: kreska 1.8, currentColor, siatka 24×24 — kolor nadaje aplikacja (tło chipa / stan wybrany).
const icon = (title, d) => `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" role="img"><title>${title}</title>${d}</svg>\n`;
const cats = {
  wszystko: icon('Wszystko', '<path d="M4 9h16l-1.5 10h-13z"/><path d="M9 9l3-5 3 5"/>'),
  owoce: icon('Owoce', '<circle cx="12" cy="14" r="7"/><path d="M12 7c0-2 1-3 3-4"/><path d="M13 5c2-1.5 4-1 5 0-1.5 1.5-3.5 1.5-5 0z"/>'),
  warzywa: icon('Warzywa', '<path d="M7 21c-1-4 2-11 9-13l1 1c-2 7-9 10-10 12z"/><path d="M16 8l3-4M16 8l4-1M16 8l1-4"/><path d="M10 15l2 1M12 12l2 1"/>'),
  pieczywo: icon('Pieczywo', '<path d="M5 12c0-4 3-7 7-7s7 3 7 7v6H5z"/><path d="M9 9v3M12 8v4M15 9v3"/>'),
  nabial: icon('Nabiał', '<path d="M8 3h8v4l2 3v11H6V10l2-3z"/><path d="M6 13h12"/>'),
  mieso: icon('Mięso i wędliny', '<path d="M5 14c0-4 4-8 9-8 3 0 5 2 5 5 0 5-5 8-9 8-3 0-5-2-5-5z"/><circle cx="15" cy="10" r="1.5"/>'),
  ryby: icon('Ryby', '<path d="M3 12c3-4 8-5 12-3l5-3v12l-5-3c-4 2-9 1-12-3z"/><circle cx="8" cy="11" r="0.8"/>'),
  napoje: icon('Napoje', '<path d="M10 2h4v3l2 3v13H8V8l2-3z"/><path d="M8 13h8"/>'),
  slodycze: icon('Słodycze', '<rect x="8" y="8" width="8" height="8" rx="2"/><path d="M8 10L4 8v8l4-2M16 10l4-2v8l-4-2"/>'),
  mrozonki: icon('Mrożonki', '<path d="M12 3v18M4.5 7.5l15 9M19.5 7.5l-15 9"/><path d="M9 4l3 2 3-2M9 20l3-2 3 2"/>'),
  chemia: icon('Chemia i dom', '<path d="M9 8h6l1 4v9H8v-9z"/><path d="M10 8V5h4l3 1"/><path d="M8 15h8"/>'),
  dziecko: icon('Dla dziecka', '<path d="M9 3h6v3H9z"/><path d="M8 6h8l1 4v10a1 1 0 0 1-1 1H8a1 1 0 0 1-1-1V10z"/><path d="M7 13h10"/>'),
};

for (const [dir, set] of [['illustrations', ill], ['categories', cats]]) {
  const out = join(ROOT, dir);
  mkdirSync(out, { recursive: true });
  for (const [name, content] of Object.entries(set)) writeFileSync(join(out, `${name}.svg`), content);
}
console.log('ok', Object.keys(ill).length, 'ilustracji,', Object.keys(cats).length, 'ikon kategorii');
