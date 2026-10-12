// Bootstrap supplies focus trapping, Escape, backdrop and scroll locking.
const desktop = window.matchMedia("(min-width: 992px)");

function closeSidebar() {
    const sidebar = document.getElementById("site-sidebar");
    if (sidebar) window.bootstrap?.Offcanvas.getInstance(sidebar)?.hide();
}

document.addEventListener("click", event => {
    if (!desktop.matches && event.target instanceof Element &&
        event.target.closest("#site-sidebar a[href]")) closeSidebar();
});

desktop.addEventListener("change", event => {
    if (event.matches) closeSidebar();
});

window.addEventListener("popstate", closeSidebar);

document.addEventListener("hidden.bs.offcanvas", event => {
    // Bootstrap 5.1 marks a closed drawer hidden; the desktop rail remains visible.
    if (desktop.matches && event.target instanceof Element && event.target.id === "site-sidebar") {
        event.target.removeAttribute("aria-hidden");
    }
});
