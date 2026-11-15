/* Admin - Room Schedule (view only).
   Same grid as the Campus Director's Room Schedule, read from AdminApi/roomState and AdminApi/roomSchedule.
   There is no add / edit / delete here. */
(function () {
    'use strict';

    if (window.CD) return;   // safe if the script ends up included twice

    var API = '/Admin/AdminApi/';
    var LOGIN_URL = '/Auth/Login/login';

    var HOUR_FIRST = 8;    // first column on the room schedule grid (7:00 AM)
    var HOUR_END = 18;     // grid ends at 9:00 PM (last column is 8:00 - 9:00 PM)

    // Room schedule colors: Open = white, Reserved = dark blue, Occupied = light blue,
    // Completed = green, Cancelled = red, Unavailable = gray
    var STATUS_COLORS = { open: '#ffffff', reserved: '#1F4E7F', occupied: '#2E6FAD', completed: '#3aa74d', cancelled: '#b3262b', unavailable: '#777777' };
    var STATUS_TEXT = { open: '#1e293b', reserved: '#ffffff', occupied: '#ffffff', completed: '#ffffff', cancelled: '#ffffff', unavailable: '#ffffff' };
    var STATUS_LABELS = { open: 'Open', reserved: 'Reserved', occupied: 'Occupied', completed: 'Completed', cancelled: 'Canceled', unavailable: 'Unavailable' };
    var MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    var DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

    // Data loaded from the database (replaced on every refresh)
    var state = { me: null, buildings: [], rooms: [], activity: [], today: '', nowMinutes: 0 };
    var scheduleItems = [];   // defense schedules for the date shown on the Room Schedule page

    /* ------------------------------------------------------------------ *
     *  Helpers
     * ------------------------------------------------------------------ */
    function $(id) { return document.getElementById(id); }
    function pad(n) { return (n < 10 ? '0' : '') + n; }

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function norm(s) { return String(s || '').trim().toLowerCase(); }
    function plural(n, word) { return n + ' ' + word + (n === 1 ? '' : 's'); }

    function bldgById(id) { return state.buildings.filter(function (b) { return b.id === id; })[0]; }
    function roomById(id) { return state.rooms.filter(function (r) { return r.id === id; })[0]; }
    function roomsOf(buildingId) { return state.rooms.filter(function (r) { return r.buildingId === buildingId; }); }

    function localISO() {
        var d = new Date();
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    // "Today" comes from the server (Philippine time) so every screen agrees.
    function todayISO() { return state.today || localISO(); }

    function fmtLong(iso) {                      // 2026-10-25 -> October 25, 2026
        var p = iso.split('-');
        return MONTHS[+p[1] - 1] + ' ' + (+p[2]) + ', ' + p[0];
    }

    function fmtFull(iso) {                      // 2026-04-25 -> Saturday, April 25, 2026
        var p = iso.split('-');
        var d = new Date(+p[0], +p[1] - 1, +p[2]);
        return DAYS[d.getDay()] + ', ' + fmtLong(iso);
    }

    function parseDate(s) {
        var mt = String(s || '').trim().match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
        if (!mt) return null;
        var y = +mt[1], m = +mt[2], d = +mt[3];
        var chk = new Date(y, m - 1, d);
        if (chk.getFullYear() !== y || chk.getMonth() !== m - 1 || chk.getDate() !== d) return null;
        return y + '-' + pad(m) + '-' + pad(d);
    }

    function fmtTime(min) {                      // 570 -> 9:30 AM
        var h = Math.floor(min / 60), m = min % 60;
        return ((h % 12) || 12) + ':' + pad(m) + ' ' + (h >= 12 ? 'PM' : 'AM');
    }

    function fmtStamp(iso) {                     // ISO time -> 10-25-2026 3:45 PM
        var d = new Date(iso);
        if (isNaN(d.getTime())) return '';
        return pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '-' + d.getFullYear() + ' ' + fmtTime(d.getHours() * 60 + d.getMinutes());
    }

    function optionsHtml(list, selectedId, labelFn) {
        return list.map(function (item) {
            return '<option value="' + esc(item.id) + '"' + (item.id === selectedId ? ' selected' : '') + '>' + esc(labelFn(item)) + '</option>';
        }).join('');
    }

    function setRadio(name, value) {
        var radios = document.getElementsByName(name);
        for (var i = 0; i < radios.length; i++) radios[i].checked = (radios[i].value === value);
    }

    function getRadio(name) {
        var radios = document.getElementsByName(name);
        for (var i = 0; i < radios.length; i++) if (radios[i].checked) return radios[i].value;
        return null;
    }

    function setErr(id, msg) { var e = $(id); if (e) e.textContent = msg || ''; }

    /* ------------------------------------------------------------------ *
     *  Server calls
     * ------------------------------------------------------------------ */
    function sessionExpired() {
        try { localStorage.removeItem('loggedInUser'); } catch (e) { }
        window.location.href = LOGIN_URL;
    }

    // GET when body is undefined, otherwise POST (JSON).
    function api(action, body) {
        var opts = {
            method: body === undefined ? 'GET' : 'POST',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        };
        if (body !== undefined) {
            opts.headers['Content-Type'] = 'application/json';
            opts.body = JSON.stringify(body);
        }

        return fetch(API + action, opts)
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res && res.unauthorized) {
                    sessionExpired();
                    return new Promise(function () { }); // page is leaving; never resolve
                }
                return res;
            });
    }

    function refreshState() {
        return api('roomState').then(function (res) {
            if (!res || !res.success) throw new Error((res && res.message) || 'Could not load data.');
            state.me = res.me || null;
            state.buildings = res.buildings || [];
            state.rooms = res.rooms || [];
            state.activity = res.activity || [];
            state.today = res.today || state.today;
            state.nowMinutes = typeof res.nowMinutes === 'number' ? res.nowMinutes : state.nowMinutes;
        });
    }

    function showLoadError(bodyId, cols, err) {
        var body = $(bodyId);
        var msg = err && err.message ? err.message : 'Could not load data.';
        if (body) {
            body.innerHTML = '<tr><td colspan="' + cols + '" style="text-align:center; color:#B3261E;">' + esc(msg) + '</td></tr>';
        } else {
            console.error(err);
        }
    }

    /* ------------------------------------------------------------------ *
     *  Modals
     *  openModal / closeModal / click-outside-to-close live in
     *  this file. Only Esc-to-close is added here.
     * ------------------------------------------------------------------ */
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var all = document.querySelectorAll('.modal-overlay');
        for (var i = 0; i < all.length; i++) all[i].style.display = 'none';
    });

    /* ------------------------------------------------------------------ *
     *  Room Schedule (view-only grid)
     *
     *  Status rules (derived each time the grid draws; the clock is the server's):
     *    Open        - default for any room / hour with nothing scheduled
     *    Reserved    - a defense schedule that has not started yet
     *    Occupied    - a defense happening right now (today, current time inside it)
     *    Completed   - a defense whose end time has passed (earlier today or a past date)
     *    Cancelled   - a defense that was cancelled (always wins over the time-based statuses)
     *    Unavailable - the room (or its building) is disabled in Room Management
     * ------------------------------------------------------------------ */
    var rendered = {};   // key -> details shown in the view pop-up

    function currentDate() { return parseDate($('sc-date').value) || todayISO(); }

    function statusOf(date, start, end, cancelled) {
        if (cancelled) return 'cancelled';
        var today = todayISO(), now = state.nowMinutes;
        if (date < today) return 'completed';
        if (date === today) {
            if (now >= end) return 'completed';
            if (now >= start) return 'occupied';
        }
        return 'reserved';
    }

    function blocksFor(roomId, date) {
        var list = [];
        scheduleItems.forEach(function (s) {
            if (s.roomId !== roomId) return;
            list.push({
                id: s.id, label: label, start: s.start, end: s.end, ref: s,
                status: statusOf(date, s.start, s.end, s.cancelled)
            });
        });
        list.sort(function (a, b) { return a.start - b.start; });
        return list;
    }

    // True when some part of the grid range (7:00 AM - 9:00 PM) has no schedule on it
    function hasOpenTime(list) {
        var cursor = HOUR_FIRST * 60, endMin = HOUR_END * 60;
        for (var i = 0; i < list.length; i++) {
            if (list[i].start > cursor) return true;
            if (list[i].end > cursor) cursor = list[i].end;
        }
        return cursor < endMin;
    }

    // Schedules are placed by the exact minute (4:30, 1:20, 3:10 ...), not rounded to the hour.
    // The hour lines and the track style are drawn here, so the grid always matches
    // HOUR_FIRST / HOUR_END no matter what the CSS says.
    function trackHtml(list, room, date) {
        var from = HOUR_FIRST * 60, span = (HOUR_END - HOUR_FIRST) * 60, html = '';

        for (var h = HOUR_FIRST; h < HOUR_END; h++) {
            html += '<div style="position:absolute; top:0; bottom:0; width:1px; background:#e3e7f0; left:' +
                ((h - HOUR_FIRST) / (HOUR_END - HOUR_FIRST) * 100).toFixed(3) + '%;"></div>';
        }

        list.forEach(function (blk) {
            var s = Math.max(blk.start, from), e = Math.min(blk.end, from + span);
            if (e <= s) return;
            rendered[blk.id] = { kind: blk.kind, ref: blk.ref, room: room, date: date, status: blk.status, start: blk.start, end: blk.end };
            var left = (s - from) / span * 100, width = (e - s) / span * 100;
            html += '<div class="sched-block sched-slot" title="' + esc(blk.label + ' | ' + fmtTime(blk.start) + ' \u2013 ' + fmtTime(blk.end)) + '" ' +
                'style="left:' + left.toFixed(3) + '%; width:' + width.toFixed(3) + '%; background:' + STATUS_COLORS[blk.status] + '; color:' + STATUS_TEXT[blk.status] + ';" ' +
                'onclick="CD.openSchedView(\'' + esc(blk.id) + '\')">' + esc(blk.label) + '</div>';
        });
        return '<div class="sched-track" style="position:relative; height:34px; background:#f7f8fc;">' + html + '</div>';
    }

    function renderSchedule() {
        var body = $('sc-body');
        if (!body) return;

        var date = currentDate();
        var sFilter = $('sc-status').value;
        var bFilter = $('sc-building') ? $('sc-building').value : 'all';
        var totalCols = HOUR_END - HOUR_FIRST;
        var rows = [];
        rendered = {};

        state.rooms.forEach(function (r) {
            if (bFilter !== 'all' && r.buildingId !== bFilter) return;
            var unavailable = !r.available;
            var list = [], present = {};

            if (unavailable) {
                present.unavailable = true;
            } else {
                list = blocksFor(r.id, date);
                list.forEach(function (blk) { present[blk.status] = true; });
                if (hasOpenTime(list)) present.open = true;
            }
            if (sFilter !== 'all' && !present[sFilter]) return;

            var html = '<td>' + esc(r.name) + '</td>';

            if (unavailable) {
                html += '<td colspan="' + totalCols + '" class="sched-cell" ' +
                    'style="background:' + STATUS_COLORS.unavailable + '; color:' + STATUS_TEXT.unavailable + '; font-weight:600;">Unavailable</td>';
            } else {
                html += '<td colspan="' + totalCols + '" class="sched-track-cell">' + trackHtml(list, r, date) + '</td>';
            }
            rows.push('<tr>' + html + '</tr>');
        });

        body.innerHTML = rows.join('') || '<tr><td colspan="' + (totalCols + 1) + '" style="color:#64748b;">No rooms match the selected filters</td></tr>';
    }

    // View-only pop-up
    function detailRow(label, valueHtml) {
        return '<div class="sched-detail-row"><div class="sched-detail-label">' + esc(label) + '</div>' +
            '<div class="sched-detail-value">' + valueHtml + '</div></div>';
    }

    function openSchedView(key) {
        var d = rendered[key];
        if (!d) return;

        var s = d.ref;
        var rows = detailRow('Title of the Manuscript', esc(s.title || '\u2014')) +
            detailRow('Authors', (s.authors || []).map(esc).join('<br>') || '\u2014') +
            detailRow('Section', esc(s.section || '\u2014')) +
            detailRow('Group Number', s.group ? 'Group ' + esc(s.group) : '\u2014') +
            detailRow('Program', esc(s.program || '\u2014')) +
            detailRow('Day, Date', esc(fmtFull(d.date))) +
            detailRow('Time', esc(fmtTime(d.start) + ' \u2013 ' + fmtTime(d.end))) +
            detailRow('Room', esc(d.room.name));
        $('es-details').innerHTML = rows;

        var badge = $('es-badge');
        badge.textContent = STATUS_LABELS[d.status];
        badge.style.background = STATUS_COLORS[d.status];
        badge.style.color = STATUS_TEXT[d.status];

        openModal('modal-edit-sched');
    }

    // Loads the defense schedules for the chosen date, then redraws the grid.
    // A reply for a date the user has already moved away from is ignored.
    function loadSchedule() {
        var date = currentDate();
        return api('roomSchedule?date=' + encodeURIComponent(date)).then(function (res) {
            if (!res || !res.success) throw new Error((res && res.message) || 'Could not load the schedule.');
            if (date !== currentDate()) return;
            scheduleItems = res.items || [];
            state.today = res.today || state.today;
            state.nowMinutes = typeof res.nowMinutes === 'number' ? res.nowMinutes : state.nowMinutes;
            renderSchedule();
        });
    }

    function fillScheduleBuildings() {
        var bSel = $('sc-building');
        if (!bSel) return;
        var keep = bSel.value;
        bSel.innerHTML = '<option value="all">All buildings</option>' +
            optionsHtml(state.buildings, null, function (b) { return b.name; });
        bSel.value = bldgById(keep) ? keep : 'all';
    }

    function initSchedule() {
        if (!$('sc-body')) return;

        // column headers: 7 AM ... 8 PM (the last column covers 8 - 9 PM), built from the grid range
        var head = '<th class="sched-room-col">Room</th>';
        for (var h = HOUR_FIRST; h < HOUR_END; h++) head += '<th>' + ((h % 12) || 12) + ':00</th>';
        $('sc-head').innerHTML = head;

        $('sc-body').innerHTML = '<tr><td colspan="' + (HOUR_END - HOUR_FIRST + 1) + '" style="color:#64748b;">Loading...</td></tr>';

        function onFail(err) { showLoadError('sc-body', HOUR_END - HOUR_FIRST + 1, err); }

        refreshState().then(function () {
            $('sc-date').value = todayISO();
            fillScheduleBuildings();
            return loadSchedule();
        }).catch(onFail);

        $('sc-building').addEventListener('change', renderSchedule);
        $('sc-status').addEventListener('change', renderSchedule);
        $('sc-date').addEventListener('change', function () { loadSchedule().catch(onFail); });

        // keep "Occupied" and the room list in step with the clock and with other users' changes
        setInterval(function () {
            refreshState().then(function () {
                fillScheduleBuildings();
                return loadSchedule();
            }).catch(function () { /* keep showing the last good data */ });
        }, 60000);
    }

    window.CD = { openSchedView: openSchedView };

    /* ------------------------------------------------------------------ *
 *  Custom dropdown filters (wraps the hidden <select> in each .cdc-select)
 * ------------------------------------------------------------------ */
    function initCdSelect(boxId) {
        var box = $(boxId);
        if (!box) return;
        var select = box.querySelector('select');
        var menu = box.querySelector('.cdc-dropdown');
        var label = box.querySelector('.cdc-selected-text');

        function build() {
            menu.innerHTML = '';
            Array.prototype.forEach.call(select.options, function (o) {
                var div = document.createElement('div');
                div.className = 'cdc-option' + (o.value === select.value ? ' active' : '');
                div.setAttribute('data-value', o.value);
                div.textContent = o.text;
                menu.appendChild(div);
            });
            var opt = select.options[select.selectedIndex];
            label.textContent = opt ? opt.text : '';
        }

        box.querySelector('.cdc-selected').addEventListener('click', function (e) {
            e.stopPropagation();
            var wasOpen = box.classList.contains('open');
            closeCdSelects();
            if (!wasOpen) { build(); box.classList.add('open'); }
        });

        menu.addEventListener('click', function (e) {
            var opt = e.target.closest('.cdc-option');
            if (!opt) return;
            select.value = opt.getAttribute('data-value');
            select.dispatchEvent(new Event('change', { bubbles: true }));
            build();
            box.classList.remove('open');
        });

        // rebuild when other code adds/removes options (e.g. buildings loaded from the server)
        new MutationObserver(build).observe(select, { childList: true });
        select.addEventListener('change', build);
        build();
    }

    function closeCdSelects() {
        var open = document.querySelectorAll('.cdc-select.open');
        for (var i = 0; i < open.length; i++) open[i].classList.remove('open');
    }

    document.addEventListener('click', closeCdSelects);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeCdSelects(); });

    /* ------------------------------------------------------------------ *
     *  Boot
     * ------------------------------------------------------------------ */
    initSchedule();

    initCdSelect('sc-building-box');   // Room Schedule
    initCdSelect('sc-status-box');     // Room Schedule
})();

function openModal(id) {
    var el = document.getElementById(id);
    if (el) el.style.display = 'flex';
}

function closeModal(id) {
    var el = document.getElementById(id);
    if (el) el.style.display = 'none';
}

window.addEventListener('click', function (e) {
    var target = e.target;
    if (target && target.classList && target.classList.contains('modal-overlay')) target.style.display = 'none';
});
