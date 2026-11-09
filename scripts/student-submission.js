(function () {
  'use strict';

  // open dialogs
  document.querySelectorAll('[data-open]').forEach(function (b) {
    b.addEventListener('click', function () { document.getElementById(b.dataset.open).showModal(); });
  });

  // close buttons, or a click on the dark backdrop (works for loaded content too)
  document.addEventListener('click', function (e) {
    var closer = e.target.closest('[data-close]');
    if (closer) closer.closest('dialog').close();
    else if (e.target.tagName === 'DIALOG') e.target.close();
  });

  // "No" on the defense prompt only hides the bubble
  document.querySelectorAll('[data-dismiss]').forEach(function (b) {
    b.addEventListener('click', function () { document.getElementById(b.dataset.dismiss).hidden = true; });
  });

    // clicking the last circle brings the "Is your defense completed?" bubble back
    document.querySelectorAll('[data-show]').forEach(function (b) {
        b.addEventListener('click', function () {
            var el = document.getElementById(b.dataset.show);
            if (el) el.hidden = false;
        });
    });

  // View Capstone Title
  var viewDialog = document.getElementById('pmViewDialog');
  document.querySelectorAll('[data-view-title]').forEach(function (a) {
    a.addEventListener('click', function (e) {
      e.preventDefault();
      fetch(viewDialog.dataset.detailsUrl + '?id=' + a.dataset.viewTitle, { credentials: 'same-origin' })
        .then(function (r) { return r.ok ? r.text() : Promise.reject(); })
        .then(function (html) {
          document.getElementById('pmViewBody').innerHTML = html;
          viewDialog.showModal();
        });
    });
  });

  /* ---- Protect the Capstone Title details ----------------------------------
     The details are shown normally. A website cannot detect a screenshot before it
     is taken, so the details are hidden (blurred) the moment one is ATTEMPTED:
       - Print Screen, the Windows / Cmd key (Win+Shift+S, Cmd+Shift+4 ...), Ctrl+P
       - the window losing focus (snipping tool, another app or tab)
     and they come back by themselves right after. Text also cannot be selected,
     copied, cut, dragged or right-clicked.
     This is a deterrent only: it does not stop a photo taken with another device. */
  var SHIELD = 'pm-shield';
  var shieldTimer = null;

  function viewCard() { return viewDialog && viewDialog.querySelector('.pm-view'); }
  function viewOpen() { return viewDialog && viewDialog.open; }

  function shieldOn() {
    clearTimeout(shieldTimer);
    var card = viewCard();
    if (card) card.classList.add(SHIELD);
  }

  // show the details again shortly after the window is focused and no key is held
  function shieldOff(delay) {
    clearTimeout(shieldTimer);
    shieldTimer = setTimeout(function () {
      if (!document.hidden && document.hasFocus()) {
        var card = viewCard();
        if (card) card.classList.remove(SHIELD);
      }
    }, delay || 700);
  }

  function clearClipboard() {
    try { if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(' '); } catch (err) { /* ignore */ }
  }

  ['copy', 'cut', 'contextmenu', 'dragstart', 'selectstart'].forEach(function (type) {
    viewDialog.addEventListener(type, function (e) {
      if (e.target.closest && e.target.closest('[data-close]')) return;
      e.preventDefault();
    });
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

    // copy / cut / select all / print / save / view-source
    if ((e.ctrlKey || e.metaKey) && ['c', 'x', 'a', 'p', 's', 'u'].indexOf(k) > -1) {
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
