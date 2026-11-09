/* Capstone Coordinator - PM Invitations page.
   - the table comes from CoordinatorApi/invitations
   - the "Add multiple users" pop-up sends CoordinatorApi/invite (the server e-mails each student a sign-up link)
   - the server validates everything again; this file only gives faster feedback. */
(function () {
    'use strict';

    var API = '/CapstoneCoordinator/CoordinatorApi/';
    var LOGIN_URL = '/Auth/Login/login';
    var EMAIL = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/;
    var MAX = 30;

    function $(id) { return document.getElementById(id); }

    var body = $('invBody');
    if (!body) return;

    var STATUS_TEXT = { pending: 'Pending', accepted: 'Signed up', expired: 'Expired', failed: 'Not sent', revoked: 'Cancelled' };

    var rows = [];
    var emails = [];          // addresses typed so far (lowercase, no duplicates)
    var sending = false;
    var toastTimer = null;

    /* ---------- server ---------- */
    function call(action, payload) {
        var opts = {
            method: payload === undefined ? 'GET' : 'POST',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        };
        if (payload !== undefined) {
            opts.headers['Content-Type'] = 'application/json';
            opts.body = JSON.stringify(payload);
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

    function toast(text) {
        var t = $('invToast');
        t.textContent = text;
        t.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.hidden = true; }, 3500);
    }

    function el(tag, cls, text) {
        var n = document.createElement(tag);
        if (cls) n.className = cls;
        if (text != null) n.textContent = text;
        return n;
    }

    /* ---------- table ---------- */
    function draw() {
        var q = $('invSearch').value.trim().toLowerCase();
        var st = $('invStatus').value;
        var list = rows.filter(function (r) {
            return (!st || r.status === st) && (!q || r.email.toLowerCase().indexOf(q) !== -1);
        });

        body.textContent = '';
        if (!list.length) {
            var tr = el('tr', 'empty-row');
            var td = el('td', null, rows.length ? 'No invitations match your search.' : 'No invitations yet. Use "Invite Project Managers" to add students.');
            td.colSpan = 5;
            tr.appendChild(td);
            body.appendChild(tr);
            return;
        }

        list.forEach(function (r) {
            var tr = el('tr');

            var tdMail = el('td');
            tdMail.appendChild(el('div', 'inv-email', r.email));
            if (r.error) tdMail.appendChild(el('span', 'inv-note', r.error));
            tr.appendChild(tdMail);

            var tdSt = el('td');
            tdSt.appendChild(el('span', 'inv-status inv-status--' + r.status, STATUS_TEXT[r.status] || r.status));
            tr.appendChild(tdSt);

            tr.appendChild(el('td', 'date-cell', r.sentAt));
            tr.appendChild(el('td', 'date-cell', r.status === 'accepted' || r.status === 'revoked' ? '-' : r.expiresAt));

            var tdAct = el('td');
            if (r.canAct) {
                var box = el('div', 'inv-row-actions');
                var again = el('button', 'inv-link', r.status === 'failed' ? 'Retry' : 'Resend');
                again.type = 'button';
                again.addEventListener('click', function () { act('resendInvite', r, again); });
                var cancel = el('button', 'inv-link inv-link--danger', 'Cancel');
                cancel.type = 'button';
                cancel.addEventListener('click', function () {
                    if (window.confirm('Cancel the invitation for ' + r.email + '? Their link will stop working.')) act('revokeInvite', r, cancel);
                });
                box.appendChild(again);
                box.appendChild(cancel);
                tdAct.appendChild(box);
            } else {
                tdAct.textContent = '-';
            }
            tr.appendChild(tdAct);
            body.appendChild(tr);
        });
    }

    function load() {
        return call('invitations').then(function (res) {
            if (!res || !res.success) {
                body.textContent = '';
                var tr = el('tr', 'empty-row'), td = el('td', null, (res && res.message) || 'Could not load the invitations.');
                td.colSpan = 5;
                tr.appendChild(td);
                body.appendChild(tr);
                return;
            }
            rows = res.invitations || [];
            draw();
        }).catch(function () {
            body.textContent = '';
            var tr = el('tr', 'empty-row'), td = el('td', null, 'Could not reach the server.');
            td.colSpan = 5;
            tr.appendChild(td);
            body.appendChild(tr);
        });
    }

    function act(action, row, btn) {
        btn.disabled = true;
        call(action, { id: row.id }).then(function (res) {
            toast((res && res.message) || 'Done.');
            return load();
        }).catch(function () {
            toast('Could not reach the server.');
            btn.disabled = false;
        });
    }

    $('invSearch').addEventListener('input', draw);
    $('invStatus').addEventListener('change', draw);

    /* ---------- pop-up: chips ---------- */
    var overlay = $('invModal');
    var input = $('invEmailInput');
    var chips = $('invChips');
    var box = $('invChipBox');
    var errorBox = $('invError');
    var submit = $('invSubmit');

    function showError(text) {
        errorBox.textContent = text || '';
        errorBox.hidden = !text;
        box.classList.toggle('is-invalid', !!text);
    }

    function drawChips() {
        chips.textContent = '';
        emails.forEach(function (mail, i) {
            var chip = el('span', 'inv-chip' + (EMAIL.test(mail) ? '' : ' is-bad'));
            chip.title = EMAIL.test(mail) ? mail : 'This is not a valid email address.';
            chip.appendChild(el('span', null, mail));
            var x = el('button', null, '×');
            x.type = 'button';
            x.setAttribute('aria-label', 'Remove ' + mail);
            x.addEventListener('click', function (e) {
                e.stopPropagation();
                emails.splice(i, 1);
                drawChips();
                input.focus();
            });
            chip.appendChild(x);
            chips.appendChild(chip);
        });
        input.placeholder = emails.length ? '' : 'Add email addresses';
    }

    // takes whatever is in the box (one address, or many separated by comma / space / semicolon / new line)
    function takeInput() {
        var parts = input.value.split(/[,;\s]+/);
        var added = false;
        parts.forEach(function (p) {
            var mail = p.trim().toLowerCase();
            if (mail && emails.indexOf(mail) === -1) { emails.push(mail); added = true; }
        });
        input.value = '';
        if (added) { drawChips(); showError(''); }
    }

    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ',' || e.key === ';' || e.key === ' ') {
            if (input.value.trim() !== '' || e.key === 'Enter') e.preventDefault();
            takeInput();
        } else if (e.key === 'Backspace' && input.value === '' && emails.length) {
            emails.pop();
            drawChips();
        }
    });
    input.addEventListener('paste', function () { setTimeout(takeInput, 0); });
    input.addEventListener('blur', takeInput);
    box.addEventListener('click', function () { input.focus(); });

    /* ---------- pop-up: open / close ---------- */
    function open() {
        emails = [];
        drawChips();
        input.value = '';
        showError('');
        $('invStepForm').hidden = false;
        $('invStepDone').hidden = true;
        submit.disabled = false;
        submit.textContent = 'Send invitations';
        overlay.classList.add('open');
        overlay.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
        setTimeout(function () { input.focus(); }, 30);
    }

    function close() {
        if (sending) return;
        overlay.classList.remove('open');
        overlay.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
    }

    $('invOpen').addEventListener('click', open);
    $('invCancel').addEventListener('click', close);
    $('invDone').addEventListener('click', function () { close(); load(); });
    overlay.addEventListener('mousedown', function (e) { if (e.target === overlay) close(); });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && overlay.classList.contains('open')) close();
    });

    /* ---------- pop-up: send ---------- */
    function showResults(res) {
        var list = $('invResults');
        list.textContent = '';
        (res.results || []).forEach(function (r) {
            var cls = r.status === 'sent' || r.status === 'resent' ? 'is-ok' : (r.status === 'failed' ? 'is-fail' : 'is-skip');
            var icon = cls === 'is-ok' ? 'fa-circle-check' : (cls === 'is-fail' ? 'fa-circle-xmark' : 'fa-circle-minus');
            var li = el('li', cls);
            var ic = el('span', 'inv-r-icon');
            var i = el('i', 'fa-solid ' + icon);
            ic.appendChild(i);
            var text = el('div');
            text.appendChild(el('span', 'inv-r-mail', r.email));
            text.appendChild(el('span', 'inv-r-msg', r.message));
            li.appendChild(ic);
            li.appendChild(text);
            list.appendChild(li);
        });

        var bits = [];
        if (res.sent) bits.push(res.sent + ' sent');
        if (res.skipped) bits.push(res.skipped + ' skipped');
        if (res.failed) bits.push(res.failed + ' failed');
        $('invDoneTitle').textContent = res.failed ? 'Some invitations were not sent' : (res.sent ? 'Invitations sent' : 'Nothing was sent');
        $('invDoneText').textContent = bits.join(' · ') + '. Students open the link in the email to sign up as Project Manager.';
        $('invStepForm').hidden = true;
        $('invStepDone').hidden = false;
    }

    submit.addEventListener('click', function () {
        takeInput();
        if (sending) return;

        if (!emails.length) { showError('Add at least one email address.'); input.focus(); return; }
        var bad = emails.filter(function (m) { return !EMAIL.test(m); });
        if (bad.length) { showError('Fix or remove the highlighted address' + (bad.length === 1 ? '' : 'es') + '.'); return; }
        if (emails.length > MAX) { showError('You can invite up to ' + MAX + ' students at a time.'); return; }

        sending = true;
        submit.disabled = true;
        submit.textContent = 'Sending…';
        showError('');

        call('invite', { emails: emails }).then(function (res) {
            sending = false;
            if (res && res.results && res.results.length) { showResults(res); load(); return; }
            submit.disabled = false;
            submit.textContent = 'Send invitations';
            showError((res && res.message) || 'Something went wrong. Please try again.');
        }).catch(function () {
            sending = false;
            submit.disabled = false;
            submit.textContent = 'Send invitations';
            showError('Could not reach the server. Please try again.');
        });
    });

    load();
})();
