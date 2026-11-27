// Zakłada 4 prawdziwe lokalizacje Rapacz Market (dane od właściciela 2026-11-14, współrzędne z OpenStreetMap)
// przez publiczne API administratora — bez zmian w kodzie serwera. Idempotentne: istniejący sklep (po slug) jest
// uzupełniany, nie dublowany.
//
// Użycie (Node ≥ 18):
//   API=http://localhost:5080/api ADMIN_EMAIL=... ADMIN_PASSWORD=... node docs/rapacz/seed-rapacz.mjs [--fee=0] [--days=7]
//
// UWAGA: asortyment jest PRZYKŁADOWY (Rapacz nie publikuje katalogu) — do zastąpienia eksportem z programu kasowego
// (panel → Oferta → Import). Opłata za dostawę (--fee) i terminy są do ustalenia z Rapaczem (docs/rapacz/README.md).
// Na produkcji uruchamiać tylko za zgodą właściciela.
const args = Object.fromEntries(process.argv.slice(2).map((a) => a.replace(/^--/, '').split('=')));
const API = process.env.API ?? 'http://localhost:5080/api';
const FEE = Number(args.fee ?? 0);
const DAYS = Number(args.days ?? 7);

const STORES = [
  { slug: 'rapacz-raba-wyzna', name: 'Rapacz Market — Raba Wyżna', city: 'Raba Wyżna', address: 'Raba Wyżna 557A, 34-721 Raba Wyżna',
    lat: 49.5661779, lng: 19.8805591, // OSM: „Delikatesy Rapacz", 557a
    zone: 'Gmina Raba Wyżna', postal: ['34-721', '34-723', '34-713'] }, // Raba Wyżna, Podsarnie, Harkabuz, Rokiciny P., Bukowina-Os. / Sieniawa, Bielanka / Skawa
  { slug: 'rapacz-spytkowice', name: 'Rapacz Market — Spytkowice', city: 'Spytkowice', address: 'Spytkowice 14B, 34-745 Spytkowice',
    lat: 49.577421, lng: 19.8254052, zone: 'Spytkowice', postal: ['34-745'] },
  { slug: 'rapacz-rabka-zdroj', name: 'Rapacz Market — Rabka-Zdrój', city: 'Rabka-Zdrój', address: 'ul. Zakopiańska 6A, 34-700 Rabka-Zdrój',
    lat: 49.6076216, lng: 19.9520946, // OSM: Zakopiańska 6a
    zone: 'Rabka-Zdrój', postal: ['34-700'] },
  { slug: 'rapacz-klikuszowa', name: 'Rapacz Market — Klikuszowa', city: 'Klikuszowa', address: 'Klikuszowa 45B, 34-404 Klikuszowa',
    lat: 49.5199754, lng: 19.9861665, zone: 'Klikuszowa', postal: ['34-404'] },
];

// Przykładowy asortyment (nazwa;kategoria;cena;jednostka;dostepny) i przypięcie kategorii sklepu do kategorii aplikacji.
const SAMPLE = `nazwa;kategoria;cena;jednostka;dostepny
Chleb wiejski 600 g;Pieczywo;6,49;szt;tak
Bułka kajzerka;Pieczywo;0,79;szt;tak
Mleko 2% 1 l;Nabiał;3,49;szt;tak
Masło 82% 200 g;Nabiał;7,99;szt;tak
Jogurt naturalny 400 g;Nabiał;3,29;szt;tak
Ser żółty gouda;Nabiał;3,19;100g;tak
Jaja z wolnego wybiegu 10 szt.;Nabiał;12,99;szt;tak
Jabłka;Owoce i warzywa;4,99;kg;tak
Ziemniaki;Owoce i warzywa;2,49;kg;tak
Pomidory;Owoce i warzywa;9,99;kg;tak
Kiełbasa podhalańska;Mięso i wędliny;3,99;100g;tak
Szynka wieprzowa;Mięso i wędliny;4,49;100g;tak
Woda mineralna 1,5 l;Napoje;1,99;szt;tak
Sok jabłkowy 1 l;Napoje;4,99;szt;tak
Płyn do naczyń 900 ml;Chemia;7,49;szt;tak`;
const PIN = { 'Pieczywo': 'pieczywo', 'Nabiał': 'nabial', 'Owoce i warzywa': 'owoce', 'Mięso i wędliny': 'mieso', 'Napoje': 'napoje', 'Chemia': 'chemia' };

const j = async (r) => { const t = await r.text(); if (!r.ok) throw new Error(`${r.status} ${r.url}: ${t.slice(0, 300)}`); return t ? JSON.parse(t) : null; };
const login = await j(await fetch(`${API}/identity/login`, { method: 'POST', headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ email: process.env.ADMIN_EMAIL, password: process.env.ADMIN_PASSWORD }) }));
const H = { 'content-type': 'application/json', authorization: `Bearer ${login.accessToken}` };
const get = async (p) => j(await fetch(`${API}${p}`, { headers: H }));
const send = async (m, p, b) => j(await fetch(`${API}${p}`, { method: m, headers: H, body: b === undefined ? undefined : JSON.stringify(b) }));

const logoBase = API.replace(/\/api$/, '');
const pad = (n) => String(n).padStart(2, '0');
const iso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

for (const s of STORES) {
  const all = await get('/catalog/stores?onlyActive=false');
  let store = all.find((x) => x.slug === s.slug);
  if (!store) {
    store = await send('POST', '/catalog/stores', { name: s.name, slug: s.slug, city: s.city, address: s.address,
      commissionRate: 0.10, minimumOrderValue: 0, logoUrl: `${logoBase}/mock/stores/rapacz.png`, latitude: s.lat, longitude: s.lng });
  }
  await send('PATCH', `/catalog/stores/${store.id}`, { latitude: s.lat, longitude: s.lng, status: 'Open',
    deliveryNote: 'Dostawę realizuje Dowózka.pl' });

  // Strefa dostaw z kodami pocztowymi (jedna na lokalizację).
  const zones = await get(`/ordering/stores/${store.id}/zones`);
  let zone = zones.find((z) => z.name === s.zone);
  if (!zone) zone = await send('POST', `/ordering/stores/${store.id}/zones`, { name: s.zone, deliveryFee: FEE, postalCodes: s.postal });

  // Terminy dostaw po rundach zakupowych 12:00 i 16:00 (pon–sob), następne DAYS dni.
  const slots = await get(`/ordering/stores/${store.id}/slots?zoneId=${zone.id}`);
  const have = new Set(slots.map((t) => `${t.date} ${String(t.startTime).slice(0, 5)}`));
  for (let i = 0; i <= DAYS; i++) {
    const d = new Date(); d.setDate(d.getDate() + i);
    if (d.getDay() === 0) continue; // niedziela
    for (const [start, end] of [['13:00', '15:00'], ['17:00', '19:00']]) {
      if (have.has(`${iso(d)} ${start}`)) continue;
      await send('POST', `/ordering/stores/${store.id}/slots`, { deliveryZoneId: zone.id, date: iso(d), startTime: `${start}:00`, endTime: `${end}:00`, maxOrders: 10 }).catch(() => {});
    }
  }

  // Przykładowy asortyment + przypięcie kategorii (import aktualizuje po nazwie — bez dubli).
  await send('POST', `/catalog/stores/${store.id}/products/import?commit=true`, { content: SAMPLE });
  for (const c of await get(`/catalog/stores/${store.id}/categories`)) {
    if (PIN[c.name] && c.platformCategoryKey !== PIN[c.name]) await send('PUT', `/catalog/categories/${c.id}/platform-category`, { key: PIN[c.name] });
  }
  const prods = await get(`/catalog/stores/${store.id}/products`);
  console.log(`${s.name}: id=${store.id}, strefa ${s.postal.join(', ')} (opłata ${FEE} zł), produktów ${prods.length}`);
}
