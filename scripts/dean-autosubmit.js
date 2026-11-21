/* Dean filters: any control with the data-autosubmit attribute submits its form when it changes.
   Replaces the inline onchange="this.form.submit()", which Visual Studio flags as TS2683. */
(function () {
    'use strict';
    document.addEventListener('change', function (e) {
        var el = e.target;
        if (el && el.hasAttribute && el.hasAttribute('data-autosubmit') && el.form) el.form.submit();
    });
})();
