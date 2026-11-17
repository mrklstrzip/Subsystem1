/* School Year filter (Capstone Coordinator and Dean tabs).
   A school year runs June 1 - May 31 and is written "2026-2027" (the same rule as Data/SchoolYear.cs).
   Every table row that belongs to a school year carries data-sy="2026-2027"; this script adds the
   "School Year" dropdown and hides the rows of the other years. Rows without data-sy are always shown.
   - Dean pages have a server-drawn <select id="fSY" data-sy-filter>; this script fills it in.
   - Coordinator pages get a dropdown that looks like the other filters, added in front of them.
   The page's own search and filters keep working: this script only adds the "sy-hidden" class. */
(function () {
    'use strict';

    var ALL = 'all';
    var KEY = 'sy-filter:' + location.pathname.toLowerCase();

    function all(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

    function currentSy() {
        var now = new Date();
        var start = now.getMonth() + 1 >= 6 ? now.getFullYear() : now.getFullYear() - 1;
        return start + '-' + (start + 1);
    }

    function remember(v) { try { sessionStorage.setItem(KEY, v); } catch (e) { } }
    function recall() { try { return sessionStorage.getItem(KEY); } catch (e) { return null; } }

    function init() {
        var rows = all('tr[data-sy]');
        var native = document.getElementById('fSY');
        if (!rows.length && !native) return;

        // school years that exist on this page (newest first), plus the current one
        var years = [];
        rows.forEach(function (r) {
            var y = r.getAttribute('data-sy');
            if (y && years.indexOf(y) < 0) years.push(y);
        });
        var cur = currentSy();
        if (years.indexOf(cur) < 0) years.push(cur);
        years.sort().reverse();

        // start on the current school year when it has records, otherwise show everything
        var value = recall();
        if (value !== ALL && years.indexOf(value) < 0) {
            value = rows.some(function (r) { return r.getAttribute('data-sy') === cur; }) ? cur : ALL;
        }

        var table = rows.length ? rows[0].closest('table') : null;
        var tbody = table ? table.tBodies[0] : null;
        var empty = null;

        function apply() {
            var shown = 0;
            rows.forEach(function (r) {
                var y = r.getAttribute('data-sy');
                var hide = value !== ALL && !!y && y !== value;
                r.classList.toggle('sy-hidden', hide);
                if (!hide) shown++;
            });

            if (!tbody) return;
            if (!empty) {
                empty = document.createElement('tr');
                empty.className = 'sy-empty-row';
                var td = document.createElement('td');
                td.colSpan = Math.max(1, table.tHead && table.tHead.rows[0] ? table.tHead.rows[0].cells.length : 1);
                td.className = 'sy-empty-cell';
                empty.appendChild(td);
                tbody.appendChild(empty);
            }
            empty.firstChild.textContent = 'No records for School Year ' + value + '.';
            empty.style.display = shown === 0 && value !== ALL ? '' : 'none';
        }

        function setValue(v) {
            value = v;
            remember(v);
            apply();
        }

        if (native) buildNative(native, years, value, setValue);
        else buildCoordinator(rows, table, years, value, setValue);

        apply();
    }

    /* ---------- Dean: fill the server-drawn <select> ---------- */
    function buildNative(select, years, value, setValue) {
        select.innerHTML = '';
        [ALL].concat(years).forEach(function (y) {
            var o = document.createElement('option');
            o.value = y;
            o.textContent = y === ALL ? 'All' : y;
            if (y === value) o.selected = true;
            select.appendChild(o);
        });
        select.addEventListener('change', function () { setValue(select.value); });
    }

    /* ---------- Coordinator: a dropdown in the same style as Program / Status / Adviser ---------- */
    function buildCoordinator(rows, table, years, value, setValue) {
        var wrap = document.querySelector('.filter-wrapper');
        if (!wrap) {
            // pages without filters (Audit History, Completed Manuscripts): a small bar above the table
            var card = table.closest('.schedule-management-card, .audit-management-card, .os-table-wrap') || table;
            var bar = document.createElement('div');
            bar.className = 'sy-bar';
            card.parentNode.insertBefore(bar, card);
            wrap = bar;
        }

        var box = document.createElement('div');
        box.className = 'custom-select-box sy-box';

        var legend = document.createElement('span');
        legend.className = 'filter-legend';
        legend.textContent = 'School Year';

        var toggle = document.createElement('div');
        toggle.className = 'select-selected';
        toggle.tabIndex = 0;
        toggle.innerHTML = '<i class="fa-solid fa-calendar-days"></i><span></span><i class="fa-solid fa-chevron-down chevron"></i>';
        var label = toggle.querySelector('span');

        var menu = document.createElement('div');
        menu.className = 'custom-select-dropdown sy-menu';
        menu.setAttribute('role', 'listbox');

        function text(v) { return v === ALL ? 'All' : v; }

        function mark() {
            all('.select-option', menu).forEach(function (o) {
                o.classList.toggle('selected', o.getAttribute('data-value') === value);
            });
            label.textContent = text(value);
        }

        [ALL].concat(years).forEach(function (y) {
            var o = document.createElement('div');
            o.className = 'select-option';
            o.setAttribute('role', 'option');
            o.setAttribute('data-value', y);
            o.textContent = text(y);
            menu.appendChild(o);
        });

        function setOpen(on) {
            if (on) {
                all('.custom-select-box.open').forEach(function (b) {
                    if (b === box) return;
                    b.classList.remove('open');
                    var m = b.querySelector('.custom-select-dropdown');
                    if (m) m.classList.remove('open');
                });
            }
            box.classList.toggle('open', on);
            menu.classList.toggle('open', on);
        }

        toggle.addEventListener('click', function (e) {
            setOpen(!menu.classList.contains('open'));
            e.stopPropagation();
        });
        toggle.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                setOpen(!menu.classList.contains('open'));
            } else if (e.key === 'Escape') {
                setOpen(false);
            }
        });
        menu.addEventListener('click', function (e) {
            var opt = e.target.closest('.select-option');
            if (!opt) return;
            value = opt.getAttribute('data-value');
            mark();
            setOpen(false);
            setValue(value);
            e.stopPropagation();
        });
        document.addEventListener('click', function () { setOpen(false); });

        box.appendChild(legend);
        box.appendChild(toggle);
        box.appendChild(menu);
        wrap.insertBefore(box, wrap.firstChild);
        mark();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
