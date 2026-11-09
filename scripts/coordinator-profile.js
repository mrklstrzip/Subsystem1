/* Capstone Coordinator - Profile page: real profile info + Change Password via the database */
(function () {
    'use strict';

    var API = '/CapstoneCoordinator/CoordinatorApi/';
    var LOGIN_URL = '/Auth/Login/login';

    function $(id) { return document.getElementById(id); }

    function sessionExpired() {
        try { localStorage.removeItem('loggedInUser'); } catch (e) { }
        window.location.href = LOGIN_URL;
    }

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
                    return new Promise(function () { });
                }
                return res;
            });
    }

    function readSaved() {
        try { return JSON.parse(localStorage.getItem('loggedInUser') || 'null'); } catch (e) { return null; }
    }

    function fill(u) {
        var full = [u.firstName, u.middleName, u.lastName].filter(Boolean).join(' ');
        var role = u.role || 'Capstone Coordinator';

        if ($('infoName')) $('infoName').textContent = full;
        if ($('infoNumber')) $('infoNumber').textContent = u.id || '';
        if ($('infoRole')) $('infoRole').textContent = role;
        if ($('infoEmail')) $('infoEmail').textContent = u.email || '';
        if ($('pmHeadName')) $('pmHeadName').textContent = [u.firstName, u.lastName].filter(Boolean).join(' ');
        if ($('pmHeadRole')) $('pmHeadRole').textContent = role;

        // Same photo rules as the sidebar: the saved photo, or the default avatar.
        var DEFAULT_PHOTO = '/Content/images/student-avatar.png';
        // PROGRAM and SECTION rows (added once, then refreshed)
        var body = $('profileTableBody');
        if (body) {
            [['PROGRAM', 'infoProgram', u.program], ['SECTION', 'infoSection', u.section]].forEach(function (r) {
                var cell = $(r[1]);
                if (!r[2]) { if (cell) cell.parentNode.remove(); return; }
                if (!cell) {
                    var tr = document.createElement('tr');
                    var label = document.createElement('td');
                    label.className = 'label';
                    label.textContent = r[0];
                    cell = document.createElement('td');
                    cell.className = 'info';
                    cell.id = r[1];
                    tr.appendChild(label);
                    tr.appendChild(cell);
                    body.appendChild(tr);
                }
                cell.textContent = r[2];
            });
        }

        var img = $('pmPhoto');
        if (img) {
            img.onerror = function () {
                this.onerror = null;
                this.src = DEFAULT_PHOTO;
            };
            var photo = (u.profilePhoto || '').trim();
            if (photo.indexOf('data:image/') === 0) img.src = photo;
            else img.src = photo ? '/Content/images/' + photo : DEFAULT_PHOTO;
            img.style.display = '';
        }
    }

    // 1) show what was saved at login straight away, 2) replace it with the database values
    var saved = readSaved();
    if (saved) fill(saved);

    api('me').then(function (res) {
        if (!res || !res.success || !res.me) return;
        var merged = {};
        var k;
        for (k in (saved || {})) merged[k] = saved[k];
        for (k in res.me) merged[k] = res.me[k];
        fill(merged);
        try { localStorage.setItem('loggedInUser', JSON.stringify(merged)); } catch (e) { }
    }).catch(function () { /* keep the saved values */ });

    // Show / hide password eye icons
    Array.prototype.forEach.call(document.querySelectorAll('.toggle-password'), function (btn) {
        btn.addEventListener('click', function () {
            var input = this.previousElementSibling;
            if (!input) return;
            if (input.type === 'password') {
                input.type = 'text';
                this.classList.remove('fa-eye');
                this.classList.add('fa-eye-slash');
            } else {
                input.type = 'password';
                this.classList.remove('fa-eye-slash');
                this.classList.add('fa-eye');
            }
        });
    });

    // Change Password
    var form = $('changePasswordForm');
    if (form) {
        var busy = false;
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            if (busy) return;

            var current = $('current-password').value;
            var next = $('new-password').value;
            var confirm = $('confirm-password').value;

            if (next !== confirm) { alert('New password and confirmation do not match.'); return; }
            if (next.length < 8) { alert('New password must be at least 8 characters long.'); return; }
            if (next === current) { alert('New password must be different from the current password.'); return; }

            busy = true;
            api('changePassword', { currentPassword: current, newPassword: next }).then(function (res) {
                busy = false;
                if (!res || !res.success) {
                    alert((res && res.message) || 'Something went wrong. Please try again.');
                    return;
                }
                alert('Password successfully updated!');
                form.reset();
            }, function () {
                busy = false;
                alert('Could not reach the server. Please check your connection and try again.');
            });
        });
    }
})();
