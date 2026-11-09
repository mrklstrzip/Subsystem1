/* Capstone Coordinator - privacy protection for the schedule / defense / conflict pop-ups.
   Same idea as the student's Capstone Title view (student-submission.js):
   A website cannot detect a screenshot before it is taken, so the details are hidden (blurred)
   the moment one is ATTEMPTED:
     - Print Screen, the Windows / Cmd key (Win+Shift+S, Cmd+Shift+4 ...), Ctrl+P
     - the window losing focus (snipping tool, another app or tab)
   and they come back by themselves right after. Text in the pop-up also cannot be selected,
   copied, cut, dragged or right-clicked (the editable time / date / room fields still work).
   It is active ONLY while one of those pop-ups is open, and it touches no data.
   This is a deterrent only: it does not stop a photo taken with another device. */
(function () {
    'use strict';

    var SHIELD = 'cc-shield';
    var shieldTimer = null;

    function openCard() {
        var overlay = document.querySelector('.modal-overlay.open');
        return overlay ? overlay.querySelector('.schedule-modal') : null;
    }
    function viewOpen() { return !!openCard(); }

    function asElement(node) {
        return node && node.nodeType === 1 ? node : (node ? node.parentElement : null);
    }
    function isEditable(node) {
        var el = asElement(node);
        return !!(el && el.closest && el.closest('input, textarea, select, [contenteditable="true"]'));
    }
    function inCard(node) {
        var el = asElement(node);
        return !!(el && el.closest && el.closest('.schedule-modal'));
    }

    function shieldOn() {
        clearTimeout(shieldTimer);
        var card = openCard();
        if (card) card.classList.add(SHIELD);
    }

    // show the details again shortly after the window is focused and no key is held
    function shieldOff(delay) {
        clearTimeout(shieldTimer);
        shieldTimer = setTimeout(function () {
            if (!document.hidden && document.hasFocus()) {
                var card = openCard();
                if (card) card.classList.remove(SHIELD);
            }
        }, delay || 700);
    }

    function clearClipboard() {
        try { if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(' '); } catch (err) { /* ignore */ }
    }

    // when a pop-up closes, make sure no card stays blurred for the next time it opens
    function resetClosed() {
        if (viewOpen()) return;
        clearTimeout(shieldTimer);
        Array.prototype.forEach.call(document.querySelectorAll('.schedule-modal.' + SHIELD), function (c) {
            c.classList.remove(SHIELD);
        });
    }
    if (window.MutationObserver) {
        new MutationObserver(resetClosed).observe(document.body, {
            attributes: true, subtree: true, attributeFilter: ['class']
        });
    }

    ['copy', 'cut', 'contextmenu', 'dragstart', 'selectstart'].forEach(function (type) {
        document.addEventListener(type, function (e) {
            if (!viewOpen() || !inCard(e.target)) return;
            if (isEditable(e.target)) return;      // typing / copying inside the edit fields is fine
            e.preventDefault();
        }, true);
    });

    document.addEventListener('keydown', function (e) {
        if (!viewOpen()) return;
        var k = (e.key || '').toLowerCase();

        // Windows / Cmd key is pressed before S in Win+Shift+S, so hide at once
        if (k === 'meta' || k === 'os' || k === 'printscreen' || k === 'snapshot') {
            shieldOn();
            if (k === 'printscreen' || k === 'snapshot') clearClipboard();
            return;
        }

        if (e.shiftKey && (e.metaKey || e.ctrlKey) && (k === 's' || k === '3' || k === '4' || k === '5')) {
            shieldOn(); e.preventDefault(); return;
        }

        // copy / cut / select all (not inside the edit fields) and print / save / view-source
        if ((e.ctrlKey || e.metaKey) && ['c', 'x', 'a', 'p', 's', 'u'].indexOf(k) > -1) {
            if (isEditable(e.target) && (k === 'c' || k === 'x' || k === 'a')) return;
            e.preventDefault();
            if (k === 'p') shieldOn();
        }
    }, true);

    document.addEventListener('keyup', function (e) {
        if (!viewOpen()) return;
        var k = (e.key || '').toLowerCase();
        if (k === 'printscreen' || k === 'snapshot') { shieldOn(); clearClipboard(); shieldOff(1200); }
        else if (k === 'meta' || k === 'os') shieldOff(1200);
    }, true);

    window.addEventListener('blur', function () { if (viewOpen()) shieldOn(); });
    window.addEventListener('focus', function () { if (viewOpen()) shieldOff(700); });
    document.addEventListener('visibilitychange', function () {
        if (!viewOpen()) return;
        if (document.hidden) shieldOn(); else shieldOff(700);
    });
    window.addEventListener('beforeprint', function () { if (viewOpen()) shieldOn(); });
    window.addEventListener('afterprint', function () { if (viewOpen()) shieldOff(300); });
})();
