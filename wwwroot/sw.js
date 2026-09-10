// Service worker de Wamani (PWA). Estrategia "primero la red": siempre trae lo último
// del servidor (datos y páginas al día); si no hay internet, usa lo que quedó en caché.
// Guarda en caché lo estático (CSS, logo) a medida que se usa, para poder abrir offline.
const CACHE = 'wamani-v2';
const ESTATICOS = [
  '/css/site.css',
  '/logo/logo-completo.webp',
  '/logo/montana.png',
  '/logo/app-192.png',
  '/logo/app-512.png'
];

self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(ESTATICOS)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys()
      .then((ks) => Promise.all(ks.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (e) => {
  const req = e.request;
  if (req.method !== 'GET') return;   // no tocar POST (login, formularios)

  e.respondWith(
    fetch(req)
      .then((res) => {
        // Guardar en caché lo estático (css/logo) para poder abrir sin internet
        const url = new URL(req.url);
        if (url.pathname.startsWith('/css/') || url.pathname.startsWith('/logo/')) {
          const copia = res.clone();
          caches.open(CACHE).then((c) => c.put(req, copia));
        }
        return res;
      })
      .catch(() => caches.match(req, { ignoreSearch: true }))
  );
});
