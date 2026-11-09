(function () {
    'use strict';

    if (window.CD) return;

    var API = '/CampusDirector/CampusDirectorApi/';
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
        return api('state').then(function (res) {
            if (!res || !res.success) throw new Error((res && res.message) || 'Could not load data.');
            state.me = res.me || null;
            state.buildings = res.buildings || [];
            state.rooms = res.rooms || [];
            state.activity = res.activity || [];
            state.today = res.today || state.today;
            state.nowMinutes = typeof res.nowMinutes === 'number' ? res.nowMinutes : state.nowMinutes;
        });
    }

    // Prevents double-clicks from sending the same request twice.
    var busy = false;

    // send(action, body, onDone, onFail): POST, then reload the lists and call onDone().
    // onFail(message) is called when the server refuses or cannot be reached.
    function send(action, body, onDone, onFail) {
        if (busy) return;
        busy = true;
        api(action, body).then(function (res) {
            if (!res || !res.success) {
                busy = false;
                onFail((res && res.message) || 'Something went wrong. Please try again.');
                return;
            }
            return refreshState().then(function () {
                busy = false;
                onDone();
            }, function () {
                busy = false;
                alert('Saved, but the list could not be reloaded. Please refresh the page.');
            });
        }, function () {
            busy = false;
            onFail('Could not reach the server. Please check your connection and try again.');
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
     *  campusdirector-modal.js. Only Esc-to-close is added here.
     * ------------------------------------------------------------------ */
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var all = document.querySelectorAll('.modal-overlay');
        for (var i = 0; i < all.length; i++) all[i].style.display = 'none';
    });

    /* ------------------------------------------------------------------ *
     *  Dashboard
     *  A room counts as "Active" only when the room AND its building are Active.
     * ------------------------------------------------------------------ */
    function renderDashboard() {
        var total = state.rooms.length;
        var active = state.rooms.filter(function (r) { return r.available; }).length;
        var disabled = total - active;

        $('dash-total-rooms').textContent = total;
        $('dash-active-rooms').textContent = active;
        $('dash-disabled-rooms').textContent = disabled;

        // Welcome banner: the director's last name from the database
        var nameEl = $('dash-welcome-name');
        if (nameEl && state.me && state.me.lastName) nameEl.textContent = state.me.lastName;

        // Room per building (bars are relative to the biggest building)
        var counts = state.buildings.map(function (b) { return { b: b, n: roomsOf(b.id).length }; });
        var max = Math.max.apply(null, counts.map(function (c) { return c.n; }).concat([1]));
        $('dash-buildings').innerHTML = counts.map(function (c) {
            var pct = Math.round(c.n / (max * 1.25) * 100);
            return '<div class="dsh-bar-row">' +
                '<div class="dsh-bar-head"><span>' + esc(c.b.name) + ' | ' + esc(c.b.code) + '</span>' +
                '<b>' + c.n + (c.n === 1 ? ' room' : ' rooms') + '</b></div>' +
                '<div class="dsh-track"><div class="dsh-fill" style="width:' + pct + '%"></div></div></div>';
        }).join('') || '<div class="dsh-empty">No buildings yet</div>';

        // Room status donut (Active vs Disabled)
        var R = 72, SW = 44, C = 2 * Math.PI * R;
        var svg = '<g transform="rotate(-90 100 100)" fill="none" stroke-width="' + SW + '">';
        if (!total) {
            svg += '<circle cx="100" cy="100" r="' + R + '" stroke="#e2e8f0"><title>No rooms yet</title></circle>';
        } else {
            var aLen = C * active / total;
            svg += '<circle cx="100" cy="100" r="' + R + '" stroke="#6b97d6"><title>Disabled Rooms: ' + disabled +
                ' (' + Math.round(disabled / total * 100) + '%)</title></circle>';
            if (active > 0) {
                svg += '<circle cx="100" cy="100" r="' + R + '" stroke="#246a86" stroke-dasharray="' + aLen.toFixed(2) + ' ' + C.toFixed(2) +
                    '"><title>Active Rooms: ' + active + ' (' + Math.round(active / total * 100) + '%)</title></circle>';
            }
        }
        $('dash-donut').innerHTML = svg + '</g>';

        // Recent activity
        $('dash-activity').innerHTML = state.activity.slice(0, 6).map(function (a) {
            return '<tr><td>' + esc(fmtStamp(a.at)) + '</td><td>' + esc(a.text) + '</td></tr>';
        }).join('') || '<tr><td colspan="2" class="dsh-empty">No recent activity</td></tr>';
    }

    function initDashboard() {
        if (!$('dash-total-rooms')) return;

        // Show the name saved at login right away, then the database value replaces it.
        try {
            var u = JSON.parse(localStorage.getItem('loggedInUser') || 'null');
            if (u && u.lastName && $('dash-welcome-name')) $('dash-welcome-name').textContent = u.lastName;
        } catch (e) { }

        $('dash-activity').innerHTML = '<tr><td colspan="2" class="dsh-empty">Loading...</td></tr>';
        refreshState().then(renderDashboard).catch(function (err) { showLoadError('dash-activity', 2, err); });
    }

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
            var label = [s.section, s.group ? 'Group ' + s.group : ''].filter(Boolean).join(' \u00b7 ') || s.title || 'Defense';
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
        return api('schedule?date=' + encodeURIComponent(date)).then(function (res) {
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

    /* ------------------------------------------------------------------ *
     *  Room Management (rooms + buildings)
     * ------------------------------------------------------------------ */
    var editRoomId = null, delRoomId = null, editBldId = null, delBldId = null;

    // A room whose building is Unavailable shows as Unavailable too.
    function statusPill(status, note) {
        var cls = status === 'Active' ? 'rmx-active' : (status === 'Under Maintenance' ? 'rmx-maint' : 'rmx-unavail');
        return '<span class="rmx-pill ' + cls + '"' + (note ? ' title="' + esc(note) + '"' : '') + '>' + esc(status) + '</span>';
    }

    function shortBldg(name) { return String(name || '').replace(/Building/i, 'Bldg.'); }

    function actionIcons(kind, id) {
        return '<button type="button" class="rmx-icon rmx-edit" title="Edit" onclick="CD.openEdit' + kind + '(\'' + esc(id) + '\')"><i class="fa-regular fa-pen-to-square"></i></button>' +
            '<button type="button" class="rmx-icon rmx-trash" title="Delete" onclick="CD.openDel' + kind + '(\'' + esc(id) + '\')"><i class="fa-regular fa-trash-can"></i></button>';
    }

    function fillBuildingSelect(sel, selectedId, includeAll) {
        if (!sel) return;
        sel.innerHTML = (includeAll ? '<option value="all">All buildings</option>' : '') +
            optionsHtml(state.buildings, selectedId, function (b) { return b.name; });
        if (includeAll) sel.value = (selectedId && bldgById(selectedId)) ? selectedId : 'all';
    }

    function renderRooms() {
        var rbody = $('rooms-tbody');
        if (!rbody) return;

        var filterSel = $('rm-building');
        fillBuildingSelect(filterSel, filterSel.value, true);
        var bf = filterSel.value;
        var q = norm(($('rm-search') || {}).value);

        // ---- rooms ----
        function shownStatus(r) { return r.available ? 'Active' : 'Unavailable'; }

        var rooms = state.rooms.filter(function (r) {
            var b = bldgById(r.buildingId) || { name: '', code: '' };
            if (bf !== 'all' && r.buildingId !== bf) return false;
            return !q || norm(r.name + ' ' + b.name + ' ' + b.code + ' ' + shownStatus(r)).indexOf(q) > -1;
        });

        rbody.innerHTML = rooms.map(function (r) {
            var b = bldgById(r.buildingId) || { name: '\u2014' };
            var note = (r.status === 'Active' && !r.available) ? 'Its building is unavailable' : '';
            return '<tr><td class="rmx-b">' + esc(shortBldg(b.name)) + '</td>' +
                '<td>' + esc(r.name) + '</td>' +
                '<td class="rmx-c">' + statusPill(shownStatus(r), note) + '</td>' +
                '<td class="rmx-act">' + actionIcons('Room', r.id) + '</td></tr>';
        }).join('') || '<tr><td colspan="4" class="rmx-empty">No rooms found</td></tr>';

        $('rooms-count').textContent = state.rooms.length + ' rooms across ' + plural(state.buildings.length, 'building');
        $('rooms-foot').textContent = 'Showing ' + rooms.length + ' of ' + state.rooms.length + ' rooms';

        // ---- buildings ----
        var blds = state.buildings.filter(function (b) {
            if (bf !== 'all' && b.id !== bf) return false;
            return !q || norm(b.name + ' ' + b.code + ' ' + b.status).indexOf(q) > -1;
        });

        $('buildings-tbody').innerHTML = blds.map(function (b) {
            return '<tr><td class="rmx-b">' + esc(shortBldg(b.name)) + '</td>' +
                '<td>' + esc(b.code) + '</td>' +
                '<td class="rmx-c">' + statusPill(b.status) + '</td>' +
                '<td class="rmx-act">' + actionIcons('Building', b.id) + '</td></tr>';
        }).join('') || '<tr><td colspan="4" class="rmx-empty">No buildings found</td></tr>';

        $('buildings-count').textContent = plural(state.buildings.length, 'building') + ' registered';
        $('buildings-foot').textContent = 'Showing ' + blds.length + ' of ' + state.buildings.length + ' buildings';
    }

    function initRooms() {
        if (!$('rooms-tbody')) return;

        $('rm-search').addEventListener('input', renderRooms);
        $('rm-building').addEventListener('change', renderRooms);
        $('add-room-building').addEventListener('change', syncAddRoomPrefix);

        $('rooms-tbody').innerHTML = '<tr><td colspan="4" class="rmx-empty">Loading...</td></tr>';
        $('buildings-tbody').innerHTML = '<tr><td colspan="4" class="rmx-empty">Loading...</td></tr>';

        refreshState().then(renderRooms).catch(function (err) {
            showLoadError('rooms-tbody', 4, err);
            showLoadError('buildings-tbody', 4, err);
        });
    }

    /* ---------- Rooms ---------- */
    function syncAddRoomPrefix() {
        var b = bldgById($('add-room-building').value);
        $('add-room-prefix').textContent = b ? b.code : '';
    }

    function openAddRoom() {
        var bf = $('rm-building').value;
        fillBuildingSelect($('add-room-building'), bf !== 'all' ? bf : (state.buildings[0] || {}).id, false);
        syncAddRoomPrefix();
        $('add-room-name').value = '';
        setErr('add-room-error', state.buildings.length ? '' : 'Add a building first.');
        openModal('modal-add-room');
    }

    function saveAddRoom() {
        var b = bldgById($('add-room-building').value);
        if (!b) { setErr('add-room-error', 'Add a building first.'); return; }

        var typed = $('add-room-name').value.trim();
        if (!typed) { setErr('add-room-error', 'Please enter a room number.'); return; }

        setErr('add-room-error', '');
        send('saveRoom', { buildingId: b.id, name: typed }, function () {
            closeModal('modal-add-room');
            renderRooms();
        }, function (msg) { setErr('add-room-error', msg); });
    }

    function openEditRoom(id) {
        var r = roomById(id);
        if (!r) return;
        editRoomId = id;
        fillBuildingSelect($('edit-room-building'), r.buildingId, false);
        $('edit-room-name').value = r.name;
        setRadio('edit-rm-status', r.status);
        setErr('edit-room-error', '');
        openModal('modal-edit-room');
    }

    function saveEditRoom() {
        var r = roomById(editRoomId);
        if (!r) { closeModal('modal-edit-room'); return; }

        var bid = $('edit-room-building').value;
        var name = $('edit-room-name').value.trim();
        if (!bldgById(bid)) { setErr('edit-room-error', 'Please choose a building.'); return; }
        if (!name) { setErr('edit-room-error', 'Please enter a room number.'); return; }

        setErr('edit-room-error', '');
        send('saveRoom', { id: r.id, buildingId: bid, name: name, status: getRadio('edit-rm-status') || r.status }, function () {
            closeModal('modal-edit-room');
            renderRooms();
        }, function (msg) { setErr('edit-room-error', msg); });
    }

    function openDelRoom(id) {
        var r = roomById(id);
        if (!r) return;
        delRoomId = id;
        $('del-room-name').textContent = r.name;
        openModal('modal-del-room');
    }

    function confirmDelRoom() {
        if (!delRoomId) { closeModal('modal-del-room'); return; }
        send('deleteRoom', { id: delRoomId }, function () {
            delRoomId = null;
            closeModal('modal-del-room');
            renderRooms();
        }, function (msg) {
            closeModal('modal-del-room');
            alert(msg);
        });
    }

    /* ---------- Buildings ---------- */
    function openAddBuilding() {
        $('add-bld-name').value = '';
        $('add-bld-code').value = '';
        setErr('add-bld-error', '');
        openModal('modal-add-building');
    }

    function saveAddBuilding() {
        var name = $('add-bld-name').value.trim();
        var code = $('add-bld-code').value.trim().toUpperCase();
        if (!name || !code) { setErr('add-bld-error', 'Please enter the building name and code.'); return; }

        setErr('add-bld-error', '');
        send('saveBuilding', { name: name, code: code }, function () {
            closeModal('modal-add-building');
            renderRooms();
        }, function (msg) { setErr('add-bld-error', msg); });
    }

    function openEditBuilding(id) {
        var b = bldgById(id);
        if (!b) return;
        editBldId = id;
        $('edit-bld-name').value = b.name;
        $('edit-bld-code').value = b.code;
        setRadio('edit-bld-status', b.status);
        setErr('edit-bld-error', '');
        openModal('modal-edit-building');
    }

    function saveEditBuilding() {
        var b = bldgById(editBldId);
        if (!b) { closeModal('modal-edit-building'); return; }
        var name = $('edit-bld-name').value.trim();
        var code = $('edit-bld-code').value.trim().toUpperCase();
        if (!name || !code) { setErr('edit-bld-error', 'Please enter the building name and code.'); return; }

        setErr('edit-bld-error', '');
        send('saveBuilding', { id: b.id, name: name, code: code, status: getRadio('edit-bld-status') || b.status }, function () {
            closeModal('modal-edit-building');
            renderRooms();
        }, function (msg) { setErr('edit-bld-error', msg); });
    }

    function openDelBuilding(id) {
        var b = bldgById(id);
        if (!b) return;
        delBldId = id;
        var n = roomsOf(id).length;
        $('del-bld-name').textContent = b.name;
        $('del-bld-extra').textContent = n ? 'Its ' + plural(n, 'room') + ' will also be deleted.' : '';
        openModal('modal-del-building');
    }

    function confirmDelBuilding() {
        if (!delBldId) { closeModal('modal-del-building'); return; }
        send('deleteBuilding', { id: delBldId }, function () {
            delBldId = null;
            closeModal('modal-del-building');
            renderRooms();
        }, function (msg) {
            closeModal('modal-del-building');
            alert(msg);
        });
    }

    /* ------------------------------------------------------------------ *
     *  Public API (used by inline onclick handlers in the views)
     * ------------------------------------------------------------------ */
    window.CD = {
        // schedule (view only)
        openSchedView: openSchedView,
        // rooms
        openAddRoom: openAddRoom, saveAddRoom: saveAddRoom,
        openEditRoom: openEditRoom, saveEditRoom: saveEditRoom,
        openDelRoom: openDelRoom, confirmDelRoom: confirmDelRoom,
        syncAddRoomPrefix: syncAddRoomPrefix,
        // buildings
        openAddBuilding: openAddBuilding, saveAddBuilding: saveAddBuilding,
        openEditBuilding: openEditBuilding, saveEditBuilding: saveEditBuilding,
        openDelBuilding: openDelBuilding, confirmDelBuilding: confirmDelBuilding
    };

    /* ------------------------------------------------------------------ *
     *  Boot
     * ------------------------------------------------------------------ */
    initDashboard();
    initSchedule();
    initRooms();
})();
