(function () {
    var form = document.getElementById('dsFilterForm');
    if (!form) return;

    // Dropdowns: submit right away
    form.querySelectorAll('select').forEach(function (el) {
        el.addEventListener('change', function () { form.submit(); });
    });

    // Search: submit after the user stops typing, keep the cursor in the box
    var search = form.querySelector('input[name="search"]');
    var timer;
    search.addEventListener('input', function () {
        clearTimeout(timer);
        timer = setTimeout(function () { form.submit(); }, 500);
    });

    if (search.value) {
        search.focus();
        search.setSelectionRange(search.value.length, search.value.length);
    }
})();