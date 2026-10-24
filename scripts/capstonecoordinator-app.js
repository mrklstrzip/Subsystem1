/* ==========================================================================
   Capstone Coordinator – shared state + search & filter behavior (no backend)
   --------------------------------------------------------------------------
   1. SHARED STATE: one status per manuscript (to-submit / under-review /
      submitted) plus its saved schedule, kept in localStorage. Every page
      reads from it, so an action on one page shows up on all the others:
        - Schedule Management: Submit  -> Submitted (open review gets resolved)
        - Under Review:        Resolve -> Submitted (review marked Resolved)
        - Defense Schedule:    shows the latest saved time/date/room
        - Dashboard:           Under Review count
   2. SEARCH & FILTERS for Schedule Management, Under Review and
      Defense Schedule tables.
   Rows are linked by their data-id (ms1, ms2, ...).
   ========================================================================== */
(function () {
    'use strict';

    if (window.CC) return;   // safe if the script ends up included twice

    /* ------------------------------------------------------------------ *
     *  Page config
     * ------------------------------------------------------------------ */
    var PAGES = [
        {
            tableId: 'scheduleTable',
            emptyText: 'No schedules found',
            filters: { program: true, status: ['To Submit', 'Under Review', 'Submitted'], adviser: true }
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
    function clone(o) { return JSON.parse(JSON.stringify(o)); }
    function norm(s) { return String(s == null ? '' : s).replace(/\s+/g, ' ').trim().toLowerCase(); }

    // "SBCS-4D" -> "sbcs"
    function programCode(text) { return norm(text).split('-')[0]; }

    function unique(list) {
        return list.filter(function (v, i) { return v && list.indexOf(v) === i; });
    }

    function fmtTime(t) {                       // "08:00" -> "8:00AM"
        if (!t) return '';
        var p = t.split(':');
        var h = parseInt(p[0], 10);
        var ap = h >= 12 ? 'PM' : 'AM';
        h = h % 12 || 12;
        return h + ':' + p[1] + ap;
    }

    function fmtShortDate(iso) {                // "2027-04-25" -> "04-25-27"
        var p = iso.split('-');
        return p[1] + '-' + p[2] + '-' + p[0].slice(2);
    }

    function todayStr() {                       // "11-5-2027" style used in the table
        var n = new Date();
        return (n.getMonth() + 1) + '-' + n.getDate() + '-' + n.getFullYear();
    }

    /* ------------------------------------------------------------------ *
     *  Shared state
     *  TODO: when you have a backend, replace readState/writeState with
     *  your API calls. The rest of the file doesn't need to change.
     * ------------------------------------------------------------------ */
    var STORE_KEY = 'cc.state.v1';

    var STATUS_LABELS = {
        'to-submit': 'To Submit',
        'under-review': 'Under Review',
        'submitted': 'Submitted'
    };

    // Starting status of each manuscript (before anyone changes anything)
    var SEED_STATUS = {
        ms1: 'to-submit',
        ms2: 'under-review',
        ms3: 'to-submit',
        ms4: 'under-review'
    };

    function readState() {
        try { return JSON.parse(localStorage.getItem(STORE_KEY)) || {}; }
        catch (e) { return {}; }
    }
    function writeState(s) {
        try { localStorage.setItem(STORE_KEY, JSON.stringify(s)); } catch (e) { /* storage blocked */ }
    }
    function entry(id) { return readState()[id] || {}; }
    function patch(id, p) {
        var s = readState();
        var cur = s[id] || {};
        for (var k in p) if (Object.prototype.hasOwnProperty.call(p, k)) cur[k] = p[k];
        s[id] = cur;
        writeState(s);
    }

    function getStatus(id) { return entry(id).status || SEED_STATUS[id] || 'to-submit'; }

    function setStatus(id, status) {
        var p = { status: status };
        if (status === 'under-review') p.resolvedDate = null;
        patch(id, p);
        sync();
    }

    // Schedule data (panelists, time, date, room). Returns a copy; falls back to `fallback`.
    function getSchedule(id, fallback) {
        var sc = entry(id).schedule;
        return clone(sc || fallback);
    }
    function saveSchedule(id, data) {
        patch(id, { schedule: clone(data) });
        sync();
    }

    // Submit the schedule. If it was Under Review, the review is resolved today.
    function submit(id, schedule) {
        var p = { status: 'submitted' };
        if (schedule) p.schedule = clone(schedule);
        if (getStatus(id) === 'under-review') p.resolvedDate = todayStr();
        patch(id, p);
        sync();
    }

    /* ------------------------------------------------------------------ *
     *  Sync: paint the shared state onto whatever page is open
     * ------------------------------------------------------------------ */
    function sync() {
        // Schedule Management
        all('#scheduleTable tbody tr[data-id]').forEach(function (row) {
            var id = row.getAttribute('data-id');
            var st = getStatus(id);
            var badge = row.querySelector('.status-badge');
            if (badge) {
                badge.className = 'status-badge ' + st;
                badge.textContent = STATUS_LABELS[st];
            }
            var btn = row.querySelector('.btn-view');
            if (btn) {
                btn.dataset.status = st;
                btn.dataset.problem = st === 'under-review' ? (btn.dataset.conflictType || '') : '';
            }
            var sc = entry(id).schedule;
            var dateCell = row.querySelector('.date-cell');
            if (sc && dateCell) dateCell.textContent = fmtShortDate(sc.date);
        });

        // Under Review list: in-progress while Under Review, resolved otherwise
        all('#conflictTable tbody tr[data-id]').forEach(function (row) {
            var id = row.getAttribute('data-id');
            var st = getStatus(id);
            var resolved = st !== 'under-review';
            var e = entry(id);

            row.dataset.status = resolved ? 'resolved' : 'in-progress';
            row.dataset.stepsDone = resolved ? '4' : '3';

            var badge = row.querySelector('.status-badge');
            if (badge) {
                badge.className = 'status-badge ' + (resolved ? 'resolved' : 'in-progress');
                badge.textContent = resolved ? 'Resolved' : 'In Progress';
            }
            var cells = row.querySelectorAll('.date-cell');
            if (cells[1]) {
                cells[1].textContent = resolved
                    ? (e.resolvedDate || row.getAttribute('data-resolved-date') || '-------')
                    : '-------';
            }
        });

        // Defense Schedule: show the latest saved time / date / room
        all('#defenseScheduleTable tbody tr[data-id]').forEach(function (row) {
            var sc = entry(row.getAttribute('data-id')).schedule;
            if (!sc) return;
            var t = row.querySelector('.time-cell');
            var d = row.querySelector('.date-cell');
            var r = row.querySelector('.room-cell');
            if (t) t.textContent = fmtTime(sc.time.start) + ' - ' + fmtTime(sc.time.end);
            if (d) d.textContent = fmtShortDate(sc.date);
            if (r) r.textContent = sc.room;
            row.dataset.date = sc.date;
        });

        // Dashboard: Under Review count
        var metrics = all('[data-metric="under-review"]');
        if (metrics.length) {
            var n = 0;
            Object.keys(SEED_STATUS).forEach(function (id) { if (getStatus(id) === 'under-review') n++; });
            metrics.forEach(function (m) { m.textContent = n; });
        }
    }

    // Row info used by search / filters
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
        // shared state
        getStatus: getStatus,
        setStatus: setStatus,
        getSchedule: getSchedule,
        saveSchedule: saveSchedule,
        submit: submit,
        sync: sync,
        resetState: function () {
            try { localStorage.removeItem(STORE_KEY); } catch (e) { }
            location.reload();
        },
        // table helpers
        refresh: function () { pages.forEach(function (p) { p.apply(); }); },
        clearFilters: function () { pages.forEach(function (p) { p.clear(); }); }
    };

    // Keep other open tabs/pages in step
    window.addEventListener('storage', function (e) { if (e.key === STORE_KEY) sync(); });

    function init() {
        sync();                    // paint shared state first
        PAGES.forEach(initPage);   // then build filters from the painted table
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();