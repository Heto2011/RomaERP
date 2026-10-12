// Service worker for ROMA. It keeps the app itself (page + scripts + styles) available with no internet so a POS terminal
// can still open and ring up tickets; business data is never cached here — the POS keeps its own copy in IndexedDB.
const CACHE = "roma-shell-v1";

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.add("/")).catch(() => {}));
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return; // the API lives on another path/origin: always straight to the network
  if (url.pathname.startsWith("/api/")) return;

  // Page loads: network first (so a deploy shows up immediately), the cached shell when there is no connection.
  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const copy = response.clone();
          caches.open(CACHE).then((cache) => cache.put("/", copy));
          return response;
        })
        .catch(() => caches.match("/"))
    );
    return;
  }

  // Built assets have a content hash in their name, so a cached copy never goes stale: cache first.
  if (url.pathname.startsWith("/assets/") || /\.(?:png|svg|ico|webmanifest|woff2?)$/.test(url.pathname)) {
    event.respondWith(
      caches.match(request).then(
        (hit) =>
          hit ||
          fetch(request).then((response) => {
            if (response.ok) {
              const copy = response.clone();
              caches.open(CACHE).then((cache) => cache.put(request, copy));
            }
            return response;
          })
      )
    );
  }
});
