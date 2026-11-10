/* Admin - connected to MongoDB through /Admin/AdminApi/* (no more localStorage demo data) */
(function () {
    'use strict';

    if (window.AD) return;

    var API = '/Admin/AdminApi/';
    var LOGIN_URL = '/Auth/Login/login';

    var ROLES = {
        'admin': { label: 'Admin', cls: 'adm-rl' },
        'campus-director': { label: 'Campus Director', cls: 'campdirect-rl' },
        'capstone-coordinator': { label: 'Capstone Coordinator', cls: 'capscoor-rl' },
        'panelist': { label: 'Panelist', cls: 'prof-rl' },
        'project-manager': { label: 'Student', cls: 'stud-rl' },
        'adviser': { label: 'Adviser', cls: 'adv-rl' },
        'dean': { label: 'Dean', cls: 'dean-rl' }
    };

    // Hierarchy order used for the users table
    var ROLE_ORDER = ['admin', 'dean', 'capstone-coordinator', 'campus-director', 'panelist', 'adviser', 'project-manager'];

    var DIST = [
        { role: 'dean', label: 'Dean', dotCls: 'dot-dean' },
        { role: 'capstone-coordinator', label: 'Capstone Coordinator', dotCls: 'dot-capstone-coordinator' },
        { role: 'campus-director', label: 'Campus Director', dotCls: 'dot-campus-director' },
        { role: 'panelist', label: 'Panelist', dotCls: 'dot-panelist' },
        { role: 'adviser', label: 'Adviser', dotCls: 'dot-adviser' },
        { role: 'project-manager', label: 'Student', dotCls: 'dot-project-manager' }
    ];

    var SINGLE_ROLES = ['admin', 'campus-director', 'capstone-coordinator', 'dean'];

    // Data loaded from the database (replaced on every refresh)
    var state = { users: [], tags: [], activity: [] };

    /* ---------------------------------------------------------------
       Server calls
       --------------------------------------------------------------- */

    function sessionExpired() {
        try { localStorage.removeItem('loggedInUser'); } catch (e) { }
        window.location.href = LOGIN_URL;
    }

    // GET when body is undefined, otherwise POST (JSON).
    function api(action, body) {
        var opts = {
            method: body === undefined ? 'GET' : 'POST',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        };
        if (body !== undefined) {
            opts.headers['Content-Type'] = 'application/json';
            opts.body = JSON.stringify(body);
        }

        return fetch(API + action, opts)
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res && res.unauthorized) {
                    sessionExpired();
                    return new Promise(function () { }); // page is leaving; never resolve
                }
                return res;
            });
    }

    function networkError() {
        alert('Could not reach the server. Please check your connection and try again.');
    }

    function notify(title, message) { if (window.Notice) Notice.success(title, message); }

    function refresh() {
        return api('state').then(function (res) {
            if (!res || !res.success) {
                throw new Error((res && res.message) || 'Could not load data.');
            }
            state = {
                users: res.users || [],
                tags: res.tags || [],
                activity: res.activity || []
            };
        });
    }

    // Load from the database, then draw. Shows a message inside the table if it fails.
    function loadThen(render, bodyId, cols) {
        var body = bodyId ? $(bodyId) : null;
        if (body) {
            body.innerHTML = '<tr><td colspan="' + cols + '" style="text-align:center; color:#9092AD;">Loading...</td></tr>';
        }
        return refresh().then(render).catch(function (err) {
            if (body) {
                body.innerHTML = '<tr><td colspan="' + cols + '" style="text-align:center; color:#B3261E;">' +
                    esc(err && err.message ? err.message : 'Could not load data.') + '</td></tr>';
            } else {
                console.error(err);
            }
        });
    }

    // Prevents double-clicks from sending the same request twice.
    var busy = false;

    // send(action, body, onDone): POST, then onDone() only when the server says success.
    function send(action, body, onDone) {
        if (busy) return;
        busy = true;
        api(action, body).then(function (res) {
            busy = false;
            if (!res || !res.success) {
                alert((res && res.message) || 'Something went wrong. Please try again.');
                return;
            }
            // Saved. Reload the lists from the database, then redraw.
            return refresh().then(onDone).catch(function () {
                alert('Saved, but the list could not be reloaded. Please refresh the page.');
            });
        }, function () {
            busy = false;
            networkError();
        });
    }

    /* ---------------------------------------------------------------
       Helpers
       --------------------------------------------------------------- */

    function $(id) { return document.getElementById(id); }
    function pad(n) { return (n < 10 ? '0' : '') + n; }

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function norm(s) { return String(s || '').trim().toLowerCase(); }

    function fmtDate(iso) {
        var d = new Date(iso);
        if (isNaN(d.getTime())) return '';
        return pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + '-' + d.getFullYear();
    }

    function fmtTime(iso) {
        var d = new Date(iso);
        if (isNaN(d.getTime())) return '';
        var h = d.getHours();
        var ampm = h >= 12 ? 'PM' : 'AM';
        h = h % 12;
        if (h === 0) h = 12;
        return h + ':' + pad(d.getMinutes()) + ' ' + ampm;
    }

    function userById(id) { return state.users.filter(function (u) { return u.id === id; })[0]; }
    function tagById(id) { return state.tags.filter(function (t) { return t.id === id; })[0]; }

    function activeTagNames() {
        return state.tags.filter(function (t) { return t.status === 'active'; }).map(function (t) { return t.name; });
    }

    function listName(u) {
        var suf = u.suffix ? ' ' + u.suffix : '';
        return u.lastName + ', ' + u.firstName + (u.middleName ? ' ' + u.middleName : '') + suf;
    }

    function plainName(u) {
        var suf = u.suffix ? ' ' + u.suffix : '';
        return u.firstName + ' ' + u.lastName + suf;
    }

    function roleLabel(role) {
        if (ROLES[role]) return ROLES[role].label;
        var u = state.users.filter(function (x) { return x.role === role; })[0];
        return (u && u.roleLabel) || role;
    }

    function statusBadge(status) {
        return status === 'active'
            ? '<span class="act" data-status="active">Active</span>'
            : '<span class="inact" data-status="inactive">Inactive</span>';
    }

    function getActivityIcon(a) {
        if (a.category === 'tag') return '<i class="fa fa-tag"></i>';
        if (a.type === 'delete') return '<i class="fa fa-ban"></i>';
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

    function renderPicker(boxId, gridId, selected, onToggle) {
        var box = $(boxId), grid = $(gridId);
        if (!box || !grid) return;

        box.innerHTML = selected.map(function (n) {
            return '<span class="modal-tag" data-tag="' + esc(n) + '">' + esc(n) + ' <i class="fa fa-times"></i></span>';
        }).join('');

        var names = activeTagNames();

        grid.innerHTML = names.map(function (n) {
            return '<div class="grid-tag' + (selected.indexOf(n) > -1 ? ' selected' : '') + '" data-tag="' + esc(n) + '">' + esc(n) + '</div>';
        }).join('') || '<span style="color:#9092AD; font-size:12px;">No active expertise tags yet. Add some in Expertise Tag.</span>';

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

                // Set the initial active option based on the hidden select
                menu.querySelectorAll('.select-option').forEach(function (opt) {
                    var val = opt.getAttribute('data-value');

                    opt.classList.toggle('active', val === hiddenSelect.value);

                    opt.addEventListener('click', function () {
                        var val = this.getAttribute('data-value');

                        // Update hidden select
                        hiddenSelect.value = val;

                        // Update displayed text
                        if (textSpan) {
                            textSpan.textContent = this.textContent.trim();
                        }

                        // Update selected icon
                        var optIcon = this.querySelector('i');
                        if (optIcon && iconElem) {
                            iconElem.className = optIcon.className;
                        }

                        // Remove active from all options
                        menu.querySelectorAll('.select-option').forEach(function (item) {
                            item.classList.remove('active');
                        });

                        // Add active to clicked option
                        this.classList.add('active');

                        // Close dropdown
                        menu.classList.remove('open');

                        // Trigger filter
                        hiddenSelect.dispatchEvent(new Event('change', {
                            bubbles: true
                        }));
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

    /* ---------------------------------------------------------------
       Dashboard
       --------------------------------------------------------------- */

    function renderDashboard() {
        if (!$('dash-total')) return;

        var known = DIST.map(function (d) { return d.role; });
        var people = state.users.filter(function (u) { return known.indexOf(u.role) > -1; });
        function count(role, status) {
            return people.filter(function (u) { return u.role === role && (!status || u.status === status); }).length;
        }

        var totalPeople = people.length;

        $('dash-total').textContent = totalPeople;
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
                '<span class="date-main">' + esc(fmtDate(a.at)) + '</span>' +
                '<span class="time-sub">' + esc(fmtTime(a.at)) + '</span>' +
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

        // Donut chart based on the role distribution
        var chartEl = document.querySelector('.donut-chart');
        if (chartEl) {
            if (totalPeople === 0) {
                chartEl.style.background = '#E2E8F0'; // fallback if there are no users yet
            } else {
                var accumulated = 0;
                var cssVarMap = {
                    'project-manager': 'var(--projectmanager)',
                    'panelist': 'var(--panelist)',
                    'adviser': 'var(--adviser)',
                    'campus-director': 'var(--campusdirector)',
                    'capstone-coordinator': 'var(--capstonecoordinator)',
                    'dean': 'var(--dean)'
                };
                var gradientStops = DIST.map(function (d) {
                    var percentage = (count(d.role) / totalPeople) * 100;
                    var start = accumulated;
                    accumulated += percentage;
                    return (cssVarMap[d.role] || '#ccc') + ' ' + start + '% ' + accumulated + '%';
                });

                chartEl.style.background = 'conic-gradient(' + gradientStops.join(', ') + ')';
            }
        }
    }

    function initDashboard() {
        if (!$('dash-total')) return;

        var welcome = $('dash-welcome');
        if (welcome) {
            try {
                var me = JSON.parse(localStorage.getItem('loggedInUser') || 'null');
                if (me && me.lastName) welcome.textContent = 'Welcome back, ' + me.lastName + '!';
            } catch (e) { }
        }

        loadThen(renderDashboard, null, 0);
    }

    /* ---------------------------------------------------------------
       Expertise tags
       --------------------------------------------------------------- */

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
                '<i class="fa fa-edit" onclick="AD.openEditTag(\'' + esc(t.id) + '\')"></i>' +
                '<i class="fa fa-trash" onclick="AD.deleteTag(\'' + esc(t.id) + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="4" style="text-align:center; color:#9092AD;">No expertise tags found</td></tr>';
    }

    function initTags() {
        if (!$('tags-tbody')) return;
        $('searchInput').addEventListener('input', renderTags);
        $('statusFilter').addEventListener('change', renderTags);
        bindCustomDropdowns();
        loadThen(renderTags, 'tags-tbody', 4);
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

        send('saveTag', { name: name, description: desc, status: 'active' }, function () {
            closeModal('addModal');
            renderTags();
            notify('Expertise tag created', 'Successfully created "' + name + '".');
        });
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

        var status = $('statusActive').checked ? 'active' : 'inactive';

        send('saveTag', { id: t.id, name: name, description: desc, status: status }, function () {
            closeModal('editModal');
            editTagId = null;
            renderTags();
            notify('Expertise tag updated', 'Successfully edited "' + name + '".');
        });
    }

    var delTagId = null;

    function deleteTag(id) {
        var t = tagById(id);
        if (!t) return;
        delTagId = id;
        if ($('delTagModalText')) {
            $('delTagModalText').innerHTML = 'Do you really want to delete <strong>' + esc(t.name) + '</strong>?<br />This process cannot be undone';
        }
        openModal('delTagModal');
    }

    function confirmDelTag() {
        if (!delTagId) { closeModal('delTagModal'); return; }

        send('deleteTag', { id: delTagId }, function () {
            delTagId = null;
            closeModal('delTagModal');
            renderTags();
        });
    }

    /* ---------------------------------------------------------------
       Faculty profile
       --------------------------------------------------------------- */

    var facultyEditId = null, facultySelected = [];

    function renderFaculty() {
        var body = $('faculty-tbody');
        if (!body) return;

        var q = norm($('searchInput').value);

        var list = state.users.filter(function (u) {
            return (u.role === 'panelist' || u.role === 'adviser') &&
                (!q || norm(plainName(u) + ' ' + listName(u) + ' ' + u.expertise.join(' ')).indexOf(q) > -1);
        });

        list.sort(function (a, b) { return listName(a).localeCompare(listName(b)); });

        body.innerHTML = list.map(function (u) {
            var tags = u.expertise.map(function (n) { return '<span class="tag">' + esc(n) + '</span>'; }).join('') ||
                '<span style="color:#9092AD; font-size:12px;">No expertise tags yet</span>';
            return '<tr>' +
                '<td class="name bold">' + esc(plainName(u)) + '</td>' +
                '<td class="expertise">' + tags + '</td>' +
                '<td class="action bold"><i class="fa fa-edit" onclick="AD.openEditFaculty(\'' + esc(u.id) + '\')"></i></td>' +
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
        loadThen(renderFaculty, 'faculty-tbody', 3);
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
        if (!facultyEditId) { closeModal('editFacultyModal'); return; }

        var fu = userById(facultyEditId);
        var fname = fu ? plainName(fu) : 'the faculty member';

        send('saveFaculty', { id: facultyEditId, expertise: facultySelected.slice() }, function () {
            facultyEditId = null;
            closeModal('editFacultyModal');
            renderFaculty();
            notify('Faculty profile updated', 'Successfully edited ' + fname + '.');
        });
    }

    /* ---------------------------------------------------------------
       User management
       --------------------------------------------------------------- */

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

        // Sort by role hierarchy, then by name
        list.sort(function (a, b) {
            var ia = ROLE_ORDER.indexOf(a.role), ib = ROLE_ORDER.indexOf(b.role);
            if (ia < 0) ia = ROLE_ORDER.length;
            if (ib < 0) ib = ROLE_ORDER.length;
            var d = ia - ib;
            return d !== 0 ? d : listName(a).localeCompare(listName(b));
        });

        body.innerHTML = list.map(function (u) {
            var r = ROLES[u.role] || { label: u.roleLabel || u.role, cls: '' };
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
                '<i class="fa fa-edit" onclick="AD.editUser(\'' + esc(u.id) + '\')"></i>' +
                '<i class="fa fa-trash" onclick="AD.deleteUser(\'' + esc(u.id) + '\')"></i>' +
                '</td></tr>';
        }).join('') || '<tr><td colspan="5" style="text-align:center; color:#9092AD;">No users found</td></tr>';
    }

    function formatDigits(digits) {
        var cleaned = String(digits).replace(/\D/g, '').slice(0, 6);
        return cleaned.length > 2 ? cleaned.slice(0, 2) + '-' + cleaned.slice(2) : cleaned;
    }

    // Existing ID numbers are shown as they are stored (e.g. "2026-0001").
    function updateIdPrefix(role, existingId) {
        var idInput = $('modalIdInput');
        if (role === 'campus-director') currentPrefix = 'CD-';
        else if (role === 'capstone-coordinator') currentPrefix = 'CC-';
        else if (role === 'dean') currentPrefix = 'DEAN-';
        else if (role === 'panelist' || role === 'adviser') currentPrefix = 'PROF-';
        else currentPrefix = '';
        if (idInput) {
            idInput.placeholder = currentPrefix + '12-3456';
            if (existingId) {
                idInput.value = existingId;
            } else {
                idInput.value = currentPrefix + formatDigits('');
            }
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
            // Expertise tags are ONLY displayed during Creation, NOT during Edit mode
            if (!isEditMode && $('fieldProfessor')) {
                $('fieldProfessor').style.display = 'flex';
                renderUserPicker();
            }
        }
    }

    function updateStudentSectionPrefix(prog) {
        var secInput = $('sectionInput');
        if (!secInput) return;
        var currentVal = secInput.value;
        var prefix = '';
        if (prog === 'BSIT') prefix = 'SBIT-';
        else if (prog === 'BSIS') prefix = 'SBIS-';
        else if (prog === 'BSCS') prefix = 'SBCS-';

        if (prefix) {
            var rest = currentVal.replace(/^(SBIT-|SBIS-|SBCS-)/i, '');
            secInput.value = prefix + rest;
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

    // Advisers shown in the student form: panelists/advisers that are active
    // (plus the one already chosen, even if that person was deactivated later).
    function fillAdviserSelect(selectedId) {
        var profs = state.users.filter(function (u) {
            return (u.role === 'panelist' || u.role === 'adviser') && (u.status === 'active' || u.id === selectedId);
        });
        profs.sort(function (a, b) { return listName(a).localeCompare(listName(b)); });

        var sel = $('professorInput');
        if (!sel) return;
        sel.innerHTML = profs.length
            ? '<option value=""' + (selectedId ? '' : ' selected') + ' disabled>Select Adviser</option>' +
            profs.map(function (u) {
                return '<option value="' + esc(u.id) + '"' + (u.id === selectedId ? ' selected' : '') + '>' + esc(plainName(u)) + '</option>';
            }).join('')
            : '<option value="" selected disabled>No professor/adviser available</option>';
    }

    // The account request (sent by an Adviser) this "Create" answers, or '' for a normal create.
    var requestId = '';

    function openCreateUser() {
        isEditMode = false;
        editUserId = null;
        requestId = '';
        if ($('modalHeaderTitle')) $('modalHeaderTitle').innerText = 'CREATE USER ACCOUNT';
        if ($('passwordGroup')) $('passwordGroup').style.display = 'flex';
        if ($('statusGroup')) $('statusGroup').style.display = 'none';

        if ($('lastNameInput')) $('lastNameInput').value = '';
        if ($('firstNameInput')) $('firstNameInput').value = '';
        if ($('middleNameInput')) $('middleNameInput').value = '';
        if ($('suffixSelect')) $('suffixSelect').value = '';
        if ($('emailInput')) $('emailInput').value = '';
        if ($('programSelect')) $('programSelect').selectedIndex = 0;
        if ($('sectionInput')) $('sectionInput').value = '';
        fillAdviserSelect('');
        if ($('passwordInput')) $('passwordInput').value = '';
        if ($('statusActive')) $('statusActive').checked = true;
        userSelected = [];

        setRoleOptions();
        if ($('modalRoleSelect')) $('modalRoleSelect').value = 'project-manager';
        updateIdPrefix('project-manager');
        handleRoleUIChange('project-manager');
        openModal('userModal');
    }

    // "Create" on a student's account request: the same Create modal, filled in from the request.
    function openCreateFromRequest(r) {
        openCreateUser();
        requestId = r.id;

        if ($('lastNameInput')) $('lastNameInput').value = r.lastName || '';
        if ($('firstNameInput')) $('firstNameInput').value = r.firstName || '';
        if ($('middleNameInput')) $('middleNameInput').value = r.middleName || '';
        if ($('suffixSelect')) $('suffixSelect').value = r.suffix || '';
        if ($('emailInput')) $('emailInput').value = r.email || '';
        if ($('modalIdInput')) $('modalIdInput').value = r.idNumber || '';
        if ($('programSelect')) $('programSelect').value = r.program || '';
        if ($('sectionInput')) $('sectionInput').value = r.section || '';
        fillAdviserSelect(r.adviserId || '');
    }

    // /Admin/UserManagement/usermanagement?request=<id>  (from the notification drawer's "Create")
    function openRequestFromUrl() {
        var m = window.location.search.match(/[?&]request=([0-9a-fA-F]+)/);
        if (!m) return;

        // so a refresh does not open it again
        if (window.history && history.replaceState) history.replaceState(null, '', window.location.pathname);

        api('requestInfo?id=' + m[1]).then(function (res) {
            if (!res || !res.success) {
                alert((res && res.message) || 'That account request could not be opened.');
                return;
            }
            openCreateFromRequest(res.request);
        }, networkError);
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
        if ($('suffixSelect')) $('suffixSelect').value = u.suffix || '';
        if ($('emailInput')) $('emailInput').value = u.email;
        if ($('statusActive')) $('statusActive').checked = (u.status === 'active');
        if ($('statusInactive')) $('statusInactive').checked = (u.status !== 'active');

        setRoleOptions();
        if ($('modalRoleSelect')) $('modalRoleSelect').value = u.role;
        updateIdPrefix(u.role, u.idNumber);

        if ($('programSelect')) $('programSelect').value = u.program || '';
        if ($('sectionInput')) $('sectionInput').value = u.section || '';
        fillAdviserSelect(u.adviserId || '');

        handleRoleUIChange(u.role);
        openModal('userModal');
    }

    function saveUser() {
        var lastName = $('lastNameInput') ? $('lastNameInput').value.trim() : '';
        var firstName = $('firstNameInput') ? $('firstNameInput').value.trim() : '';
        var middleName = $('middleNameInput') ? $('middleNameInput').value.trim() : '';
        var suffix = $('suffixSelect') ? $('suffixSelect').value : '';
        var email = $('emailInput') ? $('emailInput').value.trim() : '';
        var role = $('modalRoleSelect') ? $('modalRoleSelect').value : 'project-manager';
        var password = $('passwordInput') ? $('passwordInput').value : '';

        if (!lastName || !firstName || !email) { alert('Please fill in all required fields (Name, Email).'); return; }

        var emailTaken = state.users.some(function (o) { return o.id !== editUserId && norm(o.email) === norm(email); });
        if (emailTaken) { alert('That email is already used by another account.'); return; }

        if (SINGLE_ROLES.indexOf(role) > -1) {
            var roleTaken = state.users.some(function (o) { return o.id !== editUserId && o.role === role; });
            if (roleTaken) { alert('There is already a ' + roleLabel(role) + ' account. Only one is allowed, so edit or delete the existing one first.'); return; }
        }

        if (!isEditMode && password.length < 8) {
            alert('Temporary password must be at least 8 characters.');
            return;
        }

        var idValue = $('modalIdInput') ? $('modalIdInput').value.trim() : '';
        if (idValue === currentPrefix) idValue = '';

        var isPM = role === 'project-manager';
        var program = isPM && $('programSelect') ? $('programSelect').value || '' : '';
        var section = isPM && $('sectionInput') ? $('sectionInput').value.trim() : '';
        if (isPM && (!program || !section)) {
            alert('Please select the student\'s program and enter the section.');
            return;
        }

        var payload = {
            id: isEditMode ? editUserId : '',
            lastName: lastName,
            firstName: firstName,
            middleName: middleName,
            suffix: suffix,
            email: email,
            role: role,
            idNumber: idValue,
            program: program,
            section: section,
            adviserId: isPM && $('professorInput') ? $('professorInput').value || '' : '',
            status: isEditMode && $('statusInactive') && $('statusInactive').checked ? 'inactive' : 'active',
            password: isEditMode ? '' : password,
            requestId: isEditMode ? '' : requestId,
            expertise: (!isEditMode && (role === 'panelist' || role === 'adviser')) ? userSelected.slice() : []
        };

        var wasEdit = isEditMode;
        var shownName = (firstName + ' ' + lastName + (suffix ? ' ' + suffix : '')).trim();

        send('saveUser', payload, function () {
            closeModal('userModal');
            if ($('passwordInput')) $('passwordInput').value = '';
            renderUsers();
            notify(wasEdit ? 'User updated' : 'User created',
                'Successfully ' + (wasEdit ? 'edited ' : 'created ') + shownName + '.');
        });
    }

    var delUserId = null;

    function deleteUser(id) {
        var u = userById(id);
        if (!u) return;
        if (u.role === 'admin') { alert('Admin accounts cannot be deleted.'); return; }
        delUserId = id;
        if ($('delUserModalText')) {
            $('delUserModalText').innerHTML = 'Do you really want to delete <strong>' + esc(plainName(u)) + ' (' + esc(roleLabel(u.role)) + ')</strong>?<br />This process cannot be undone';
        }
        openModal('delUserModal');
    }

    function confirmDelUser() {
        if (!delUserId) { closeModal('delUserModal'); return; }

        send('deleteUser', { id: delUserId }, function () {
            delUserId = null;
            closeModal('delUserModal');
            renderUsers();
        });
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

        if ($('programSelect')) {
            $('programSelect').addEventListener('change', function (e) {
                updateStudentSectionPrefix(e.target.value);
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
        loadThen(renderUsers, 'users-tbody', 5).then(openRequestFromUrl);
    }

    window.AD = {
        refresh: refresh,
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

    document.addEventListener('DOMContentLoaded', function () {
        initDashboard();
        initTags();
        initFaculty();
        initUsers();
    });
})();