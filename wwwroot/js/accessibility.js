/**
 * Didasco — Accessibility Widget
 * Pannello accessibilità nativo WCAG 2.1 AA — nessuna dipendenza esterna.
 * Profili: anti-convulsioni, ipovedenti, ADHD, cognitivo, tastiera, screen reader, anziani.
 * Le preferenze vengono salvate in localStorage e applicate immediatamente ad ogni pagina.
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'didasco_a11y';

    var profiles = [
        {
            id: 'seizure',
            icon: 'bi-lightning-charge-fill',
            it: ['Profilo anti-convulsioni', 'Elimina animazioni e riduce il colore'],
            en: ['Seizure-safe profile', 'Removes animations and reduces colour'],
            cls: 'acc-seizure'
        },
        {
            id: 'low-vision',
            icon: 'bi-eye-fill',
            it: ['Persone ipovedenti', 'Migliora la visibilità del sito'],
            en: ['Visually impaired', 'Improves site visibility'],
            cls: 'acc-low-vision'
        },
        {
            id: 'adhd',
            icon: 'bi-bounding-box',
            it: ["Profilo adatto all'ADHD", 'Più concentrazione, meno distrazioni'],
            en: ['ADHD-friendly profile', 'More focus, fewer distractions'],
            cls: 'acc-adhd'
        },
        {
            id: 'cognitive',
            icon: 'bi-crosshair',
            it: ['Disabilità cognitiva', 'Aiuta a leggere e a concentrarsi'],
            en: ['Cognitive disability', 'Helps with reading and focus'],
            cls: 'acc-cognitive'
        },
        {
            id: 'keyboard',
            icon: 'bi-keyboard-fill',
            it: ['Navigazione da tastiera', 'Usa il sito con la tastiera'],
            en: ['Keyboard navigation', 'Use the site with the keyboard'],
            cls: 'acc-keyboard'
        },
        {
            id: 'screen-reader',
            icon: 'bi-soundwave',
            it: ['Screen reader', 'Ottimizzato per lettori dello schermo'],
            en: ['Screen reader', 'Optimised for screen readers'],
            cls: 'acc-screen-reader'
        },
        {
            id: 'elderly',
            icon: 'bi-zoom-in',
            it: ['Persone anziane', 'Testo più grande e più comfort di lettura'],
            en: ['Elderly users', 'Larger text and more reading comfort'],
            cls: 'acc-elderly'
        }
    ];

    // ── Carica lo stato e applica le classi PRIMA del paint per evitare flash ────
    var state = {};
    try { state = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}'); } catch (e) { }
    profiles.forEach(function (p) {
        if (state[p.id]) document.documentElement.classList.add(p.cls);
    });

    function saveState() {
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(state)); } catch (e) { }
    }

    function hasAnyActive() {
        return profiles.some(function (p) { return !!state[p.id]; });
    }

    // ── Init widget (dopo DOMContentLoaded) ─────────────────────────────────────
    function init() {
        var lang = (window.currentLang || 'it').toLowerCase();
        var isIt = lang === 'it';

        var i18n = {
            title:    isIt ? "Regolazioni per l'accessibilità" : 'Accessibility settings',
            reset:    isIt ? 'Reset impostazioni' : 'Reset settings',
            subtitle: isIt ? 'Scegli il profilo più adatto a te' : 'Choose the profile that suits you best',
            footer:   isIt ? 'Accessibilità WCAG 2.1 livello AA' : 'Accessibility WCAG 2.1 level AA',
            open:     isIt ? 'Apri impostazioni accessibilità' : 'Open accessibility settings',
            close:    isIt ? 'Chiudi' : 'Close'
        };

        // ── Panel ────────────────────────────────────────────────────────────────
        var panel = document.createElement('div');
        panel.id = 'a11y-panel';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-modal', 'false');
        panel.setAttribute('aria-label', i18n.title);
        panel.setAttribute('aria-hidden', 'true');

        // Header
        var header = document.createElement('div');
        header.className = 'a11y-header';
        header.innerHTML =
            '<div class="a11y-header-top">' +
                '<h2 class="a11y-title">' + i18n.title + '</h2>' +
                '<button class="a11y-close-btn" id="a11y-close-btn" type="button" aria-label="' + i18n.close + '">' +
                    '<i class="bi bi-x-lg" aria-hidden="true"></i>' +
                '</button>' +
            '</div>' +
            '<button class="a11y-reset-btn" id="a11y-reset" type="button">' +
                '<i class="bi bi-arrow-counterclockwise me-1" aria-hidden="true"></i>' + i18n.reset +
            '</button>';
        panel.appendChild(header);

        // Subtitle
        var sub = document.createElement('p');
        sub.className = 'a11y-subtitle';
        sub.textContent = i18n.subtitle;
        panel.appendChild(sub);

        // Profile list
        var list = document.createElement('ul');
        list.className = 'a11y-list';
        list.setAttribute('role', 'list');

        profiles.forEach(function (p) {
            var labels = isIt ? p.it : p.en;
            var on = !!state[p.id];

            var li = document.createElement('li');
            li.className = 'a11y-item';

            var toggle = document.createElement('button');
            toggle.className = 'a11y-toggle' + (on ? ' on' : '');
            toggle.setAttribute('type', 'button');
            toggle.setAttribute('role', 'switch');
            toggle.setAttribute('aria-checked', on ? 'true' : 'false');
            toggle.setAttribute('aria-label', labels[0]);
            toggle.dataset.profile = p.id;
            toggle.innerHTML =
                '<span class="a11y-toggle-no" aria-hidden="true">' + (isIt ? 'NO' : 'NO') + '</span>' +
                '<span class="a11y-toggle-knob" aria-hidden="true"></span>' +
                '<span class="a11y-toggle-si" aria-hidden="true">' + (isIt ? 'SÌ' : 'ON') + '</span>';

            var icon = document.createElement('span');
            icon.className = 'a11y-icon';
            icon.setAttribute('aria-hidden', 'true');
            icon.innerHTML = '<i class="bi ' + p.icon + '"></i>';

            var textWrap = document.createElement('div');
            textWrap.className = 'a11y-text';
            textWrap.innerHTML =
                '<span class="a11y-label">' + labels[0] + '</span>' +
                '<span class="a11y-desc">' + labels[1] + '</span>';

            li.appendChild(toggle);
            li.appendChild(icon);
            li.appendChild(textWrap);

            // Click anywhere on the row fires the toggle
            li.addEventListener('click', function (e) {
                if (e.target === toggle || toggle.contains(e.target)) return;
                toggleProfile(p, toggle);
            });
            toggle.addEventListener('click', function () {
                toggleProfile(p, toggle);
            });

            list.appendChild(li);
        });

        panel.appendChild(list);

        // Footer
        var footer = document.createElement('div');
        footer.className = 'a11y-footer';
        footer.innerHTML =
            '<i class="bi bi-universal-access me-1" aria-hidden="true"></i>' + i18n.footer;
        panel.appendChild(footer);

        // ── Floating trigger button ──────────────────────────────────────────────
        var btn = document.createElement('button');
        btn.id = 'a11y-btn';
        btn.setAttribute('type', 'button');
        btn.setAttribute('aria-expanded', 'false');
        btn.setAttribute('aria-controls', 'a11y-panel');
        btn.setAttribute('aria-label', i18n.open);
        btn.innerHTML = '<i class="bi bi-universal-access" aria-hidden="true"></i>';
        if (hasAnyActive()) btn.classList.add('a11y-active');

        document.body.appendChild(panel);
        document.body.appendChild(btn);

        // ── Open / Close ─────────────────────────────────────────────────────────
        var isOpen = false;

        function openPanel() {
            isOpen = true;
            panel.classList.add('a11y-open');
            panel.setAttribute('aria-hidden', 'false');
            btn.setAttribute('aria-expanded', 'true');
            var closeBtn = document.getElementById('a11y-close-btn');
            if (closeBtn) closeBtn.focus();
        }

        function closePanel() {
            isOpen = false;
            panel.classList.remove('a11y-open');
            panel.setAttribute('aria-hidden', 'true');
            btn.setAttribute('aria-expanded', 'false');
            btn.focus();
        }

        btn.addEventListener('click', function () {
            if (isOpen) closePanel(); else openPanel();
        });

        document.getElementById('a11y-close-btn').addEventListener('click', closePanel);

        // Reset
        document.getElementById('a11y-reset').addEventListener('click', function () {
            profiles.forEach(function (p) {
                state[p.id] = false;
                document.documentElement.classList.remove(p.cls);
            });
            saveState();
            btn.classList.remove('a11y-active');
            panel.querySelectorAll('.a11y-toggle').forEach(function (t) {
                t.className = 'a11y-toggle';
                t.setAttribute('aria-checked', 'false');
            });
        });

        // Chiudi cliccando fuori
        document.addEventListener('click', function (e) {
            if (isOpen && !panel.contains(e.target) && e.target !== btn && !btn.contains(e.target)) {
                closePanel();
            }
        });

        // Chiudi con Escape
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && isOpen) closePanel();
        });
    }

    function toggleProfile(p, toggle) {
        var on = !state[p.id];
        state[p.id] = on;
        toggle.className = 'a11y-toggle' + (on ? ' on' : '');
        toggle.setAttribute('aria-checked', on ? 'true' : 'false');
        if (on) document.documentElement.classList.add(p.cls);
        else    document.documentElement.classList.remove(p.cls);
        saveState();

        var mainBtn = document.getElementById('a11y-btn');
        if (mainBtn) {
            if (hasAnyActive()) mainBtn.classList.add('a11y-active');
            else                mainBtn.classList.remove('a11y-active');
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
