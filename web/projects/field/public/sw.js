/*
 * Service worker of the field app: keeps the app (its page, bundles, fonts and settings) on the device so it opens
 * without a connection. Data is not cached here: visits and reports live in the app's IndexedDB, and API calls always
 * go to the network.
 */
const CACHE = 'dc-field-v1';
const SHELL = ['/', '/index.html', '/config.json', '/manifest.webmanifest', '/icon.svg', '/favicon.ico'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll(SHELL))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

/** Network first, the cached copy when offline (the page and the settings, which change). */
async function networkFirst(request, fallback) {
  const cache = await caches.open(CACHE);
  try {
    const response = await fetch(request);
    if (response.ok) await cache.put(fallback ?? request, response.clone());
    return response;
  } catch (error) {
    const cached = await cache.match(fallback ?? request);
    if (cached) return cached;
    throw error;
  }
}

/** Cached copy first (bundles are named by their content hash, so a copy never goes stale). */
async function cacheFirst(request) {
  const cache = await caches.open(CACHE);
  const cached = await cache.match(request);
  if (cached) return cached;
  const response = await fetch(request);
  if (response.ok) await cache.put(request, response.clone());
  return response;
}

self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin) return;
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/health') || url.pathname.startsWith('/auth/')) return;
  if (request.mode === 'navigate') {
    event.respondWith(networkFirst(request, '/index.html'));
  } else if (url.pathname === '/config.json' || url.pathname === '/manifest.webmanifest') {
    event.respondWith(networkFirst(request));
  } else {
    event.respondWith(cacheFirst(request));
  }
});
