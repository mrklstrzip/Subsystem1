/* ==========================================================================
   Capstone Coordinator - search & filters for the Schedule Management,
   Under Review and Defense Schedule tables.
   The rows themselves come from the database (drawn by the server).
   ========================================================================== */
(function () {
    'use strict';

    if (window.CC) return;   // safe if the script ends up included twice

    /* ------------------------------------------------------------------ *
     *  Page config
     * ------------------------------------------------------------------ */
    var PAGES = [
        {
            tableId: 'assessmentCasesTable',
            emptyText: 'No assessment cases found',
            filters: { status: ['Pending', 'In Progress', 'Completed', 'Resolved', 'Approved', 'Rejected'] }
        },
        {
            tableId: 'scheduleTable',
            emptyText: 'No schedules found',
            pageSize: 8,
            filters: { section: true, status: ['Submitted', 'Not submitted', 'Not sent to Dean', 'Submitted to Dean', 'Under Review'], adviser: true }
        },
        {
            tableId: 'conflictTable',
            emptyText: 'No items under review',
            filters: { status: ['Resolved', 'In Progress'] }
        },
        {
            tableId: 'defenseScheduleTable',
            emptyText: 'No defense schedules found',
            filters: { program: true, status: ['Upcoming', 'Ongoing', 'Completed'], adviser: true }
        }
    ];

    var pages = [];

    /* ------------------------------------------------------------------ *
     *  Helpers
     * ------------------------------------------------------------------ */
    function $(id) { return document.getElementById(id); }
    function all(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }
    function norm(s) { return String(s == null ? '' : s).replace(/\s+/g, ' ').trim().toLowerCase(); }

    // "SBCS-4D" -> "sbcs"
    function programCode(text) { return norm(text).split('-')[0]; }

    function unique(list) {
        return list.filter(function (v, i) { return v && list.indexOf(v) === i; });
    }


    // Row info used by search / filters
    function rowInfo(row) {
        var program = row.querySelector('.program-badge');
        var adviser = row.querySelector('.adviser-cell');
        var section = row.querySelector('.program-badge');

        var text = [];
        var cells = row.querySelectorAll('td');
        for (var i = 0; i < cells.length; i++) {
            if (!cells[i].classList.contains('action-cell')) text.push(cells[i].textContent);   // skip "View" buttons
        }

        return {
            program: norm(row.getAttribute('data-program')) || (program ? programCode(program.textContent) : ''),
            section: norm(row.getAttribute('data-section')) || (section ? norm(section.textContent) : ''),
            manuscriptStatus: norm(row.getAttribute('data-manuscript-status')),
            deanStatus: norm(row.getAttribute('data-dean-status')),
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

        var allOpts = [{ value: 'all', label: 'All' }].concat(options);
        allOpts.forEach(function (o, i) {
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
        var state = { search: '', program: 'all', section: 'all', status: 'all', adviser: 'all', page: 1 };
        var pagination = cfg.pageSize ? $('schedulePagination') : null;
        var paginationSummary = $('schedulePaginationSummary');
        var paginationControls = $('schedulePaginationControls');

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
            var matchingRows = [];
            dataRows().forEach(function (row) {
                var info = rowInfo(row);
                var ok =
                    tokens.every(function (t) { return info.haystack.indexOf(t) > -1; }) &&
                    (state.program === 'all' || info.program === state.program) &&
                    (state.status === 'all' || info.manuscriptStatus === state.status || info.deanStatus === state.status) &&
                    (state.adviser === 'all' || info.adviser === state.adviser) &&
                    (state.section === 'all' || info.section === state.section);
                if (ok) matchingRows.push(row);
            });

            var totalPages = cfg.pageSize ? Math.max(1, Math.ceil(matchingRows.length / cfg.pageSize)) : 1;
            state.page = Math.max(1, Math.min(state.page, totalPages));
            var firstIndex = cfg.pageSize ? (state.page - 1) * cfg.pageSize : 0;
            var lastIndex = cfg.pageSize ? firstIndex + cfg.pageSize : matchingRows.length;
            var visibleRows = matchingRows.slice(firstIndex, lastIndex);
            var visibleSet = new Set(visibleRows);
            dataRows().forEach(function (row) { row.hidden = !visibleSet.has(row); });
            emptyRow.hidden = matchingRows.length > 0;

            if (pagination && paginationSummary && paginationControls) {
                var from = matchingRows.length ? firstIndex + 1 : 0;
                var to = Math.min(lastIndex, matchingRows.length);
                paginationSummary.textContent = 'Showing ' + from + '-' + to + ' of ' + matchingRows.length + ' titles';
                renderPagination(totalPages);
            }
        }

        function renderPagination(totalPages) {
            if (!paginationControls) return;
            paginationControls.innerHTML = '';
            function addButton(label, page, active, disabled) {
                var button = document.createElement('button');
                button.type = 'button';
                button.className = 'schedule-page-link' + (active ? ' active' : '');
                button.textContent = label;
                button.disabled = !!disabled;
                if (active) button.setAttribute('aria-current', 'page');
                button.setAttribute('data-page', page);
                paginationControls.appendChild(button);
            }
            addButton('Previous', Math.max(1, state.page - 1), false, state.page <= 1);
            var start = Math.max(1, state.page - 2);
            var end = Math.min(totalPages, state.page + 2);
            for (var page = start; page <= end; page++) addButton(String(page), page, page === state.page, false);
            addButton('Next', Math.min(totalPages, state.page + 1), false, state.page >= totalPages);
        }

        if (paginationControls) paginationControls.addEventListener('click', function (event) {
            var button = event.target.closest ? event.target.closest('[data-page]') : null;
            if (!button || button.disabled) return;
            state.page = parseInt(button.getAttribute('data-page'), 10) || 1;
            apply();
        });

        // Search bar
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                state.search = searchInput.value;
                state.page = 1;
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
                    } else if (key === 'section') {
                        options = unique(rows.map(function (r) { return r.section; })).sort()
                            .map(function (section) { return { value: section, label: section.toUpperCase() }; });
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
                    state.page = 1;
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
                state = { search: '', program: 'all', section: 'all', status: 'all', adviser: 'all', page: 1 };
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

    function init() {
        PAGES.forEach(initPage);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
