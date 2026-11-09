document.addEventListener('DOMContentLoaded', function () {
    const bell = document.getElementById('notifBell');
    const panel = document.getElementById('notifPanel');
    const closeBtn = document.getElementById('notifClose');

    if (!bell || !panel) return;

    const items = panel.querySelectorAll('.notif-item');
    const categoryTabs = panel.querySelectorAll('.notif-category-tabs .notif-tab');
    const standardTabs = panel.querySelectorAll('.notif-tabs:not(.notif-category-tabs) .notif-tab');

    // Toggle notification panel visibility
    function toggle(open) {
        panel.hidden = !open;
        bell.setAttribute('aria-expanded', String(open));

        const icon = bell.querySelector('i');
        if (icon) {
            if (open) {
                icon.classList.remove('fa-regular');
                icon.classList.add('fa');
            } else {
                icon.classList.remove('fa');
                icon.classList.add('fa-regular');
            }
        }
    }

    // Bell click event
    bell.addEventListener('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const isOpen = bell.getAttribute('aria-expanded') === 'true';
        toggle(!isOpen);
    });

    // Close button click event
    if (closeBtn) {
        closeBtn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            toggle(false);
        });
    }

    // Close panel when clicking outside
    document.addEventListener('click', function (e) {
        const target = e.target;
        if (!(target instanceof Element)) return;
        if (panel.contains(target) || bell.contains(target)) return;
        toggle(false);
    });

    // Close panel with Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            toggle(false);
        }
    });

    // Unified filtering function to apply both Main Filter and Category Filter together
    function applyFilters() {
        const activeStandardTab = panel.querySelector('.notif-tabs:not(.notif-category-tabs) .notif-tab.active');
        const activeCategoryTab = panel.querySelector('.notif-category-tabs .notif-tab.active');

        const filter = activeStandardTab ? (activeStandardTab.getAttribute('data-filter') || activeStandardTab.getAttribute('data-tab')) : 'all';
        const type = activeCategoryTab ? activeCategoryTab.getAttribute('data-type') : 'all';

        items.forEach(function (item) {
            const isUnread = item.classList.contains('unread');
            const itemType = item.getAttribute('data-type');

            let matchesStandard = (filter === 'all' || (filter === 'unread' && isUnread));
            let matchesCategory = (type === 'all' || itemType === type);

            if (matchesStandard && matchesCategory) {
                item.style.display = '';
            } else {
                item.style.display = 'none';
            }
        });
    }

    // Category/Type filter click event
    categoryTabs.forEach(function (tab) {
        tab.addEventListener('click', function (e) {
            e.preventDefault();
            categoryTabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            applyFilters();
        });
    });

    // Standard All / Unread filter click event
    standardTabs.forEach(function (tab) {
        tab.addEventListener('click', function (e) {
            e.preventDefault();
            standardTabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            applyFilters();
        });
    });

    // Refresh unread counts, badges, and indicator dots
    function refreshCounts() {
        const currentItems = panel.querySelectorAll('.notif-item');
        let total = currentItems.length;
        let unread = 0;

        currentItems.forEach(function (item) {
            if (item.classList.contains('unread')) {
                unread++;
            }
        });

        const allTab = panel.querySelector('.notif-tab[data-filter="all"], .notif-tab[data-tab="all"]');
        if (allTab) {
            const allCount = allTab.querySelector('.notif-count');
            if (allCount) {
                allCount.textContent = '(' + total + ')';
            }
        }

        const unreadTab = panel.querySelector('.notif-tab[data-filter="unread"], .notif-tab[data-tab="unread"]');
        if (unreadTab) {
            const unreadCount = unreadTab.querySelector('.notif-count');
            if (unreadCount) {
                unreadCount.textContent = '(' + unread + ')';
            }
        }

        const notifDot = document.getElementById('notifDot');
        if (notifDot) {
            notifDot.style.display = unread > 0 ? '' : 'none';
        }

        const newBadge = panel.querySelector('.notif-badge');
        if (newBadge) {
            if (unread > 0) {
                newBadge.textContent = unread + ' new';
                newBadge.style.display = '';
            } else {
                newBadge.style.display = 'none';
            }
        }

        // Re-apply filters to update view based on counts/status changes
        applyFilters();
    }

    // Item click handling: button actions vs. read-only item click
    panel.addEventListener('click', function (e) {
        const target = e.target;
        if (!(target instanceof Element)) return;

        const button = target.closest('[data-action]');

        // If clicking an explicit action button
        if (button) {
            e.preventDefault();
            e.stopPropagation();

            const item = button.closest('.notif-item');
            if (!item) return;

            const action = button.getAttribute('data-action');

            if (action === 'create' || action === 'approve' || action === 'cancel') {
                item.classList.remove('unread');
                const unreadDot = item.querySelector('.unread-dot');
                if (unreadDot) unreadDot.remove();

                const actions = item.querySelector('.notif-actions');
                if (actions) {
                    let labelText = 'Created';
                    if (action === 'approve') labelText = 'Approved';
                    if (action === 'cancel') labelText = 'Cancelled';
                    actions.innerHTML = '<span class="notif-time">' + labelText + '</span>';
                }
                refreshCounts();
            }
            return;
        }

        // If clicking anywhere on a read-only notification item that is unread
        const item = target.closest('.notif-item');
        if (item && item.classList.contains('unread')) {
            item.classList.remove('unread');
            const unreadDot = item.querySelector('.unread-dot');
            if (unreadDot) unreadDot.remove();
            refreshCounts();
        }
    });

    // Initialize notification panel state
    toggle(false);
    refreshCounts();
});