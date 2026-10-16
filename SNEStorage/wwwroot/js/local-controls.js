// Only presentation state lives here. Data, permissions and submissions stay in their owners.
function owned(root, selector) {
    return [...root.querySelectorAll(selector)].filter(node => node.closest("[data-ui-tabs]") === root);
}

export function selectLocalTab(root, index, focus = false) {
    if (!root) return;
    const tabs = owned(root, "[data-ui-tab]");
    const selected = tabs.find(tab => Number(tab.dataset.uiTab) === index);
    if (!selected) return;
    for (const tab of tabs) {
        const active = tab === selected;
        tab.setAttribute("aria-selected", String(active));
        tab.tabIndex = active ? 0 : -1;
    }
    for (const panel of owned(root, "[data-ui-panel]")) {
        const hidden = Number(panel.dataset.uiPanel) !== index;
        panel.hidden = hidden;
        if (!hidden) continue;
        for (const video of panel.querySelectorAll("video")) video.pause();
        for (const picker of panel.querySelectorAll("[data-resource-picker]"))
            picker.dispatchEvent(new Event("resource-picker-dismiss"));
    }
    for (const progress of owned(root, "[data-ui-progress]")) progress.textContent = String(index + 1);
    for (const step of owned(root, "[data-ui-step]"))
        step.hidden = Number(step.dataset.uiStep) < 0 ? index === 0 : selected === tabs.at(-1);
    for (const submit of owned(root, "[data-ui-last]")) submit.hidden = selected !== tabs.at(-1);
    const list = selected.closest('[role="tablist"]');
    if (list) {
        const left = selected.offsetLeft - list.offsetLeft;
        if (left < list.scrollLeft) list.scrollLeft = left;
        else if (left + selected.offsetWidth > list.scrollLeft + list.clientWidth)
            list.scrollLeft = left + selected.offsetWidth - list.clientWidth;
    }
    if (focus) selected.focus({ preventScroll: true });
}

document.addEventListener("click", event => {
    if (!(event.target instanceof Element)) return;
    const valuesToggle = event.target.closest("[data-ui-values-toggle]");
    if (valuesToggle) {
        const expanded = valuesToggle.getAttribute("aria-expanded") !== "true";
        const list = valuesToggle.closest("[data-ui-values]");
        for (const item of list.querySelectorAll('[data-ui-values-extra="true"]')) item.hidden = !expanded;
        valuesToggle.setAttribute("aria-expanded", String(expanded));
        return;
    }
    const passwordToggle = event.target.closest("[data-ui-password-toggle]");
    if (passwordToggle) {
        const reveal = passwordToggle.getAttribute("aria-pressed") !== "true";
        for (const input of passwordToggle.closest("form").querySelectorAll("[data-ui-password]"))
            input.type = reveal ? "text" : "password";
        passwordToggle.setAttribute("aria-pressed", String(reveal));
        passwordToggle.textContent = reveal ? "Hide passwords" : "Show passwords";
        return;
    }
    const toggle = event.target.closest("[data-ui-toggle]");
    if (toggle) {
        const panel = document.getElementById(toggle.getAttribute("aria-controls"));
        if (panel) {
            panel.hidden = !panel.hidden;
            toggle.setAttribute("aria-expanded", String(!panel.hidden));
        }
        return;
    }
    const tab = event.target.closest("[data-ui-tab]");
    if (tab instanceof HTMLButtonElement && !tab.disabled) {
        selectLocalTab(tab.closest("[data-ui-tabs]"), Number(tab.dataset.uiTab));
        return;
    }
    const step = event.target.closest("[data-ui-step]");
    if (!(step instanceof HTMLButtonElement) || step.disabled) return;
    const root = step.closest("[data-ui-tabs]");
    const tabs = root && owned(root, "[data-ui-tab]");
    const index = tabs?.findIndex(tab => tab.getAttribute("aria-selected") === "true") ?? -1;
    // Click the actual tab so an optional data-loading handler also runs once selected.
    tabs?.[index + Number(step.dataset.uiStep)]?.click();
}, true);

document.addEventListener("keydown", event => {
    if (!(event.target instanceof Element) || event.altKey || event.ctrlKey || event.metaKey) return;
    const tab = event.target.closest("[data-ui-tab]");
    const root = tab?.closest("[data-ui-tabs]");
    if (!root) return;
    const tabs = owned(root, "[data-ui-tab]");
    const index = tabs.indexOf(tab);
    const next = event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1 :
        event.key === "ArrowRight" ? (index + 1) % tabs.length :
        event.key === "ArrowLeft" ? (index + tabs.length - 1) % tabs.length : -1;
    if (next < 0) return;
    event.preventDefault();
    tabs[next].focus({ preventScroll: true });
    tabs[next].click();
});

function closeDetails(details, focus = false) {
    details.open = false;
    if (focus) details.querySelector(":scope > summary")?.focus({ preventScroll: true });
}

document.addEventListener("change", event => {
    if (event.target instanceof Element && event.target.matches("[data-ui-close-details]")) {
        const details = event.target.closest("[data-ui-dismiss-details]");
        if (details) closeDetails(details, true);
    }
}, true);

document.addEventListener("pointerdown", event => {
    for (const details of document.querySelectorAll("[data-ui-dismiss-details][open]"))
        if (!details.contains(event.target)) closeDetails(details);
});

document.addEventListener("keydown", event => {
    if (event.key !== "Escape" || !(event.target instanceof Element)) return;
    const details = event.target.closest("[data-ui-dismiss-details][open]");
    if (details) { event.preventDefault(); closeDetails(details, true); }
});
