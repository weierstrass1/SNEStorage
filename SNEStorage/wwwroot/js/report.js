import { getReportReasons, getReportContext, submitReport } from "./report-api.js?v=2";

let host;
const booleanValue = value => value === "true" ? true : value === "false" ? false : null;
const svgNamespace = "http://www.w3.org/2000/svg";

function closeMenus(except) {
    document.querySelectorAll("details[data-report-menu][open]").forEach(menu => {
        if (menu !== except) menu.open = false;
    });
}

export function initialize() {
    const dialog = document.getElementById("report-dialog");
    if (!(dialog instanceof HTMLDialogElement) || host?.dialog === dialog) return;
    dispose();
    const events = new AbortController();
    const find = name => dialog.querySelector(`[data-report-${name}]`);
    host = { dialog, events, active: null, categories: find("categories"), heading: find("heading"), description: find("description"),
        status: find("status"), error: find("error"), retry: find("retry"),
        submit: find("submit"), cancel: find("cancel") };
    const on = (element, name, callback) => element.addEventListener(name, callback, { signal: events.signal });
    on(document, "click", event => {
        if (!(event.target instanceof Element)) return;
        const button = event.target.closest("[data-report-trigger]");
        const menu = event.target.closest("details[data-report-menu]");
        closeMenus(menu);
        if (button instanceof HTMLButtonElement) { event.preventDefault(); open(button); }
    });
    on(document, "keydown", event => {
        if (event.key !== "Escape" || host?.dialog.open) return;
        const menu = event.target instanceof Element ? event.target.closest("details[data-report-menu][open]") : null;
        if (menu) { menu.open = false; menu.querySelector("summary")?.focus(); }
    });
    on(find("close"), "click", close);
    on(host.cancel, "click", close);
    on(dialog, "cancel", event => { event.preventDefault(); close(); });
    on(dialog, "close", () => { if (host?.active && !dialog.open) close(); });
    on(host.retry, "click", () => { if (host?.active) load(host.active); });
    on(host.submit, "click", send);
    on(host.categories, "change", event => {
        if (!(event.target instanceof HTMLInputElement) || !host?.active || host.active.busy) return;
        host.active.selected = Number(event.target.value);
        host.submit.disabled = !host.active.reasons.some(reason => reason.id === host.active.selected);
        host.error.hidden = true;
    });
    on(window, "pagehide", close);
    host.observer = new MutationObserver(() => {
        if (host?.active && !host.active.source.isConnected) close();
    });
    host.observer.observe(document.body, { childList: true, subtree: true });
}

function open(source) {
    if (!host || host.dialog.open) return;
    const id = Number(source.dataset.reportId), context = source.dataset.reportContext;
    if (!Number.isSafeInteger(id) || id <= 0 || !["resource", "comment"].includes(context) || !source.dataset.reportUser) return;
    const active = { id, context, userId: source.dataset.reportUser, source,
        hasPreviews: booleanValue(source.dataset.reportPreviews), hasVersions: booleanValue(source.dataset.reportVersions),
        previousFocus: source.closest("details")?.querySelector("summary"), selected: null,
        reasons: [], busy: false, completed: false, controller: new AbortController() };
    closeMenus();
    host.active = active;
    host.heading.textContent = `Report this ${context}`;
    host.description.textContent = "Choose the reason that best describes the issue. SNEStorage staff will review your report.";
    host.cancel.textContent = "Cancel";
    host.submit.textContent = "Submit report";
    host.categories.hidden = host.submit.hidden = false;
    host.status.classList.remove("is-success", "is-notice");
    host.dialog.showModal();
    load(active);
}

async function load(active) {
    if (host?.active !== active || active.busy) return;
    active.selected = null;
    host.categories.replaceChildren();
    host.categories.setAttribute("aria-busy", "true");
    host.submit.disabled = true;
    host.error.hidden = host.retry.hidden = true;
    host.status.textContent = "Loading report reasons…";
    const timer = setTimeout(() => active.controller.abort(), 30000);
    try {
        const [catalog, context] = await Promise.all([
            getReportReasons(active.controller.signal), getReportContext(active, active.controller.signal)
        ]);
        if (host?.active !== active) return;
        active.reasons = catalog.filter(reason => reason.contexts.includes(active.context)
            && (!reason.requiresPreviews || context.hasPreviews === true)
            && (!reason.requiresNoVersions || context.hasVersions === false));
        renderReasons(active.reasons);
        host.status.textContent = active.reasons.length ? "Open a category and select one reason."
            : "There are no applicable reporting reasons available for this item.";
        host.retry.hidden = active.reasons.length > 0;
    } catch (error) {
        if (host?.active !== active) return;
        showError(error.name === "AbortError" ? "Loading took too long. Please try again."
            : error instanceof TypeError ? "Could not load report reasons. Check your connection and try again." : error.message);
        active.controller.abort();
        active.controller = new AbortController();
        host.retry.hidden = false;
        host.status.textContent = "Your report has not been sent.";
    } finally {
        clearTimeout(timer);
        if (host?.active === active) host.categories.setAttribute("aria-busy", "false");
    }
}

function renderReasons(reasons) {
    const groups = new Map();
    for (const reason of reasons) {
        if (!groups.has(reason.category)) groups.set(reason.category, []);
        groups.get(reason.category).push(reason);
    }
    const fragment = document.createDocumentFragment();
    for (const [category, items] of [...groups].sort(([a], [b]) => a.localeCompare(b))) {
        const details = document.createElement("details");
        details.className = "report-category";
        const summary = document.createElement("summary"), title = document.createElement("span");
        title.textContent = category;
        const chevron = document.createElementNS(svgNamespace, "svg"), path = document.createElementNS(svgNamespace, "path");
        chevron.setAttribute("viewBox", "0 0 24 24");
        chevron.setAttribute("aria-hidden", "true");
        path.setAttribute("d", "m6 9 6 6 6-6");
        chevron.append(path); summary.append(title, chevron);
        const list = document.createElement("div");
        list.className = "report-reasons";
        for (const reason of items.sort((a, b) => b.priority - a.priority || a.description.localeCompare(b.description))) {
            const label = document.createElement("label"), radio = document.createElement("input"), text = document.createElement("span");
            label.className = "report-reason";
            radio.type = "radio"; radio.name = "report-reason"; radio.value = String(reason.id);
            text.textContent = reason.description;
            label.append(radio, text); list.append(label);
        }
        details.append(summary, list); fragment.append(details);
    }
    host.categories.replaceChildren(fragment);
}

async function send() {
    const active = host?.active;
    if (!active || active.busy || active.completed || !active.reasons.some(reason => reason.id === active.selected)) return;
    active.busy = true;
    host.submit.disabled = true;
    host.submit.textContent = "Sending…";
    host.error.hidden = true;
    host.status.textContent = "Complete the verification to send your report.";
    host.categories.querySelectorAll("input").forEach(input => input.disabled = true);
    const timer = setTimeout(() => active.controller.abort(), 240000);
    try {
        await submitReport(active, active.selected, active.controller.signal);
        if (host?.active === active) complete("Thank you. Your report has been sent to SNEStorage staff.");
    } catch (error) {
        if (host?.active !== active) return;
        if (error.code === "RepeatedFlag") { complete(error.message, true); return; }
        const uncertain = error.uncertain || error instanceof TypeError || error.name === "AbortError";
        showError(uncertain ? "We could not confirm whether the report was received. Please do not send it again immediately." : error.message);
        active.completed = uncertain;
        host.status.textContent = uncertain ? "Close this window and check again later." : "Your selected reason has been kept.";
        if (error.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
    } finally {
        clearTimeout(timer);
        if (host?.active === active) {
            active.busy = false;
            host.submit.textContent = "Submit report";
            host.submit.disabled = active.completed;
            host.categories.querySelectorAll("input").forEach(input => input.disabled = active.completed);
        }
    }
}

function complete(message, repeated = false) {
    host.active.completed = true;
    host.heading.textContent = repeated ? "Already reported" : "Report sent";
    host.description.textContent = repeated ? "Your earlier report is already on record." : "SNEStorage staff will review your report.";
    host.categories.hidden = host.submit.hidden = host.retry.hidden = true;
    host.cancel.textContent = "Close";
    host.status.textContent = message;
    host.status.classList.toggle("is-success", !repeated);
    host.status.classList.toggle("is-notice", repeated);
    host.status.focus({ preventScroll: true });
}

function showError(message) { host.error.textContent = message; host.error.hidden = false; }

export function close() {
    const active = host?.active;
    if (!host) return;
    host.active = null;
    active?.controller.abort();
    if (host.dialog.open) host.dialog.close();
    if (active?.previousFocus?.isConnected) active.previousFocus.focus({ preventScroll: true });
}

export function dispose() {
    if (!host) return;
    close();
    host.events.abort();
    host.observer.disconnect();
    host = undefined;
}
