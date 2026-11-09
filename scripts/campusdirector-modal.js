function openModal(id) {
    var el = document.getElementById(id);
    if (el) {
        el.style.display = 'flex';
    }
}

function closeModal(id) {
    var el = document.getElementById(id);
    if (el) {
        el.style.display = 'none';
    }
}

window.addEventListener('click', function(e) {
    var target = e.target;
    if (target && target.classList && target.classList.contains('modal-overlay')) {
        target.style.display = 'none';
    }
});