(function () {
    const MIN_PASSWORD_LENGTH = 8;      // same rule as the Admin Change Password form
    const LOGIN_URL = '/Auth/Login/login';
    const API = '/Auth/ForgotPassword/';

    const stepEmail = document.getElementById('stepEmail');
    const stepCode = document.getElementById('stepCode');
    const stepReset = document.getElementById('stepReset');

    let currentEmail = '';
    let verifiedCode = '';   // the code the server accepted; sent again with the new password
    let busy = false;

    function show(step) {
        [stepEmail, stepCode, stepReset].forEach(function (s) { s.hidden = (s !== step); });
    }

    function showError(id, msg) {
        const el = document.getElementById(id);
        el.textContent = msg;
        el.hidden = !msg;
    }

    function post(action, data) {
        return fetch(API + action, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
            body: new URLSearchParams(data).toString()
        }).then(function (r) { return r.json(); })
          .catch(function () { return { success: false, message: 'Could not reach the server. Please try again.' }; });
    }

    function setBusy(form, on, label) {
        busy = on;
        const btn = form.querySelector('.auth-btn');
        if (!btn) return;
        if (on) { btn.dataset.label = btn.textContent; btn.textContent = label; }
        else if (btn.dataset.label) btn.textContent = btn.dataset.label;
        btn.disabled = on;
    }

    // Asks the server to e-mail a code. errorId = where to show a problem.
    function sendCode(email, fromForm, errorId) {
        if (busy) return;
        setBusy(fromForm, true, 'Sending...');
        showError(errorId, '');
        post('requestCode', { email: email }).then(function (res) {
            setBusy(fromForm, false);
            if (!res.success) {
                showError(errorId, res.message || 'Could not send the code.');
                return;
            }
            currentEmail = email;
            verifiedCode = '';
            document.getElementById('codeDesc').textContent =
                'A 6-digit code has been sent to ' + email + '. The code expires in 10 minutes.';
            document.getElementById('fpCode').value = '';
            showError('codeError', '');
            show(stepCode);
            document.getElementById('fpCode').focus();
        });
    }

    stepEmail.addEventListener('submit', function (e) {
        e.preventDefault();
        const email = document.getElementById('fpEmail').value.trim();

        if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
            showError('emailError', 'Please enter a valid email address.');
            return;
        }
        sendCode(email, stepEmail, 'emailError');
    });

    stepCode.addEventListener('submit', function (e) {
        e.preventDefault();
        if (busy) return;
        const entered = document.getElementById('fpCode').value.trim();
        if (!/^\d{6}$/.test(entered)) {
            showError('codeError', 'Enter the 6-digit code.');
            return;
        }

        setBusy(stepCode, true, 'Checking...');
        post('verifyCode', { email: currentEmail, code: entered }).then(function (res) {
            setBusy(stepCode, false);
            if (!res.success) {
                showError('codeError', res.message || 'That code is not valid.');
                return;
            }
            verifiedCode = entered;
            showError('codeError', '');
            show(stepReset);
            document.getElementById('fpNewPass').focus();
        });
    });

    document.getElementById('resendCode').addEventListener('click', function (e) {
        e.preventDefault();
        sendCode(currentEmail, stepCode, 'codeError');
    });

    document.getElementById('changeEmail').addEventListener('click', function (e) {
        e.preventDefault();
        verifiedCode = '';
        show(stepEmail);
        document.getElementById('fpEmail').focus();
    });

    stepReset.addEventListener('submit', function (e) {
        e.preventDefault();
        if (busy) return;

        if (!verifiedCode) {
            show(stepEmail);
            showError('emailError', 'Your session expired. Please start again.');
            return;
        }

        const newPass = document.getElementById('fpNewPass').value;
        const confirmPass = document.getElementById('fpConfirmPass').value;

        if (newPass.length < MIN_PASSWORD_LENGTH) {
            var lengthMessage = 'New password must be at least ' + MIN_PASSWORD_LENGTH + ' characters long.';
            showError('resetError', lengthMessage);
            if (window.Notice) Notice.error('Password not changed', lengthMessage);
            return;
        }
        if (newPass !== confirmPass) {
            var mismatchMessage = 'New password and confirmation do not match.';
            showError('resetError', mismatchMessage);
            if (window.Notice) Notice.error('Password not changed', mismatchMessage);
            return;
        }
        showError('resetError', '');

        setBusy(stepReset, true, 'Saving...');
        post('resetPassword', { email: currentEmail, code: verifiedCode, newPassword: newPass }).then(function (res) {
            setBusy(stepReset, false);
            if (!res.success) {
                var errorMessage = res.message || 'Could not reset the password.';
                showError('resetError', errorMessage);
                if (window.Notice) Notice.error('Password not changed', errorMessage);
                return;
            }
            if (window.Notice) Notice.success('Password changed', 'Your password has been reset successfully.', function () { window.location.href = LOGIN_URL; });
            else { alert('Password successfully reset. You can now log in with your new password.'); window.location.href = LOGIN_URL; }
        });
    });

})();
