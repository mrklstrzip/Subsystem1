document.getElementById('loginForm').addEventListener('submit', function (e) {
    e.preventDefault();

    const email = document.getElementById('Email').value.trim();
    const password = document.getElementById('Password').value;

    const match = loginUsers.find(u => u.email === email && u.password === password);

    if (!match) {
        alert("Invalid email or password.");
        return;
    }

    localStorage.setItem('loggedInUser', JSON.stringify(match));

    switch (match.role) {
        case "Campus Director":
            window.location.href = '/CampusDirector/Dashboard/dashboard';
            break;
        case "Capstone Coordinator":
            window.location.href = '/CapstoneCoordinator/Dashboard/dashboard';
            break;
        case "Professor":
            window.location.href = '/Professor/Dashboard/dashboard';
            break;
        case "Student":
            window.location.href = '/Student/Dashboard/dashboard';
            break;
        default:
            alert("Unknown role.");
    }
});