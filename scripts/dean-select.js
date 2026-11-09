(function () {
    function build(filter) {
        var select = filter.querySelector("select");
        if (!select || filter.querySelector(".custom-select-dropdown")) return;

        var dd = document.createElement("div");
        dd.className = "custom-select-dropdown";
        filter.appendChild(dd);

        function render() {
            dd.innerHTML = "";
            for (var i = 0; i < select.options.length; i++) {
                var o = select.options[i];
                var div = document.createElement("div");
                div.className = "select-option" + (i === select.selectedIndex ? " selected" : "");
                div.textContent = o.text;
                div.setAttribute("data-index", i);
                dd.appendChild(div);
            }
        }

        function open() {
            closeAll();
            render();
            dd.classList.add("open");
            filter.classList.add("open");
        }

        function close() {
            dd.classList.remove("open");
            filter.classList.remove("open");
        }

        function closeAll() {
            var all = document.querySelectorAll(".sa-filter.open");
            for (var i = 0; i < all.length; i++) {
                all[i].classList.remove("open");
                var d = all[i].querySelector(".custom-select-dropdown");
                if (d) d.classList.remove("open");
            }
        }

        // stop the native popup, show the custom one instead
        select.addEventListener("mousedown", function (e) {
            e.preventDefault();
            select.blur();
            if (dd.classList.contains("open")) close(); else open();
        });

        select.addEventListener("keydown", function (e) {
            if (e.key === "Enter" || e.key === " " || e.key === "ArrowDown") {
                e.preventDefault();
                open();
            } else if (e.key === "Escape") {
                close();
            }
        });

        dd.addEventListener("click", function (e) {
            var opt = e.target.closest ? e.target.closest(".select-option") : null;
            if (!opt) return;
            select.selectedIndex = parseInt(opt.getAttribute("data-index"), 10);
            close();
            select.dispatchEvent(new Event("change", { bubbles: true }));
        });
    }

    function init() {
        var filters = document.querySelectorAll(".sa-filter");
        for (var i = 0; i < filters.length; i++) build(filters[i]);

        document.addEventListener("click", function (e) {
            if (e.target.closest && e.target.closest(".sa-filter")) return;
            var open = document.querySelectorAll(".sa-filter.open");
            for (var i = 0; i < open.length; i++) {
                open[i].classList.remove("open");
                var d = open[i].querySelector(".custom-select-dropdown");
                if (d) d.classList.remove("open");
            }
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();