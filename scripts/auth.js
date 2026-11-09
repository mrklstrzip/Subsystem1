/* Sign-in pages: show/hide password, Caps Lock hint, password rules, and field checks.
   The server checks everything again, so this only gives faster feedback. It never blocks typing. */
(function () {
    "use strict";

    var EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    function byId(id) { return document.getElementById(id); }

    // The server can print empty data-* attributes (data-digits=""), so an attribute only counts when it has a value.
    function attr(el, name) { var v = el.getAttribute(name); return v && v.trim() ? v : null; }

    function setError(field, message) {
        var box = document.querySelector('[data-error-for="' + field.name + '"]');
        field.classList.toggle("is-invalid", !!message);
        if (message) field.setAttribute("aria-invalid", "true"); else field.removeAttribute("aria-invalid");
        if (box) box.textContent = message || "";
    }

    function rulesMet(value) {
        return { length: value.length >= 8, "case": /[a-z]/.test(value) && /[A-Z]/.test(value), number: /\d/.test(value) };
    }

    function messageFor(field, form) {
        var v = field.value, text = v.trim();
        if (attr(field, "data-required") && !text) return attr(field, "data-required");
        if (!text) return "";
        if (attr(field, "data-email") && !EMAIL.test(text)) return attr(field, "data-email");
        var pattern = attr(field, "data-pattern");
        if (pattern && !new RegExp(pattern).test(text)) return attr(field, "data-pattern-message") || "Check this field.";
        if (attr(field, "data-rules")) {
            var r = rulesMet(v);
            if (!(r.length && r["case"] && r.number)) return "Your password doesn't meet all the requirements below.";
        }
        var other = attr(field, "data-match");
        if (other && form.elements[other] && v !== form.elements[other].value) return attr(field, "data-match-message");
        return "";
    }

    // show / hide password
    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-toggle-password]");
        if (!btn) return;
        var input = byId(btn.getAttribute("data-toggle-password"));
        if (!input) return;
        var show = input.type === "password";
        input.type = show ? "text" : "password";
        btn.setAttribute("aria-pressed", show ? "true" : "false");
        btn.setAttribute("aria-label", show ? "Hide password" : "Show password");
        input.focus();
    });

    // Caps Lock hint
    document.querySelectorAll("[data-caps]").forEach(function (input) {
        var hint = attr(input, "data-caps") && byId(attr(input, "data-caps"));
        if (!hint) return;
        function update(e) { if (e.getModifierState) hint.hidden = !e.getModifierState("CapsLock"); }
        input.addEventListener("keydown", update);
        input.addEventListener("keyup", update);
        input.addEventListener("blur", function () { hint.hidden = true; });
    });

    // code boxes keep digits only
    document.querySelectorAll('[data-digits="true"]').forEach(function (input) {
        input.addEventListener("input", function () {
            var clean = input.value.replace(/\D/g, "");
            if (clean !== input.value) input.value = clean;
        });
    });

    // live password rules
    document.querySelectorAll("[data-rules]").forEach(function (input) {
        var list = attr(input, "data-rules") && byId(attr(input, "data-rules"));
        if (!list) return;
        input.addEventListener("input", function () {
            var r = rulesMet(input.value);
            list.querySelectorAll("[data-rule]").forEach(function (li) {
                li.classList.toggle("is-met", !!r[li.getAttribute("data-rule")]);
            });
        });
    });

    // form checks
    document.querySelectorAll("[data-auth-form]").forEach(function (form) {
        var fields = Array.prototype.slice.call(form.querySelectorAll(".f__control[name]"));

        fields.forEach(function (field) {
            var checkNow = function () { setError(field, messageFor(field, form)); };
            field.addEventListener(field.tagName === "SELECT" ? "change" : "blur", checkNow);
            field.addEventListener("input", function () { if (field.classList.contains("is-invalid")) checkNow(); });
        });

        // re-check the confirmation when the password changes
        var pw = form.elements.Password, confirm = form.elements.ConfirmPassword;
        if (pw && confirm) pw.addEventListener("input", function () { if (confirm.value) setError(confirm, messageFor(confirm, form)); });

        var btn = form.querySelector('button[type="submit"]');
        var label = btn ? btn.textContent : "";

        form.addEventListener("submit", function (e) {
            var firstBad = null;
            fields.forEach(function (field) {
                var msg = messageFor(field, form);
                setError(field, msg);
                if (msg && !firstBad) firstBad = field;
            });
            if (firstBad) { e.preventDefault(); firstBad.focus(); return; }
            if (btn) {
                if (btn.getAttribute("aria-busy") === "true") { e.preventDefault(); return; }
                btn.setAttribute("aria-busy", "true");
                btn.textContent = btn.getAttribute("data-busy-text") || "Please wait…";
            }
        });

        // coming back with the browser's Back button must not leave the button stuck on "Signing in…"
        window.addEventListener("pageshow", function () {
            if (btn) { btn.removeAttribute("aria-busy"); btn.textContent = label; }
        });
    });
})();
