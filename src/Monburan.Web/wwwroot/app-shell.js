// Comportamentos do layout (tema, sidebar, menu mobile, sinal de prontidão do Blazor) e o service worker do PWA.
// Arquivo próprio para o CSP não precisar de 'unsafe-inline' nos scripts.
// Navegação mobile e sinal de prontidão — delegados porque o layout é SSR.
(function () {
    function markReady() {
        var shell = document.querySelector('[data-testid="app-shell"]');
        if (shell) shell.setAttribute('data-blazor-ready', 'true');
    }
    function syncMenu() {
        var sidebar = document.querySelector('.app-sidebar');
        var toggle = document.querySelector('[data-testid="mobile-menu-toggle"]');
        if (sidebar && toggle) {
            toggle.setAttribute('aria-expanded', sidebar.classList.contains('app-sidebar--open') ? 'true' : 'false');
        }
    }
    function setUiCookie(name, value) {
        try {
            document.cookie = name + '=' + encodeURIComponent(value) + ';path=/;max-age=31536000;samesite=lax';
        } catch (e) { }
    }
    function applyStoredUi() {
        // Reaplica tema e rail após enhanced navigation repor os atributos do <html>.
        var theme = 'dark', rail = true;
        try {
            theme = localStorage.getItem('monburan-theme') || 'dark';
            rail = localStorage.getItem('monburan-sidebar') !== 'expanded';
        } catch (e) { }
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.classList.toggle('sidebar-rail', rail);
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', theme === 'dark' ? '#0A0806' : '#F2EBD9');
    }
    if (window.Blazor && Blazor.addEventListener) {
        Blazor.addEventListener('enhancedload', function () { applyStoredUi(); markReady(); });
    }
    // Sinal de prontidão: quando o socket /_blazor abre, os componentes
    // interativos anexam em seguida; fallback cobre páginas sem circuito.
    var NativeWS = window.WebSocket;
    if (NativeWS) {
        var WSProxy = function (url, protocols) {
            var ws = protocols !== undefined ? new NativeWS(url, protocols) : new NativeWS(url);
            if (typeof url === 'string' && url.indexOf('/_blazor') !== -1) {
                ws.addEventListener('open', function () { setTimeout(markReady, 700); });
            }
            return ws;
        };
        WSProxy.prototype = NativeWS.prototype;
        ['CONNECTING', 'OPEN', 'CLOSING', 'CLOSED'].forEach(function (k) { WSProxy[k] = NativeWS[k]; });
        window.WebSocket = WSProxy;
    }
    window.addEventListener('load', function () { setTimeout(markReady, 1500); });
    document.addEventListener('click', function (e) {
        var themeBtn = e.target.closest('[data-action="theme-toggle"]');
        if (themeBtn) {
            var html = document.documentElement;
            var next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            html.setAttribute('data-theme', next);
            try { localStorage.setItem('monburan-theme', next); } catch (err) { }
            setUiCookie('monburan-theme', next);
            var meta = document.querySelector('meta[name="theme-color"]');
            if (meta) meta.setAttribute('content', next === 'dark' ? '#0A0806' : '#F2EBD9');
            var f32 = document.getElementById('favicon-32');
            var f16 = document.getElementById('favicon-16');
            if (f32) f32.href = next === 'dark' ? '/favicon-dark-32x32.png' : '/favicon-32x32.png';
            if (f16) f16.href = next === 'dark' ? '/favicon-dark-16x16.png' : '/favicon-16x16.png';
            return;
        }
        var pinBtn = e.target.closest('[data-action="sidebar-pin"]');
        if (pinBtn) {
            var collapsed = document.documentElement.classList.toggle('sidebar-rail');
            pinBtn.setAttribute('aria-pressed', collapsed ? 'false' : 'true');
            var sidebarState = collapsed ? 'rail' : 'expanded';
            try { localStorage.setItem('monburan-sidebar', sidebarState); } catch (err) { }
            setUiCookie('monburan-sidebar', sidebarState);
            return;
        }
        var sidebar = document.querySelector('.app-sidebar');
        if (!sidebar) return;
        if (e.target.closest('[data-testid="mobile-menu-toggle"]')) {
            sidebar.classList.toggle('app-sidebar--open');
            syncMenu();
            return;
        }
        if (sidebar.classList.contains('app-sidebar--open') &&
            (e.target.closest('.sidebar-close') || e.target.closest('.sidebar-scrim') || e.target.closest('.app-sidebar a'))) {
            sidebar.classList.remove('app-sidebar--open');
            syncMenu();
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            var sidebar = document.querySelector('.app-sidebar--open');
            if (sidebar) { sidebar.classList.remove('app-sidebar--open'); syncMenu(); }
        }
    });
})();

// Register service worker for PWA
if ('serviceWorker' in navigator) {
    window.addEventListener('load', function() {
    navigator.serviceWorker.register('/service-worker.js')
            .then(function(registration) {
                return registration.update();
            })
            .then(function(registration) {
                console.log('ServiceWorker registration successful');
            })
            .catch(function(err) {
                console.log('ServiceWorker registration failed');
            });
    });
}
