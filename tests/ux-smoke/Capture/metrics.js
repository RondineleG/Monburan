// Métricas de UX de uma página já renderizada (somente leitura): contraste de cada texto visível sobre o fundo
// efetivo, texto < 12px, alvos de toque, campos sem rótulo, controles sem nome, ids duplicados, overflow horizontal
// e estrutura de títulos. Avaliado pelo Playwright com page.EvaluateAsync.
() => {
  const cv = document.createElement('canvas'); cv.width = cv.height = 1;
  const cx = cv.getContext('2d', { willReadFrequently: true });
  const rgba = (c) => { cx.clearRect(0, 0, 1, 1); cx.fillStyle = '#000'; cx.fillStyle = c; cx.fillRect(0, 0, 1, 1); const d = cx.getImageData(0, 0, 1, 1).data; return [d[0], d[1], d[2], d[3] / 255]; };
  const lum = ([r, g, b]) => { const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
  const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  const blend = (top, bottom) => { const a = top[3]; return [top[0] * a + bottom[0] * (1 - a), top[1] * a + bottom[1] * (1 - a), top[2] * a + bottom[2] * (1 - a), 1]; };
  const bgOf = (el) => {
    const stack = []; let n = el;
    while (n && n.nodeType === 1) { const c = rgba(getComputedStyle(n).backgroundColor); if (c[3] > 0) { stack.push(c); if (c[3] >= 1) break; } n = n.parentElement; }
    let base = rgba(getComputedStyle(document.documentElement).backgroundColor);
    if (base[3] === 0) base = [255, 255, 255, 1];
    for (let i = stack.length - 1; i >= 0; i--) base = blend(stack[i], base); return base;
  };
  const visible = (el) => { if (el.closest('details:not([open])') && !el.closest('summary')) return false; const r = el.getBoundingClientRect(); const cs = getComputedStyle(el); return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none' && parseFloat(cs.opacity) > 0.05; };
  const label = (el) => (el.getAttribute('aria-label') || el.innerText || el.value || '').trim().replace(/\s+/g, ' ').slice(0, 40);
  const sel = (el) => el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') + (el.classList.length ? '.' + [...el.classList].slice(0, 2).join('.') : '');

  const lowContrast = [], smallText = [];
  const seen = new Set();
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  while (walker.nextNode()) {
    const t = walker.currentNode; if (!t.textContent.trim()) continue;
    const el = t.parentElement; if (!el || seen.has(el) || !visible(el)) continue; seen.add(el);
    if (el.closest('[aria-hidden=true], .cv-sheet, script, style, noscript, .skip-link, #blazor-error-ui')) continue;
    const cs = getComputedStyle(el); const fs = parseFloat(cs.fontSize); const fw = parseInt(cs.fontWeight) || 400;
    const fg = rgba(cs.color); const bg = bgOf(el); const col = blend([fg[0], fg[1], fg[2], fg[3] * parseFloat(cs.opacity || 1)], bg);
    const r = ratio(col, bg); const large = fs >= 24 || (fs >= 18.66 && fw >= 700);
    const disabled = el.closest(':disabled, [aria-disabled=true]');
    if (!disabled && r < (large ? 3 : 4.5)) lowContrast.push({ el: sel(el), text: t.textContent.trim().slice(0, 40), ratio: +r.toFixed(2), fs });
    if (fs < 12) smallText.push({ el: sel(el), text: t.textContent.trim().slice(0, 30), fs });
  }

  const interactive = [...document.querySelectorAll('a[href], button, input:not([type=hidden]), select, textarea, summary, [role=button], [role=menuitem], [role=tab], [tabindex]:not([tabindex="-1"])')].filter(visible).filter(el => !el.closest('#blazor-error-ui'));
  // Alvo < 24px sem a exceção de texto corrido nem a de controle dentro de rótulo clicável (WCAG 2.5.8)
  const smallTargets = interactive.filter(el => {
    const r = el.getBoundingClientRect();
    if (r.width >= 24 && r.height >= 24) return false;
    if (el.tagName === 'A' && getComputedStyle(el).display === 'inline') return false;
    const lab = el.closest('label') || (el.id && document.querySelector(`label[for="${CSS.escape(el.id)}"]`));
    if (lab) { const lr = lab.getBoundingClientRect(); if (lr.height >= 24) return false; }
    return true;
  }).map(el => { const r = el.getBoundingClientRect(); return { el: sel(el), text: label(el), w: Math.round(r.width), h: Math.round(r.height) }; });
  const under44 = interactive.filter(el => { const r = el.getBoundingClientRect(); return r.height < 44 && getComputedStyle(el).display !== 'inline'; }).length;

  const unlabeled = [...document.querySelectorAll('input:not([type=hidden]):not([type=submit]):not([type=button]), select, textarea')].filter(visible).filter(el => {
    if (el.getAttribute('aria-label') || el.getAttribute('aria-labelledby') || el.title) return false;
    if (el.id && document.querySelector(`label[for="${CSS.escape(el.id)}"]`)) return false;
    if (el.closest('label')) return false; return true;
  }).map(el => ({ el: sel(el), type: el.type, placeholder: el.placeholder }));
  const nameless = interactive.filter(el => el.matches('a[href], button, [role=button]')).filter(el => !label(el) && !el.getAttribute('aria-labelledby') && !el.title && !el.querySelector('img[alt]:not([alt=""])')).map(el => sel(el) + (el.getAttribute('href') ? '[' + el.getAttribute('href') + ']' : ''));

  const ids = {}; document.querySelectorAll('[id]').forEach(e => ids[e.id] = (ids[e.id] || 0) + 1);
  const overflowEls = []; const vw = document.documentElement.clientWidth;
  if (document.documentElement.scrollWidth > vw + 1) document.querySelectorAll('body *').forEach(el => { const r = el.getBoundingClientRect(); if (r.right > vw + 1 && r.width > 0 && overflowEls.length < 6 && visible(el)) { const p = el.parentElement && el.parentElement.getBoundingClientRect(); if (!p || p.right <= vw + 1) overflowEls.push({ el: sel(el), right: Math.round(r.right) }); } });

  const headings = [...document.querySelectorAll('h1,h2,h3,h4,h5,h6')].filter(visible).map(h => h.tagName[1] + ':' + h.innerText.trim().slice(0, 40));
  const skipped = []; let prev = 0; headings.forEach(h => { const l = +h[0]; if (prev && l > prev + 1) skipped.push(`h${prev}->h${l}`); prev = l; });
  const main = document.querySelector('main') || document.body;

  return {
    title: document.title, lang: document.documentElement.lang, theme: document.documentElement.getAttribute('data-theme'),
    h1: headings.filter(h => h.startsWith('1:')).map(h => h.slice(2)), headingSkips: [...new Set(skipped)], headingsSample: headings.slice(0, 14),
    landmarks: { main: document.querySelectorAll('main').length, nav: document.querySelectorAll('nav').length, header: document.querySelectorAll('header').length },
    pageHeight: document.documentElement.scrollHeight, horizontalOverflow: document.documentElement.scrollWidth - vw, overflowEls,
    lowContrastCount: lowContrast.length, lowContrast: lowContrast.sort((a, b) => a.ratio - b.ratio).slice(0, 8),
    smallTextCount: smallText.length, smallText: smallText.slice(0, 6),
    interactiveCount: interactive.length, under44, smallTargets: smallTargets.slice(0, 8), smallTargetCount: smallTargets.length,
    unlabeled, nameless: nameless.slice(0, 8), imgNoAlt: document.querySelectorAll('img:not([alt])').length,
    dupIds: Object.entries(ids).filter(([, c]) => c > 1).map(([k]) => k).slice(0, 8),
    errorUiVisible: (() => { const e = document.getElementById('blazor-error-ui'); return !!e && getComputedStyle(e).display !== 'none'; })(),
    bodyTextSample: main.innerText.trim().replace(/\s+/g, ' ').slice(0, 260),
    englishHits: (main.innerText.match(/\b(Save|Username|Phone number|Error|Loading|Close|Submit|Active|Pending|Rejected|Archived|Draft|Free|Dashboard|Upload|2FA)\b/g) || []).slice(0, 10),
  };
}
