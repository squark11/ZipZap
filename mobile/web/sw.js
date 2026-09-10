// ZipZap PWA — service worker z runtime cache powłoki aplikacji (same-origin).
// Nie cache'uje API (inny origin). Strategia: cache-first z odświeżeniem w tle.
const CACHE = 'zipzap-shell-v1';

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
  if (url.origin !== self.location.origin) return; // API i inne originy — pomijamy

  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    const cached = await cache.match(req);
    const network = fetch(req)
      .then((resp) => {
        if (resp && resp.status === 200 && resp.type === 'basic') {
          cache.put(req, resp.clone());
        }
        return resp;
      })
      .catch(() => cached);
    return cached || network;
  })());
});
