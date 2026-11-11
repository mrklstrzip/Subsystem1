/* Privacy shield for Dean schedule details. Screenshot detection is best-effort;
   the modal is blurred when screenshot/print shortcuts are attempted or the tab loses focus. */
(function () {
    'use strict';

    var SHIELD = 'sa-shield';
    var timer = null;

    function modal() {
        var overlay = document.getElementById('saOverlay');
        return overlay && overlay.classList.contains('open') ? overlay.querySelector('.sa-modal') : null;
    }
    function isOpen() { return !!modal(); }
    function element(node) { return node && node.nodeType === 1 ? node : (node ? node.parentElement : null); }
    function isEditable(node) {
        var el = element(node);
        return !!(el && el.closest && el.closest('input, textarea, select, [contenteditable="true"]'));
    }
    function inModal(node) {
        var el = element(node);
        return !!(el && el.closest && el.closest('.sa-modal'));
    }
    function shieldOn() {
        clearTimeout(timer);
        var card = modal();
        if (card) card.classList.add(SHIELD);
    }
    function shieldOff(delay) {
        clearTimeout(timer);
        timer = setTimeout(function () {
            if (!document.hidden && document.hasFocus()) {
                var card = modal();
                if (card) card.classList.remove(SHIELD);
            }
        }, delay || 700);
    }
    function clearClipboard() {
        try { if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(' '); } catch (ignore) { }
    }
    function resetClosed() {
        if (isOpen()) return;
        clearTimeout(timer);
        Array.prototype.forEach.call(document.querySelectorAll('.sa-modal.' + SHIELD), function (card) {
            card.classList.remove(SHIELD);
        });
    }

    if (window.MutationObserver) {
        new MutationObserver(resetClosed).observe(document.body, { attributes: true, subtree: true, attributeFilter: ['class'] });
    }
    ['copy', 'cut', 'contextmenu', 'dragstart', 'selectstart'].forEach(function (type) {
        document.addEventListener(type, function (event) {
            if (isOpen() && inModal(event.target) && !isEditable(event.target)) event.preventDefault();
        }, true);
    });
    document.addEventListener('keydown', function (event) {
        if (!isOpen()) return;
        var key = (event.key || '').toLowerCase();
        if (key === 'meta' || key === 'os' || key === 'printscreen' || key === 'snapshot') {
            shieldOn();
            if (key === 'printscreen' || key === 'snapshot') clearClipboard();
            return;
        }
        if (event.shiftKey && (event.metaKey || event.ctrlKey) && ['s', '3', '4', '5'].indexOf(key) > -1) {
            shieldOn();
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && ['c', 'x', 'a', 'p', 's', 'u'].indexOf(key) > -1) {
            if (isEditable(event.target) && ['c', 'x', 'a'].indexOf(key) > -1) return;
            event.preventDefault();
            if (key === 'p') shieldOn();
        }
    }, true);
    document.addEventListener('keyup', function (event) {
        if (!isOpen()) return;
        var key = (event.key || '').toLowerCase();
        if (key === 'printscreen' || key === 'snapshot') { shieldOn(); clearClipboard(); shieldOff(1200); }
        else if (key === 'meta' || key === 'os') shieldOff(1200);
    }, true);
    window.addEventListener('blur', function () { if (isOpen()) shieldOn(); });
    window.addEventListener('focus', function () { if (isOpen()) shieldOff(700); });
    document.addEventListener('visibilitychange', function () {
        if (!isOpen()) return;
        if (document.hidden) shieldOn(); else shieldOff(700);
    });
    window.addEventListener('beforeprint', function () { if (isOpen()) shieldOn(); });
    window.addEventListener('afterprint', function () { if (isOpen()) shieldOff(300); });
})();
