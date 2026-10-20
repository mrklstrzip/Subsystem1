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
});