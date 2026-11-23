(function () {
    'use strict';

    var delayMs = 400;
    var selector = 'input';

    function isSearchField(input) {
        if (!input || input.tagName !== 'INPUT' || input.disabled || input.readOnly ||
            input.hasAttribute('data-search-no-debounce')) return false;
        var id = (input.id || '').toLowerCase();
        var name = (input.name || '').toLowerCase();
        var placeholder = (input.getAttribute('placeholder') || '').toLowerCase();
        return input.type.toLowerCase() === 'search' ||
            id.indexOf('search') !== -1 ||
            name === 'search' || name === 'q' ||
            placeholder.indexOf('search') !== -1;
    }

    function addStyles() {
        if (document.getElementById('searchExperienceStyles')) return;
        var style = document.createElement('style');
        style.id = 'searchExperienceStyles';
        style.textContent =
            '.search-ux-pending{position:relative!important}' +
            '.search-ux-spinner{position:absolute;right:12px;top:50%;width:16px;height:16px;margin-top:-8px;border:2px solid rgba(45,55,72,.18);border-top-color:#374A60;border-radius:50%;animation:searchUxSpin .7s linear infinite;pointer-events:none;z-index:2}' +
            '.search-ux-sr{position:absolute!important;width:1px!important;height:1px!important;padding:0!important;margin:-1px!important;overflow:hidden!important;clip:rect(0,0,0,0)!important;white-space:nowrap!important;border:0!important}' +
            '@keyframes searchUxSpin{to{transform:rotate(360deg)}}' +
            '@media(prefers-reduced-motion:reduce){.search-ux-spinner{animation-duration:1.5s}}';
        document.head.appendChild(style);
    }

    function setPending(input, pending) {
        var host = input.closest('.sa-search,.search,.cdc-search,.dean-exception-search,.dean-repo-search') || input.parentElement;
        if (!host) return;
        var spinner = host.querySelector('.search-ux-spinner');
        if (pending) {
            host.classList.add('search-ux-pending');
            host.setAttribute('aria-busy', 'true');
            if (!spinner) {
                spinner = document.createElement('span');
                spinner.className = 'search-ux-spinner';
                spinner.setAttribute('aria-hidden', 'true');
                host.appendChild(spinner);
                var status = document.createElement('span');
                status.className = 'search-ux-sr';
                status.setAttribute('role', 'status');
                status.textContent = 'Searching…';
                host.appendChild(status);
            }
        } else {
            host.classList.remove('search-ux-pending');
            host.removeAttribute('aria-busy');
            if (spinner) spinner.remove();
            var statusEl = host.querySelector('.search-ux-sr');
            if (statusEl) statusEl.remove();
        }
    }

    function cancelTimer(input) {
        if (input.__searchUxTimer) {
            clearTimeout(input.__searchUxTimer);
            input.__searchUxTimer = null;
        }
        setPending(input, false);
    }

    function onInput(event) {
        var input = event.target;
        if (event.__searchUxDebounced || !isSearchField(input) || event.isComposing) return;
        event.stopImmediatePropagation();
        event.preventDefault();

        if (input.__searchUxTimer) clearTimeout(input.__searchUxTimer);
        input.__searchUxSequence = (input.__searchUxSequence || 0) + 1;
        var sequence = input.__searchUxSequence;
        var form = input.form;
        var serverSearch = form && form.method.toLowerCase() === 'get' &&
            (input.type.toLowerCase() === 'search' || input.name.toLowerCase() === 'q' || input.name.toLowerCase() === 'search');
        setPending(input, true);

        input.__searchUxTimer = setTimeout(function () {
            if (sequence !== input.__searchUxSequence) return;
            input.__searchUxTimer = null;

            if (serverSearch) {
                var value = input.value;
                if (input.__searchUxLastSubmitted !== value) {
                    input.__searchUxLastSubmitted = value;
                    if (typeof form.requestSubmit === 'function') form.requestSubmit();
                    else form.submit();
                } else {
                    setPending(input, false);
                }
                return;
            }

            if (input.__searchUxLastApplied !== input.value) {
                input.__searchUxLastApplied = input.value;
                var delayed = new Event('input', { bubbles: true });
                delayed.__searchUxDebounced = true;
                input.dispatchEvent(delayed);
            }
            window.setTimeout(function () {
                if (sequence === input.__searchUxSequence) setPending(input, false);
            }, 0);
        }, delayMs);
    }

    function init() {
        addStyles();
        document.addEventListener('input', onInput, true);
        document.addEventListener('submit', function (event) {
            var form = event.target;
            if (!form || !form.querySelectorAll) return;
            form.querySelectorAll(selector).forEach(function (input) {
                if (isSearchField(input)) cancelTimer(input);
            });
        }, true);
        document.addEventListener('invalid', function (event) {
            if (isSearchField(event.target)) cancelTimer(event.target);
        }, true);
        window.addEventListener('pagehide', function () {
            document.querySelectorAll('input').forEach(function (input) {
                if (input.__searchUxTimer) clearTimeout(input.__searchUxTimer);
            });
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();