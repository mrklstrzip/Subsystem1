/* ==========================================================================
   Campus Director – demo behavior (no backend)
   --------------------------------------------------------------------------
   All data lives in the browser (localStorage) so that changes made on one
   page (e.g. adding a room) show up on the others (e.g. event dropdowns,
   dashboard counts). To wipe the demo data and start over, run
   CD.reset() in the browser console.
   ========================================================================== */
(function () {
    'use strict';

    var KEY = 'cdDemoData_v6';   // bumped: completed / cancelled schedules
    var HOUR_FIRST = 7;    // first column on the room schedule grid (7:00 AM)
    var HOUR_END = 21;     // grid ends at 9:00 PM (last column is 8:00 – 9:00 PM)

    // Room schedule colors: Open = white, Reserved = dark blue, Occupied = light blue,
    // Completed = green, Cancelled = red, Unavailable = gray
    var STATUS_COLORS = { open: '#ffffff', reserved: '#2E5C8A', occupied: '#bfdbfe', completed: '#3aa76d', cancelled: '#d9534f', unavailable: '#7b8798' };
    var STATUS_TEXT = { open: '#1e293b', reserved: '#ffffff', occupied: '#1e3a5f', completed: '#ffffff', cancelled: '#ffffff', unavailable: '#ffffff' };
    var STATUS_LABELS = { open: 'Open', reserved: 'Reserved', occupied: 'Occupied', completed: 'Completed', cancelled: 'Cancelled', unavailable: 'Unavailable' };
    var MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    var DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

    var state;

    /* ------------------------------------------------------------------ *
     *  Data
     * ------------------------------------------------------------------ */
    function isoPlus(days) {
        var d = new Date();
        d.setDate(d.getDate() + days);
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    function seed() {
        var PROGRAMS = { IT: 'BS Information Technology', IS: 'BS Information System', CS: 'BS Computer Science' };
        var FIRST = ['Juan', 'Maria', 'Jose', 'Angelica', 'Mark', 'Kristine', 'Paolo', 'Bianca', 'Carlo', 'Janine', 'Miguel', 'Patricia'];
        var LAST = ['Santos', 'Reyes', 'Cruz', 'Bautista', 'Garcia', 'Mendoza', 'Torres', 'Flores', 'Ramos', 'Aquino', 'Dela Cruz', 'Villanueva'];

        function authorsFor(i) {
            var n = 3 + (i % 2), out = [];
            for (var j = 0; j < n; j++) out.push(FIRST[(i * 3 + j) % 12] + ' ' + LAST[(i * 5 + j * 7) % 12]);
            return out;
        }

        // [room, day offset from today, start min, end min, program, section, group no., manuscript title, cancelled?]
        var rows = [
            // today
            ['r3', 0, 540, 720, 'IT', 'SBIT-4A', 4, 'Web-Based Barangay Document Request and Tracking System'],
            ['r4', 0, 600, 720, 'CS', 'SBCS-4A', 7, 'Machine Learning Model for Early Detection of Rice Leaf Diseases'],
            ['r5', 0, 420, 600, 'IS', 'SBIS-4A', 1, 'Inventory and Sales Monitoring System for Small Retail Stores'],
            ['r5', 0, 720, 960, 'CS', 'SBCS-4B', 2, 'Real-Time Sign Language Recognition Using Convolutional Neural Networks'],
            ['r6', 0, 480, 600, 'IT', 'SBIT-4B', 3, 'IoT-Based Smart Classroom Energy Monitoring System'],
            ['r6', 0, 780, 900, 'IS', 'SBIS-4B', 1, 'Online Enrollment and Student Records Management System'],
            ['r3', 0, 840, 1020, 'IT', 'SBIT-4A', 6, 'Mobile Application for Campus Navigation and Room Finder'],
            ['r4', 0, 960, 1080, 'CS', 'SBCS-4A', 9, 'Sentiment Analysis of Student Feedback Using Natural Language Processing', true],
            ['r2', 0, 1080, 1200, 'IT', 'SBIT-4B', 8, 'Cloud-Based Attendance System with Facial Recognition'],
            // tomorrow
            ['r3', 1, 480, 660, 'IT', 'SBIT-4B', 10, 'Automated Library Book Borrowing and Reservation System'],
            ['r4', 1, 540, 720, 'CS', 'SBCS-4A', 3, 'Chatbot for Frequently Asked Questions of University Admissions', true],
            ['r5', 1, 780, 960, 'IS', 'SBIS-4A', 4, 'Decision Support System for Scholarship Applicant Ranking'],
            ['r6', 1, 600, 780, 'IT', 'SBIT-4A', 2, 'Web Portal for Capstone Project Archiving and Plagiarism Checking'],
            ['r2', 1, 840, 1020, 'CS', 'SBCS-4B', 11, 'Crime Hotspot Prediction Using K-Means Clustering'],
            // in 2 days
            ['r3', 2, 480, 660, 'IT', 'SBIT-4A', 1, 'Online Appointment and Queueing System for Barangay Health Centers'],
            ['r5', 2, 780, 960, 'CS', 'SBCS-4A', 5, 'Handwritten Character Recognition for Filipino Documents'],
            ['r4', 2, 420, 540, 'IS', 'SBIS-4B', 2, 'Payroll and Human Resource Information System for Small Businesses'],
            ['r6', 2, 900, 1080, 'IT', 'SBIT-4B', 5, 'Smart Parking Management System Using License Plate Detection'],
            ['r2', 2, 540, 720, 'CS', 'SBCS-4B', 8, 'Predictive Analytics for Student Dropout Risk'],
            // in 3 days
            ['r3', 3, 600, 780, 'IS', 'SBIS-4A', 6, 'E-Commerce Platform for Local Farmers with Order Tracking'],
            ['r4', 3, 780, 960, 'IT', 'SBIT-4A', 7, 'Disaster Alert and Evacuation Guide Mobile Application'],
            ['r5', 3, 480, 600, 'CS', 'SBCS-4A', 1, 'Plant Disease Identification Using Image Processing'],
            ['r6', 3, 1020, 1200, 'IS', 'SBIS-4B', 3, 'Document Management System with Role-Based Access Control'],
            // in 4 days
            ['r3', 4, 540, 660, 'CS', 'SBCS-4B', 6, 'Fake News Detection on Social Media Using Text Classification'],
            ['r5', 4, 720, 900, 'IT', 'SBIT-4B', 4, 'Student Grievance Reporting and Monitoring System'],
            ['r2', 4, 1080, 1260, 'IS', 'SBIS-4A', 5, 'Hospital Patient Records and Billing Information System'],
            // yesterday and 2 days ago (these show as Completed, unless cancelled)
            ['r3', -1, 540, 720, 'IT', 'SBIT-4A', 3, 'Learning Management System with Automated Grading for Senior High School'],
            ['r4', -1, 780, 960, 'CS', 'SBCS-4B', 4, 'Voice-Controlled Assistant for Visually Impaired Students'],
            ['r5', -1, 480, 600, 'IS', 'SBIS-4B', 4, 'Barangay Complaint Management System with SMS Notifications'],
            ['r6', -1, 900, 1080, 'IT', 'SBIT-4A', 8, 'Augmented Reality Campus Tour Application', true],
            ['r3', -2, 600, 780, 'CS', 'SBCS-4A', 2, 'Traffic Flow Prediction Using Time Series Forecasting'],
            ['r5', -2, 840, 1020, 'IS', 'SBIS-4A', 2, 'Online Voting System with Blockchain-Based Verification']
        ];

        var schedules = rows.map(function (r, i) {
            return {
                id: 's' + (i + 1), roomId: r[0], date: isoPlus(r[1]), start: r[2], end: r[3],
                program: PROGRAMS[r[4]], section: r[5], group: r[6], title: r[7], authors: authorsFor(i + 1), cancelled: !!r[8]
            };
        });

        return {
            nextId: 100,
            buildings: [
                { id: 'b3', name: 'Bautista Bldg.', code: 'IK', status: 'Active' }
            ],
            rooms: [
                { id: 'r1', buildingId: 'b3', name: 'IK506', status: 'Unavailable' },
                { id: 'r2', buildingId: 'b3', name: 'IK505', status: 'Active' },
                { id: 'r3', buildingId: 'b3', name: 'IK501', status: 'Active' },
                { id: 'r4', buildingId: 'b3', name: 'IK502', status: 'Active' },
                { id: 'r5', buildingId: 'b3', name: 'IK503', status: 'Active' },
                { id: 'r6', buildingId: 'b3', name: 'IK504', status: 'Active' },
                { id: 'r7', buildingId: 'b3', name: 'Auditorium', status: 'Active' }
            ],
            // start / end are minutes since midnight (540 = 9:00 AM)
            events: [
                { id: 'e1', name: 'Freshmen Orientation', roomId: 'r2', date: '2026-10-25', start: 540, end: 720 },
                { id: 'e2', name: 'CCS Dept. Seminar', roomId: 'r2', date: '2026-10-26', start: 780, end: 930 },
                { id: 'e3', name: 'Faculty Meeting', roomId: 'r7', date: isoPlus(0), start: 960, end: 1080 },
                { id: 'e4', name: 'Research Colloquium', roomId: 'r7', date: isoPlus(1), start: 540, end: 720 },
                { id: 'e5', name: 'Capstone Orientation', roomId: 'r2', date: isoPlus(1), start: 600, end: 750 },
                { id: 'e6', name: 'NSTP Graduation Practice', roomId: 'r7', date: isoPlus(3), start: 480, end: 600 },
                { id: 'e7', name: 'Alumni Homecoming Program', roomId: 'r7', date: isoPlus(4), start: 1020, end: 1260 }
            ],
            // Defense schedules (shown as Reserved; Occupied while happening right now).
            // Dates are relative to today so the demo always has data.
            schedules: schedules,
            activity: [
                { ts: fmtMDY(isoPlus(0)), text: 'Assigned as panelist for Room XXX' },
                { ts: fmtMDY(isoPlus(0)), text: 'Assigned as panelist for Room XXX' },
                { ts: fmtMDY(isoPlus(0)), text: 'Declined panel invitation for Room XXX' },
                { ts: fmtMDY(isoPlus(0)), text: 'Room IK506 status changed to Unavailable' }
            ]
        };
    }

    function save() {
        try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { /* storage unavailable – keep in memory */ }
    }

    function load() {
        try {
            var raw = localStorage.getItem(KEY);
            if (raw) {
                var parsed = JSON.parse(raw);
                if (parsed && parsed.buildings && parsed.rooms && parsed.events && parsed.schedules && parsed.activity) {
                    state = parsed;
                    return;
                }
            }
        } catch (e) { /* fall through to seed */ }
        state = seed();
        save();
    }

    function uid(prefix) {
        var id = prefix + state.nextId;
        state.nextId += 1;
        return id;
    }

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

    function bldgById(id) { return state.buildings.filter(function (b) { return b.id === id; })[0]; }
    function roomById(id) { return state.rooms.filter(function (r) { return r.id === id; })[0]; }
    function eventById(id) { return state.events.filter(function (e) { return e.id === id; })[0]; }
    function roomsOf(buildingId) { return state.rooms.filter(function (r) { return r.buildingId === buildingId; }); }

    function todayISO() {
        var d = new Date();
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    function nowMinutes() {
        var d = new Date();
        return d.getHours() * 60 + d.getMinutes();
    }

    function fmtMDY(iso) {                       // 2026-10-25 -> 10-25-2026
        var p = iso.split('-');
        return p[1] + '-' + p[2] + '-' + p[0];
    }

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
        s = String(s || '').trim();
        var y, m, d, mt;
        if ((mt = s.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/))) { y = +mt[1]; m = +mt[2]; d = +mt[3]; }
        else if ((mt = s.match(/^(\d{1,2})[-\/](\d{1,2})[-\/](\d{4})$/))) { m = +mt[1]; d = +mt[2]; y = +mt[3]; }
        else {
            var t = new Date(s);
            if (isNaN(t.getTime())) return null;
            y = t.getFullYear(); m = t.getMonth() + 1; d = t.getDate();
        }
        var chk = new Date(y, m - 1, d);
        if (chk.getFullYear() !== y || chk.getMonth() !== m - 1 || chk.getDate() !== d) return null;
        return y + '-' + pad(m) + '-' + pad(d);
    }

    function parseTime(s) {                      // "9:00 AM" / "1pm" / "13:30" -> minutes since midnight
        var mt = String(s || '').trim().match(/^(\d{1,2})(?::(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.)?$/i);
        if (!mt) return null;
        var h = +mt[1], m = mt[2] ? +mt[2] : 0, mer = mt[3] ? mt[3].toLowerCase().charAt(0) : '';
        if (m > 59) return null;
        if (mer) {
            if (h < 1 || h > 12) return null;
            h = (h % 12) + (mer === 'p' ? 12 : 0);
        } else if (h > 23) {
            return null;
        }
        return h * 60 + m;
    }

    function fmtTime(min) {                      // 570 -> 9:30 AM
        var h = Math.floor(min / 60), m = min % 60;
        return ((h % 12) || 12) + ':' + pad(m) + ' ' + (h >= 12 ? 'PM' : 'AM');
    }

    function overlaps(aStart, aEnd, bStart, bEnd) { return aStart < bEnd && bStart < aEnd; }

    function logActivity(text) {
        state.activity.unshift({ ts: fmtMDY(todayISO()), text: text });
        if (state.activity.length > 50) state.activity.length = 50;
    }

    function badgeFor(status) {
        var cls = status === 'Active' ? 'badge-active' : 'badge-disabled';
        return '<span class="badge ' + cls + '">' + esc(status) + '</span>';
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
     * ------------------------------------------------------------------ */
    function renderDashboard() {
        if (!$('dash-total-rooms')) return;

        var active = state.rooms.filter(function (r) { return r.status === 'Active'; }).length;
        var today = todayISO();
        var upcoming = state.events.filter(function (e) { return e.date >= today; }).length;

        $('dash-total-rooms').textContent = state.rooms.length;
        $('dash-active-rooms').textContent = active;
        $('dash-disabled-rooms').textContent = state.rooms.length - active;
        $('dash-events').textContent = upcoming;

        $('dash-activity').innerHTML = state.activity.slice(0, 6).map(function (a) {
            return '<tr><td>' + esc(a.ts) + '</td><td>' + esc(a.text) + '</td></tr>';
        }).join('') || '<tr><td colspan="2" style="text-align:center; color:#64748b;">No recent activity</td></tr>';
    }

    /* ------------------------------------------------------------------ *
     *  Room Availability (view-only schedule grid)
     *
     *  Status rules (nothing is stored; it is derived each time the grid draws):
     *    Open        – default for any room / hour with nothing scheduled
     *    Reserved    – a defense schedule, or an event added in Event Management, that has not started yet
     *    Occupied    – a scheduled block that is happening right now (today, current time inside it)
     *    Completed   – a scheduled block whose end time has passed (earlier today or a past date)
     *    Cancelled   – a schedule that was cancelled (always wins over the time-based statuses)
     *    Unavailable – the room is disabled in Room Management (status is not Active)
     * ------------------------------------------------------------------ */
    var rendered = {};   // key -> details shown in the view pop-up

    function currentDate() { return parseDate($('sc-date').value) || todayISO(); }

    function statusOf(date, start, end, cancelled) {
        if (cancelled) return 'cancelled';
        var today = todayISO(), now = nowMinutes();
        if (date < today) return 'completed';
        if (date === today) {
            if (now >= end) return 'completed';
            if (now >= start) return 'occupied';
        }
        return 'reserved';
    }

    function blocksFor(roomId, date) {
        var list = [];

        function add(id, kind, label, start, end, ref) {
            var status = statusOf(date, start, end, ref.cancelled);
            list.push({ id: id, kind: kind, label: label, start: start, end: end, status: status, ref: ref });
        }

        state.schedules.forEach(function (s) {
            if (s.roomId === roomId && s.date === date) add(s.id, 'defense', s.section + ' \u00b7 Group ' + s.group, s.start, s.end, s);
        });
        state.events.forEach(function (e) {
            if (e.roomId === roomId && e.date === date) add(e.id, 'event', e.name, e.start, e.end, e);
        });

        list.sort(function (a, b) { return a.start - b.start; });
        return list;
    }

    // Turns blocks into grid cells: null = an open hour, otherwise { block, span }
    function layout(blocks) {
        var cells = [], h = HOUR_FIRST;
        blocks.forEach(function (b) {
            var sc = Math.max(Math.floor(b.start / 60), h);
            var ec = Math.min(Math.ceil(b.end / 60), HOUR_END);
            if (ec <= sc) return;
            for (; h < sc; h++) cells.push(null);
            cells.push({ block: b, span: ec - sc });
            h = ec;
        });
        for (; h < HOUR_END; h++) cells.push(null);
        return cells;
    }

    function renderSchedule() {
        var body = $('sc-body');
        if (!body) return;

        var date = currentDate();
        var sFilter = $('sc-status').value;
        var totalCols = HOUR_END - HOUR_FIRST;
        var rows = [];
        rendered = {};

        state.rooms.forEach(function (r) {
            var unavailable = r.status !== 'Active';
            var cells = [], present = {};

            if (unavailable) {
                present.unavailable = true;
            } else {
                cells = layout(blocksFor(r.id, date));
                cells.forEach(function (c) { present[c ? c.block.status : 'open'] = true; });
            }
            if (sFilter !== 'all' && !present[sFilter]) return;

            var html = '<td>' + esc(r.name) + '</td>';

            if (unavailable) {
                // whole row is Unavailable; nothing to open
                html += '<td colspan="' + totalCols + '" class="sched-cell" ' +
                    'style="background:' + STATUS_COLORS.unavailable + '; color:' + STATUS_TEXT.unavailable + '; font-weight:600;">Unavailable</td>';
            } else {
                cells.forEach(function (c) {
                    if (!c) { html += '<td class="sched-cell"></td>'; return; }
                    var blk = c.block;
                    rendered[blk.id] = { kind: blk.kind, ref: blk.ref, room: r, date: date, status: blk.status, start: blk.start, end: blk.end };
                    html += '<td colspan="' + c.span + '" class="sched-block sched-cell" title="Click to view details" ' +
                        'style="background:' + STATUS_COLORS[blk.status] + '; color:' + STATUS_TEXT[blk.status] + '; font-weight:600;" ' +
                        'onclick="CD.openSchedView(\'' + blk.id + '\')">' + esc(blk.label) + '</td>';
                });
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

        var day = esc(fmtFull(d.date));
        var time = d.start == null ? '' : esc(fmtTime(d.start) + ' \u2013 ' + fmtTime(d.end));
        var rows = '';

        if (d.kind === 'defense') {
            var s = d.ref;
            rows = detailRow('Title of the Manuscript', esc(s.title)) +
                detailRow('Authors', (s.authors || []).map(esc).join('<br>')) +
                detailRow('Section', esc(s.section)) +
                detailRow('Group Number', 'Group ' + esc(s.group)) +
                detailRow('Program', esc(s.program)) +
                detailRow('Day, Date', day) +
                detailRow('Time', time) +
                detailRow('Room', esc(d.room.name));
        } else {
            rows = detailRow('Event', esc(d.ref.name)) +
                detailRow('Day, Date', day) +
                detailRow('Time', time) +
                detailRow('Room', esc(d.room.name));
        }
        $('es-details').innerHTML = rows;

        var badge = $('es-badge');
        badge.textContent = STATUS_LABELS[d.status];
        badge.style.background = STATUS_COLORS[d.status];
        badge.style.color = STATUS_TEXT[d.status];

        openModal('modal-edit-sched');
    }

    function initSchedule() {
        if (!$('sc-body')) return;

        // column headers: 7 AM ... 8 PM (the last column covers 8 - 9 PM), built from the grid range
        var head = '<th class="sched-room-col">Room</th>';
        for (var h = HOUR_FIRST; h < HOUR_END; h++) head += '<th>' + ((h % 12) || 12) + ' ' + (h >= 12 ? 'PM' : 'AM') + '</th>';
        $('sc-head').innerHTML = head;

        $('sc-date').value = todayISO();

        $('sc-date').addEventListener('change', renderSchedule);
        $('sc-status').addEventListener('change', renderSchedule);

        renderSchedule();

        // keep "Occupied" in step with the clock
        setInterval(renderSchedule, 60000);
    }

    /* ------------------------------------------------------------------ *
     *  Room Management
     * ------------------------------------------------------------------ */
    var editRoomId = null, delRoomId = null;

    function renderRooms() {
        var rbody = $('rooms-tbody');
        if (!rbody) return;

        var q = norm(($('rm-search') || {}).value);

        var rooms = state.rooms.filter(function (r) {
            return !q || norm(r.name + ' ' + r.status).indexOf(q) > -1;
        });

        rbody.innerHTML = rooms.map(function (r) {
            return '<tr>' +
                '<td>' + esc(r.name) + '</td>' +
                '<td>' + badgeFor(r.status) + '</td>' +
                '<td>' +
                '<i class="fa fa-edit" style="color:var(--primary-color); cursor:pointer;" onclick="CD.openEditRoom(\'' + r.id + '\')"></i>' +
                '<i class="fa fa-trash" style="color:var(--danger); cursor:pointer; margin-left:8px;" onclick="CD.openDelRoom(\'' + r.id + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="3" style="text-align:center; color:#64748b;">No rooms found</td></tr>';

        $('rooms-count').textContent = '(' + state.rooms.length + ' rooms)';
    }

    function initRooms() {
        if (!$('rooms-tbody')) return;

        var search = $('rm-search');
        if (search) search.addEventListener('input', renderRooms);
        renderRooms();
    }

    function openAddRoom() {
        $('add-room-name').value = '';
        openModal('modal-add-room');
    }

    function saveAddRoom() {
        // there is only one building (Bautista), so new rooms always go to it
        var bid = state.buildings.length ? state.buildings[0].id : null;
        var name = $('add-room-name').value.trim();
        if (!bid) { alert('No building is set up yet.'); return; }
        if (!name) { alert('Please enter a room number.'); return; }
        var dup = roomsOf(bid).some(function (r) { return norm(r.name) === norm(name); });
        if (dup) { alert('That room already exists in this building.'); return; }

        state.rooms.push({ id: uid('r'), buildingId: bid, name: name, status: 'Active' });
        logActivity('Room ' + name + ' added to ' + bldgById(bid).name);
        save();
        closeModal('modal-add-room');
        renderRooms();
    }

    function openEditRoom(id) {
        var r = roomById(id);
        if (!r) return;
        editRoomId = id;
        $('edit-room-name').value = r.name;
        setRadio('edit-rm-status', r.status);
        openModal('modal-edit-room');
    }

    function saveEditRoom() {
        var r = roomById(editRoomId);
        if (!r) { closeModal('modal-edit-room'); return; }
        var name = $('edit-room-name').value.trim();
        if (!name) { alert('Please enter a room name.'); return; }
        var dup = roomsOf(r.buildingId).some(function (o) { return o.id !== r.id && norm(o.name) === norm(name); });
        if (dup) { alert('That room already exists.'); return; }

        var oldStatus = r.status;
        r.name = name;
        r.status = getRadio('edit-rm-status') || r.status;

        logActivity(r.status !== oldStatus
            ? 'Room ' + r.name + ' status changed to ' + r.status
            : 'Room ' + r.name + ' details updated');
        save();
        closeModal('modal-edit-room');
        renderRooms();
    }

    function openDelRoom(id) { delRoomId = id; openModal('modal-del-room'); }

    function confirmDelRoom() {
        var r = roomById(delRoomId);
        if (r) {
            state.rooms = state.rooms.filter(function (x) { return x.id !== r.id; });
            state.events = state.events.filter(function (e) { return e.roomId !== r.id; });
            state.schedules = state.schedules.filter(function (s) { return s.roomId !== r.id; });
            logActivity('Room ' + r.name + ' deleted');
            save();
        }
        delRoomId = null;
        closeModal('modal-del-room');
        renderRooms();
    }

    /* ------------------------------------------------------------------ *
     *  Event Management
     * ------------------------------------------------------------------ */
    var editEventId = null, delEventId = null;
    var addEventDefaults = {};

    function renderEvents() {
        var body = $('events-tbody');
        if (!body) return;
        var q = norm(($('ev-search') || {}).value);

        var list = state.events.slice().sort(function (a, b) {
            return a.date === b.date ? a.start - b.start : (a.date < b.date ? -1 : 1);
        }).filter(function (e) {
            var r = roomById(e.roomId) || { name: '' };
            return !q || norm(e.name + ' ' + r.name + ' ' + fmtMDY(e.date) + ' ' + fmtLong(e.date)).indexOf(q) > -1;
        });

        body.innerHTML = list.map(function (e) {
            var r = roomById(e.roomId) || { name: '\u2014' };
            return '<tr>' +
                '<td>' + esc(e.name) + '</td>' +
                '<td>' + esc(r.name) + '</td>' +
                '<td>' + fmtMDY(e.date) + '</td>' +
                '<td>' + fmtTime(e.start) + '</td>' +
                '<td>' + fmtTime(e.end) + '</td>' +
                '<td>' +
                '<i class="fa fa-edit" style="color:var(--primary-color); cursor:pointer;" onclick="CD.openEditEvent(\'' + e.id + '\')"></i>' +
                '<i class="fa fa-trash" style="color:var(--danger); cursor:pointer; margin-left:8px;" onclick="CD.openDelEvent(\'' + e.id + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="6" style="text-align:center; color:#64748b;">No events found</td></tr>';
    }

    function initEvents() {
        if (!$('events-tbody')) return;
        var search = $('ev-search');
        if (search) search.addEventListener('input', renderEvents);

        addEventDefaults = { date: todayISO(), start: 480, end: 780 };   // today, 8:00 AM - 1:00 PM
        lockDateInput($('add-ev-date'));
        lockDateInput($('edit-ev-date'));
        renderEvents();
    }

    // Date must be chosen from the calendar, not typed
    function lockDateInput(input) {
        if (!input) return;
        input.addEventListener('keydown', function (e) { if (e.key !== 'Tab') e.preventDefault(); });
        input.addEventListener('click', function () {
            try { if (input.showPicker) input.showPicker(); } catch (err) { /* picker already open */ }
        });
    }

    // Time dropdown in 30-minute steps (7:00 AM - 9:00 PM); keeps an existing off-range value selectable
    function fillTimeSelect(select, selectedMin) {
        var times = [];
        for (var m = 420; m <= 1260; m += 30) times.push(m);
        if (selectedMin != null && times.indexOf(selectedMin) === -1) {
            times.push(selectedMin);
            times.sort(function (a, b) { return a - b; });
        }
        select.innerHTML = times.map(function (m) {
            return '<option value="' + m + '"' + (m === selectedMin ? ' selected' : '') + '>' + fmtTime(m) + '</option>';
        }).join('');
    }

    // Fill the room dropdown in the given modal ('add' or 'edit') with the active rooms
    function refreshEventRooms(which, keepRoomId) {
        var rooms = state.rooms.filter(function (r) { return r.status === 'Active' || r.id === keepRoomId; });
        var sel = $(which + '-ev-room');
        sel.innerHTML = rooms.length
            ? optionsHtml(rooms, keepRoomId, function (r) { return r.name; })
            : '<option value="" selected disabled>No rooms available</option>';
    }

    function readEventForm(which, ignoreId) {
        var name = $(which + '-ev-name').value.trim();
        var roomId = $(which + '-ev-room').value;
        var date = parseDate($(which + '-ev-date').value);
        var start = parseInt($(which + '-ev-start').value, 10);
        var end = parseInt($(which + '-ev-end').value, 10);

        if (!name) { alert('Please enter an event name.'); return null; }
        if (!roomId) { alert('Please choose a room.'); return null; }
        if (!date) { alert('Please pick a date.'); return null; }
        if (isNaN(start)) { alert('Please choose a start time.'); return null; }
        if (isNaN(end)) { alert('Please choose an end time.'); return null; }
        if (end <= start) { alert('End time must be later than the start time.'); return null; }

        var clash = state.events.filter(function (e) {
            return e.id !== ignoreId && e.roomId === roomId && e.date === date && overlaps(start, end, e.start, e.end);
        })[0];
        if (clash) {
            alert('That room is already booked for "' + clash.name + '" (' + fmtTime(clash.start) + ' \u2013 ' + fmtTime(clash.end) + ') on that date.');
            return null;
        }

        var defense = state.schedules.filter(function (s) {
            return s.roomId === roomId && s.date === date && !s.cancelled && overlaps(start, end, s.start, s.end);
        })[0];
        if (defense) {
            alert('That room is already reserved for ' + defense.section + ' \u00b7 Group ' + defense.group + ' (' + fmtTime(defense.start) + ' \u2013 ' + fmtTime(defense.end) + ') on that date.');
            return null;
        }
        return { name: name, roomId: roomId, date: date, start: start, end: end };
    }

    function openAddEvent() {
        $('add-ev-name').value = '';
        refreshEventRooms('add', null);
        $('add-ev-date').value = addEventDefaults.date;
        fillTimeSelect($('add-ev-start'), addEventDefaults.start);
        fillTimeSelect($('add-ev-end'), addEventDefaults.end);
        openModal('modal-add-event');
    }

    function saveAddEvent() {
        var data = readEventForm('add', null);
        if (!data) return;
        data.id = uid('e');
        state.events.push(data);
        logActivity('Event "' + data.name + '" scheduled for ' + fmtMDY(data.date));
        save();
        closeModal('modal-add-event');
        renderEvents();
    }

    function openEditEvent(id) {
        var e = eventById(id);
        if (!e) return;
        editEventId = id;
        $('edit-ev-name').value = e.name;
        refreshEventRooms('edit', e.roomId);
        $('edit-ev-date').value = e.date;
        fillTimeSelect($('edit-ev-start'), e.start);
        fillTimeSelect($('edit-ev-end'), e.end);
        openModal('modal-edit-event');
    }

    function saveEditEvent() {
        var e = eventById(editEventId);
        if (!e) { closeModal('modal-edit-event'); return; }
        var data = readEventForm('edit', e.id);
        if (!data) return;
        e.name = data.name;
        e.roomId = data.roomId;
        e.date = data.date;
        e.start = data.start;
        e.end = data.end;
        logActivity('Event "' + e.name + '" updated');
        save();
        closeModal('modal-edit-event');
        renderEvents();
    }

    function openDelEvent(id) { delEventId = id; openModal('modal-del-event'); }

    function confirmDelEvent() {
        var e = eventById(delEventId);
        if (e) {
            state.events = state.events.filter(function (x) { return x.id !== e.id; });
            logActivity('Event "' + e.name + '" deleted');
            save();
        }
        delEventId = null;
        closeModal('modal-del-event');
        renderEvents();
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
        // events
        openAddEvent: openAddEvent, saveAddEvent: saveAddEvent,
        openEditEvent: openEditEvent, saveEditEvent: saveEditEvent,
        openDelEvent: openDelEvent, confirmDelEvent: confirmDelEvent,
        refreshEventRooms: function (which) { refreshEventRooms(which, null); },
        // utility
        reset: function () { state = seed(); save(); window.location.reload(); }
    };

    /* ------------------------------------------------------------------ *
     *  Boot
     * ------------------------------------------------------------------ */
    load();
    renderDashboard();
    initSchedule();
    initRooms();
    initEvents();
})();