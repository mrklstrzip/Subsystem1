(function () {
    const SHOW_DEMO_CODE = true;        // show the code on screen
    const CODE_TTL_MS = 10 * 60 * 1000; // code is valid for 10 minutes
    const MAX_ATTEMPTS = 5;             // wrong tries before the code is thrown away
    const MIN_PASSWORD_LENGTH = 6;      // same rule as the profile Change Password form
    const STORE_KEY = 'passwordResetRequest';
    const LOGIN_URL = '/Auth/Login/login';

    const stepEmail = document.getElementById('stepEmail');
    const stepCode = document.getElementById('stepCode');
    const stepReset = document.getElementById('stepReset');

    let currentEmail = '';
    let verified = false;

    function show(step) {
        [stepEmail, stepCode, stepReset].forEach(function (s) { s.hidden = (s !== step); });
    }

    function showError(id, msg) {
        const el = document.getElementById(id);
        el.textContent = msg;
        el.hidden = !msg;
    }

    function findUser(email) {
        return loginUsers.find(function (u) { return u.email.toLowerCase() === email.toLowerCase(); });
    }

    function readRequest() {
        try { return JSON.parse(sessionStorage.getItem(STORE_KEY)); } catch (err) { return null; }
    }

    function clearRequest() {
        sessionStorage.removeItem(STORE_KEY);
    }

    function newCode() {
        const buf = new Uint32Array(1);
        (window.crypto || window.msCrypto).getRandomValues(buf);
        return String(100000 + (buf[0] % 900000));
    }

    function sendCode(email) {
        currentEmail = email;
        const user = findUser(email);
        const demo = document.getElementById('demoCode');

        if (!user) {
            clearRequest();
            demo.hidden = true;
            show(stepEmail);
            showError('emailError', 'No account found with that email address.');
            return;
        }

        const code = newCode();
        sessionStorage.setItem(STORE_KEY, JSON.stringify({
            email: user.email,
            code: code,
            expires: Date.now() + CODE_TTL_MS,
            attempts: 0
        }));
        if (SHOW_DEMO_CODE) {
            document.getElementById('demoCodeValue').textContent = code;
            demo.hidden = false;
        }

        document.getElementById('codeDesc').textContent =
            'A 6-digit code has been sent to ' + email + '. The code expires in 10 minutes.';
        document.getElementById('fpCode').value = '';
        showError('codeError', '');
        show(stepCode);
        document.getElementById('fpCode').focus();
    }

    stepEmail.addEventListener('submit', function (e) {
        e.preventDefault();
        const email = document.getElementById('fpEmail').value.trim();

        if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
            showError('emailError', 'Please enter a valid email address.');
            return;
        }
        showError('emailError', '');
        sendCode(email);
    });

    stepCode.addEventListener('submit', function (e) {
        e.preventDefault();
        const entered = document.getElementById('fpCode').value.trim();
        const req = readRequest();

        if (!req) {
            showError('codeError', 'That code is not valid. Request a new one.');
            return;
        }
        if (Date.now() > req.expires) {
            clearRequest();
            showError('codeError', 'That code has expired. Request a new one.');
            return;
        }
        if (entered !== req.code) {
            req.attempts += 1;
            if (req.attempts >= MAX_ATTEMPTS) {
                clearRequest();
                showError('codeError', 'Too many incorrect attempts. Request a new code.');
            } else {
                sessionStorage.setItem(STORE_KEY, JSON.stringify(req));
                showError('codeError', 'Incorrect code. ' + (MAX_ATTEMPTS - req.attempts) + ' attempt(s) left.');
            }
            return;
        }

        verified = true;
        showError('codeError', '');
        show(stepReset);
        document.getElementById('fpNewPass').focus();
    });

    document.getElementById('resendCode').addEventListener('click', function (e) {
        e.preventDefault();
        sendCode(currentEmail);
    });

    document.getElementById('changeEmail').addEventListener('click', function (e) {
        e.preventDefault();
        clearRequest();
        verified = false;
        show(stepEmail);
        document.getElementById('fpEmail').focus();
    });

    function saveNewPassword(email, newPass) {
        let overrides = {};
        try { overrides = JSON.parse(localStorage.getItem('passwordOverrides') || '{}'); } catch (err) { overrides = {}; }
        overrides[email] = newPass;
        localStorage.setItem('passwordOverrides', JSON.stringify(overrides));
    }

    stepReset.addEventListener('submit', function (e) {
        e.preventDefault();

        const req = readRequest();
        if (!verified || !req) {
            show(stepEmail);
            showError('emailError', 'Your session expired. Please start again.');
            return;
        }

        const newPass = document.getElementById('fpNewPass').value;
        const confirmPass = document.getElementById('fpConfirmPass').value;

        if (newPass.length < MIN_PASSWORD_LENGTH) {
            showError('resetError', 'New password must be at least ' + MIN_PASSWORD_LENGTH + ' characters long.');
            return;
        }
        if (newPass !== confirmPass) {
            showError('resetError', 'New password and confirmation do not match.');
            return;
        }

        saveNewPassword(req.email, newPass);
        clearRequest();
        alert('Password successfully reset. You can now log in with your new password.');
        window.location.href = LOGIN_URL;
    });

    document.querySelectorAll('.toggle-password').forEach(function (icon) {
        icon.addEventListener('click', function () {
            const input = icon.parentElement.querySelector('input');
            const hidden = input.type === 'password';
            input.type = hidden ? 'text' : 'password';
            icon.classList.toggle('fa-eye', !hidden);
            icon.classList.toggle('fa-eye-slash', hidden);
        });
    });
})();