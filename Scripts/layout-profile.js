(function () {
    'use strict';

    const LOGIN_URL = '/Auth/Login/login';
    const ADMIN_LOGIN_URL = '/Auth/AdminLogin/adminlogin';

    // where each role's own pages live (used to send someone back to where they belong)
    const ROLE_HOME = {
        "Admin": '/Admin/Dashboard/dashboard',
        "Dean": '/Dean/Dashboard/dashboard',
        "Campus Director": '/CampusDirector/Dashboard/dashboard',
        "Capstone Coordinator": '/CapstoneCoordinator/Dashboard/dashboard',
        "Professor": '/Professor/Dashboard/dashboard',
        "Panelist": '/Professor/Dashboard/dashboard',
        "Adviser": '/Adviser/Dashboard/dashboard',
        "Student": '/Student/Dashboard/dashboard',
        "Project Manager": '/Student/Dashboard/dashboard'
    };

    // which roles may use each layout (decided by the role-* class on .layout-wrapper)
    const LAYOUT_ROLES = {
        "role-admin": ["Admin"],
        "role-dean": ["Dean"],
        "role-campus-director": ["Campus Director"],
        "role-capstone-coordinator": ["Capstone Coordinator"],
        "role-professor": ["Professor", "Panelist"],
        "role-adviser": ["Adviser"],
        "role-student": ["Student", "Project Manager"]
    };

    // ---- which layout is this? ------------------------------------------------
    const wrapper = document.querySelector('.layout-wrapper');
    const layoutClasses = wrapper ? wrapper.className.split(/\s+/) : [];
    let allowedRoles = null;
    layoutClasses.forEach(function (c) { if (LAYOUT_ROLES[c]) allowedRoles = LAYOUT_ROLES[c]; });

    // admin pages go back to the admin login, everyone else to the regular login
    const loginUrl = layoutClasses.indexOf('role-admin') > -1 ? ADMIN_LOGIN_URL : LOGIN_URL;

    function leave(url) { window.location.href = url; }

    // keep the page hidden until we know this person is allowed to see it
    document.body.style.visibility = 'hidden';

    // ---- who is logged in? ----------------------------------------------------
    let user = null;
    try { user = JSON.parse(localStorage.getItem('loggedInUser') || 'null'); } catch (err) { user = null; }

    // 1) not logged in -> the right login page
    if (!user) { leave(loginUrl); return; }

    // 2) logged in, but this layout is not for their role -> send them to their own dashboard
    if (allowedRoles && allowedRoles.indexOf(user.role) === -1) {
        const ownHome = ROLE_HOME[user.role];
        if (ownHome) { leave(ownHome); }
        else { localStorage.removeItem('loggedInUser'); leave(LOGIN_URL); }
        return;
    }

    // allowed - show the page
    document.body.style.visibility = '';

    // Apply the student's program colour theme (BSIT / BSIS / BSCS).
    // layout.css only colours the header and sidebar when the body has a matching
    // data-program attribute, e.g. data-program="BS Computer Science".
    if (user.program) {
        document.body.setAttribute('data-program', user.program);
    }

    // ---- fill the sidebar -----------------------------------------------------
    function fillProfile() {
        const nameEl = document.getElementById('profileName');
        if (nameEl) nameEl.textContent = user.firstName + ' ' + user.lastName;

        const roleEl = document.getElementById('profileRole');
        if (roleEl) roleEl.textContent = user.role;

        const photoEl = document.getElementById('profilePhoto');
        if (photoEl && user.profilePhoto) photoEl.src = '/Content/images/' + user.profilePhoto;

        // Student only
        const courseEl = document.getElementById('profileCourse');
        const sectionEl = document.getElementById('profileSection');
        const numberEl = document.getElementById('profileNumber');

        if (courseEl && user.program) courseEl.textContent = user.program;
        if (sectionEl && user.section) sectionEl.textContent = user.section;
        if (numberEl && user.id) numberEl.textContent = 'Student no. ' + user.id;

        const logoutBtn = document.getElementById('logoutBtn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', function (e) {
                e.preventDefault();
                localStorage.removeItem('loggedInUser');
                leave(loginUrl);
            });
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', fillProfile);
    else fillProfile();
})();