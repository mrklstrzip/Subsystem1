document.addEventListener('DOMContentLoaded', function () {
    const userData = localStorage.getItem('loggedInUser');

    if (!userData) {
        window.location.href = '/Auth/Login/login';
        return;
    }

    const user = JSON.parse(userData);

    document.getElementById('profileName').textContent = user.firstName + ' ' + user.lastName;

    const roleEl = document.getElementById('profileRole');
    if (roleEl) {
        roleEl.textContent = user.role;
    }

    if (user.profilePhoto) {
        document.getElementById('profilePhoto').src = '/Content/images/' + user.profilePhoto;
    }

    // Student only
    const courseEl = document.getElementById('profileCourse');
    const sectionEl = document.getElementById('profileSection');
    const numberEl = document.getElementById('profileNumber');

    if (courseEl && user.program) {
        courseEl.textContent = user.program;
    }
    if (sectionEl && user.section) {
        sectionEl.textContent = user.section;
    }
    if (numberEl && user.id) {
        numberEl.textContent = 'Student no. ' + user.id;
    }

    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            localStorage.removeItem('loggedInUser');
            window.location.href = '/Auth/Login/login';
        });
    }
});