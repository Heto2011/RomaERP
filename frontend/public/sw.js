// Minimal service worker: no caching, pure network passthrough.
// Its only job is to satisfy the browser's "installable web app" requirement
// for ROMA People / ROMA Restaurant (Add to Home Screen); the data here changes
// too often to safely cache offline.
self.addEventListener("install", () => {
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener("fetch", (event) => {
  event.respondWith(fetch(event.request));
});
