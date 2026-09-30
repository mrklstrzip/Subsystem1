//para mapunta sa adminlogin

document.addEventListener('keydown', function (e) {
    if (e.ctrlKey && e.shiftKey && e.key === 'A') {
        window.location.href = '/Auth/AdminLogin/adminlogin';
    }
});