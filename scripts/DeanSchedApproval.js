(function () {
    'use strict';

    function $(id) { return document.getElementById(id); }

    var config = $('saConfig');
    if (!config) return; // page has no schedule pop-up

    var detailsUrl = config.dataset.detailsUrl;
    var approveUrl = config.dataset.approveUrl;
    var manuscriptUrl = config.dataset.manuscriptUrl;
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';

    var overlay = $('saOverlay');
    var approveBtn = $('saApprove');
    var currentId = null;
    var pdfDocument = null;
    var pdfPageNumber = 1;
    var pdfZoom = 1;
    var pdfLoadId = 0;
    var pdfRenderTask = null;
    if (window.pdfjsLib) {
        window.pdfjsLib.GlobalWorkerOptions.workerSrc =
            'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
    }

    function expired() {
        try { localStorage.removeItem('loggedInUser'); } catch (e) { }
        window.location.href = '/Auth/Login/login';
    }

    function esc(s) {
        var d = document.createElement('div');
        d.textContent = s == null ? '' : s;
        return d.innerHTML;
    }

    /* ---------- Filters (Schedule Approval page only) ---------- */
    if ($('saSearch')) {
        var applyFilters = function () {
            var q = $('saSearch').value.trim().toLowerCase();
            var p = $('fProgram').value, s = $('fStatus').value, a = $('fAdviser').value;
            var shown = 0;
            document.querySelectorAll('#saTable tbody tr').forEach(function (tr) {
                var ok = (!q || tr.dataset.search.indexOf(q) > -1) &&
                    (!p || tr.dataset.program === p) &&
                    (!s || tr.dataset.status === s) &&
                    (!a || tr.dataset.adviser === a);
                tr.style.display = ok ? '' : 'none';
                if (ok) shown++;
            });
            $('saEmpty').style.display = shown ? 'none' : 'block';
        };
        ['saSearch', 'fProgram', 'fStatus', 'fAdviser'].forEach(function (id) {
            $(id).addEventListener(id === 'saSearch' ? 'input' : 'change', applyFilters);
        });
    }

    /* ---------- Modal ---------- */
    function setStatusBadge(el, status) {
        el.textContent = status;
        el.className = 'sa-status ' + (status === 'Approved' ? 'st-approved' : 'st-toapprove');
    }

    function clearManuscript() {
        pdfLoadId++;
        if (pdfRenderTask) { pdfRenderTask.cancel(); pdfRenderTask = null; }
        if (pdfDocument) { pdfDocument.destroy(); pdfDocument = null; }
        $('saPdfCanvas').hidden = true;
        $('saPdfPage').textContent = $('saPdfPages').textContent = '\u2014';
        $('mManuscriptOpen').hidden = true;
        $('saPdfMessage').hidden = false;
        $('saPdfMessage').textContent = 'No PDF manuscript is available for this submission.';
        $('saPdfPrev').disabled = true;
        $('saPdfNext').disabled = true;
    }

    function renderManuscriptPage() {
        if (!pdfDocument) return;
        pdfDocument.getPage(pdfPageNumber).then(function (page) {
            var canvas = $('saPdfCanvas');
            var context = canvas.getContext('2d');
            var base = page.getViewport({ scale: 1 });
            var maxWidth = Math.max(100, $('saPdfCanvasWrap').clientWidth - 24);
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
            $('saPdfPage').textContent = pdfPageNumber;
            $('saPdfPages').textContent = pdfDocument.numPages;
            $('saPdfPrev').disabled = pdfPageNumber <= 1;
            $('saPdfNext').disabled = pdfPageNumber >= pdfDocument.numPages;
        }).catch(function () { });
    }

    function loadManuscript(id, available) {
        clearManuscript();
        var loadId = pdfLoadId;
        if (!available || !manuscriptUrl) return;
        var href = manuscriptUrl + (manuscriptUrl.indexOf('?') >= 0 ? '&' : '?') + 'id=' + encodeURIComponent(id);
        var link = $('mManuscriptOpen');
        var message = $('saPdfMessage');
        link.href = href;
        link.hidden = false;
        message.textContent = 'Loading manuscript…';
        if (!window.pdfjsLib) { message.textContent = 'The PDF viewer could not be loaded. Use Open PDF to view the manuscript.'; return; }
        fetch(href, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (response) {
                if (!response.ok) { var error = new Error('Unable to load the PDF.'); error.status = response.status; throw error; }
                return response.arrayBuffer();
            })
            .then(function (buffer) {
                if (loadId !== pdfLoadId) return null;
                var bytes = new Uint8Array(buffer);
                if (new TextDecoder('ascii').decode(bytes.subarray(0, 1024)).indexOf('%PDF-') < 0)
                    throw new Error('The response is not a PDF.');
                return window.pdfjsLib.getDocument({ data: bytes, isEvalSupported: false, enableXfa: false }).promise;
            })
            .then(function (doc) {
                if (!doc) return;
                if (loadId !== pdfLoadId) { doc.destroy(); return; }
                pdfDocument = doc;
                pdfPageNumber = 1;
                pdfZoom = 1;
                $('saPdfCanvas').hidden = false;
                message.hidden = true;
                renderManuscriptPage();
            })
            .catch(function (error) {
                if (loadId !== pdfLoadId) return;
                message.textContent = error && error.status === 404
                    ? 'No PDF manuscript is attached to this schedule.'
                    : 'Could not load the Cloudinary manuscript. Use Open PDF to try again.';
            });
    }

    function openModal(id) {
        currentId = id;
        clearManuscript();
        fetch(detailsUrl + '?id=' + encodeURIComponent(id), { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
            .then(function (d) {
                if (d && d.unauthorized) { expired(); return; }
                if (d && d.success === false) { alert(d.message || 'Unable to load schedule details.'); return; }
                $('mTitle').textContent = d.ProjectTitle;
                $('mSubmitted').textContent = d.DateSubmitted;
                $('mSubmittedSmall').textContent = 'Submitted ' + d.DateSubmitted;
                setStatusBadge($('mStatus'), d.Status);
                $('mStudent').textContent = d.Student;
                $('mProgram').textContent = d.ProgramSection;
                $('mAdviser').textContent = d.Adviser;
                $('mTime').textContent = d.Time;
                $('mDate').textContent = d.DefenseDate;
                $('mRoom').textContent = d.Room;

                $('mPanelists').innerHTML = d.Panelists.map(function (p) {
                    var pct = (p.Percent === null || p.Percent === undefined) ? '\u2014' : Number(p.Percent).toFixed(1) + '%';
                    var role = p.Role || 'Member';
                    return '<div class="sa-panel-row"><span>' + esc(p.Name) +
                        '<small class="panelist-role ' + esc(role).toLowerCase() + '">' + esc(role) + '</small></span>' +
                        '<span class="sa-match-wrap"><span class="sa-match-pct">' + pct + '</span>' +
                        '<span class="sa-match match-' + esc(p.Expertise).toLowerCase() + '">' + esc(p.Expertise) + '</span></span></div>';
                }).join('');

                var approved = d.Status === 'Approved';
                approveBtn.disabled = approved;
                approveBtn.textContent = approved ? 'APPROVED' : 'APPROVE';

                overlay.classList.add('open');
                overlay.setAttribute('aria-hidden', 'false');
                document.body.style.overflow = 'hidden';
                loadManuscript(id, d.HasManuscriptPdf);
            })
            .catch(function () { alert('Unable to load schedule details.'); });
    }

    function closeModal() {
        overlay.classList.remove('open');
        overlay.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
        currentId = null;
        clearManuscript();
    }

    // Any element with data-sa-open + data-id opens the pop-up (works on both pages)
    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!(t instanceof Element)) return;
        var trigger = t.closest('[data-sa-open]');
        if (!trigger) return;
        e.preventDefault();
        openModal(trigger.dataset.id);
    });

    try {
        var wanted = new URLSearchParams(window.location.search).get('open');
        if (wanted) {
            openModal(wanted);
            if (window.history && history.replaceState) history.replaceState(null, '', window.location.pathname);
        }
    } catch (e) { }

    $('saX').addEventListener('click', closeModal);
    $('saClose').addEventListener('click', closeModal);
    $('saPdfPrev').addEventListener('click', function () { if (pdfPageNumber > 1) { pdfPageNumber--; renderManuscriptPage(); } });
    $('saPdfNext').addEventListener('click', function () { if (pdfDocument && pdfPageNumber < pdfDocument.numPages) { pdfPageNumber++; renderManuscriptPage(); } });
    $('saPdfZoomOut').addEventListener('click', function () { pdfZoom = Math.max(.6, pdfZoom - .15); renderManuscriptPage(); });
    $('saPdfZoomIn').addEventListener('click', function () { pdfZoom = Math.min(2, pdfZoom + .15); renderManuscriptPage(); });
    window.addEventListener('resize', function () { if (pdfDocument && overlay.classList.contains('open')) renderManuscriptPage(); });
    overlay.addEventListener('click', function (e) { if (e.target === overlay) closeModal(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeModal(); });

    /* ---------- Approve ---------- */
    approveBtn.addEventListener('click', function () {
        if (!currentId || approveBtn.disabled) return;
        approveBtn.disabled = true;

        var body = new URLSearchParams();
        body.append('id', currentId);
        body.append('__RequestVerificationToken', token);

        fetch(approveUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body.toString()
        })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res && res.unauthorized) { expired(); return; }
                if (!res.success) {
                    alert(res.message || 'Approval failed.');
                    approveBtn.disabled = false;
                    return;
                }

                var row = document.querySelector('tr[data-id="' + currentId + '"]');
                if (row) {
                    var badge = row.querySelector('.sa-status');
                    if (badge) {
                        // Schedule Approval page: flip the badge
                        row.dataset.status = 'Approved';
                        setStatusBadge(badge, 'Approved');
                    } else {
                        // Dashboard "To Approve" table: it's no longer pending
                        row.remove();
                    }
                }
                var approvedTitle = $('mTitle').textContent;
                closeModal();
                if (window.Notice) Notice.success('Schedule approved', '"' + approvedTitle + '" has been approved.');
            })
            .catch(function () {
                alert('Something went wrong.');
                approveBtn.disabled = false;
            });
    });
})();
