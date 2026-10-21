// Comportamentos de interface que não dependem do circuito do Blazor. Tudo é delegado no document, então
// continua valendo depois da navegação melhorada (as páginas de vagas são renderizadas no servidor).
(function () {
    'use strict';

    // ---- Formulários de busca (GET) ----------------------------------------------------------------
    // Campos vazios ou no valor padrão ficam fora do endereço: /jobs?q=dev em vez de
    // /jobs?q=dev&local=&modo=&salario=&ordem=recentes. O Blazor monta o endereço logo depois deste
    // evento, então os campos são desativados só durante o envio.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-clean-query')) return;

        var muted = [];
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!el.name || el.disabled) return;
            if ((el.type === 'radio' || el.type === 'checkbox') && !el.checked) return;
            var fallback = el.hasAttribute('data-default') ? el.getAttribute('data-default') : '';
            if (el.value.trim() === fallback) {
                el.disabled = true;
                muted.push(el);
            }
        });
        setTimeout(function () {
            muted.forEach(function (el) { el.disabled = false; });
        }, 0);
    }, true);

    // ---- Rolagem até os resultados -------------------------------------------------------------------
    // Busca enviada pelo botão (ou Enter) e troca de página levam ao início da lista: no celular os resultados
    // ficam abaixo dos filtros. Mudar filtro ou ordenação não mexe na rolagem. Não usamos âncora (#) na URL
    // porque o Blazor trata o Voltar para um endereço com âncora como mudança só de âncora e não recarrega.
    var SCROLL_KEY = 'monburan-scroll-results';

    function markScroll() {
        try { sessionStorage.setItem(SCROLL_KEY, '1'); } catch (err) { }
    }

    function applyScroll() {
        try {
            if (sessionStorage.getItem(SCROLL_KEY) !== '1') return;
            sessionStorage.removeItem(SCROLL_KEY);
        } catch (err) {
            return;
        }
        var target = document.getElementById('jobs-results');
        if (target) target.scrollIntoView();
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        // submitter é nulo quando o envio vem de um filtro (requestSubmit sem botão)
        if (form instanceof HTMLFormElement && form.hasAttribute('data-scroll-results') && e.submitter) markScroll();
    }, true);

    document.addEventListener('click', function (e) {
        if (e.target.closest && e.target.closest('a[data-scroll-results]')) markScroll();
    }, true);

    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', applyScroll);

    // Filtros e ordenação aplicam ao mudar com o mouse ou o toque, sem exigir o botão de busca.
    // Pelo teclado, setas só escolhem a opção (WCAG 3.2.2: mudar um campo não pode trocar a página);
    // aplica-se com Enter ou com o botão do formulário.
    var keyboardChange = false;
    function submitForm(form) {
        if (typeof form.requestSubmit === 'function') form.requestSubmit();
        else form.submit();
    }

    document.addEventListener('keydown', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLElement) || !el.hasAttribute('data-autosubmit') || !el.form) return;
        if (e.key === 'Enter') {
            e.preventDefault();
            submitForm(el.form);
            return;
        }
        keyboardChange = e.key !== 'Tab';
    }, true);

    document.addEventListener('pointerdown', function () { keyboardChange = false; }, true);

    document.addEventListener('change', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLElement) || !el.hasAttribute('data-autosubmit') || !el.form) return;
        if (keyboardChange) return;
        submitForm(el.form);
    });

    // ---- Lista de vagas: lembra a busca para o "Voltar para as vagas" da página da vaga -------------
    var LIST_KEY = 'monburan-jobs-list';

    function rememberList() {
        try {
            if (location.pathname === '/jobs') sessionStorage.setItem(LIST_KEY, location.pathname + location.search);
        } catch (err) { }
    }

    rememberList();
    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', rememberList);

    // Fase de captura: o Blazor lê o href do link depois deste ajuste.
    document.addEventListener('click', function (e) {
        var link = e.target.closest ? e.target.closest('[data-action="back-to-list"]') : null;
        if (!link) return;
        try {
            var saved = sessionStorage.getItem(LIST_KEY);
            if (saved && saved.indexOf('/jobs') === 0) link.setAttribute('href', saved);
        } catch (err) { }
    }, true);

    // ---- Compartilhar vaga ---------------------------------------------------------------------------
    function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) return navigator.clipboard.writeText(text);
        return new Promise(function (resolve, reject) {
            try {
                var area = document.createElement('textarea');
                area.value = text;
                area.setAttribute('readonly', '');
                area.style.position = 'fixed';
                area.style.opacity = '0';
                document.body.appendChild(area);
                area.select();
                var ok = document.execCommand('copy');
                document.body.removeChild(area);
                if (ok) resolve();
                else reject(new Error('copy'));
            } catch (err) {
                reject(err);
            }
        });
    }

    // Região viva única para confirmações curtas (ex.: "Link copiado"), que só trocam texto na tela
    function announce(text) {
        var region = document.getElementById('monburan-announcer');
        if (!region) {
            region = document.createElement('div');
            region.id = 'monburan-announcer';
            region.className = 'visually-hidden';
            region.setAttribute('role', 'status');
            region.setAttribute('aria-live', 'polite');
            document.body.appendChild(region);
        }
        region.textContent = '';
        setTimeout(function () { region.textContent = text; }, 50);
    }

    function flash(button, text) {
        announce(text);
        var label = button.querySelector('[data-copy-label]');
        if (label) {
            var original = label.getAttribute('data-original') || label.textContent;
            label.setAttribute('data-original', original);
            label.textContent = text;
            clearTimeout(button._flash);
            button._flash = setTimeout(function () {
                label.textContent = original;
                button.classList.remove('is-done');
            }, 2200);
        } else {
            clearTimeout(button._flash);
            button._flash = setTimeout(function () { button.classList.remove('is-done'); }, 2200);
        }
        button.classList.add('is-done');
    }

    document.addEventListener('click', function (e) {
        var button = e.target.closest ? e.target.closest('[data-action="copy-link"], [data-action="share-job"]') : null;
        if (!button) return;

        var url = button.getAttribute('data-url') || location.href;
        var title = button.getAttribute('data-title') || document.title;

        // "Compartilhar" abre o menu do aparelho quando existe; "Copiar link" sempre copia.
        if (button.getAttribute('data-action') === 'share-job' && navigator.share) {
            navigator.share({ title: title, url: url }).catch(function () { /* cancelado pela pessoa */ });
            return;
        }

        copyText(url).then(
            function () { flash(button, 'Link copiado'); },
            function () { flash(button, 'Copie o endereço do navegador'); }
        );
    });
})();

// ---- Erros de formulário: foco no primeiro campo inválido -------------------------------------------
// Depois de um envio com erro, quem usa teclado ou leitor de tela vai direto ao campo a corrigir (e não ao
// título da página). Vale para formulários interativos (o Blazor valida no envio) e para os renderizados
// no servidor (a página volta com aria-invalid marcado).
(function () {
    'use strict';

    function focusFirstInvalid(root) {
        var field = (root || document).querySelector('main [aria-invalid="true"]');
        if (!field || field === document.activeElement || field.contains(document.activeElement)) return false;
        if (typeof field.focus === 'function') field.focus();
        return true;
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement) || form.getAttribute('role') === 'search') return;
        // O Blazor marca os campos depois do evento: espera a renderização
        setTimeout(function () { if (form.isConnected) focusFirstInvalid(form); }, 300);
    }, true);

    // Envio renderizado no servidor: a página nova já chega com os campos marcados
    function afterLoad() { setTimeout(function () { focusFirstInvalid(document); }, 50); }
    if (document.readyState === 'complete') afterLoad();
    else window.addEventListener('load', afterLoad);
    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', afterLoad);
})();

// ---- Foco programático (acessibilidade) -----------------------------------------------------------
// O Blazor move o foco para o título ao trocar de etapa em fluxos guiados (candidatura, wizard de vaga).
window.monburanFocusById = function (id) {
    var el = document.getElementById(id);
    if (el && typeof el.focus === 'function') el.focus();
};

// ---- Armadilha de foco para gavetas e diálogos -----------------------------------------------------
// O Blazor controla abrir/fechar; daqui sai só o Tab preso dentro do painel enquanto ele existir.
window.monburanTrapFocus = function (container) {
    if (!container || !container.addEventListener || container._monburanTrap) return;
    container._monburanTrap = true;
    container.addEventListener('keydown', function (e) {
        if (e.key !== 'Tab') return;
        var items = container.querySelectorAll(
            'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])');
        if (!items.length) return;
        var first = items[0];
        var last = items[items.length - 1];
        if (e.shiftKey && (document.activeElement === first || !container.contains(document.activeElement))) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && (document.activeElement === last || !container.contains(document.activeElement))) {
            e.preventDefault();
            first.focus();
        }
    });
};

// ---- Diálogo modal nativo (ConfirmDialog) -----------------------------------------------------------
// showModal() prende o foco e deixa o resto da página inerte. Esc e clique no fundo não fecham o <dialog>
// por conta própria: acionam o botão Cancelar, para o Blazor decidir e devolver o resultado.
window.monburanDialog = (function () {
    var returnTo = null;

    function cancel(dialog) {
        var button = dialog.querySelector('[data-dialog-cancel]');
        if (button) button.click();
    }

    return {
        open: function (dialog) {
            if (!dialog || typeof dialog.showModal !== 'function') return;
            var active = document.activeElement;
            returnTo = active && active !== document.body ? active : null;
            if (!dialog.open) dialog.showModal();
            if (!dialog._monburanWired) {
                dialog._monburanWired = true;
                dialog.addEventListener('cancel', function (e) { e.preventDefault(); cancel(dialog); });
                // O fundo (::backdrop) pertence ao próprio <dialog>: clique fora do conteúdo tem o dialog como alvo
                dialog.addEventListener('click', function (e) { if (e.target === dialog) cancel(dialog); });
            }
            var safe = dialog.querySelector('[data-dialog-cancel]');
            if (safe) safe.focus();
        },
        // Quem abriu pode ter sumido (ex.: item de menu fechado): então o foco vai para o título da página
        restoreFocus: function () {
            var target = returnTo;
            returnTo = null;
            if (target && target.isConnected && typeof target.focus === 'function') {
                target.focus();
                return;
            }
            var heading = document.querySelector('main h1');
            if (heading) {
                if (!heading.hasAttribute('tabindex')) heading.setAttribute('tabindex', '-1');
                heading.focus();
            }
        }
    };
})();
