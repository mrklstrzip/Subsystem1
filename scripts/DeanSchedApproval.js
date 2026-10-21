(function () {
    'use strict';

    function $(id) { return document.getElementById(id); }

    var config = $('saConfig');
    if (!config) return; // page has no schedule pop-up

    var detailsUrl = config.dataset.detailsUrl;
    var approveUrl = config.dataset.approveUrl;
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenInput ? tokenInput.value : '';

    var overlay = $('saOverlay');
    var approveBtn = $('saApprove');
    var currentId = null;

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

    function openModal(id) {
        currentId = id;
        fetch(detailsUrl + '?id=' + encodeURIComponent(id), { credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
            .then(function (d) {
                $('mTitle').textContent = d.ProjectTitle;
                $('mSubmitted').textContent = d.DateSubmitted;
                $('mSubmittedSmall').textContent = 'Submitted ' + d.DateSubmitted;
                setStatusBadge($('mStatus'), d.Status);
                $('mStudent').textContent = d.Student;
                $('mProgram').textContent = d.ProgramSection;
                $('mAdviser').textContent = d.Adviser;
                $('mAbstract').textContent = d.Abstract;
                $('mKeywords').textContent = d.Keywords;
                $('mTime').textContent = d.Time;
                $('mDate').textContent = d.DefenseDate;
                $('mRoom').textContent = d.Room;

                $('mPanelists').innerHTML = d.Panelists.map(function (p) {
                    return '<div class="sa-panel-row"><span>' + esc(p.Name) + '</span>' +
                        '<span class="sa-match match-' + esc(p.Expertise).toLowerCase() + '">' + esc(p.Expertise) + '</span></div>';
                }).join('');

                var approved = d.Status === 'Approved';
                approveBtn.disabled = approved;
                approveBtn.textContent = approved ? 'APPROVED' : 'APPROVE';

                overlay.classList.add('open');
                overlay.setAttribute('aria-hidden', 'false');
                document.body.style.overflow = 'hidden';
            })
            .catch(function () { alert('Unable to load schedule details.'); });
    }

    function closeModal() {
        overlay.classList.remove('open');
        overlay.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
        currentId = null;
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

    $('saX').addEventListener('click', closeModal);
    $('saClose').addEventListener('click', closeModal);
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
                closeModal();
            })
            .catch(function () {
                alert('Something went wrong.');
                approveBtn.disabled = false;
            });
    });
})();