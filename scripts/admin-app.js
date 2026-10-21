/* ==========================================================================
   Admin – demo behavior (no backend)
   --------------------------------------------------------------------------
   Powers four pages: Dashboard, Expertise Tag Management, Faculty Profile
   and User Management. All data lives in the browser (localStorage), so a
   change on one page shows up on the others. To wipe the demo data and start over, 
   run AD.reset() in the browser console.
   ========================================================================== */
(function () {
    'use strict';

    if (window.AD) return;   // safe if the script ends up included twice

    var KEY = 'adminDemoData_v5';   // v5: updated with activity badge types and icons

    // role value -> label + CSS class
    var ROLES = {
        'admin': { label: 'Admin', cls: 'adm-rl' },
        'campus-director': { label: 'Campus Director', cls: 'campdirect-rl' },
        'capstone-coordinator': { label: 'Capstone Coordinator', cls: 'capscoor-rl' },
        'panelist': { label: 'Panelist', cls: 'prof-rl' },
        'project-manager': { label: 'Student', cls: 'stud-rl' },
        'adviser': { label: 'Adviser', cls: 'adv-rl' },
        'dean': { label: 'Dean', cls: 'dean-rl' }
    };

    // Dashboard "Role Distribution" rows mapped with legend dot CSS classes
    var DIST = [
        { role: 'project-manager', label: 'Student', dotCls: 'dot-project-manager' },
        { role: 'panelist', label: 'Panelist', dotCls: 'dot-panelist' },
        { role: 'adviser', label: 'Adviser', dotCls: 'dot-adviser' },
        { role: 'campus-director', label: 'Campus Director', dotCls: 'dot-campus-director' },
        { role: 'capstone-coordinator', label: 'Capstone Coordinator', dotCls: 'dot-capstone-coordinator' },
        { role: 'dean', label: 'Dean', dotCls: 'dot-dean' }
    ];

    var SINGLE_ROLES = ['admin', 'campus-director', 'capstone-coordinator', 'dean'];

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
                user('u2', 'Amor', 'Samantha', '', 'professor@qcu.edu.ph', 'panelist', 'active',
                    { idNumber: 'PROF-12-3456', department: 'Computer Studies', expertise: ['Database', 'Machine Learning'] }),
                user('u3', 'Alcantara', 'Julian', '', 'campusdirect@qcu.edu.ph', 'campus-director', 'active', { idNumber: 'CD-12-3456' }),
                user('u4', 'Castaneda', 'Mark', '', 'student@qcu.edu.ph', 'project-manager', 'inactive',
                    { idNumber: '21-0421', program: 'BSIT', section: 'SBIT-4A', professor: 'u2' }),
                user('u5', 'Batumbakal', 'Clove', '', 'capstonecoor@qcu.edu.ph', 'capstone-coordinator', 'active', { idNumber: 'CC-12-3456' }),
                user('u6', 'Mendoza', 'Iso', '', 'dean@qcu.edu.ph', 'dean', 'active', { idNumber: 'DEAN-12-3456' }),
                user('u7', 'Villanueva', 'Chamber', '', 'adviser@qcu.edu.ph', 'adviser', 'active',
                    { idNumber: 'ADV-10-1180', department: 'Computer Studies', expertise: ['Web Development', 'Cyber Security'] })
            ],
            activity: [
                { ts: today, text: 'New student account created: Mark Castaneda', type: 'create', category: 'user' },
                { ts: today, text: 'Panelist profile updated: Samantha Amor', type: 'edit', category: 'user' },
                { ts: today, text: 'Account deactivated: Mark Castaneda (Student)', type: 'delete', category: 'user' },
                { ts: today, text: 'New Expertise Tag created: Networking', type: 'create', category: 'tag' }
            ]
        };
    }

    function save() {
        try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { /* storage unavailable */ }
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

    function fmtMDY(iso) {
        var p = iso.split('-');
        return p[1] + '-' + p[2] + '-' + p[0];
    }

    function userById(id) { return state.users.filter(function (u) { return u.id === id; })[0]; }
    function activeTagNames() {
        return state.tags.filter(function (t) { return t.status === 'active'; }).map(function (t) { return t.name; });
    }

    function tagById(id) { return state.tags.filter(function (t) { return t.id === id; })[0]; }

    function listName(u) {
        return u.lastName + ', ' + u.firstName + (u.middleName ? ' ' + u.middleName : '');
    }

    function plainName(u) { return u.firstName + ' ' + u.lastName; }

    function roleLabel(role) { return (ROLES[role] || { label: role }).label; }

    function logActivity(text, type, category) {
        state.activity.unshift({
            ts: fmtMDY(todayISO()),
            text: text,
            type: type || 'create',
            category: category || 'user'
        });
        if (state.activity.length > 50) state.activity.length = 50;
    }

    function statusBadge(status) {
        return status === 'active'
            ? '<span class="act" data-status="active">Active</span>'
            : '<span class="inact" data-status="inactive">Inactive</span>';
    }

    function getActivityIcon(a) {
        if (a.category === 'tag') {
            return '<i class="fa fa-tag"></i>';
        }
        if (a.type === 'delete') {
            return '<i class="fa fa-ban"></i>';
        }
        var t = (a.text || '').toLowerCase();
        if (t.indexOf('student') > -1) return '<i class="fa fa-graduation-cap"></i>';
        if (t.indexOf('panelist') > -1) return '<i class="fa fa-chalkboard-user"></i>';
        if (t.indexOf('adviser') > -1) return '<i class="fa fa-display"></i>';
        if (t.indexOf('campus director') > -1) return '<i class="fa fa-city"></i>';
        if (t.indexOf('capstone coordinator') > -1) return '<i class="fa fa-folder-open"></i>';
        if (t.indexOf('dean') > -1) return '<i class="fa fa-user-tie"></i>';
        return '<i class="fa fa-user"></i>';
    }

    function getActivityBadgeClass(type) {
        if (type === 'edit') return 'badge-edit';
        if (type === 'delete') return 'badge-delete';
        return 'badge-create';
    }

    /* ------------------------------------------------------------------ *
     *  Modals
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
     *  Tag picker
     * ------------------------------------------------------------------ */
    function renderPicker(boxId, gridId, selected, onToggle) {
        var box = $(boxId), grid = $(gridId);
        if (!box || !grid) return;

        box.innerHTML = selected.map(function (n) {
            return '<span class="modal-tag" data-tag="' + esc(n) + '">' + esc(n) + ' <i class="fa fa-times"></i></span>';
        }).join('');

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
     *  Custom Dropdown Menu Binder Helper
     * ------------------------------------------------------------------ */
    function bindCustomDropdowns() {
        document.querySelectorAll('.custom-select-box').forEach(function (box) {
            var toggle = box.querySelector('.select-selected');
            var menu = box.querySelector('.custom-select-dropdown');
            var hiddenSelect = box.querySelector('select');
            var textSpan = box.querySelector('.select-selected span');
            var iconElem = toggle ? toggle.querySelector('i:first-child') : null;

            if (toggle && menu) {
                toggle.addEventListener('click', function (e) {
                    document.querySelectorAll('.custom-select-dropdown').forEach(function (m) {
                        if (m !== menu) m.classList.remove('open');
                    });
                    menu.classList.toggle('open');
                    e.stopPropagation();
                });
            }

            if (menu && hiddenSelect) {
                menu.querySelectorAll('.select-option').forEach(function (opt) {
                    opt.addEventListener('click', function () {
                        var val = this.getAttribute('data-value');
                        hiddenSelect.value = val;
                        if (textSpan) textSpan.textContent = this.textContent.trim();

                        var optIcon = this.querySelector('i');
                        if (optIcon && iconElem) {
                            iconElem.className = optIcon.className;
                        }

                        menu.classList.remove('open');

                        var event = new Event('change', { bubbles: true });
                        hiddenSelect.dispatchEvent(event);
                    });
                });
            }
        });

        document.addEventListener('click', function () {
            document.querySelectorAll('.custom-select-dropdown').forEach(function (m) {
                m.classList.remove('open');
            });
        });
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
        if ($('dash-students')) $('dash-students').textContent = count('project-manager');
        if ($('dash-professors')) $('dash-professors').textContent = count('panelist');
        if ($('dash-advisers')) $('dash-advisers').textContent = count('adviser');
        if ($('dash-coordinators')) $('dash-coordinators').textContent = count('capstone-coordinator');
        if ($('dash-directors')) $('dash-directors').textContent = count('campus-director');
        if ($('dash-dean')) $('dash-dean').textContent = count('dean');

        $('dash-activity').innerHTML = state.activity.slice(0, 6).map(function (a) {
            return '<tr>' +
                '<td class="actdate">' +
                '<div class="activity-date-wrapper">' +
                '<span class="activity-badge ' + getActivityBadgeClass(a.type) + '">' + getActivityIcon(a) + '</span>' +
                '<div>' +
                '<span class="date-main">' + esc(a.ts) + '</span>' +
                '<span class="time-sub">10:12 AM</span>' +
                '</div>' +
                '</div>' +
                '</td>' +
                '<td class="actname bold">' + esc(a.text) + '</td>' +
                '</tr>';
        }).join('') || '<tr><td colspan="2" class="actdate">No recent activity</td></tr>';

        $('dash-roles').innerHTML = DIST.map(function (d) {
            return '<tr>' +
                '<td class="role-name bold"><span class="legend-dot ' + d.dotCls + '"></span>' + esc(d.label) + '</td>' +
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
        var st = $('statusFilter') ? $('statusFilter').value : 'all';

        var list = state.tags.filter(function (t) {
            var matchesSearch = !q || norm(t.name + ' ' + t.description).indexOf(q) > -1;
            return matchesSearch && (st === 'all' || t.status === st);
        });

        body.innerHTML = list.map(function (t) {
            return '<tr>' +
                '<td class="name bold">' + esc(t.name) + '</td>' +
                '<td class="desc bold">' + esc(t.description) + '</td>' +
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
        bindCustomDropdowns();
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
        logActivity('New Expertise Tag created: ' + name, 'create', 'tag');
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

        if (oldName !== name) {
            state.users.forEach(function (u) {
                u.expertise = u.expertise.map(function (n) { return n === oldName ? name : n; });
            });
        }

        if (t.status === 'inactive') {
            state.users.forEach(function (u) {
                u.expertise = u.expertise.filter(function (n) { return n !== name; });
            });
        }

        logActivity('Expertise Tag modified: ' + name, 'edit', 'tag');
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
            logActivity('Expertise Tag deleted: ' + t.name, 'delete', 'tag');
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
            return (u.role === 'panelist' || u.role === 'adviser') &&
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
            logActivity('Faculty profile updated: ' + plainName(u), 'edit', 'user');
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
            var roleIcon = '<i class="fa fa-user"></i>';
            if (u.role === 'admin') roleIcon = '<i class="fa fa-user-gear"></i>';
            else if (u.role === 'panelist') roleIcon = '<i class="fa fa-pen-nib"></i>';
            else if (u.role === 'campus-director') roleIcon = '<i class="fa fa-building"></i>';
            else if (u.role === 'project-manager') roleIcon = '<i class="fa fa-graduation-cap"></i>';
            else if (u.role === 'capstone-coordinator') roleIcon = '<i class="fa fa-folder-open"></i>';
            else if (u.role === 'dean') roleIcon = '<i class="fa fa-user-tie"></i>';
            else if (u.role === 'adviser') roleIcon = '<i class="fa fa-chalkboard-user"></i>';

            return '<tr>' +
                '<td class="name bold">' + esc(listName(u)) + '</td>' +
                '<td class="email bold">' + esc(u.email) + '</td>' +
                '<td class="role bold"><span class="' + r.cls + '" data-role="' + esc(u.role) + '">' + roleIcon + ' ' + esc(r.label) + '</span></td>' +
                '<td class="status bold">' + statusBadge(u.status) + '</td>' +
                '<td class="action bold">' +
                '<i class="fa fa-edit" onclick="AD.editUser(\'' + u.id + '\')"></i>' +
                '<i class="fa fa-trash" onclick="AD.deleteUser(\'' + u.id + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="5" style="text-align:center; color:#9092AD;">No users found</td></tr>';
    }

    function formatDigits(digits) {
        var cleaned = String(digits).replace(/\D/g, '').slice(0, 6);
        return cleaned.length > 2 ? cleaned.slice(0, 2) + '-' + cleaned.slice(2) : cleaned;
    }

    function updateIdPrefix(role, existingDigits) {
        var idInput = $('modalIdInput');
        if (role === 'campus-director') currentPrefix = 'CD-';
        else if (role === 'capstone-coordinator') currentPrefix = 'CC-';
        else if (role === 'dean') currentPrefix = 'DEAN-';
        else if (role === 'panelist' || role === 'adviser') currentPrefix = 'PROF-';
        else currentPrefix = '';
        if (idInput) {
            idInput.placeholder = currentPrefix + '12-3456';
            idInput.value = currentPrefix + formatDigits(existingDigits || '');
        }
    }

    function renderUserPicker() {
        renderPicker('selectedTagsBox', 'systemTagsGrid', userSelected, function (name) {
            toggleIn(userSelected, name);
            renderUserPicker();
        });
    }

    function handleRoleUIChange(role) {
        if ($('fieldProgram')) $('fieldProgram').style.display = 'none';
        if ($('fieldProfessor')) $('fieldProfessor').style.display = 'none';

        if (role === 'project-manager') {
            if ($('fieldProgram')) $('fieldProgram').style.display = 'flex';
        } else if (role === 'panelist' || role === 'adviser') {
            if ($('fieldProfessor')) $('fieldProfessor').style.display = 'flex';
            if ($('expertiseSection')) $('expertiseSection').style.display = isEditMode ? 'none' : 'flex';
            if (!isEditMode) renderUserPicker();
        }
    }

    function setRoleOptions() {
        var roleSel = $('modalRoleSelect');
        if (roleSel) {
            roleSel.innerHTML =
                '<option value="project-manager">Student</option>' +
                '<option value="panelist">Panelist</option>' +
                '<option value="adviser">Adviser</option>' +
                '<option value="campus-director">Campus Director</option>' +
                '<option value="capstone-coordinator">Capstone Coordinator</option>' +
                '<option value="dean">Dean</option>';
        }
    }

    function fillProfessorSelect(selectedId) {
        var profs = state.users.filter(function (u) { return u.role === 'panelist' || u.role === 'adviser'; });
        var sel = $('professorInput');
        if (!sel) return;
        sel.innerHTML = profs.length
            ? '<option value=""' + (selectedId ? '' : ' selected') + ' disabled>Select Professor/Adviser</option>' +
            profs.map(function (u) {
                return '<option value="' + esc(u.id) + '"' + (u.id === selectedId ? ' selected' : '') + '>' + esc(plainName(u)) + '</option>';
            }).join('')
            : '<option value="" selected disabled>No professor/adviser available</option>';
    }

    function openCreateUser() {
        isEditMode = false;
        editUserId = null;
        if ($('modalHeaderTitle')) $('modalHeaderTitle').innerText = 'CREATE USER ACCOUNT';
        if ($('passwordGroup')) $('passwordGroup').style.display = 'flex';
        if ($('statusGroup')) $('statusGroup').style.display = 'none';

        if ($('lastNameInput')) $('lastNameInput').value = '';
        if ($('firstNameInput')) $('firstNameInput').value = '';
        if ($('middleNameInput')) $('middleNameInput').value = '';
        if ($('emailInput')) $('emailInput').value = '';
        if ($('programSelect')) $('programSelect').selectedIndex = 0;
        if ($('sectionInput')) $('sectionInput').value = '';
        fillProfessorSelect('');
        if ($('passwordInput')) $('passwordInput').value = '';
        if ($('statusActive')) $('statusActive').checked = true;
        userSelected = [];

        setRoleOptions();
        if ($('modalRoleSelect')) $('modalRoleSelect').value = 'project-manager';
        updateIdPrefix('project-manager');
        handleRoleUIChange('project-manager');
        openModal('userModal');
    }

    function editUser(id) {
        var u = userById(id);
        if (!u) return;
        if (u.role === 'admin') { alert('Admin accounts cannot be edited.'); return; }

        isEditMode = true;
        editUserId = id;
        if ($('modalHeaderTitle')) $('modalHeaderTitle').innerText = 'EDIT USER ACCOUNT';
        if ($('passwordGroup')) $('passwordGroup').style.display = 'none';
        if ($('statusGroup')) $('statusGroup').style.display = 'flex';

        if ($('lastNameInput')) $('lastNameInput').value = u.lastName;
        if ($('firstNameInput')) $('firstNameInput').value = u.firstName;
        if ($('middleNameInput')) $('middleNameInput').value = u.middleName;
        if ($('emailInput')) $('emailInput').value = u.email;
        if ($('statusActive')) $('statusActive').checked = (u.status === 'active');
        if ($('statusInactive')) $('statusInactive').checked = (u.status !== 'active');

        setRoleOptions();
        if ($('modalRoleSelect')) $('modalRoleSelect').value = u.role;
        updateIdPrefix(u.role, u.idNumber);

        if ($('programSelect')) $('programSelect').value = u.program || '';
        if ($('sectionInput')) $('sectionInput').value = u.section || '';
        fillProfessorSelect(u.professor || '');
        var activeNames = activeTagNames();
        userSelected = u.expertise.filter(function (n) { return activeNames.indexOf(n) > -1; });

        handleRoleUIChange(u.role);
        openModal('userModal');
    }

    function saveUser() {
        var lastName = $('lastNameInput') ? $('lastNameInput').value.trim() : '';
        var firstName = $('firstNameInput') ? $('firstNameInput').value.trim() : '';
        var middleName = $('middleNameInput') ? $('middleNameInput').value.trim() : '';
        var email = $('emailInput') ? $('emailInput').value.trim() : '';
        var role = $('modalRoleSelect') ? $('modalRoleSelect').value : 'project-manager';

        if (!lastName || !firstName || !email) { alert('Please fill in all required fields (Name, Email).'); return; }

        var emailTaken = state.users.some(function (o) { return o.id !== editUserId && norm(o.email) === norm(email); });
        if (emailTaken) { alert('That email is already used by another account.'); return; }

        if (SINGLE_ROLES.indexOf(role) > -1) {
            var roleTaken = state.users.some(function (o) { return o.id !== editUserId && o.role === role; });
            if (roleTaken) { alert('There is already a ' + roleLabel(role) + ' account. Only one is allowed, so edit or delete the existing one first.'); return; }
        }

        var idValue = $('modalIdInput') ? $('modalIdInput').value : '';
        if (idValue === currentPrefix) idValue = '';

        var isPM = role === 'project-manager';
        var fields = {
            lastName: lastName, firstName: firstName, middleName: middleName, email: email, role: role,
            idNumber: idValue,
            program: isPM ? ($('programSelect') ? $('programSelect').value || '' : '') : '',
            section: isPM ? ($('sectionInput') ? $('sectionInput').value.trim() : '') : '',
            professor: isPM ? ($('professorInput') ? $('professorInput').value || '' : '') : ''
        };

        if (isEditMode) {
            var u = userById(editUserId);
            if (!u) { closeModal('userModal'); return; }

            var oldStatus = u.status;
            var wasFaculty = (u.role === 'panelist' || u.role === 'adviser');
            for (var k in fields) u[k] = fields[k];
            var isFaculty = (role === 'panelist' || role === 'adviser');
            if (wasFaculty && !isFaculty) clearProfessorFromStudents(u.id);
            if ($('statusActive')) u.status = $('statusActive').checked ? 'active' : 'inactive';
            if (!isFaculty) { u.expertise = []; u.department = ''; }
            else if (!wasFaculty) u.expertise = [];

            if (u.status !== oldStatus) {
                var actType = u.status === 'active' ? 'create' : 'delete';
                logActivity('Account ' + (u.status === 'active' ? 'activated' : 'deactivated') + ': ' + plainName(u) + ' (' + roleLabel(u.role) + ')', actType, 'user');
            } else {
                logActivity('Account updated: ' + plainName(u) + ' (' + roleLabel(u.role) + ')', 'edit', 'user');
            }
        } else {
            var isFacultyNew = (role === 'panelist' || role === 'adviser');
            var nu = {
                id: uid('u'), status: 'active', department: '',
                expertise: isFacultyNew ? userSelected.slice() : []
            };
            for (var f in fields) nu[f] = fields[f];
            state.users.push(nu);
            logActivity('New ' + roleLabel(role).toLowerCase() + ' account created: ' + plainName(nu), 'create', 'user');
        }

        save();
        closeModal('userModal');
        renderUsers();
    }

    function clearProfessorFromStudents(profId) {
        state.users.forEach(function (x) { if (x.role === 'project-manager' && x.professor === profId) x.professor = ''; });
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
            if (u.role === 'panelist' || u.role === 'adviser') clearProfessorFromStudents(u.id);
            logActivity('Account deleted: ' + plainName(u) + ' (' + roleLabel(u.role) + ')', 'delete', 'user');
            save();
        }
        delUserId = null;
        closeModal('delUserModal');
        renderUsers();
    }

    function initUsers() {
        if (!$('users-tbody')) return;

        if ($('searchInput')) $('searchInput').addEventListener('input', renderUsers);
        if ($('roleFilter')) $('roleFilter').addEventListener('change', renderUsers);
        if ($('statusFilter')) $('statusFilter').addEventListener('change', renderUsers);

        if ($('openModalBtn')) $('openModalBtn').addEventListener('click', openCreateUser);
        if ($('confirmBtn')) $('confirmBtn').addEventListener('click', saveUser);
        if ($('cancelBtn')) $('cancelBtn').addEventListener('click', function () { closeModal('userModal'); });

        if ($('modalRoleSelect')) {
            $('modalRoleSelect').addEventListener('change', function (e) {
                updateIdPrefix(e.target.value);
                handleRoleUIChange(e.target.value);
            });
        }

        if ($('modalIdInput')) {
            $('modalIdInput').addEventListener('input', function () {
                var val = this.value;
                if (val.indexOf(currentPrefix) !== 0) val = currentPrefix;
                this.value = currentPrefix + formatDigits(val.substring(currentPrefix.length));
            });
        }

        bindCustomDropdowns();
        renderUsers();
    }

    /* ------------------------------------------------------------------ *
     *  Public API
     * ------------------------------------------------------------------ */
    window.AD = {
        reset: function () {
            localStorage.removeItem(KEY);
            state = seed();
            save();
            console.log('Demo data reset to default.');
            location.reload();
        },
        openAddTag: openAddTag,
        saveNewTag: saveNewTag,
        openEditTag: openEditTag,
        saveEditedTag: saveEditedTag,
        deleteTag: deleteTag,
        confirmDelTag: confirmDelTag,
        openEditFaculty: openEditFaculty,
        saveExpertise: saveExpertise,
        openCreateUser: openCreateUser,
        editUser: editUser,
        deleteUser: deleteUser,
        confirmDelUser: confirmDelUser,
        closeModals: closeModals
    };

    load();

    document.addEventListener('DOMContentLoaded', function () {
        renderDashboard();
        initTags();
        initFaculty();
        initUsers();
    });
})();