/* ==========================================================================
   Capstone Coordinator – search & filter behavior (no backend)
   --------------------------------------------------------------------------
   Powers three pages: Schedule Management, Conflict Result and Defense
   Schedule. Each page is detected by its table id, so the same script can
   be included on all of them. Rows are filtered in the browser by reading
   the table itself, so rows whose status changes at runtime (Submit /
   Resolve in the popups) are picked up automatically.
   ========================================================================== */
(function () {
    'use strict';

    if (window.CC) return;   // safe if the script ends up included twice

    /* ------------------------------------------------------------------ *
     *  Page config
     *  filters: which filter boxes (matched by their legend text) are live.
     *    - an array  -> fixed option list (status labels, in this order)
     *    - true      -> options are built from the table's own rows
     * ------------------------------------------------------------------ */
    var PAGES = [
        {
            tableId: 'scheduleTable',
            emptyText: 'No schedules found',
            filters: { program: true, status: ['To Submit', 'In Conflict', 'Submitted'], adviser: true }
        },
        {
            tableId: 'conflictTable',
            emptyText: 'No conflicts found',
            filters: { status: ['Resolved', 'In Progress'] }
        },
        {
            tableId: 'defenseScheduleTable',
            emptyText: 'No defense schedules found',
            filters: { program: true, status: ['Upcoming', 'Ongoing', 'Completed'], adviser: true }
        }
    ];

    var pages = [];   // initialised pages (used by CC.refresh / CC.clearFilters)

    /* ------------------------------------------------------------------ *
     *  Helpers
     * ------------------------------------------------------------------ */
    function $(id) { return document.getElementById(id); }

    function norm(s) { return String(s == null ? '' : s).replace(/\s+/g, ' ').trim().toLowerCase(); }

    // "BSCS-4D" -> "bscs"
    function programCode(text) { return norm(text).split('-')[0]; }

    function unique(list) {
        return list.filter(function (v, i) { return v && list.indexOf(v) === i; });
    }

    // Everything the filters/search need to know about one table row
    function rowInfo(row) {
        var program = row.querySelector('.program-badge');
        var status = row.querySelector('.status-badge');
        var adviser = row.querySelector('.adviser-cell');

        var text = [];
        var cells = row.querySelectorAll('td');
        for (var i = 0; i < cells.length; i++) {
            if (!cells[i].classList.contains('action-cell')) text.push(cells[i].textContent);   // skip "View" buttons
        }

        return {
            program: program ? programCode(program.textContent) : '',
            status: status ? norm(status.textContent) : '',
            adviser: adviser ? norm(adviser.textContent) : '',
            haystack: norm(text.join(' '))
        };
    }

    /* ------------------------------------------------------------------ *
     *  Filter dropdowns
     * ------------------------------------------------------------------ */
    function closeAllDropdowns(except) {
        var boxes = document.querySelectorAll('.custom-select-box.open');
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i] === except) continue;
            boxes[i].classList.remove('open');
            var m = boxes[i].querySelector('.custom-select-dropdown');
            if (m) m.classList.remove('open');
        }
    }

    function buildDropdown(box, options, onPick) {
        var toggle = box.querySelector('.select-selected');
        var label = toggle.querySelector('span');

        var menu = document.createElement('div');
        menu.className = 'custom-select-dropdown';
        menu.setAttribute('role', 'listbox');

        var all = [{ value: 'all', label: 'All' }].concat(options);
        all.forEach(function (o, i) {
            var el = document.createElement('div');
            el.className = 'select-option' + (i === 0 ? ' selected' : '');
            el.setAttribute('role', 'option');
            el.setAttribute('data-value', o.value);
            el.textContent = o.label;
            menu.appendChild(el);
        });
        box.appendChild(menu);

        function setOpen(on) {
            closeAllDropdowns(on ? box : null);
            box.classList.toggle('open', on);
            menu.classList.toggle('open', on);
        }

        toggle.setAttribute('tabindex', '0');
        toggle.addEventListener('click', function (e) {
            setOpen(!menu.classList.contains('open'));
            e.stopPropagation();
        });
        toggle.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                setOpen(!menu.classList.contains('open'));
            }
        });

        menu.addEventListener('click', function (e) {
            var opt = e.target.closest('.select-option');
            if (!opt) return;
            var sel = menu.querySelector('.select-option.selected');
            if (sel) sel.classList.remove('selected');
            opt.classList.add('selected');
            label.textContent = opt.textContent;
            setOpen(false);
            onPick(opt.getAttribute('data-value'));
            e.stopPropagation();
        });

        // used by clearFilters()
        box._reset = function () {
            var sel = menu.querySelector('.select-option.selected');
            if (sel) sel.classList.remove('selected');
            menu.firstChild.classList.add('selected');
            label.textContent = 'All';
        };
    }

    document.addEventListener('click', function () { closeAllDropdowns(null); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeAllDropdowns(null); });

    /* ------------------------------------------------------------------ *
     *  One page: search + filters over its table
     * ------------------------------------------------------------------ */
    function initPage(cfg) {
        var table = $(cfg.tableId);
        if (!table || !table.tBodies.length) return;

        var tbody = table.tBodies[0];
        var searchInput = $('searchInput');
        var state = { search: '', program: 'all', status: 'all', adviser: 'all' };

        // "No results" row (kept in the tbody, toggled with the hidden attribute)
        var cols = table.tHead ? table.tHead.rows[0].cells.length : 1;
        var emptyRow = document.createElement('tr');
        emptyRow.className = 'no-results-row';
        emptyRow.hidden = true;
        emptyRow.innerHTML = '<td colspan="' + cols + '"></td>';
        emptyRow.firstChild.textContent = cfg.emptyText;
        tbody.appendChild(emptyRow);

        function dataRows() {
            return Array.prototype.filter.call(tbody.rows, function (r) { return r !== emptyRow; });
        }

        function apply() {
            var tokens = norm(state.search).split(' ').filter(Boolean);
            var visible = 0;

            dataRows().forEach(function (row) {
                var info = rowInfo(row);
                var ok =
                    tokens.every(function (t) { return info.haystack.indexOf(t) > -1; }) &&
                    (state.program === 'all' || info.program === state.program) &&
                    (state.status === 'all' || info.status === state.status) &&
                    (state.adviser === 'all' || info.adviser === state.adviser);
                row.hidden = !ok;
                if (ok) visible++;
            });

            emptyRow.hidden = visible > 0;
        }

        // Search bar
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                state.search = searchInput.value;
                apply();
            });
        }

        // Filter boxes (matched by legend text: Program / Status / Adviser)
        var boxes = [];
        var allBoxes = document.querySelectorAll('.custom-select-box');
        for (var i = 0; i < allBoxes.length; i++) {
            (function (box) {
                var legend = box.querySelector('.filter-legend');
                if (!legend) return;
                var key = norm(legend.textContent);
                var def = cfg.filters[key];
                if (!def) return;

                var options;
                if (Array.isArray(def)) {
                    options = def.map(function (l) { return { value: norm(l), label: l }; });
                } else {
                    var rows = dataRows().map(rowInfo);
                    if (key === 'program') {
                        options = unique(rows.map(function (r) { return r.program; })).sort()
                            .map(function (c) { return { value: c, label: c.toUpperCase() }; });
                    } else {   // adviser: keep the original capitalisation for the label
                        var seen = {};
                        options = [];
                        dataRows().forEach(function (r) {
                            var cell = r.querySelector('.adviser-cell');
                            if (!cell) return;
                            var name = cell.textContent.trim();
                            if (!seen[norm(name)]) { seen[norm(name)] = true; options.push({ value: norm(name), label: name }); }
                        });
                        options.sort(function (a, b) { return a.label.localeCompare(b.label); });
                    }
                }

                buildDropdown(box, options, function (value) {
                    state[key] = value;
                    apply();
                });
                boxes.push(box);
            })(allBoxes[i]);
        }

        // Re-apply when the table changes (e.g. a row's status updates after Submit / Resolve)
        new MutationObserver(apply).observe(tbody, { childList: true, characterData: true, subtree: true });

        pages.push({
            apply: apply,
            clear: function () {
                state = { search: '', program: 'all', status: 'all', adviser: 'all' };
                if (searchInput) searchInput.value = '';
                boxes.forEach(function (b) { b._reset(); });
                apply();
            }
        });

        apply();
    }

    /* ------------------------------------------------------------------ *
     *  Public API
     * ------------------------------------------------------------------ */
    window.CC = {
        refresh: function () { pages.forEach(function (p) { p.apply(); }); },
        clearFilters: function () { pages.forEach(function (p) { p.clear(); }); }
    };

    function init() { PAGES.forEach(initPage); }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();