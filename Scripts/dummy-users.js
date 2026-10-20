const adminUser = {
    lastName: "Dela Cruz",
    firstName: "Juan",
    middleName: "Santos",
    id: "ADM-12-3456",
    role: "Admin",
    email: "admin@qcu.edu.ph",
    profilePhoto: "admin-avatar.jpg",
    password: "admin123"
};

const deanUser = {
    lastName: "Bautista",
    firstName: "Elena",
    middleName: "Cruz",
    college: "College of Computer Studies",
    id: "DEAN-12-3456",
    role: "Dean",
    email: "dean@qcu.edu.ph",
    profilePhoto: "dean-avatar.jpg",
    password: "dean123"
};

const campusDirectorUser = {
    lastName: "Chua",
    firstName: "Justin Karl",
    middleName: "Reyes",
    id: "CD-12-3456",
    role: "Campus Director",
    email: "campusdirector@qcu.edu.ph",
    profilePhoto: "campus-avatar.jpg",
    password: "campusdirector123"
};

const capstoneCoordinatorUser = {
    lastName: "Dimagiba",
    firstName: "Nanami",
    middleName: "Mendoza",
    id: "CC-12-3456",
    role: "Capstone Coordinator",
    email: "capstonecoordinator@qcu.edu.ph",
    profilePhoto: "capstone-avatar.jpg",
    password: "capstonecoordinator123"
};

// Panelist (Professor)
const professorUser = {
    lastName: "George",
    firstName: "Regina",
    middleName: "Villanueva",
    college: "College of Computer Studies",
    id: "PROF-12-3456",
    role: "Professor",
    email: "professor@qcu.edu.ph",
    profilePhoto: "professor-avatar.jpg",
    password: "professor123"
};

const adviserUser = {
    lastName: "Heron",
    firstName: "Cady",
    middleName: "Lopez",
    college: "College of Computer Studies",
    id: "ADV-12-3456",
    role: "Adviser",
    email: "adviser@qcu.edu.ph",
    profilePhoto: "adviser-avatar.jpg",
    password: "adviser123"
};

// Project Manager (Student) - one account per program
const studentUser = {
    lastName: "Clove",
    firstName: "Castro",
    middleName: "Garcia",
    program: "BS Information Technology",
    section: "SBIT-4E",
    id: "12-3456",
    role: "Student",
    email: "student.bsit@qcu.edu.ph",
    profilePhoto: "student-avatar.png",
    password: "student123"
};

const studentBsisUser = {
    lastName: "Richards",
    firstName: "Alden",
    middleName: "Torres",
    program: "BS Information System",
    section: "SBIS-4A",
    id: "12-3457",
    role: "Student",
    email: "student.bsis@qcu.edu.ph",
    profilePhoto: "student-avatar.png",
    password: "student123"
};

const studentBscsUser = {
    lastName: "Lopez",
    firstName: "Bianca",
    middleName: "Ramos",
    program: "BS Computer Science",
    section: "SBCS-4A",
    id: "12-3458",
    role: "Student",
    email: "student.bscs@qcu.edu.ph",
    profilePhoto: "student-avatar.png",
    password: "student123"
};

// everyone who can sign in on the regular login page (the admin has its own login page)
const loginUsers = [
    deanUser,
    campusDirectorUser,
    capstoneCoordinatorUser,
    professorUser,
    adviserUser,
    studentUser,
    studentBsisUser,
    studentBscsUser
];

const adminLoginUser = adminUser;