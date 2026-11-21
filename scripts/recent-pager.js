/* Title Screening > Recent screenings: show 5 at a time ("1-5 of 30") with Prev / Next. */
(function () {
    'use strict';

    var PAGE_SIZE = 5;
    var items = Array.prototype.slice.call(document.querySelectorAll('#recentScreenings .recent-item'));
    var pager = document.getElementById('recentPager');
    var label = document.getElementById('recentPagerText');
    var prevBtn = /** @type {HTMLButtonElement | null} */ (document.getElementById('recentPrev'));
    var nextBtn = /** @type {HTMLButtonElement | null} */ (document.getElementById('recentNext'));
    if (!items.length || !pager || !label || !prevBtn || !nextBtn) return;

    var page = 0;
    var pages = Math.ceil(items.length / PAGE_SIZE);

    function draw() {
        var from = page * PAGE_SIZE;
        var to = Math.min(from + PAGE_SIZE, items.length);

        items.forEach(function (el, i) { el.hidden = i < from || i >= to; });
        pager.hidden = pages <= 1;
        label.textContent = (from + 1) + '\u2013' + to + ' of ' + items.length;
        prevBtn.disabled = page === 0;
        nextBtn.disabled = page >= pages - 1;
    }

    prevBtn.addEventListener('click', function () {
        if (page > 0) { page--; draw(); }
    });
    nextBtn.addEventListener('click', function () {
        if (page < pages - 1) { page++; draw(); }
    });

    draw();
})();
