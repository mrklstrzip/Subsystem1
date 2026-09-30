document.getElementById('adminLoginForm').addEventListener('submit', function (e) {
    e.preventDefault();

    const email = document.getElementById('Email').value.trim();
    const password = document.getElementById('Password').value;

    if (email === adminLoginUser.email && password === adminLoginUser.password) {
        localStorage.setItem('loggedInUser', JSON.stringify(adminLoginUser));
        window.location.href = '/Admin/Dashboard/dashboard';
    } else {
        alert("Invalid admin email or password.");
    }
});