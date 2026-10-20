/* ==========================================================================
   Admin – demo behavior (no backend)
   --------------------------------------------------------------------------
   Powers four pages: Dashboard, Expertise Tag Management, Faculty Profile
   and User Management. All data lives in the browser (localStorage), so a
   change on one page shows up on the others (e.g. creating a student updates
   the dashboard counts, a new expertise tag appears in the faculty and user
   tag pickers). To wipe the demo data and start over, run AD.reset() in the
   browser console.
   ========================================================================== */
(function () {
    'use strict';

    if (window.AD) return;   // safe if the script ends up included twice

    var KEY = 'adminDemoData_v3';   // v3: a student's professor is now stored as the professor's account id

    // role value -> label + CSS class (from usermanagement.css)
    var ROLES = {
        'admin': { label: 'Admin', cls: 'adm-rl' },
        'campus-director': { label: 'Campus Director', cls: 'campdirect-rl' },
        'capstone-coordinator': { label: 'Capstone Coordinator', cls: 'capscoor-rl' },
        'professor': { label: 'Professor', cls: 'prof-rl' },
        'student': { label: 'Student', cls: 'stud-rl' }
    };

    // Dashboard "Role Distribution" rows (admin accounts are not counted on the dashboard)
    var DIST = [
        { role: 'student', label: 'Student' },
        { role: 'professor', label: 'Professor/Panelist' },
        { role: 'capstone-coordinator', label: 'Capstone Coordinator' },
        { role: 'campus-director', label: 'Campus Director' }
    ];

    // only one account is allowed for each of these roles
    var SINGLE_ROLES = ['admin', 'campus-director', 'capstone-coordinator'];

    var state;

    /* ------------------------------------------------------------------ *
     *  Data
     * ------------------------------------------------------------------ */
    function seed() {
        var today = fmtMDY(todayISO());

        function user(id, last, first, middle, email, role, status, extra) {
            var u = {
                id: id, lastName: last, firstName: first, middleName: middle, email: email,
                role: role, status: status, idNumber: '',
                program: '', section: '', professor: '', department: '', expertise: []
            };
            for (var k in extra) u[k] = extra[k];
            return u;
        }

        return {
            nextId: 100,
            tags: [
                { id: 't1', name: 'Networking', description: 'Computer networks, protocols, routing and network administration.', status: 'active' },
                { id: 't2', name: 'Machine Learning', description: 'Training models that learn patterns from data.', status: 'active' },
                { id: 't3', name: 'Database', description: 'Database design, SQL, and data management systems.', status: 'active' },
                { id: 't4', name: 'Artificial Intelligence', description: 'Intelligent systems, reasoning, NLP and computer vision.', status: 'active' },
                { id: 't5', name: 'App Development', description: 'Mobile and desktop application development.', status: 'inactive' },
                { id: 't6', name: 'Cyber Security', description: 'Protecting systems and data from attacks.', status: 'active' },
                { id: 't7', name: 'Web Development', description: 'Front-end and back-end web applications.', status: 'active' },
                { id: 't8', name: 'Cloud Computing', description: 'Cloud platforms, deployment and virtualization.', status: 'active' },
                { id: 't9', name: 'Data Science', description: 'Data analysis, statistics and visualization.', status: 'active' },
                { id: 't10', name: 'Renewable Energy', description: 'Solar, wind and other sustainable energy systems.', status: 'active' },
                { id: 't11', name: 'Power Systems', description: 'Electrical power generation, transmission and distribution.', status: 'active' }
            ],
            users: [
                user('u1', 'Dela Cruz', 'Juan', '', 'admin@qcu.edu.ph', 'admin', 'active', {}),
                user('u2', 'Chua', 'Justin', 'Karl', 'campusdirector@qcu.edu.ph', 'campus-director', 'active', { idNumber: 'CD-12-3456' }),
                user('u3', 'Dimagiba', 'Nanami', '', 'capstonecoordinator@qcu.edu.ph', 'capstone-coordinator', 'active', { idNumber: 'CC-12-3456' }),
                user('u5', 'George', 'Regina', '', 'professor@qcu.edu.ph', 'professor', 'active',
                    { idNumber: 'PROF-12-3456', department: 'Computer Studies', expertise: ['Database', 'Machine Learning', 'Networking'] }),
                user('u6', 'Heron', 'Cady', '', 'cady.heron@qcu.edu.ph', 'professor', 'active',
                    { idNumber: 'PROF-15-7788', department: 'Computer Studies', expertise: ['Artificial Intelligence', 'Data Science'] }),
                user('u7', 'Ian', 'Janice', '', 'janice.ian@qcu.edu.ph', 'professor', 'active',
                    { idNumber: 'PROF-11-2043', department: 'Engineering', expertise: ['Renewable Energy', 'Power Systems'] }),
                user('u8', 'Amor', 'Sam', '', 'sam.amor@qcu.edu.ph', 'professor', 'active',
                    { idNumber: 'PROF-10-1180', department: 'Computer Studies', expertise: ['Cyber Security', 'Cloud Computing'] }),
                user('u9', 'Wieners', 'Gretchen', '', 'gretchen.wieners@qcu.edu.ph', 'professor', 'active',
                    { idNumber: 'PROF-16-5521', department: 'Computer Studies', expertise: ['Web Development', 'Data Science'] }),
                user('u10', 'Castro', 'Clove', '', 'student@qcu.edu.ph', 'student', 'inactive',
                    { idNumber: '21-0421', program: 'BSIT', section: 'SBIT-4A', professor: 'u5' }),
                user('u11', 'Richards', 'Alden', '', 'alden.richards@qcu.edu.ph', 'student', 'active',
                    { idNumber: '22-1010', program: 'BSCS', section: 'SBCS-4A', professor: 'u6' }),
                user('u12', 'Sequitin', 'Sean', '', 'sean.sequitin@qcu.edu.ph', 'student', 'active',
                    { idNumber: '22-0873', program: 'BSIS', section: 'SBIS-4B', professor: 'u8' }),
                user('u13', 'Alcantara', 'Julian', '', 'julian.alcantara@qcu.edu.ph', 'student', 'active',
                    { idNumber: '21-0954', program: 'BSIT', section: 'SBIT-4A', professor: 'u5' }),
                user('u14', 'Lopez', 'Bianca', '', 'bianca.lopez@qcu.edu.ph', 'student', 'active',
                    { idNumber: '23-0112', program: 'BSCS', section: 'SBCS-4B', professor: 'u9' })
            ],
            activity: [
                { ts: today, text: 'New student account created: Alden Richards' },
                { ts: today, text: 'Faculty profile updated: Sam Amor' },
                { ts: today, text: 'Account deactivated: Clove Castro (Student)' },
                { ts: today, text: 'New Expertise Tag created: Networking' },
                { ts: today, text: 'Expertise Tag modified: Artificial Intelligence' },
                { ts: today, text: 'New student account created: Sean Sequitin' }
            ]
        };
    }

    function save() {
        try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { /* storage unavailable – keep in memory */ }
    }

    function load() {
        try {
            var raw = localStorage.getItem(KEY);
            if (raw) {
                var parsed = JSON.parse(raw);
                if (parsed && parsed.users && parsed.tags && parsed.activity) {
                    state = parsed;
                    return;
                }
            }
        } catch (e) { /* fall through to seed */ }
        state = seed();
        save();
    }

    function uid(prefix) {
        var id = prefix + state.nextId;
        state.nextId += 1;
        return id;
    }

    /* ------------------------------------------------------------------ *
     *  Helpers
     * ------------------------------------------------------------------ */
    function $(id) { return document.getElementById(id); }
    function pad(n) { return (n < 10 ? '0' : '') + n; }

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function norm(s) { return String(s || '').trim().toLowerCase(); }

    function todayISO() {
        var d = new Date();
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
    }

    function fmtMDY(iso) {                       // 2026-10-25 -> 10-25-2026
        var p = iso.split('-');
        return p[1] + '-' + p[2] + '-' + p[0];
    }

    function userById(id) { return state.users.filter(function (u) { return u.id === id; })[0]; }
    function activeTagNames() {
        return state.tags.filter(function (t) { return t.status === 'active'; }).map(function (t) { return t.name; });
    }

    function tagById(id) { return state.tags.filter(function (t) { return t.id === id; })[0]; }

    function listName(u) {                       // Dela Cruz, Juan Santos
        return u.lastName + ', ' + u.firstName + (u.middleName ? ' ' + u.middleName : '');
    }

    function plainName(u) { return u.firstName + ' ' + u.lastName; }   // Juan Dela Cruz

    function roleLabel(role) { return (ROLES[role] || { label: role }).label; }

    function logActivity(text) {
        state.activity.unshift({ ts: fmtMDY(todayISO()), text: text });
        if (state.activity.length > 50) state.activity.length = 50;
    }

    function statusBadge(status) {
        return status === 'active'
            ? '<span class="act" data-status="active">Active</span>'
            : '<span class="inact" data-status="inactive">Inactive</span>';
    }

    /* ------------------------------------------------------------------ *
     *  Modals (open / close / Esc / click outside)
     * ------------------------------------------------------------------ */
    function openModal(id) { var el = $(id); if (el) el.style.display = 'flex'; }
    function closeModal(id) { var el = $(id); if (el) el.style.display = 'none'; }

    function closeModals() {
        var all = document.querySelectorAll('.modal-overlay');
        for (var i = 0; i < all.length; i++) all[i].style.display = 'none';
    }

    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeModals(); });
    document.addEventListener('click', function (e) {
        if (e.target && e.target.classList && e.target.classList.contains('modal-overlay')) e.target.style.display = 'none';
    });

    /* ------------------------------------------------------------------ *
     *  Tag picker (shared by Faculty Profile and User Management modals)
     * ------------------------------------------------------------------ */
    function renderPicker(boxId, gridId, selected, onToggle) {
        var box = $(boxId), grid = $(gridId);
        if (!box || !grid) return;

        box.innerHTML = selected.map(function (n) {
            return '<span class="modal-tag" data-tag="' + esc(n) + '">' + esc(n) + ' <i class="fa fa-times"></i></span>';
        }).join('');

        // only active tags can be chosen
        var names = activeTagNames();

        grid.innerHTML = names.map(function (n) {
            return '<div class="grid-tag' + (selected.indexOf(n) > -1 ? ' selected' : '') + '" data-tag="' + esc(n) + '">' + esc(n) + '</div>';
        }).join('');

        var handler = function (e) {
            var el = e.target.closest('[data-tag]');
            if (el) onToggle(el.getAttribute('data-tag'));
            e.stopPropagation();
        };
        box.onclick = handler;
        grid.onclick = handler;
    }

    function toggleIn(list, name) {
        var i = list.indexOf(name);
        if (i > -1) list.splice(i, 1); else list.push(name);
    }

    /* ------------------------------------------------------------------ *
     *  Dashboard
     * ------------------------------------------------------------------ */
    function renderDashboard() {
        if (!$('dash-total')) return;

        var people = state.users.filter(function (u) { return u.role !== 'admin'; });
        function count(role, status) {
            return people.filter(function (u) { return u.role === role && (!status || u.status === status); }).length;
        }

        $('dash-total').textContent = people.length;
        $('dash-students').textContent = count('student');
        $('dash-professors').textContent = count('professor');
        $('dash-coordinators').textContent = count('capstone-coordinator');
        $('dash-directors').textContent = count('campus-director');

        $('dash-activity').innerHTML = state.activity.slice(0, 6).map(function (a) {
            return '<tr><td class="actdate">' + esc(a.ts) + '</td><td class="actname bold">' + esc(a.text) + '</td></tr>';
        }).join('') || '<tr><td colspan="2" class="actdate">No recent activity</td></tr>';

        $('dash-roles').innerHTML = DIST.map(function (d) {
            return '<tr>' +
                '<td class="role-name bold">' + esc(d.label) + '</td>' +
                '<td class="total-num bold">' + count(d.role) + '</td>' +
                '<td class="active-num bold">' + count(d.role, 'active') + '</td>' +
                '<td class="inactive-num bold">' + count(d.role, 'inactive') + '</td>' +
                '</tr>';
        }).join('');
    }

    /* ------------------------------------------------------------------ *
     *  Expertise Tag Management
     * ------------------------------------------------------------------ */
    var editTagId = null;

    function renderTags() {
        var body = $('tags-tbody');
        if (!body) return;

        var q = norm($('searchInput').value);
        var st = $('statusFilter').value;

        var list = state.tags.filter(function (t) {
            var matchesSearch = !q || norm(t.name + ' ' + t.description).indexOf(q) > -1;
            return matchesSearch && (st === 'all' || t.status === st);
        });

        body.innerHTML = list.map(function (t) {
            return '<tr>' +
                '<td class="expertise bold">' + esc(t.name) + '</td>' +
                '<td class="description bold">' + esc(t.description) + '</td>' +
                '<td class="status bold">' + statusBadge(t.status) + '</td>' +
                '<td class="action bold">' +
                '<i class="fa fa-edit" onclick="AD.openEditTag(\'' + t.id + '\')"></i>' +
                '<i class="fa fa-trash" onclick="AD.deleteTag(\'' + t.id + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="4" style="text-align:center; color:#9092AD;">No expertise tags found</td></tr>';
    }

    function initTags() {
        if (!$('tags-tbody')) return;
        $('searchInput').addEventListener('input', renderTags);
        $('statusFilter').addEventListener('change', renderTags);
        renderTags();
    }

    function tagNameTaken(name, ignoreId) {
        return state.tags.some(function (t) { return t.id !== ignoreId && norm(t.name) === norm(name); });
    }

    function openAddTag() {
        $('addTagName').value = '';
        $('addTagDesc').value = '';
        openModal('addModal');
    }

    function saveNewTag() {
        var name = $('addTagName').value.trim();
        var desc = $('addTagDesc').value.trim();
        if (!name || !desc) return;
        if (tagNameTaken(name, null)) { alert('An expertise tag with that name already exists.'); return; }

        state.tags.push({ id: uid('t'), name: name, description: desc, status: 'active' });
        logActivity('New Expertise Tag created: ' + name);
        save();
        closeModal('addModal');
        renderTags();
    }

    function openEditTag(id) {
        var t = tagById(id);
        if (!t) return;
        editTagId = id;
        $('editTagName').value = t.name;
        $('editTagDesc').value = t.description;
        $('statusActive').checked = (t.status === 'active');
        $('statusInactive').checked = (t.status !== 'active');
        openModal('editModal');
    }

    function saveEditedTag() {
        var t = tagById(editTagId);
        if (!t) { closeModal('editModal'); return; }
        var name = $('editTagName').value.trim();
        var desc = $('editTagDesc').value.trim();
        if (!name || !desc) return;
        if (tagNameTaken(name, t.id)) { alert('An expertise tag with that name already exists.'); return; }

        var oldName = t.name;
        t.name = name;
        t.description = desc;
        t.status = $('statusActive').checked ? 'active' : 'inactive';

        // keep faculty expertise in step with a renamed tag
        if (oldName !== name) {
            state.users.forEach(function (u) {
                u.expertise = u.expertise.map(function (n) { return n === oldName ? name : n; });
            });
        }

        // an inactive tag can no longer be chosen, so take it off every professor
        if (t.status === 'inactive') {
            state.users.forEach(function (u) {
                u.expertise = u.expertise.filter(function (n) { return n !== name; });
            });
        }

        logActivity('Expertise Tag modified: ' + name);
        save();
        closeModal('editModal');
        editTagId = null;
        renderTags();
    }

    var delTagId = null;

    function deleteTag(id) {
        if (!tagById(id)) return;
        delTagId = id;
        openModal('delTagModal');
    }

    function confirmDelTag() {
        var t = tagById(delTagId);
        if (t) {
            state.tags = state.tags.filter(function (x) { return x.id !== t.id; });
            state.users.forEach(function (u) {
                u.expertise = u.expertise.filter(function (n) { return n !== t.name; });
            });
            logActivity('Expertise Tag deleted: ' + t.name);
            save();
        }
        delTagId = null;
        closeModal('delTagModal');
        renderTags();
    }

    /* ------------------------------------------------------------------ *
     *  Faculty Profile
     * ------------------------------------------------------------------ */
    var facultyEditId = null, facultySelected = [];

    function renderFaculty() {
        var body = $('faculty-tbody');
        if (!body) return;

        var q = norm($('searchInput').value);

        var list = state.users.filter(function (u) {
            return u.role === 'professor' &&
                (!q || norm(plainName(u) + ' ' + listName(u) + ' ' + u.expertise.join(' ')).indexOf(q) > -1);
        });

        body.innerHTML = list.map(function (u) {
            var tags = u.expertise.map(function (n) { return '<span class="tag">' + esc(n) + '</span>'; }).join('') ||
                '<span style="color:#9092AD; font-size:12px;">No expertise tags yet</span>';
            return '<tr>' +
                '<td class="name bold">' + esc(plainName(u)) + '</td>' +
                '<td class="expertise">' + tags + '</td>' +
                '<td class="action bold"><i class="fa fa-edit" onclick="AD.openEditFaculty(\'' + u.id + '\')"></i></td>' +
                '</tr>';
        }).join('') || '<tr><td colspan="3" style="text-align:center; color:#9092AD;">No faculty found</td></tr>';
    }

    function renderFacultyPicker() {
        renderPicker('selectedTagsBox', 'systemTagsGrid', facultySelected, function (name) {
            toggleIn(facultySelected, name);
            renderFacultyPicker();
        });
    }

    function initFaculty() {
        if (!$('faculty-tbody')) return;
        $('searchInput').addEventListener('input', renderFaculty);
        renderFaculty();
    }

    function openEditFaculty(id) {
        var u = userById(id);
        if (!u) return;
        facultyEditId = id;
        var active = activeTagNames();
        facultySelected = u.expertise.filter(function (n) { return active.indexOf(n) > -1; });
        $('facultyName').value = plainName(u);
        renderFacultyPicker();
        openModal('editFacultyModal');
    }

    function saveExpertise() {
        var u = userById(facultyEditId);
        if (u) {
            u.expertise = facultySelected.slice();
            logActivity('Faculty profile updated: ' + plainName(u));
            save();
        }
        facultyEditId = null;
        closeModal('editFacultyModal');
        renderFaculty();
    }

    /* ------------------------------------------------------------------ *
     *  User Management
     * ------------------------------------------------------------------ */
    var isEditMode = false, editUserId = null, currentPrefix = '', userSelected = [];

    function renderUsers() {
        var body = $('users-tbody');
        if (!body) return;

        var q = norm($('searchInput').value);
        var role = $('roleFilter').value;
        var st = $('statusFilter').value;

        var list = state.users.filter(function (u) {
            var matchesSearch = !q || norm(listName(u) + ' ' + u.email).indexOf(q) > -1;
            return matchesSearch && (role === 'all' || u.role === role) && (st === 'all' || u.status === st);
        });

        body.innerHTML = list.map(function (u) {
            var r = ROLES[u.role] || { label: u.role, cls: '' };
            return '<tr>' +
                '<td class="name bold">' + esc(listName(u)) + '</td>' +
                '<td class="email bold">' + esc(u.email) + '</td>' +
                '<td class="role bold"><span class="' + r.cls + '" data-role="' + esc(u.role) + '">' + esc(r.label) + '</span></td>' +
                '<td class="status bold">' + statusBadge(u.status) + '</td>' +
                '<td class="action bold">' +
                '<i class="fa fa-edit" onclick="AD.editUser(\'' + u.id + '\')"></i>' +
                '<i class="fa fa-trash" onclick="AD.deleteUser(\'' + u.id + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="5" style="text-align:center; color:#9092AD;">No users found</td></tr>';
    }

    // ID mask: role prefix + NN-NNNN
    function formatDigits(digits) {
        var cleaned = String(digits).replace(/\D/g, '').slice(0, 6);
        return cleaned.length > 2 ? cleaned.slice(0, 2) + '-' + cleaned.slice(2) : cleaned;
    }

    function updateIdPrefix(role, existingDigits) {
        var idInput = $('modalIdInput');
        if (role === 'campus-director') currentPrefix = 'CD-';
        else if (role === 'capstone-coordinator') currentPrefix = 'CC-';
        else if (role === 'professor') currentPrefix = 'PROF-';
        else currentPrefix = '';
        idInput.placeholder = currentPrefix + '12-3456';
        idInput.value = currentPrefix + formatDigits(existingDigits || '');
    }

    function renderUserPicker() {
        renderPicker('selectedTagsBox', 'systemTagsGrid', userSelected, function (name) {
            toggleIn(userSelected, name);
            renderUserPicker();
        });
    }

    function handleRoleUIChange(role) {
        $('fieldProgram').style.display = 'none';
        $('fieldProfessor').style.display = 'none';

        if (role === 'student') {
            $('fieldProgram').style.display = 'flex';
        } else if (role === 'professor') {
            $('fieldProfessor').style.display = 'flex';
            // expertise is picked when creating; existing professors are edited on the Faculty Profile page
            $('expertiseSection').style.display = isEditMode ? 'none' : 'flex';
            if (!isEditMode) renderUserPicker();
        }
    }

    // Admin is never offered. Campus Director and Capstone Coordinator are always listed;
    // saveUser() enforces the one-account-per-role rule with a message.
    function setRoleOptions() {
        $('modalRoleSelect').innerHTML =
            '<option value="student">Student</option>' +
            '<option value="professor">Professor / Panelist</option>' +
            '<option value="campus-director">Campus Director</option>' +
            '<option value="capstone-coordinator">Capstone Coordinator</option>';
    }

    // Professor dropdown for students: every professor account that exists
    function fillProfessorSelect(selectedId) {
        var profs = state.users.filter(function (u) { return u.role === 'professor'; });
        var sel = $('professorInput');
        sel.innerHTML = profs.length
            ? '<option value=""' + (selectedId ? '' : ' selected') + ' disabled>Select Professor</option>' +
            profs.map(function (u) {
                return '<option value="' + esc(u.id) + '"' + (u.id === selectedId ? ' selected' : '') + '>' + esc(plainName(u)) + '</option>';
            }).join('')
            : '<option value="" selected disabled>No professors available</option>';
    }

    function openCreateUser() {
        isEditMode = false;
        editUserId = null;
        $('modalHeaderTitle').innerText = 'CREATE USER ACCOUNT';
        $('passwordGroup').style.display = 'flex';
        $('statusGroup').style.display = 'none';

        $('lastNameInput').value = '';
        $('firstNameInput').value = '';
        $('middleNameInput').value = '';
        $('emailInput').value = '';
        $('programSelect').selectedIndex = 0;
        $('sectionInput').value = '';
        fillProfessorSelect('');
        $('passwordInput').value = '';
        $('statusActive').checked = true;
        userSelected = [];

        setRoleOptions();
        $('modalRoleSelect').value = 'student';
        updateIdPrefix('student');
        handleRoleUIChange('student');
        openModal('userModal');
    }

    function editUser(id) {
        var u = userById(id);
        if (!u) return;
        if (u.role === 'admin') { alert('Admin accounts cannot be edited.'); return; }

        isEditMode = true;
        editUserId = id;
        $('modalHeaderTitle').innerText = 'EDIT USER ACCOUNT';
        $('passwordGroup').style.display = 'none';
        $('statusGroup').style.display = 'flex';

        $('lastNameInput').value = u.lastName;
        $('firstNameInput').value = u.firstName;
        $('middleNameInput').value = u.middleName;
        $('emailInput').value = u.email;
        $('statusActive').checked = (u.status === 'active');
        $('statusInactive').checked = (u.status !== 'active');

        setRoleOptions();
        $('modalRoleSelect').value = u.role;
        updateIdPrefix(u.role, u.idNumber);

        $('programSelect').value = u.program || '';
        $('sectionInput').value = u.section || '';
        fillProfessorSelect(u.professor || '');
        var activeNames = activeTagNames();
        userSelected = u.expertise.filter(function (n) { return activeNames.indexOf(n) > -1; });

        handleRoleUIChange(u.role);
        openModal('userModal');
    }

    function saveUser() {
        var lastName = $('lastNameInput').value.trim();
        var firstName = $('firstNameInput').value.trim();
        var middleName = $('middleNameInput').value.trim();
        var email = $('emailInput').value.trim();
        var role = $('modalRoleSelect').value;

        if (!lastName || !firstName || !email) { alert('Please fill in all required fields (Name, Email).'); return; }

        var emailTaken = state.users.some(function (o) { return o.id !== editUserId && norm(o.email) === norm(email); });
        if (emailTaken) { alert('That email is already used by another account.'); return; }

        if (SINGLE_ROLES.indexOf(role) > -1) {
            var roleTaken = state.users.some(function (o) { return o.id !== editUserId && o.role === role; });
            if (roleTaken) { alert('There is already a ' + roleLabel(role) + ' account. Only one is allowed, so edit or delete the existing one first.'); return; }
        }

        var idValue = $('modalIdInput').value;
        if (idValue === currentPrefix) idValue = '';

        var isStudent = role === 'student';
        var fields = {
            lastName: lastName, firstName: firstName, middleName: middleName, email: email, role: role,
            idNumber: idValue,
            program: isStudent ? ($('programSelect').value || '') : '',
            section: isStudent ? $('sectionInput').value.trim() : '',
            professor: isStudent ? ($('professorInput').value || '') : ''
        };

        if (isEditMode) {
            var u = userById(editUserId);
            if (!u) { closeModal('userModal'); return; }

            var oldStatus = u.status;
            var wasProfessor = u.role === 'professor';
            for (var k in fields) u[k] = fields[k];
            if (wasProfessor && role !== 'professor') clearProfessorFromStudents(u.id);
            u.status = $('statusActive').checked ? 'active' : 'inactive';
            // expertise is only kept for professors (edited on the Faculty Profile page)
            if (role !== 'professor') { u.expertise = []; u.department = ''; }
            else if (!wasProfessor) u.expertise = [];

            if (u.status !== oldStatus) {
                logActivity('Account ' + (u.status === 'active' ? 'activated' : 'deactivated') + ': ' + plainName(u) + ' (' + roleLabel(u.role) + ')');
            } else {
                logActivity('Account updated: ' + plainName(u) + ' (' + roleLabel(u.role) + ')');
            }
        } else {
            var nu = {
                id: uid('u'), status: 'active', department: '',
                expertise: role === 'professor' ? userSelected.slice() : []
            };
            for (var f in fields) nu[f] = fields[f];
            state.users.push(nu);
            logActivity('New ' + roleLabel(role).toLowerCase() + ' account created: ' + plainName(nu));
        }

        save();
        closeModal('userModal');
        renderUsers();
    }

    function clearProfessorFromStudents(profId) {
        state.users.forEach(function (x) { if (x.role === 'student' && x.professor === profId) x.professor = ''; });
    }

    var delUserId = null;

    function deleteUser(id) {
        var u = userById(id);
        if (!u) return;
        if (u.role === 'admin') { alert('Admin accounts cannot be deleted.'); return; }
        delUserId = id;
        openModal('delUserModal');
    }

    function confirmDelUser() {
        var u = userById(delUserId);
        if (u) {
            state.users = state.users.filter(function (x) { return x.id !== u.id; });
            if (u.role === 'professor') clearProfessorFromStudents(u.id);
            logActivity('Account deleted: ' + plainName(u) + ' (' + roleLabel(u.role) + ')');
            save();
        }
        delUserId = null;
        closeModal('delUserModal');
        renderUsers();
    }

    function initUsers() {
        if (!$('users-tbody')) return;

        $('searchInput').addEventListener('input', renderUsers);
        $('roleFilter').addEventListener('change', renderUsers);
        $('statusFilter').addEventListener('change', renderUsers);

        $('openModalBtn').addEventListener('click', openCreateUser);
        $('confirmBtn').addEventListener('click', saveUser);
        $('cancelBtn').addEventListener('click', function () { closeModal('userModal'); });

        $('modalRoleSelect').addEventListener('change', function (e) {
            updateIdPrefix(e.target.value);
            handleRoleUIChange(e.target.value);
        });

        $('modalIdInput').addEventListener('input', function () {
            var val = this.value;
            if (val.indexOf(currentPrefix) !== 0) val = currentPrefix;
            this.value = currentPrefix + formatDigits(val.substring(currentPrefix.length));
        });

        renderUsers();
    }

    /* ------------------------------------------------------------------ *
     *  Public API (used by inline onclick handlers in the views)
     * ------------------------------------------------------------------ */
    window.AD = {
        closeModals: closeModals,
        // expertise tags
        openAddTag: openAddTag, saveNewTag: saveNewTag,
        openEditTag: openEditTag, saveEditedTag: saveEditedTag, deleteTag: deleteTag, confirmDelTag: confirmDelTag,
        // faculty profile
        openEditFaculty: openEditFaculty, saveExpertise: saveExpertise,
        // users
        editUser: editUser, deleteUser: deleteUser, confirmDelUser: confirmDelUser,
        // utility
        reset: function () { state = seed(); save(); window.location.reload(); }
    };

    /* ------------------------------------------------------------------ *
     *  Boot
     * ------------------------------------------------------------------ */
    function boot() {
        load();
        renderDashboard();
        initTags();
        initFaculty();
        initUsers();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})();