// Dowózka.pl PWA — service worker: offline-powłoka aplikacji (same-origin). API (inny origin) nie jest cache'owane.
//
// Strategia NETWORK-FIRST: online zawsze pobieramy aktualne pliki (po wdrożeniu nowej wersji klient z kodu QR nie
// dostaje starej aplikacji ani pomieszanych wersji plików), a z pamięci podręcznej korzystamy tylko bez sieci.
// Nawigacje (np. /s/{slug} z kodu QR) bez sieci dostają zapamiętany index.html — trasę obsługuje aplikacja.
const CACHE = 'dowozka-shell-v2';

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

  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    try {
      const resp = await fetch(req);
      if (resp && resp.status === 200 && resp.type === 'basic') {
        // Nawigacje zapisujemy pod jednym kluczem (index.html) — różne adresy sklepów to ta sama powłoka.
        cache.put(req.mode === 'navigate' ? 'index.html' : req, resp.clone());
      }
      return resp;
    } catch (e) {
      const cached = await cache.match(req.mode === 'navigate' ? 'index.html' : req);
      if (cached) return cached;
      throw e;
    }
  })());
});
