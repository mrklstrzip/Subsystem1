/* Dean - notification drawer.
   The drawer is drawn by the server and Notifs.js handles opening, tabs and counts.
   This file only saves "read" when a notification is clicked. */
(function () {
    'use strict';

    var panel = document.getElementById('notifPanel');
    if (!panel) return;

    // capture phase: runs before Notifs.js changes the item
    panel.addEventListener('click', function (e) {
        var target = e.target;
        if (!(target instanceof Element)) return;

        var item = target.closest('.notif-item');
        if (!item) return;

        var nid = item.getAttribute('data-id');
        var sid = item.getAttribute('data-schedule');
        if (!item.classList.contains('unread')) {
            if (sid) window.location.href = '/Dean/ScheduleApproval/ScheduleApprovalView?open=' + encodeURIComponent(sid);
            return;
        }
        if (!nid) return;

        try {
            fetch('/Dean/DeanApi/notificationRead', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: JSON.stringify({ id: nid }),
                keepalive: true
            });
        } catch (err) { /* it will simply show as unread again next time */ }

        if (sid) window.location.href = '/Dean/ScheduleApproval/ScheduleApprovalView?open=' + encodeURIComponent(sid);
    }, true);
})();
