// Collapsible sidebar (all roles): thin by default, wide while hovered, or pinned wide with the hamburger.
// The pinned choice is remembered, so it stays that way on every page.
(function () {
    'use strict';

    var KEY = 'sidebarOpen';
    var wrapper = document.getElementById('layoutWrapper');
    var sidebar = document.getElementById('sidebar');
    var toggle = document.getElementById('sidebarToggle');
    if (!wrapper || !sidebar || !toggle) return;

    function setOpen(open) {
        wrapper.classList.toggle('sidebar-open', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    // restore the saved choice without animating
    var saved = false;
    try { saved = localStorage.getItem(KEY) === '1'; } catch (err) { saved = false; }
    wrapper.classList.add('no-anim');
    setOpen(saved);
    window.setTimeout(function () { wrapper.classList.remove('no-anim'); }, 50);

    toggle.addEventListener('click', function () {
        var open = !wrapper.classList.contains('sidebar-open');
        setOpen(open);
        try { localStorage.setItem(KEY, open ? '1' : '0'); } catch (err) { /* storage unavailable */ }

        // after collapsing, stay thin until the mouse leaves (otherwise hover keeps it wide and it looks stuck)
        if (!open) wrapper.classList.add('sidebar-force-thin');
        else wrapper.classList.remove('sidebar-force-thin');
    });

    sidebar.addEventListener('mouseleave', function () {
        wrapper.classList.remove('sidebar-force-thin');
    });
})();