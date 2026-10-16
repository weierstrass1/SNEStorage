// Bootstrap supplies focus trapping, Escape, backdrop and scroll locking.
const desktop = window.matchMedia("(min-width: 992px)");

function closeSidebar() {
    const sidebar = document.getElementById("site-sidebar");
    if (sidebar) window.bootstrap?.Offcanvas.getInstance(sidebar)?.hide();
}

document.addEventListener("click", event => {
    if (!(event.target instanceof Element)) return;
    const minimize = event.target.closest("[data-sidebar-minimize]");
    if (minimize && desktop.matches) {
        const sidebar = minimize.closest("#site-sidebar");
        if (!sidebar) return;
        const minimized = sidebar.classList.toggle("is-minimized");
        sidebar.closest(".app-shell")?.classList.toggle("sidebar-minimized", minimized);
        minimize.setAttribute("aria-expanded", String(!minimized));
        const label = minimized ? "Expand navigation" : "Minimize navigation";
        minimize.setAttribute("aria-label", label);
        minimize.title = label;
        return;
    }
    const group = event.target.closest("[data-sidebar-group]");
    if (group) {
        const panel = document.getElementById(group.getAttribute("aria-controls"));
        if (!panel) return;
        panel.hidden = !panel.hidden;
        group.setAttribute("aria-expanded", String(!panel.hidden));
        const label = `${panel.hidden ? "Expand" : "Collapse"} ${group.closest(".navigation-group").getAttribute("aria-label")}`;
        group.setAttribute("aria-label", label);
        group.title = label;
        return;
    }
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
