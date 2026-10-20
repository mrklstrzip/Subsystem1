(function () {
  'use strict';

  // who is submitting? (same localStorage user the sidebar uses)
  try {
    var u = JSON.parse(localStorage.getItem('loggedInUser') || 'null');
    var pm = document.getElementById('pmProjectManager');
    if (u && pm) pm.value = u.firstName + ' ' + u.lastName;
  } catch (e) { }

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
})();
