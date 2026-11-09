/* Student - saves "read" when a notification is clicked (the drawer itself is drawn by the server) */
(function () {
    'use strict';

    var panel = document.getElementById('notifPanel');
    if (!panel) return;

    // Capture phase: runs before Notifs.js removes the "unread" class from the item.
    panel.addEventListener('click', function (e) {
        var target = e.target;
        if (!(target instanceof Element)) return;

        var item = target.closest('.notif-item');
        if (!item || !item.classList.contains('unread')) return;

        var id = item.getAttribute('data-id');
        if (!id) return;

        try {
            fetch('/Student/StudentApi/notificationRead', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: JSON.stringify({ id: id }),
                keepalive: true
            });
        } catch (err) { /* it will simply show as unread again next time */ }
    }, true);
})();
