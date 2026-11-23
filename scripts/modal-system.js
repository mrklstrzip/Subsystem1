/* Replaces native browser alert boxes with the shared accessible dialog. */
(function () {
    'use strict';

    var queue = [];
    var active = false;
    var previousFocus = null;

    function closeCurrent() {
        var overlay = document.querySelector('.system-alert-backdrop');
        if (overlay) overlay.remove();
        active = false;
        if (previousFocus && typeof previousFocus.focus === 'function') previousFocus.focus();
        showNext();
    }

    function showNext() {
        if (active || !queue.length || !document.body) return;
        active = true;
        previousFocus = document.activeElement;
        var message = queue.shift();
        var overlay = document.createElement('div');
        overlay.className = 'system-alert-backdrop';
        overlay.setAttribute('role', 'presentation');

        var dialog = document.createElement('section');
        dialog.className = 'system-alert-modal';
        dialog.setAttribute('role', 'alertdialog');
        dialog.setAttribute('aria-modal', 'true');
        dialog.setAttribute('aria-labelledby', 'systemAlertTitle');
        dialog.setAttribute('aria-describedby', 'systemAlertMessage');

        var title = document.createElement('h2');
        title.id = 'systemAlertTitle';
        title.textContent = 'Notice';
        var body = document.createElement('p');
        body.id = 'systemAlertMessage';
        body.textContent = message;
        var actions = document.createElement('div');
        actions.className = 'system-alert-actions';
        var button = document.createElement('button');
        button.type = 'button';
        button.textContent = 'OK';
        button.addEventListener('click', closeCurrent);
        actions.appendChild(button);
        dialog.appendChild(title);
        dialog.appendChild(body);
        dialog.appendChild(actions);
        overlay.appendChild(dialog);
        document.body.appendChild(overlay);
        button.focus();
    }

    window.addEventListener('keydown', function (event) {
        if (!active) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            closeCurrent();
        } else if (event.key === 'Enter') {
            var button = document.querySelector('.system-alert-actions button');
            if (button && document.activeElement !== button) {
                event.preventDefault();
                button.click();
            }
        }
    });

    window.alert = function (message) {
        queue.push(String(message == null ? '' : message));
        showNext();
    };
})();
/* Adds a consistent active/focus state to filter dropdowns without changing filtering behavior. */
(function () {
    'use strict';

    var filterSelector = [
        '.filter-wrapper .custom-select-box',
        '.filter-wrapper select',
        '.distribution-filter-box',
        '.sa-filter',
        '.cdc-select',
        '.inv-filter',
        '.sy-box',
        '.dean-report-filters select',
        '.dean-exception-filters select',
        '.dean-performance-filters select',
        '.dean-submission-filter-main select',
        '.dean-record-tools select',
        '.dean-repo-search select',
        '.dean-filter-card select',
        '[data-sy-filter]'
    ].join(',');

    function selectedValue(control) {
        var select = control.matches('select') ? control : control.querySelector('select');
        if (select) return String(select.value == null ? '' : select.value);
        var chosen = control.querySelector('.select-option.selected, .select-option.active, .cdc-option.active');
        if (chosen) return String(chosen.getAttribute('data-value') || chosen.textContent || '').trim();
        var text = control.querySelector('.select-selected span, .cdc-selected-text');
        return text ? String(text.textContent || '').trim() : '';
    }

    function defaultValue(control) {
        var explicit = control.getAttribute('data-filter-default');
        if (explicit !== null) return explicit;
        var select = control.matches('select') ? control : control.querySelector('select');
        if (select && select.options && select.options.length) {
            for (var i = 0; i < select.options.length; i++) {
                var option = select.options[i];
                var optionText = String(option.textContent || '').trim();
                if (/^(all|any|select|choose)(\b|\s)/i.test(optionText) || /^(all|any|select|choose)$/i.test(optionText) || option.value === '') return String(option.value || '');
            }
            return String(select.options[0].value || '');
        }
        var options = control.querySelectorAll('.select-option[data-value], .cdc-option[data-value]');
        if (options.length) return String(options[0].getAttribute('data-value') || options[0].textContent || '').trim();
        return '';
    }

    function refresh(control) {
        if (!control || !control.isConnected) return;
        var value = selectedValue(control);
        var reset = defaultValue(control);
        var active = value !== reset && !(/^(all|any|select|choose)(\b|\s)/i.test(value));
        var menuOpen = !!control.querySelector('.custom-select-dropdown.open, .cdc-dropdown.open') ||
            control.classList.contains('open') || control.getAttribute('aria-expanded') === 'true' ||
            !!control.querySelector('[aria-expanded="true"]');
        control.classList.toggle('filter-active', active);
        control.classList.toggle('filter-open', menuOpen);
    }

    function initialize(control) {
        if (!control || control._systemFilterIndicator) return;
        control._systemFilterIndicator = true;
        control.classList.add('system-filter-control');
        refresh(control);
        control.addEventListener('change', function () { refresh(control); });
        control.addEventListener('input', function () { refresh(control); });
        control.addEventListener('focusin', function () { control.classList.add('filter-focused'); });
        control.addEventListener('focusout', function () {
            window.setTimeout(function () {
                if (!control.contains(document.activeElement)) control.classList.remove('filter-focused');
                refresh(control);
            }, 0);
        });
        if (window.MutationObserver) {
            var observer = new MutationObserver(function () { refresh(control); });
            observer.observe(control, { attributes: true, childList: true, characterData: true, subtree: true });
        }
    }

    function scan(root) {
        if (!root || root.nodeType !== 1) return;
        if (root.matches(filterSelector)) initialize(root);
        root.querySelectorAll(filterSelector).forEach(initialize);
    }

    function start() {
        scan(document.documentElement);
        if (window.MutationObserver && document.body) {
            var observer = new MutationObserver(function (records) {
                records.forEach(function (record) {
                    record.addedNodes.forEach(function (node) { scan(node); });
                });
            });
            observer.observe(document.body, { childList: true, subtree: true });
        }
        document.addEventListener('click', function (event) {
            var option = event.target.closest && event.target.closest('.select-option, .cdc-option');
            var control = option && option.closest(filterSelector);
            if (control) window.setTimeout(function () { refresh(control); }, 0);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();