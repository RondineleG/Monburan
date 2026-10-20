// Monburan Service Worker - PWA Support
// Network First Strategy with basic offline caching

// Nomes novos a cada mudança de regra: o activate apaga os caches antigos (o v8 guardava páginas logadas)
const CACHE_NAME = 'monburan-v12';
const RUNTIME_CACHE = 'monburan-runtime-v12';

// Só arquivos estáticos entram no cache. A navegação aprimorada do Blazor busca as páginas por fetch (não por "navigate"),
// então páginas logadas (currículos, candidaturas, a chave do autenticador) ficavam guardadas no navegador depois de sair da conta
const CACHEABLE_DESTINATIONS = ['style', 'script', 'image', 'font', 'manifest'];

// Assets to cache on install
const PRECACHE_ASSETS = [
    '/',
    '/app.css',
    '/ui.css',
    '/manifest.json',
    '/favicon-16x16.png',
    '/favicon-32x32.png',
    '/icon-192.png',
    '/icon-512.png',
    '/_framework/blazor.web.js'
];

// Install event - precache basic assets
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                return cache.addAll(PRECACHE_ASSETS);
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
                        .filter((cacheName) => {
                            return cacheName !== CACHE_NAME && cacheName !== RUNTIME_CACHE;
                        })
                        .map((cacheName) => caches.delete(cacheName))
                );
            })
            .then(() => self.clients.claim())
    );
});

// Fetch event - network first strategy
self.addEventListener('fetch', (event) => {
    // Skip cross-origin requests
    if (!event.request.url.startsWith(self.location.origin)) {
        return;
    }

    // Skip Blazor SignalR connections
    if (event.request.url.includes('/_blazor')) {
        return;
    }

    // Never cache document responses: a transient router response must not
    // become the page users see after a later deployment.
    if (event.request.mode === 'navigate') {
        return;
    }

    if (event.request.method !== 'GET' || !CACHEABLE_DESTINATIONS.includes(event.request.destination)) {
        return;
    }

    // Network first strategy
    event.respondWith(
        fetch(event.request)
            .then((response) => {
                // Cache successful responses
                if (response && response.status === 200 && response.type === 'basic') {
                    const responseToCache = response.clone();
                    caches.open(RUNTIME_CACHE)
                        .then((cache) => {
                            cache.put(event.request, responseToCache);
                        });
                }
                return response;
            })
            .catch(() => {
                // Fallback to cache if network fails
                return caches.match(event.request)
                    .then((response) => {
                        if (response) {
                            return response;
                        }
                        // Fallback to offline page for navigation requests
                        if (event.request.mode === 'navigate') {
                            return caches.match('/');
                        }
                        return null;
                    });
            })
    );
});

// Push notification handler (optional)
self.addEventListener('push', (event) => {
    if (!event.data) {
        return;
    }

    let data;
    try {
        data = event.data.json();
    } catch (e) {
        data = { title: 'Monburan', body: 'Nova notificação' };
    }

    const title = data.title || 'Monburan';
    const options = {
        body: data.body || 'Você tem uma nova notificação',
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        data: data.url || '/',
        actions: [
            { action: 'view', title: 'Ver' }
        ]
    };

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

// Notification click handler
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    event.waitUntil(
        clients.openWindow(event.notification.data || '/')
    );
});
