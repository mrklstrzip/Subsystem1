/* Capstone Coordinator - Profile page: real profile info, Edit Profile (name + email) and Change Password via the database */
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

    function saveUser(u) {
        try { localStorage.setItem('loggedInUser', JSON.stringify(u)); } catch (e) { }
    }

    // "Samantha Nicole Amor" -> "SA" (no profile picture, only initials)
    function initialsOf(u) {
        var f = String(u.firstName || '').trim().charAt(0);
        var l = String(u.lastName || '').trim().charAt(0);
        return (f + l).toUpperCase() || '?';
    }

    var current = readSaved() || {};

    function fill(u) {
        var full = [u.firstName, u.middleName, u.lastName].filter(Boolean).join(' ');
        var role = u.role || 'Capstone Coordinator';

        if ($('infoName')) $('infoName').textContent = full;
        if ($('infoNumber')) $('infoNumber').textContent = u.id || '';
        if ($('infoRole')) $('infoRole').textContent = role;
        if ($('infoEmail')) $('infoEmail').textContent = u.email || '';
        if ($('pmHeadName')) $('pmHeadName').textContent = [u.firstName, u.lastName].filter(Boolean).join(' ');
        if ($('pmHeadRole')) $('pmHeadRole').textContent = role;
        if ($('pmPhoto')) $('pmPhoto').textContent = initialsOf(u);

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
    }

    // 1) show what was saved at login straight away, 2) replace it with the database values
    fill(current);

    api('me').then(function (res) {
        if (!res || !res.success || !res.me) return;
        var merged = {};
        var k;
        for (k in current) merged[k] = current[k];
        for (k in res.me) merged[k] = res.me[k];
        current = merged;
        fill(current);
        saveUser(current);
        if (window.refreshSidebarProfile) window.refreshSidebarProfile();
    }).catch(function () { /* keep the saved values */ });

    // ---------------------------------------------------------------
    // Edit Profile (name and email)
    // ---------------------------------------------------------------
    var editBtn = $('editProfileBtn');
    var editForm = $('editProfileForm');
    var modal = $('editProfileModal');
    var profileRoot = document.querySelector('.pm');
    if (profileRoot && modal && modal.parentNode !== profileRoot) profileRoot.appendChild(modal);
    var cancelBtn = $('cancelEditProfileBtn');
    var saveBtn = $('saveProfileBtn');
    var message = $('profileEditMessage');

    if (modal) { modal.hidden = true; modal.setAttribute('aria-hidden', 'true'); }
    if (editForm) editForm.hidden = false;

    function say(text, bad) {
        if (!message) return;
        message.textContent = text || '';
        message.style.color = bad ? '#B42318' : '';
    }

    if (editBtn && editForm) {
        editBtn.addEventListener('click', function () {
            $('editFirstName').value = current.firstName || '';
            $('editMiddleName').value = current.middleName || '';
            $('editLastName').value = current.lastName || '';
            $('editEmail').value = current.email || '';
            say('');
            if (modal) { modal.hidden = false; modal.setAttribute('aria-hidden', 'false'); }
            $('editFirstName').focus();
        });
    }

    function closeEdit() {
        $('editFirstName').value = current.firstName || '';
        $('editMiddleName').value = current.middleName || '';
        $('editLastName').value = current.lastName || '';
        $('editEmail').value = current.email || '';
        if (modal) { modal.hidden = true; modal.setAttribute('aria-hidden', 'true'); }
        say('');
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && modal && !modal.hidden) closeEdit();
    });
    if (modal) modal.addEventListener('mousedown', function (e) { if (e.target === modal) closeEdit(); });
    if (cancelBtn) cancelBtn.addEventListener('click', closeEdit);

    if (editForm) {
        var busyEdit = false;
        editForm.addEventListener('submit', function (e) {
            e.preventDefault();
            if (busyEdit) return;

            var body = {
                firstName: $('editFirstName').value.trim(),
                middleName: $('editMiddleName').value.trim(),
                lastName: $('editLastName').value.trim(),
                email: $('editEmail').value.trim()
            };

            if (!body.firstName || !body.lastName || !body.email) {
                say('Please fill in your first name, last name and email.', true);
                if (window.Notice) Notice.error('Profile not updated', 'Please fill in your first name, last name and email.');
                return;
            }
            if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(body.email)) {
                say('Please enter a valid email address.', true);
                if (window.Notice) Notice.error('Profile not updated', 'Please enter a valid email address.');
                return;
            }

            busyEdit = true;
            if (saveBtn) saveBtn.disabled = true;
            say('Saving...');

            api('updateProfile', body).then(function (res) {
                busyEdit = false;
                if (saveBtn) saveBtn.disabled = false;

                if (!res || !res.success) {
                    var errorMessage = (res && res.message) || 'Something went wrong. Please try again.';
                    say(errorMessage, true);
                    if (window.Notice) Notice.error('Profile not updated', errorMessage);
                    return;
                }

                // keep the browser copy in step with the database, then redraw page + sidebar
                var saved = res.me || body;
                var merged = {};
                var k;
                for (k in current) merged[k] = current[k];
                for (k in saved) merged[k] = saved[k];
                current = merged;
                saveUser(current);
                fill(current);
                if (window.refreshSidebarProfile) window.refreshSidebarProfile();

                closeEdit();
                if (window.Notice) Notice.success('Profile updated', 'Your profile changes have been saved.');
                else alert('Profile updated successfully.');
            }, function () {
                busyEdit = false;
                if (saveBtn) saveBtn.disabled = false;
                var errorMessage = 'Could not reach the server. Please check your connection and try again.';
                say(errorMessage, true);
                if (window.Notice) Notice.error('Profile not updated', errorMessage);
            });
        });
    }

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

            var currentPw = $('current-password').value;
            var next = $('new-password').value;
            var confirm = $('confirm-password').value;

            if (next !== confirm) {
                if (window.Notice) Notice.error('Password not changed', 'New password and confirmation do not match.');
                else alert('New password and confirmation do not match.');
                return;
            }
            if (next.length < 8) {
                if (window.Notice) Notice.error('Password not changed', 'New password must be at least 8 characters long.');
                else alert('New password must be at least 8 characters long.');
                return;
            }
            if (next === currentPw) {
                if (window.Notice) Notice.error('Password not changed', 'New password must be different from the current password.');
                else alert('New password must be different from the current password.');
                return;
            }

            busy = true;
            api('changePassword', { currentPassword: currentPw, newPassword: next }).then(function (res) {
                busy = false;
                if (!res || !res.success) {
                    var errorMessage = (res && res.message) || 'Something went wrong. Please try again.';
                    if (window.Notice) Notice.error('Password not changed', errorMessage);
                    else alert(errorMessage);
                    return;
                }
                if (window.Notice) Notice.success('Password changed', 'Your password has been updated successfully.');
                else alert('Password successfully updated!');
                form.reset();
            }, function () {
                busy = false;
                var errorMessage = 'Could not reach the server. Please check your connection and try again.';
                if (window.Notice) Notice.error('Password not changed', errorMessage);
                else alert(errorMessage);
            });
        });
    }
})();
