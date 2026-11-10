(function () {
    var overlay, box, iconEl, titleEl, textEl, btn, onClose = null;
    var ICONS = { success: "fa-check", error: "fa-xmark", info: "fa-info" };
    var KEY = "notice.pending";

    function build() {
        if (overlay) return;
        overlay = document.createElement("div");
        overlay.className = "nt-overlay";
        overlay.setAttribute("aria-hidden", "true");
        overlay.innerHTML =
            '<div class="nt-box nt-success" role="alertdialog" aria-modal="true">' +
            '<div class="nt-icon"><i class="fa-solid fa-check"></i></div>' +
            '<h3 class="nt-title"></h3>' +
            '<p class="nt-text"></p>' +
            '<button type="button" class="nt-btn">OK</button>' +
            '</div>';
        document.body.appendChild(overlay);
        box = overlay.querySelector(".nt-box");
        iconEl = overlay.querySelector(".nt-icon i");
        titleEl = overlay.querySelector(".nt-title");
        textEl = overlay.querySelector(".nt-text");
        btn = overlay.querySelector(".nt-btn");

        btn.addEventListener("click", close);
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.addEventListener("keydown", function (e) {
            if (!overlay.classList.contains("open")) return;
            if (e.key === "Escape" || e.key === "Enter") { e.preventDefault(); close(); }
        });
    }

    function show(type, title, message, cb) {
        build();
        type = ICONS[type] ? type : "info";
        box.className = "nt-box nt-" + type;
        iconEl.className = "fa-solid " + ICONS[type];
        titleEl.textContent = title || "";          // textContent: names are never read as HTML
        textEl.textContent = message || "";
        textEl.style.display = message ? "" : "none";
        onClose = typeof cb === "function" ? cb : null;
        overlay.classList.add("open");
        overlay.setAttribute("aria-hidden", "false");
        btn.focus();
    }

    function close() {
        if (!overlay) return;
        overlay.classList.remove("open");
        overlay.setAttribute("aria-hidden", "true");
        var cb = onClose; onClose = null;
        if (cb) cb();
    }

    window.Notice = {
        success: function (t, m, cb) { show("success", t, m, cb); },
        error: function (t, m, cb) { show("error", t, m, cb); },
        info: function (t, m, cb) { show("info", t, m, cb); },
        close: close,
        // for pages that reload after saving: call this BEFORE the reload, the next page shows it
        after: function (type, t, m) {
            try { sessionStorage.setItem(KEY, JSON.stringify({ type: type, t: t, m: m })); } catch (e) { }
        }
    };

    function showPending() {
        try {
            var raw = sessionStorage.getItem(KEY);
            if (!raw) return;
            sessionStorage.removeItem(KEY);
            var p = JSON.parse(raw);
            show(p.type, p.t, p.m);
        } catch (e) { }
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", showPending);
    else showPending();
})();