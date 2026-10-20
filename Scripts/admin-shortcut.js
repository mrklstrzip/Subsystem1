//para mapunta sa adminlogin (and pabalik sa login)
// Ctrl + Shift + A   (or Alt + Shift + A if the browser keeps Ctrl + Shift + A for itself)
//   - on the regular login / request pages -> goes to the admin login
//   - on the admin login page              -> goes back to the regular login

(function () {
    var LOGIN_URL = '/Auth/Login/login';
    var ADMIN_LOGIN_URL = '/Auth/AdminLogin/adminlogin';

    document.addEventListener('keydown', function (e) {
        var isA = e.code === 'KeyA' || (e.key && e.key.toLowerCase() === 'a');
        var combo = isA && e.shiftKey && (e.ctrlKey || e.altKey);
        if (!combo) return;

        e.preventDefault();
        var onAdminLogin = window.location.pathname.toLowerCase().indexOf('/adminlogin') > -1;
        window.location.href = onAdminLogin ? LOGIN_URL : ADMIN_LOGIN_URL;
    }, true);   // capture phase, so nothing else on the page can swallow the key press
})();