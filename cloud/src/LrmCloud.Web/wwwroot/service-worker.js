// LRM Cloud Service Worker
// Provides offline caching for static assets and read-only data

// Version placeholder - replaced by Dockerfile at build time
const SW_VERSION = '__BUILD_VERSION__';
const CACHE_NAME = `lrm-cloud-${SW_VERSION}`;
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

// Install event - cache static assets (don't auto-skipWaiting, let user control)
self.addEventListener('install', (event) => {
    console.log(`[SW ${SW_VERSION}] Installing...`);
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log(`[SW ${SW_VERSION}] Caching static assets`);
                return cache.addAll(STATIC_ASSETS);
            })
        // Note: skipWaiting() removed - now triggered via message from UI
    );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
    console.log(`[SW ${SW_VERSION}] Activating...`);
    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((name) => name !== CACHE_NAME)
                        .map((name) => {
                            console.log(`[SW ${SW_VERSION}] Deleting old cache:`, name);
                            return caches.delete(name);
                        })
                );
            })
            .then(() => self.clients.claim())
    );
});

// External URLs that should bypass service worker entirely
const EXTERNAL_BYPASS = [
    'raw.githubusercontent.com',
    'api.github.com',
    'fonts.googleapis.com',
    'fonts.gstatic.com'
];

// Fetch event - serve from cache, fall back to network
self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET requests
    if (request.method !== 'GET') {
        return;
    }

    // Skip external URLs - let browser handle them directly
    if (EXTERNAL_BYPASS.some(domain => url.hostname.includes(domain))) {
        return;
    }

    // Skip invalid CSS requests (Radzen theme loader with undefined theme)
    if (url.pathname.includes('/-') || url.pathname.includes('/-.')) {
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
    if (event.data?.type === 'SKIP_WAITING') {
        console.log(`[SW ${SW_VERSION}] Activating new version...`);
        self.skipWaiting();
    }
});
