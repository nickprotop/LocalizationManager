// LRM Cloud Service Worker
// Provides offline caching for static assets and read-only data

const CACHE_NAME = 'lrm-cloud-v2';
const OFFLINE_URL = 'offline.html';

// Static assets to cache immediately (relative to /app/)
const STATIC_ASSETS = [
    './',
    'css/app.css',
    'js/app.js',
    'icon-192.png',
    'icon-512.png',
    'favicon.png',
    'manifest.json'
];

// Install event - cache static assets
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log('[SW] Caching static assets');
                return cache.addAll(STATIC_ASSETS);
            })
            .then(() => self.skipWaiting())
    );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((name) => name !== CACHE_NAME)
                        .map((name) => {
                            console.log('[SW] Deleting old cache:', name);
                            return caches.delete(name);
                        })
                );
            })
            .then(() => self.clients.claim())
    );
});

// Fetch event - serve from cache, fall back to network
self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET requests
    if (request.method !== 'GET') {
        return;
    }

    // Skip API requests - always go to network
    if (url.pathname.startsWith('/api/') || url.pathname.includes('/api/')) {
        event.respondWith(
            fetch(request)
                .catch(() => new Response(
                    JSON.stringify({ error: 'Offline', message: 'You are currently offline' }),
                    {
                        status: 503,
                        headers: { 'Content-Type': 'application/json' }
                    }
                ))
        );
        return;
    }

    // For navigation requests, try network first, then cache
    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request)
                .catch(() => caches.match(request))
                .catch(() => caches.match(OFFLINE_URL))
        );
        return;
    }

    // For static assets, try cache first, then network
    if (
        url.pathname.includes('/css/') ||
        url.pathname.includes('/js/') ||
        url.pathname.includes('/_content/') ||
        url.pathname.includes('/_framework/') ||
        url.pathname.endsWith('.png') ||
        url.pathname.endsWith('.ico') ||
        url.pathname.endsWith('.json') ||
        url.pathname.endsWith('.woff2')
    ) {
        event.respondWith(
            caches.match(request)
                .then((cachedResponse) => {
                    if (cachedResponse) {
                        // Return cached response and update cache in background
                        fetch(request)
                            .then((response) => {
                                if (response.ok) {
                                    caches.open(CACHE_NAME)
                                        .then((cache) => cache.put(request, response));
                                }
                            })
                            .catch(() => {});
                        return cachedResponse;
                    }

                    // Not in cache, fetch from network and cache
                    return fetch(request)
                        .then((response) => {
                            if (response.ok) {
                                const responseClone = response.clone();
                                caches.open(CACHE_NAME)
                                    .then((cache) => cache.put(request, responseClone));
                            }
                            return response;
                        });
                })
        );
        return;
    }

    // Default: network first, then cache
    event.respondWith(
        fetch(request)
            .then((response) => {
                if (response.ok) {
                    const responseClone = response.clone();
                    caches.open(CACHE_NAME)
                        .then((cache) => cache.put(request, responseClone));
                }
                return response;
            })
            .catch(() => caches.match(request))
    );
});

// Handle messages from the main thread
self.addEventListener('message', (event) => {
    if (event.data === 'skipWaiting') {
        self.skipWaiting();
    }
});
