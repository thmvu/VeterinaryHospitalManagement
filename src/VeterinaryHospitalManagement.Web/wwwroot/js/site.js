// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Accessible mobile navigation for the back-office shell.
(function () {
    const toggle = document.getElementById("sidebarToggle");
    const sidebar = document.getElementById("appSidebar");
    const overlay = document.getElementById("sidebarOverlay");
    if (!toggle || !sidebar) return;

    const focusableSelector = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const isMobile = () => window.matchMedia("(max-width: 991.98px)").matches;

    function setOpen(open, restoreFocus) {
        sidebar.classList.toggle("open", open);
        overlay?.classList.toggle("show", open);
        toggle.setAttribute("aria-expanded", String(open));
        sidebar.setAttribute("aria-hidden", String(isMobile() && !open));
        document.body.classList.toggle("sidebar-open", open);
        if (open) {
            sidebar.querySelector(focusableSelector)?.focus();
        } else if (restoreFocus) {
            toggle.focus();
        }
    }

    toggle.addEventListener("click", () => setOpen(!sidebar.classList.contains("open"), false));
    overlay?.addEventListener("click", () => setOpen(false, true));

    document.addEventListener("keydown", function (event) {
        if (!isMobile() || !sidebar.classList.contains("open")) return;
        if (event.key === "Escape") {
            event.preventDefault();
            setOpen(false, true);
            return;
        }
        if (event.key !== "Tab") return;
        const focusable = Array.from(sidebar.querySelectorAll(focusableSelector))
            .filter(element => element.getClientRects().length > 0 && !element.closest("[inert]"));
        if (focusable.length === 0) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    });

    const media = window.matchMedia("(max-width: 991.98px)");
    const syncMode = () => {
        if (!media.matches) setOpen(false, false);
        sidebar.setAttribute("aria-hidden", String(media.matches && !sidebar.classList.contains("open")));
    };
    media.addEventListener?.("change", syncMode);
    syncMode();
})();
