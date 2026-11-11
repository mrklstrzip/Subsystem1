
(function () {
    'use strict';

    const LOGIN_URL = '/Auth/Login/login';

    // Dashboard destinations for each role.
    const ROLE_HOME = {
        "Admin": '/Admin/Dashboard/dashboard',
        "Dean": '/Dean/Dashboard/dashboard',
        "Campus Director": '/CampusDirector/Dashboard/dashboard',
        "Capstone Coordinator": '/CapstoneCoordinator/Dashboard/dashboard',
        "Student": '/Student/Dashboard/dashboard',
        "Project Manager": '/Student/Dashboard/dashboard'
    };

    // Roles allowed to access each layout.
    const LAYOUT_ROLES = {
        "role-admin": ["Admin"],
        "role-dean": ["Dean"],
        "role-campus-director": ["Campus Director"],
        "role-capstone-coordinator": ["Capstone Coordinator"],
        "role-student": ["Student", "Project Manager"]
    };

    // Identify the current layout.
    const wrapper = document.querySelector('.layout-wrapper');
    const layoutClasses = wrapper
        ? wrapper.className.split(/\s+/)
        : [];

    let allowedRoles = null;

    layoutClasses.forEach(function (className) {
        if (LAYOUT_ROLES[className]) {
            allowedRoles = LAYOUT_ROLES[className];
        }
    });

    const loginUrl = LOGIN_URL;

    function leave(url) {
        window.location.href = url;
    }

    // Hide the page until the user's access is checked.
    document.body.style.visibility = 'hidden';

    // Get the logged-in user.
    let user = null;

    try {
        user = JSON.parse(
            localStorage.getItem('loggedInUser') || 'null'
        );
    } catch (error) {
        user = null;
    }

    // Redirect users who are not logged in.
    if (!user) {
        leave(loginUrl);
        return;
    }

    // Prevent users from accessing the wrong role's layout.
    if (allowedRoles && allowedRoles.indexOf(user.role) === -1) {
        const ownHome = ROLE_HOME[user.role];

        if (ownHome) {
            leave(ownHome);
        } else {
            localStorage.removeItem('loggedInUser');
            leave(LOGIN_URL);
        }

        return;
    }

    // Normalize the program value to match layout.css.
    function getProgramTheme(program) {
        if (!program) {
            return null;
        }

        const value = String(program)
            .trim()
            .toUpperCase()
            .replace(/\s+/g, ' ');

        const programThemes = {
            "BSCS": "BS Computer Science",
            "BS COMPUTER SCIENCE": "BS Computer Science",
            "BS COMPUTER SCIENCES": "BS Computer Science",

            "BSIS": "BS Information System",
            "BS INFORMATION SYSTEM": "BS Information System",
            "BS INFORMATION SYSTEMS": "BS Information System",

            "BSIT": "BS Information Technology",
            "BS INFORMATION TECHNOLOGY": "BS Information Technology"
        };

        return programThemes[value] || null;
    }

    // Apply the program-specific student theme.
    const programTheme = getProgramTheme(user.program);

    if (programTheme) {
        document.body.setAttribute('data-program', programTheme);
    } else {
        document.body.removeAttribute('data-program');
    }

    // Show the page after the access check.
    document.body.style.visibility = '';

    // Latest saved user (Edit Profile updates localStorage, then calls refreshSidebarProfile).
    function currentUser() {
        try {
            return JSON.parse(localStorage.getItem('loggedInUser') || 'null') || user;
        } catch (error) {
            return user;
        }
    }

    // "Samantha Nicole Amor" -> initials "SA"
    function initialsOf(u) {
        const first = String(u.firstName || '').trim().charAt(0);
        const last = String(u.lastName || '').trim().charAt(0);
        return (first + last).toUpperCase() || '?';
    }

    // Fill the sidebar profile.
    function fillProfile() {
        const user = currentUser();
        const nameEl = document.getElementById('profileName');

        if (nameEl) {
            // Surname on top, given name + middle initial below:
            //   AMOR,
            //   Samantha Nicole O.
            const surname = String(user.lastName || '').trim().toUpperCase();
            const middle = String(user.middleName || '').trim();
            const given = [
                String(user.firstName || '').trim(),
                middle ? middle.charAt(0).toUpperCase() + '.' : ''
            ].filter(Boolean).join(' ');

            nameEl.textContent = '';

            const top = document.createElement('span');
            top.className = 'sb-surname';
            top.textContent = surname ? surname + ',' : '';

            const bottom = document.createElement('span');
            bottom.className = 'sb-given';
            bottom.textContent = given;

            nameEl.appendChild(top);
            nameEl.appendChild(bottom);
            nameEl.title = [surname ? surname + ',' : '', given].join(' ').trim();
            nameEl.setAttribute('data-fullname',
                [user.firstName, user.lastName].filter(Boolean).join(' '));
        }

        const roleEl = document.getElementById('profileRole');

        if (roleEl) {
            roleEl.textContent = user.role || '';
        }


        // No profile picture any more: show the user's initials.
        const photoEl = document.getElementById('profilePhoto');

        if (photoEl) {
            photoEl.textContent = initialsOf(user);
        }


        // Student information.
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

        // Logout behavior.
        const logoutBtn = document.getElementById('logoutBtn');

        if (logoutBtn && !logoutBtn.dataset.logoutBound) {
            logoutBtn.dataset.logoutBound = 'true';

            logoutBtn.addEventListener('click', function (event) {
                event.preventDefault();

                // End the server-side session (same logout for every role).
                try {
                    fetch('/Auth/Login/logout', {
                        method: 'POST',
                        credentials: 'same-origin',
                        keepalive: true
                    });
                } catch (e) { /* ignore */ }

                localStorage.removeItem('loggedInUser');
                leave(loginUrl);
            });
        }
    }

    window.refreshSidebarProfile = fillProfile;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', fillProfile);
    } else {
        fillProfile();
    }
})();
