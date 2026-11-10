(function () {
    var DEFAULT_TEXT = "Processing...";
    var PAGE_TEXT = "Loading...";
    var DELAY = 250;          // ms before the modal appears (avoids flicker)
    var NAV_FAILSAFE = 20000; // ms: auto-hide if a navigation never leaves (e.g. file download)

    // requests whose URL contains any of these never show the modal
    var ignore = ["/Notification"];

    // custom text per request URL (case-insensitive). "linger" keeps the modal up a little after
    // the response so it does not blink off just before the page redirects.
    var texts = [
        { match: "/login/check", text: "Signing in...", linger: 1500 },
        { match: "/logout", text: "Signing out...", linger: 1500 }
    ];

    var overlay, label;
    var count = 0, timer = null, failsafe = null, skipUnload = false;

    function build() {
        if (overlay || !document.body) return;
        overlay = document.createElement("div");
        overlay.className = "ld-overlay";
        overlay.setAttribute("aria-hidden", "true");
        overlay.innerHTML =
            '<div class="ld-box" role="status" aria-live="polite">' +
            '<div class="ld-spinner"></div>' +
            '<div class="ld-text"></div>' +
            '</div>';
        document.body.appendChild(overlay);
        label = overlay.querySelector(".ld-text");
        label.textContent = DEFAULT_TEXT;
    }

    function reveal() {
        build();
        if (!overlay) return;
        overlay.classList.add("open");
        overlay.setAttribute("aria-hidden", "false");
    }

    function conceal() {
        if (!overlay) return;
        overlay.classList.remove("open");
        overlay.setAttribute("aria-hidden", "true");
        label.textContent = DEFAULT_TEXT;
    }

    function begin(text, delay) {
        build();
        count++;
        if (text && label) label.textContent = text;
        if (delay === 0) {
            clearTimeout(timer);
            reveal();
        } else if (count === 1) {
            clearTimeout(timer);
            timer = setTimeout(reveal, delay);
        }
    }

    function end() {
        if (count > 0) count--;
        if (count === 0) {
            clearTimeout(timer);
            conceal();
        }
    }

    function reset() {
        count = 0;
        clearTimeout(timer);
        clearTimeout(failsafe);
        conceal();
    }

    function urlHas(url, list) {
        url = String(url || "").toLowerCase();
        for (var i = 0; i < list.length; i++) {
            if (url.indexOf(String(list[i]).toLowerCase()) !== -1) return true;
        }
        return false;
    }

    function ruleFor(url) {
        url = String(url || "").toLowerCase();
        for (var i = 0; i < texts.length; i++) {
            if (url.indexOf(texts[i].match) !== -1) return texts[i];
        }
        return null;
    }

    function startNavigation(text) {
        begin(text, DELAY);
        clearTimeout(failsafe);
        failsafe = setTimeout(reset, NAV_FAILSAFE);
    }

    function dontShowForNextUnload() {
        skipUnload = true;
        setTimeout(function () { skipUnload = false; }, 1500);
    }

    //public API
    window.Loading = {
        show: function (text) { begin(text, 0); },
        hide: end,
        reset: reset,
        ignore: ignore,
        texts: texts
    };

    //form submits
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (form.hasAttribute && form.hasAttribute("data-no-loading")) dontShowForNextUnload();
        setTimeout(function () {
            if (e.defaultPrevented) return; // validation or a script cancelled it
            if (form.hasAttribute("data-no-loading")) return;
            startNavigation(form.getAttribute("data-loading-text"));
        }, 0);
    });

    // form.submit() called from a script does not fire the submit event
    var nativeSubmit = HTMLFormElement.prototype.submit;
    HTMLFormElement.prototype.submit = function () {
        if (!this.hasAttribute("data-no-loading")) {
            startNavigation(this.getAttribute("data-loading-text"));
        } else {
            dontShowForNextUnload();
        }
        return nativeSubmit.apply(this, arguments);
    };

    //links to other pages
    document.addEventListener("click", function (e) {
        if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var a = e.target.closest ? e.target.closest("a[href]") : null;
        if (!a) return;
        if (a.hasAttribute("data-no-loading") || a.hasAttribute("download")) dontShowForNextUnload();
        setTimeout(function () {
            if (e.defaultPrevented) return;
            var href = a.getAttribute("href") || "";
            if (!href || href.charAt(0) === "#") return;
            if (/^(javascript:|mailto:|tel:)/i.test(href)) return;
            if (a.target && a.target !== "_self") return;
            if (a.hasAttribute("download") || a.hasAttribute("data-no-loading")) return;
            if (a.hostname && a.hostname !== window.location.hostname) return;
            startNavigation(a.getAttribute("data-loading-text"));
        }, 0);
    });

    //any other navigation (location.href = ..., redirects from scripts)
    window.addEventListener("beforeunload", function () {
        if (skipUnload || count > 0) return;
        startNavigation(PAGE_TEXT);
    });

    //fetch
    if (window.fetch) {
        var nativeFetch = window.fetch;
        window.fetch = function (input) {
            var url = typeof input === "string" ? input : (input && input.url) || "";
            if (urlHas(url, ignore)) return nativeFetch.apply(this, arguments);
            var rule = ruleFor(url);
            var done = function () {
                if (rule && rule.linger) setTimeout(end, rule.linger); else end();
            };
            begin(rule ? rule.text : null, rule ? 0 : DELAY);
            return nativeFetch.apply(this, arguments).then(
                function (res) { done(); return res; },
                function (err) { done(); throw err; }
            );
        };
    }

    //XMLHttpRequest (jQuery $.ajax, etc.)
    var nativeOpen = XMLHttpRequest.prototype.open;
    var nativeSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function (method, url) {
        this._ldSkip = urlHas(url, ignore);
        this._ldRule = ruleFor(url);
        return nativeOpen.apply(this, arguments);
    };
    XMLHttpRequest.prototype.send = function () {
        if (!this._ldSkip) {
            var rule = this._ldRule;
            begin(rule ? rule.text : null, rule ? 0 : DELAY);
            this.addEventListener("loadend", function () {
                if (rule && rule.linger) setTimeout(end, rule.linger); else end();
            });
        }
        return nativeSend.apply(this, arguments);
    };

    //back/forward button restores the page from cache
    window.addEventListener("pageshow", reset);
})();