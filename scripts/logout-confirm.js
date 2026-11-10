(function () {
    "use strict";

    var modal = null;

    function getProfileName() {
        var profileName = document.getElementById("profileName");

        if (profileName && profileName.textContent.trim() !== "") {
            return profileName.textContent.trim();
        }

        return "your account";
    }

    function createModal() {
        if (modal) {
            return;
        }

        modal = document.createElement("div");
        modal.id = "logoutConfirmModal";
        modal.className = "logout-overlay";
        modal.style.display = "none";

        modal.innerHTML = `
            <div class="lo-card"
                 role="dialog"
                 aria-modal="true"
                 aria-labelledby="lo-title">

                <div class="lo-icon">
                    <i class="fa-solid fa-right-to-bracket"></i>
                </div>

                <h3 id="lo-title" class="lo-title">
                    Log out of your account?
                </h3>

                <p class="lo-sub">
                    Signed in as: <span id="lo-name"></span>
                </p>

                <div class="lo-actions">
                    <button type="button"
                            class="lo-btn lo-fill"
                            id="lo-confirm">
                        Log out
                    </button>

                    <button type="button"
                            class="lo-btn lo-outline"
                            id="lo-cancel">
                        Cancel
                    </button>
                </div>

            </div>
        `;

        document.body.appendChild(modal);

        var confirmButton = modal.querySelector("#lo-confirm");
        var cancelButton = modal.querySelector("#lo-cancel");

        /*
           CONFIRM LOGOUT*/
        confirmButton.onclick = function (event) {
            event.preventDefault();
            event.stopImmediatePropagation();

            closeModal();                                        // close the "Log out?" card
            if (window.Loading) Loading.show("Signing out...");  // show the loader

            // End the server-side session (every role uses the same login/logout).
            try {
                fetch("/Auth/Login/logout", {
                    method: "POST",
                    credentials: "same-origin",
                    keepalive: true
                });
            } catch (e) { /* ignore */ }

            // Clear the saved login so layout-profile.js stops letting this user in.
            try {
                localStorage.removeItem("loggedInUser");
            } catch (e) { /* ignore */ }

            window.location.href = "/Auth/Login/login";
        };

        /* CANCEL */
        cancelButton.onclick = function (event) {
            event.preventDefault();
            event.stopImmediatePropagation();

            closeModal();

            return false;
        };

        /* CLICK OUTSIDE */
        modal.onclick = function (event) {

            if (event.target === modal) {
                event.preventDefault();
                event.stopImmediatePropagation();

                closeModal();

                return false;
            }
        };
    }

    function openModal() {
        createModal();

        var nameElement = modal.querySelector("#lo-name");

        if (nameElement) {
            nameElement.textContent = getProfileName();
        }

        modal.style.display = "flex";

        document.body.classList.add("logout-modal-open");

        var cancelButton = modal.querySelector("#lo-cancel");

        if (cancelButton) {
            setTimeout(function () {
                cancelButton.focus();
            }, 50);
        }
    }

    function closeModal() {
        if (!modal) {
            return;
        }

        modal.style.display = "none";

        document.body.classList.remove("logout-modal-open");
    }

    /* LOGOUT BUTTON */

    document.addEventListener("click", function (event) {

        var logoutButton = event.target.closest("#logoutBtn");

        if (!logoutButton) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();

        openModal();

        return false;

    }, true);


    /*ESCAPE KEY*/

    document.addEventListener("keydown", function (event) {

        if (
            event.key === "Escape" &&
            modal &&
            modal.style.display === "flex"
        ) {
            event.preventDefault();
            event.stopImmediatePropagation();

            closeModal();
        }

    }, true);

})();