document.addEventListener('DOMContentLoaded', function () {
    const userData = localStorage.getItem('loggedInUser');

    if (!userData) {
        window.location.href = '/Auth/Login/login';
        return;
    }

    const user = JSON.parse(userData);

    // 2. Set dynamic role class on outer wrapper (e.g., role-student, role-professor, role-admin)
    const wrapperElement = document.querySelector('[class*="role-"]');
    if (wrapperElement && user.role) {
        const newRoleClass = 'role-' + user.role.toLowerCase().replace(/\s+/g, '');
        wrapperElement.className = wrapperElement.className
            .replace(/\brole-[^\s]+\b/g, '')
            .trim() + ' ' + newRoleClass;
    }

    // Helper for full name
    const fullName = `${user.firstName}${user.middleName ? ' ' + user.middleName : ''} ${user.lastName}`;

    // 3. Populate Profile Information Table
    const nameEl = document.getElementById('infoName');
    const numberEl = document.getElementById('infoNumber');
    const roleEl = document.getElementById('infoRole');
    const emailEl = document.getElementById('infoEmail');
    const profileTableBody = document.getElementById('profileTableBody');

    if (nameEl) nameEl.textContent = fullName;
    if (roleEl) roleEl.textContent = user.role;
    if (emailEl) emailEl.textContent = user.email;

    if (numberEl && user.id) {
        numberEl.textContent = user.id;
    }

    // Dynamic role-specific fields (College / Program / Section)
    document.querySelectorAll('.dynamic-row').forEach(row => row.remove());

    if (user.college && profileTableBody) {
        profileTableBody.innerHTML += `
            <tr class="dynamic-row">
                <td class="label">COLLEGE</td>
                <td class="info">${user.college}</td>
            </tr>`;
    }

    if (user.program && profileTableBody) {
        profileTableBody.innerHTML += `
            <tr class="dynamic-row">
                <td class="label">PROGRAM</td>
                <td class="info">${user.program}</td>
            </tr>`;
    }

    if (user.section && profileTableBody) {
        profileTableBody.innerHTML += `
            <tr class="dynamic-row">
                <td class="label">SECTION</td>
                <td class="info">${user.section}</td>
            </tr>`;
    }

    // 4. Password Toggle Eye Icon
    const toggleButtons = document.querySelectorAll('.toggle-password');
    toggleButtons.forEach(button => {
        button.addEventListener('click', function () {
            const input = this.previousElementSibling;
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

    // 5. Change Password Form
    const passwordForm = document.getElementById('changePasswordForm');
    if (passwordForm) {
        passwordForm.addEventListener('submit', function (e) {
            e.preventDefault();

            const currentPass = document.getElementById('current-password').value;
            const newPass = document.getElementById('new-password').value;
            const confirmPass = document.getElementById('confirm-password').value;

            if (currentPass !== user.password) {
                alert('Incorrect current password.');
                return;
            }

            if (newPass !== confirmPass) {
                alert('New password and confirmation do not match.');
                return;
            }

            if (newPass.length < 6) {
                alert('New password must be at least 6 characters long.');
                return;
            }

            user.password = newPass;
            localStorage.setItem('loggedInUser', JSON.stringify(user));

            let overrides = {};
            try { overrides = JSON.parse(localStorage.getItem('passwordOverrides') || '{}'); } catch (err) { overrides = {}; }
            overrides[user.email] = newPass;
            localStorage.setItem('passwordOverrides', JSON.stringify(overrides));

            alert('Password successfully updated!');
            passwordForm.reset();
        });
    }

    // 6. Logout Handler
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            localStorage.removeItem('loggedInUser');
            window.location.href = '/Auth/Login/login';
        });
    }


    // Profile editing and photo upload (profile page only; login files are unchanged).
    const editProfileBtn = document.getElementById('editProfileBtn');
    const changePhotoBtn = document.getElementById('changePhotoBtn');
    const photoInput = document.getElementById('profilePhotoInput');
    const editProfileForm = document.getElementById('editProfileForm');
    const cancelEditProfileBtn = document.getElementById('cancelEditProfileBtn');
    const editMessage = document.getElementById('profileEditMessage');
    const profilePhoto = document.getElementById('pmPhoto');

    function renderProfileHeader() {
        const headName = document.getElementById('pmHeadName');
        const headRole = document.getElementById('pmHeadRole');
        const full = `${user.firstName || ''}${user.middleName ? ' ' + user.middleName : ''} ${user.lastName || ''}`.trim();
        if (headName) headName.textContent = full;
        if (headRole) headRole.textContent = user.role || 'Student';
        if (profilePhoto) {
            profilePhoto.onerror = function () {
                this.onerror = null;
                this.src = '/Content/images/student-avatar.png';
            };
            if (user.profilePhoto && user.profilePhoto.indexOf('data:image/') === 0) {
                profilePhoto.src = user.profilePhoto;
            } else {
                profilePhoto.src = user.profilePhoto ? '/Content/images/' + user.profilePhoto : '/Content/images/student-avatar.png';
            }
            profilePhoto.style.display = '';
        }
        const nameEl = document.getElementById('infoName');
        const emailEl = document.getElementById('infoEmail');
        if (nameEl) nameEl.textContent = full;
        if (emailEl) emailEl.textContent = user.email || '';
    }

    function showEditMessage(message) {
        if (editMessage) editMessage.textContent = message;
    }

    if (editProfileBtn && editProfileForm) {
        editProfileBtn.addEventListener('click', function () {
            document.getElementById('editFirstName').value = user.firstName || '';
            document.getElementById('editLastName').value = user.lastName || '';
            document.getElementById('editEmail').value = user.email || '';
            editProfileForm.hidden = false;
            showEditMessage('');
        });
    }

    if (cancelEditProfileBtn && editProfileForm) {
        cancelEditProfileBtn.addEventListener('click', function () {
            editProfileForm.reset();
            editProfileForm.hidden = true;
            showEditMessage('');
        });
    }

    if (editProfileForm) {
        editProfileForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const firstName = document.getElementById('editFirstName').value.trim();
            const lastName = document.getElementById('editLastName').value.trim();
            const email = document.getElementById('editEmail').value.trim();
            if (!firstName || !lastName || !email) {
                showEditMessage('Please complete all fields.');
                return;
            }
            user.firstName = firstName;
            user.lastName = lastName;
            user.email = email;
            localStorage.setItem('loggedInUser', JSON.stringify(user));
            renderProfileHeader();
            editProfileForm.hidden = true;
            showEditMessage('Profile updated.');
            alert('Profile updated successfully.');
        });
    }

    if (changePhotoBtn && photoInput) {
        changePhotoBtn.addEventListener('click', function () { photoInput.click(); });
        photoInput.addEventListener('change', function () {
            const file = this.files && this.files[0];
            if (!file) return;
            if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
                alert('Please choose a JPG, PNG, or WEBP image.');
                this.value = '';
                return;
            }
            if (file.size > 2 * 1024 * 1024) {
                alert('Please choose an image smaller than 2 MB.');
                this.value = '';
                return;
            }
            const reader = new FileReader();
            reader.onload = function (event) {
                user.profilePhoto = event.target.result;
                try {
                    localStorage.setItem('loggedInUser', JSON.stringify(user));
                    renderProfileHeader();
                    alert('Profile picture updated successfully.');
                } catch (err) {
                    alert('The image could not be saved in browser storage. Please choose a smaller image.');
                }
            };
            reader.readAsDataURL(file);
        });
    }

    renderProfileHeader();
});