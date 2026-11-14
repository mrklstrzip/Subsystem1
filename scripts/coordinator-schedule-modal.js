/* Capstone Coordinator - the "Generated Schedule" pop-up (Schedule Management and Under Review).
   Everything is read from / saved to the database through CoordinatorApi.
   Use:  CCSchedule.open(scheduleId)   */
(function () {
    'use strict';

    var API = '/CapstoneCoordinator/CoordinatorApi/';
    var LOGIN_URL = '/Auth/Login/login';
    var PANEL_SIZE = 3;
    var STATUS_LABELS = { 'to-submit': 'To Submit', 'under-review': 'Under Review', 'submitted': 'Submitted' };
    var MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

    var overlay = document.getElementById('scheduleModal');
    if (!overlay) return;

    function $(id) { return document.getElementById(id); }

    var submitBtn = $('modalSubmitBtn');
    var panel = overlay.querySelector('.panelists-panel');
    var btnEdit = $('btnEditPanelists');
    var btnSave = $('btnSaveEdit');
    var btnCancel = $('btnCancelEdit');

    var current = null;      // the schedule being shown (from the server)
    var editing = false;
    var busy = false;
    var pdfDocument = null;
    var pdfPageNumber = 1;
    var pdfZoom = 1;
    var pdfLoadId = 0;
    var pdfRenderTask = null;

    if (window.pdfjsLib) {
        window.pdfjsLib.GlobalWorkerOptions.workerSrc =
            'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
    }

    /* ---------- server ---------- */
    function call(action, body) {
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
        return fetch(API + action, opts).then(function (r) { return r.json(); }).then(function (res) {
            if (res && res.unauthorized) {
                try { localStorage.removeItem('loggedInUser'); } catch (e) { }
                window.location.href = LOGIN_URL;
                return new Promise(function () { });
            }
            return res;
        });
    }

    /* ---------- formatting ---------- */
    function fmtTime(t) {
        if (!t) return '';
        var p = t.split(':');
        var h = parseInt(p[0], 10);
        var ap = h >= 12 ? 'PM' : 'AM';
        h = h % 12 || 12;
        return h + ':' + p[1] + ap;
    }
    function fmtDate(iso) {
        if (!iso) return '';
        var p = iso.split('-');
        return MONTHS[parseInt(p[1], 10) - 1] + ' ' + parseInt(p[2], 10) + ', ' + p[0];
    }
    function showError(msg) {
        var el = $('mEditError');
        el.textContent = msg || '';
        el.hidden = !msg;
    }
    function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
    // exact percentage with one decimal (66.7%), never rounded to a level
    function fmtPercent(v) { return (v === null || v === undefined || isNaN(v)) ? '\u2014' : Number(v).toFixed(1) + '%'; }

    function renderPdfPage() {
        if (!pdfDocument) return;
        var canvas = $('pdfCanvas');
        var context = canvas.getContext('2d');
        pdfDocument.getPage(pdfPageNumber).then(function (page) {
            var base = page.getViewport({ scale: 1 });
            var maxWidth = $('pdfCanvasWrap').clientWidth - 24;
            var viewport = page.getViewport({ scale: Math.min(maxWidth / base.width, 1.5) * pdfZoom });
            var ratio = window.devicePixelRatio || 1;
            canvas.width = Math.floor(viewport.width * ratio);
            canvas.height = Math.floor(viewport.height * ratio);
            canvas.style.width = Math.floor(viewport.width) + 'px';
            canvas.style.height = Math.floor(viewport.height) + 'px';
            context.setTransform(ratio, 0, 0, ratio, 0, 0);
            if (pdfRenderTask) pdfRenderTask.cancel();
            pdfRenderTask = page.render({ canvasContext: context, viewport: viewport });
            pdfRenderTask.promise.catch(function () { });
            $('pdfPage').textContent = pdfPageNumber;
            $('pdfPages').textContent = pdfDocument.numPages;
            $('pdfPrev').disabled = pdfPageNumber <= 1;
            $('pdfNext').disabled = pdfPageNumber >= pdfDocument.numPages;
        });
    }

    function loadManuscript(url) {
        var id = ++pdfLoadId;
        var link = $('mManuscriptOpen');
        var message = $('pdfMessage');
        var canvas = $('pdfCanvas');
        if (pdfRenderTask) { pdfRenderTask.cancel(); pdfRenderTask = null; }
        if (pdfDocument) { pdfDocument.destroy(); pdfDocument = null; }
        canvas.hidden = true;
        $('pdfPage').textContent = $('pdfPages').textContent = '\u2014';
        var parsed;
        try { parsed = new URL(url || '', window.location.href); } catch (e) { parsed = null; }
        var valid = parsed && parsed.origin === window.location.origin &&
            /\/CapstoneCoordinator\/CoordinatorApi\/manuscript$/i.test(parsed.pathname);
        link.hidden = !valid;
        if (valid) link.href = parsed.href;
        if (!valid) { message.textContent = 'No PDF manuscript is available for this submission.'; return; }
        message.hidden = false;
        message.textContent = 'Loading manuscript…';
        if (!window.pdfjsLib) { message.textContent = 'The PDF viewer could not be loaded. Use Open PDF to view the manuscript.'; return; }
        fetch(parsed.href, { method: 'GET', credentials: 'same-origin', cache: 'no-store' }).then(function (response) {
            if (!response.ok) {
                var error = new Error('The manuscript could not be loaded.');
                error.status = response.status;
                throw error;
            }
            return response.arrayBuffer();
        }).then(function (buffer) {
            if (id !== pdfLoadId) return null;
            var bytes = new Uint8Array(buffer);
            if (new TextDecoder('ascii').decode(bytes.subarray(0, 1024)).indexOf('%PDF-') < 0)
                throw new Error('The manuscript response is not a readable PDF.');
            return window.pdfjsLib.getDocument({
                data: bytes,
                isEvalSupported: false,
                enableXfa: false
            }).promise;
        }).then(function (doc) {
            if (!doc) return;
            if (id !== pdfLoadId) { doc.destroy(); return; }
            pdfDocument = doc;
            pdfPageNumber = 1;
            pdfZoom = 1;
            canvas.hidden = false;
            message.hidden = true;
            renderPdfPage();
        }).catch(function (error) {
            if (id !== pdfLoadId) return;
            if (error && error.status === 415) {
                link.hidden = true;
                message.textContent = 'No PDF manuscript is attached to this submission.';
                return;
            }
            message.textContent = 'Could not load this read-only PDF preview. Check that the manuscript file is accessible, then use Open PDF.';
        });
    }

    /* ---------- drawing ---------- */
    function renderPanelists() {
        var list = $('mPanelists');
        list.innerHTML = '';
        var have = current.panelists || [];

        if (!editing && have.length === 0) {
            var none = document.createElement('p');
            none.className = 'panel-text';
            none.textContent = 'No panelists assigned yet. Please edit to choose them.';
            list.appendChild(none);
            return;
        }

        var rows = editing ? PANEL_SIZE : have.length;
        for (var i = 0; i < rows; i++) {
            var p = have[i];
            var row = document.createElement('div');
            row.className = 'panelist-row';

            if (editing) {
                var input = document.createElement('input');
                input.type = 'text';
                input.className = 'edit-input panelist-input';
                input.maxLength = 60;
                input.value = p ? p.name : '';
                input.setAttribute('list', 'facultyList');
                input.setAttribute('placeholder', 'Panelist ' + (i + 1));
                input.setAttribute('aria-label', 'Panelist name');
                row.appendChild(input);
            } else {
                var name = document.createElement('span');
                name.className = 'panelist-name';
                name.textContent = p.name;
                var role = document.createElement('small');
                role.className = 'panelist-role ' + String(p.role || 'Member').toLowerCase();
                role.textContent = p.role || 'Member';
                name.appendChild(role);
                row.appendChild(name);
            }

            if (p) {   // percentage, match level and role are read-only (worked out by the system)
                var wrap = document.createElement('span');
                wrap.className = 'match-wrap';
                var pct = document.createElement('span');
                pct.className = 'match-percent';
                pct.textContent = fmtPercent(p.percent);
                var pill = document.createElement('span');
                pill.className = 'match-pill ' + p.match;
                pill.textContent = cap(p.match);
                wrap.appendChild(pct);
                wrap.appendChild(pill);
                row.appendChild(wrap);
            }
            list.appendChild(row);
        }
    }

    function renderSchedule() {
        var has = current.start && current.end && current.date;
        $('mTime').textContent = has ? fmtTime(current.start) + ' - ' + fmtTime(current.end) : '—';
        $('mDate').textContent = current.date ? fmtDate(current.date) : '—';
        $('mRoom').textContent = String(current.room || '').trim() || 'TBA';

        $('eTimeStart').value = current.start || '';
        $('eTimeEnd').value = current.end || '';
        $('eDate').value = current.date || '';
        $('eRoom').value = current.room || '';
        $('eRoom').setAttribute('list', 'roomList');
    }

    function setEditing(on) {
        editing = on;
        panel.classList.toggle('editing', on);
        btnEdit.hidden = on || !current.editable;
        btnSave.hidden = !on;
        btnCancel.hidden = !on;
        submitBtn.disabled = on;
        showError('');
        renderPanelists();
        renderSchedule();
        if (on) {
            var first = panel.querySelector('.panelist-input');
            if (first) first.focus();
        }
    }

    function fill(d) {
        current = d;
        $('mTitle').textContent = d.title;
        $('mDateSubmitted').textContent = d.dateSubmitted;
        $('mProblem').textContent = d.problem && d.problem.length ? d.problem : '—';
        $('mStudent').textContent = d.student;
        $('mProgram').textContent = d.program;
        $('mAdviser').textContent = d.adviser;

        var badge = $('mStatus');
        badge.className = 'status-badge ' + d.status;
        badge.textContent = STATUS_LABELS[d.status] || d.status;

        $('mSubmittedNote').textContent = d.dateSubmitted && d.dateSubmitted !== '-' ? 'Submitted ' + d.dateSubmitted : '';
        loadManuscript('/CapstoneCoordinator/CoordinatorApi/manuscript?id=' + encodeURIComponent(d.subsystem2Id || d.id || ''));

        submitBtn.style.display = d.editable ? '' : 'none';
        setEditing(false);
    }

    /* ---------- actions ---------- */
    function open(id) {
        if (busy) return;
        busy = true;
        call('schedule?id=' + encodeURIComponent(id)).then(function (res) {
            busy = false;
            if (!res || !res.success) { alert((res && res.message) || 'Could not open this schedule.'); return; }
            fill(res.schedule);
            overlay.classList.add('open');
            overlay.setAttribute('aria-hidden', 'false');
            document.body.classList.add('modal-open');
        }, function () {
            busy = false;
            alert('Could not reach the server. Please check your connection and try again.');
        });
    }

    function close() {
        if (editing) setEditing(false);
        overlay.classList.remove('open');
        overlay.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('modal-open');
    }

    function collect(force) {
        var names = [];
        Array.prototype.forEach.call(panel.querySelectorAll('.panelist-input'), function (i) { names.push(i.value.trim()); });
        return {
            id: current.id,
            panelists: names,
            date: $('eDate').value,
            start: $('eTimeStart').value,
            end: $('eTimeEnd').value,
            room: $('eRoom').value.trim(),
            force: !!force
        };
    }

    function saveEdit(force) {
        if (busy) return;
        var body = collect(force);

        // quick checks first; the server checks everything again
        if (body.panelists.length !== PANEL_SIZE || body.panelists.some(function (n) { return !n; })) { showError('Please choose ' + PANEL_SIZE + ' panelists.'); return; }
        var lower = body.panelists.map(function (n) { return n.toLowerCase(); });
        if (lower.some(function (n, i) { return lower.indexOf(n) !== i; })) { showError('A panelist can only be assigned once.'); return; }
        if (!body.start || !body.end) { showError('Please set a start and end time.'); return; }
        if (body.end <= body.start) { showError('End time must be later than start time.'); return; }
        if (!body.date) { showError('Please choose a defense date.'); return; }
        if (!body.room) { showError('Please enter a room.'); return; }

        busy = true;
        btnSave.disabled = true;
        showError('');
        call('save', body).then(function (res) {
            busy = false;
            btnSave.disabled = false;
            if (res && res.needsConfirm) {
                if (window.confirm('Class schedule conflict:\n\n' + res.conflicts.join('\n') + '\n\nSave this schedule anyway?')) saveEdit(true);
                return;
            }
            if (!res || !res.success) { showError((res && res.message) || 'Something went wrong. Please try again.'); return; }
            fill(res.schedule);
        }, function () {
            busy = false;
            btnSave.disabled = false;
            showError('Could not reach the server. Please check your connection and try again.');
        });
    }

    function submit() {
        if (editing || !current || busy) return;
        busy = true;
        submitBtn.disabled = true;
        showError('');
        var title = current.title;
        call('submit', { id: current.id }).then(function (res) {
            busy = false;
            submitBtn.disabled = false;
            if (!res || !res.success) { showError((res && res.message) || 'Something went wrong. Please try again.'); return; }
            if (window.Notice) Notice.after('success', 'Schedule submitted', 'The generated schedule for "' + title + '" was submitted successfully.');
            window.location.reload();     // the lists show the new status
        }, function () {
            busy = false;
            submitBtn.disabled = false;
            showError('Could not reach the server. Please check your connection and try again.');
        });
    }

    $('modalCloseIcon').addEventListener('click', close);
    $('modalCloseBtn').addEventListener('click', close);
    overlay.addEventListener('click', function (e) { if (e.target === overlay && !editing) close(); });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || !overlay.classList.contains('open')) return;
        if (editing) setEditing(false); else close();
    });

    btnEdit.addEventListener('click', function () { setEditing(true); });
    btnCancel.addEventListener('click', function () { setEditing(false); });
    btnSave.addEventListener('click', function () { saveEdit(false); });
    panel.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && editing && e.target.tagName === 'INPUT') { e.preventDefault(); saveEdit(false); }
    });
    submitBtn.addEventListener('click', submit);
    $('pdfPrev').addEventListener('click', function () { if (pdfPageNumber > 1) { pdfPageNumber--; renderPdfPage(); } });
    $('pdfNext').addEventListener('click', function () { if (pdfDocument && pdfPageNumber < pdfDocument.numPages) { pdfPageNumber++; renderPdfPage(); } });
    $('pdfZoomOut').addEventListener('click', function () { pdfZoom = Math.max(.6, pdfZoom - .15); renderPdfPage(); });
    $('pdfZoomIn').addEventListener('click', function () { pdfZoom = Math.min(2, pdfZoom + .15); renderPdfPage(); });
    window.addEventListener('resize', function () { if (pdfDocument && overlay.classList.contains('open')) renderPdfPage(); });

    window.CCSchedule = { open: open, close: close };
})();
