// Dowózka.pl PWA — service worker: offline-powłoka aplikacji (same-origin). API (inny origin) nie jest cache'owane.
//
// Strategia NETWORK-FIRST: online zawsze pobieramy aktualne pliki (po wdrożeniu nowej wersji klient z kodu QR nie
// dostaje starej aplikacji ani pomieszanych wersji plików), a z pamięci podręcznej korzystamy tylko bez sieci.
// `cache: 'no-cache'` wymusza rewalidację także w pamięci HTTP przeglądarki (304 jest tani) — bez tego hosting bez
// nagłówków Cache-Control mógłby heurystycznie podać stary index.html/main.dart.js po wdrożeniu.
// Nawigacje (np. /s/{slug} z kodu QR) bez sieci dostają zapamiętany index.html — trasę obsługuje aplikacja.
// Zakres: katalog główny (sw.js leży w „/" — aplikacja jest budowana z --base-href /).
const CACHE = 'dowozka-shell-v3';

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)));
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return; // API, CDN i inne originy — bez ingerencji

  const navigate = req.mode === 'navigate';
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    try {
      // Dla nawigacji tryb zmienia się na same-origin, a przekierowania zostają „manual" (przeglądarka je wykona).
      const resp = await fetch(req, { cache: 'no-cache' });
      if (resp && resp.status === 200 && resp.type === 'basic') {
        // Nawigacje zapisujemy pod jednym kluczem (index.html) — różne adresy sklepów to ta sama powłoka.
        cache.put(navigate ? 'index.html' : req, resp.clone());
      }
      return resp;
    } catch (e) {
      const cached = await cache.match(navigate ? 'index.html' : req);
      if (cached) return cached;
      throw e;
    }
  })());
});
