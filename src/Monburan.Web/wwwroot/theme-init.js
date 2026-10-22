// Tema, sidebar e favicon antes do primeiro paint (evita o flash). Arquivo próprio para o CSP não precisar de 'unsafe-inline'.
// Aplica tema e estado da sidebar antes do primeiro paint (evita flash).
(function () {
    // Sem escolha salva, segue o tema do sistema operacional
    var theme = null;
    try { theme = localStorage.getItem('monburan-theme'); } catch (e) { }
    if (theme !== 'dark' && theme !== 'light') {
        theme = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-theme', theme);
    var rail = false;
    try { rail = localStorage.getItem('monburan-sidebar') === 'rail'; } catch (e) { }
    if (rail) document.documentElement.classList.add('sidebar-rail');
    else document.documentElement.classList.remove('sidebar-rail');
})();

// Troca favicon + theme-color conforme o tema ativo.
(function () {
    var isDark = (document.documentElement.getAttribute('data-theme') || 'light') === 'dark';
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute('content', isDark ? '#0A0806' : '#F2EBD9');
    var f32 = document.getElementById('favicon-32');
    var f16 = document.getElementById('favicon-16');
    if (f32) f32.href = isDark ? '/favicon-dark-32x32.png' : '/favicon-32x32.png';
    if (f16) f16.href = isDark ? '/favicon-dark-16x16.png' : '/favicon-16x16.png';
})();
