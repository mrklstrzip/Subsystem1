(function () {
    const bell = document.getElementById('notifBell');
    const panel = document.getElementById('notifPanel');
    const closeBtn = document.getElementById('notifClose');
    const dot = document.getElementById('notifDot');
    const badge = document.getElementById('notifBadge');
    const countAll = document.getElementById('countAll');
    const countUnread = document.getElementById('countUnread');

    if (!bell || !panel || !closeBtn || !dot || !badge || !countAll || !countUnread) return;

    function toggle(open) {
        panel.hidden = !open;
        bell.setAttribute('aria-expanded', String(open));
    }

    bell.addEventListener('click', function (e) {
        e.stopPropagation();
        toggle(panel.hidden);
    });

    closeBtn.addEventListener('click', function () { toggle(false); });

    document.addEventListener('click', function (e) {
        const t = e.target;
        if (!(t instanceof Node)) return;
        if (!panel.hidden && !panel.contains(t) && !bell.contains(t)) toggle(false);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') toggle(false);
    });

    // All / Unread tabs
    const tabs = panel.querySelectorAll('.notif-tab');
    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            tabs.forEach(function (t) { t.classList.remove('active'); });
            tab.classList.add('active');
            const onlyUnread = tab.getAttribute('data-tab') === 'unread';
            panel.querySelectorAll('.notif-item').forEach(function (item) {
                if (!(item instanceof HTMLElement)) return;
                item.style.display = (onlyUnread && !item.classList.contains('unread')) ? 'none' : '';
            });
        });
    });

    function refreshCounts() {
        const unread = panel.querySelectorAll('.notif-item.unread').length;
        countUnread.textContent = String(unread);
        countAll.textContent = String(panel.querySelectorAll('.notif-item').length);
        badge.textContent = unread + ' new';
        badge.style.display = unread ? '' : 'none';
        dot.classList.toggle('hidden', unread === 0);
    }

    // Approve / Cancel buttons
    panel.addEventListener('click', function (e) {
        const t = e.target;
        if (!(t instanceof Element)) return;

        const btn = t.closest('[data-action]');
        if (!btn) return;
        const item = btn.closest('.notif-item');
        if (!item) return;

        const action = btn.getAttribute('data-action');

        // TODO: call your controller here, e.g.
        // fetch('/Dean/Dashboard/' + (action === 'approve' ? 'Approve' : 'Cancel') + '?id=' + item.getAttribute('data-id'), { method: 'POST' });

        item.classList.remove('unread');
        const d = item.querySelector('.unread-dot');
        if (d) d.remove();
        const actions = item.querySelector('.notif-actions');
        if (actions) {
            actions.innerHTML = '<span class="notif-time">' + (action === 'approve' ? 'Approved' : 'Cancelled') + '</span>';
        }
        refreshCounts();
    });

    refreshCounts();
})();