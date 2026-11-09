
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

    // Fill the sidebar profile.
    function fillProfile() {
        const nameEl = document.getElementById('profileName');

        if (nameEl) {
            nameEl.textContent =
                [user.firstName, user.lastName]
                    .filter(Boolean)
                    .join(' ');
        }

        const roleEl = document.getElementById('profileRole');

        if (roleEl) {
            roleEl.textContent = user.role || '';
        }


        const photoEl = document.getElementById('profilePhoto');

        if (photoEl) {
            const defaultPhoto = '/Content/images/student-avatar.png';
            const photoName = user.profilePhoto
                ? user.profilePhoto.trim()
                : '';

            photoEl.onerror = function () {
                this.onerror = null;
                this.src = defaultPhoto;
            };

            photoEl.src = photoName
                ? '/Content/images/' + photoName
                : defaultPhoto;
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

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', fillProfile);
    } else {
        fillProfile();
    }
})();
