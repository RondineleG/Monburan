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
    // Botão de recolher: expandido = menu com rótulos visíveis
    function syncPin(rail) {
        var pin = document.querySelector('[data-action="sidebar-pin"]');
        if (pin) pin.setAttribute('aria-expanded', rail ? 'false' : 'true');
    }
    syncPin(document.documentElement.classList.contains('sidebar-rail'));
    function applyStoredUi() {
        // Reaplica tema e rail após enhanced navigation repor os atributos do <html>.
        var theme = null, rail = false;
        try {
            theme = localStorage.getItem('monburan-theme');
            rail = localStorage.getItem('monburan-sidebar') === 'rail';
        } catch (e) { }
        if (theme !== 'dark' && theme !== 'light') {
            theme = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.classList.toggle('sidebar-rail', rail);
        syncPin(rail);
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
            syncPin(collapsed);
            var sidebarState = collapsed ? 'rail' : 'expanded';
            try { localStorage.setItem('monburan-sidebar', sidebarState); } catch (err) { }
            setUiCookie('monburan-sidebar', sidebarState);
            return;
        }
        var sidebar = document.querySelector('.app-sidebar');
        if (!sidebar) return;
        var scrimHit = e.target.closest('.sidebar-scrim');
        if (scrimHit) { setDrawer(false, true); return; }
        if (e.target.closest('[data-testid="mobile-menu-toggle"]')) {
            setDrawer(!sidebar.classList.contains('app-sidebar--open'), false);
            return;
        }
        if (sidebar.classList.contains('app-sidebar--open') &&
            (e.target.closest('.sidebar-close') || e.target.closest('.app-sidebar a[href]'))) {
            setDrawer(false, e.target.closest('.sidebar-close') !== null);
        }
    });
    // Drawer mobile de nível superior: uma função, listeners únicos, foco preso
    var drawerTrigger = null;
    function setDrawer(open, restoreFocus) {
        var sidebar = document.querySelector('.app-sidebar');
        var main = document.querySelector('.app-main');
        var toggle = document.querySelector('.menu-toggle');
        if (window.innerWidth > 980) open = false;
        if (open) drawerTrigger = drawerTrigger || toggle;
        if (sidebar) sidebar.classList.toggle('app-sidebar--open', open);
        document.body.classList.toggle('drawer-open', open);
        if (main) main.inert = !!open;
        if (toggle) toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open && sidebar) {
            var first = sidebar.querySelector('.sidebar-close') || sidebar.querySelector('a[href],button:not([disabled])');
            if (first) first.focus();
        }
        if (!open && restoreFocus && drawerTrigger) { drawerTrigger.focus(); drawerTrigger = null; }
    }
    document.addEventListener('keydown', function (e) {
        var sidebar = document.querySelector('.app-sidebar--open');
        if (!sidebar) return;
        if (e.key === 'Escape') { e.preventDefault(); setDrawer(false, true); return; }
        if (e.key !== 'Tab') return;
        var items = Array.from(sidebar.querySelectorAll('a[href],button:not([disabled])'))
            .filter(function (el) { return el.getClientRects().length > 0; });
        if (!items.length) return;
        var first = items[0], last = items[items.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    });
    window.addEventListener('resize', function () {
        if (window.innerWidth > 980) setDrawer(false);
    });
    window.addEventListener('pageshow', function () { setDrawer(false); });
    if (window.Blazor && window.Blazor.addEventListener) {
        try { window.Blazor.addEventListener('enhancedload', function () { setDrawer(false); }); } catch (e) { }
    }
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
