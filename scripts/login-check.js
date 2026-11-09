const ROLE_HOME = {
    "Admin": '/Admin/Dashboard/dashboard',
    "Dean": '/Dean/Dashboard/dashboard',
    "Campus Director": '/CampusDirector/Dashboard/dashboard',
    "Capstone Coordinator": '/CapstoneCoordinator/Dashboard/dashboard',
    "Student": '/Student/Dashboard/dashboard',
    "Project Manager": '/Student/Dashboard/dashboard'
};

document.getElementById('loginForm').addEventListener('submit', function (e) {
    e.preventDefault();

    const form = e.currentTarget;
    const button = form.querySelector('button[type="submit"]');
    const errorBox = document.getElementById('loginError');
    const buttonLabel = 'Login';

    // auth.js turns the button into "Signing in..." on submit; put it back when the login fails.
    function resetButton() {
        if (!button) return;
        button.removeAttribute('aria-busy');
        button.textContent = buttonLabel;
    }

    function fail(message) {
        resetButton();
        if (errorBox) {
            errorBox.textContent = message;
        } else {
            alert(message);
        }
        const pw = document.getElementById('Password');
        if (pw) { pw.focus(); pw.select(); }
    }

    if (errorBox) errorBox.textContent = '';

    const email = document.getElementById('Email').value.trim();
    const password = document.getElementById('Password').value;

    fetch('/Auth/Login/check', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: 'email=' + encodeURIComponent(email) + '&password=' + encodeURIComponent(password)
    })
        .then(function (response) { return response.json(); })
        .then(function (result) {
            if (!result.success) {
                fail(result.message || 'Invalid email or password.');
                return;
            }

            const home = ROLE_HOME[result.user.role];
            if (!home) {
                fail('Unknown role.');
                return;
            }

            localStorage.setItem('loggedInUser', JSON.stringify(result.user));
            window.location.href = home;
        })
        .catch(function () {
            fail('Could not reach the server. Please try again.');
        });
});