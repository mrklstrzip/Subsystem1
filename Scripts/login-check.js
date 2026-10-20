// where each role lands after signing in
const ROLE_HOME = {
    "Dean": '/Dean/Dashboard/dashboard',
    "Campus Director": '/CampusDirector/Dashboard/dashboard',
    "Capstone Coordinator": '/CapstoneCoordinator/Dashboard/dashboard',
    "Professor": '/Professor/Dashboard/dashboard',
    "Panelist": '/Professor/Dashboard/dashboard',            // same as Professor
    "Adviser": '/Adviser/Dashboard/dashboard',
    "Student": '/Student/Dashboard/dashboard',
    "Project Manager": '/Student/Dashboard/dashboard'        // same as Student
};

document.getElementById('loginForm').addEventListener('submit', function (e) {
    e.preventDefault();

    const email = document.getElementById('Email').value.trim();
    const password = document.getElementById('Password').value;

    let overrides = {};
    try { overrides = JSON.parse(localStorage.getItem('passwordOverrides') || '{}'); } catch (err) { overrides = {}; }

    const match = loginUsers.find(u => u.email.toLowerCase() === email.toLowerCase() && (overrides[u.email] || u.password) === password);

    if (!match) {
        alert("Invalid email or password.");
        return;
    }

    const home = ROLE_HOME[match.role];
    if (!home) {
        alert("Unknown role.");
        return;
    }

    localStorage.setItem('loggedInUser', JSON.stringify(match));
    window.location.href = home;
});