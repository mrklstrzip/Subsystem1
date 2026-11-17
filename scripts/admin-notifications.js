/* Admin - "Create" on an account request in the notification drawer opens
   User Management with that request pre-filled (see openRequestFromUrl in admin-app.js). */
(function () {
    'use strict';

    var panel = document.getElementById('notifPanel');
    if (!panel) return;

    // Capture phase: runs before Notifs.js, which would otherwise flip the item to "Created" on screen.
    panel.addEventListener('click', function (e) {
        var t = e.target;
        if (!(t instanceof Element)) return;

        var btn = t.closest('[data-action="create"]');
        if (!btn) return;

        var id = btn.getAttribute('data-request');
        if (!id) return;

        e.preventDefault();
        e.stopPropagation();
        window.location.href = '/Admin/UserManagement/usermanagement?request=' + encodeURIComponent(id);
    }, true);
})();
