const pickers = new Map();

function dismiss(state) {
    if (state.dismissing || state.trigger.getAttribute("aria-expanded") !== "true") return;
    state.dismissing = true;
    state.reference.invokeMethodAsync("DismissAsync")
        .catch(error => {
            if (!state.events.signal.aborted) console.warn("Resource picker dismissal failed.", error);
        })
        .finally(() => { state.dismissing = false; });
}

export function initializePicker(id, reference) {
    disposePicker(id);
    const picker = document.getElementById(id);
    const trigger = picker?.querySelector("[data-picker-trigger]");
    const panel = picker?.querySelector(".picker-options");
    if (!picker || !trigger || !panel) throw new Error(`Resource picker ${id} was not rendered.`);
    const events = new AbortController();
    const options = { signal: events.signal };
    let hoverClose;
    const state = {
        picker, trigger, panel, reference, events, dismissing: false,
        cancelHoverClose: () => clearTimeout(hoverClose)
    };
    pickers.set(id, state);

    panel.addEventListener("pointerdown", event => {
        if (event.button !== 0 || !(event.target instanceof Element)) return;
        const label = event.target.closest("label");
        const control = label?.control;
        if (!label || !panel.contains(label) || !(control instanceof HTMLInputElement) ||
            control.type !== "checkbox" || control.disabled || event.target === control) return;

        // Focus the checkbox before a label click can blur the trigger to an outside element.
        // The native label click still toggles the checkbox and emits its change event.
        state.cancelHoverClose();
        event.preventDefault();
        control.focus({ preventScroll: true });
    }, options);
    picker.addEventListener("pointerenter", state.cancelHoverClose, options);
    picker.addEventListener("pointerleave", () => {
        if (picker.dataset.hover !== "true") return;
        hoverClose = setTimeout(() => {
            if (!picker.contains(document.activeElement)) dismiss(state);
        }, 150);
    }, options);
    document.addEventListener("pointerdown", event => {
        if (!picker.contains(event.target)) dismiss(state);
    }, options);
    picker.addEventListener("focusout", event => {
        if (event.relatedTarget && !picker.contains(event.relatedTarget)) dismiss(state);
    }, options);
    picker.addEventListener("resource-picker-dismiss", () => dismiss(state), options);
    picker.addEventListener("keydown", event => {
        if (event.key === "Escape" && trigger.getAttribute("aria-expanded") === "true") {
            event.preventDefault();
        }
    }, options);
}

export function syncPicker(id, focusSearch, restoreFocus) {
    const state = pickers.get(id);
    if (!state) return;
    state.cancelHoverClose();
    if (state.trigger.getAttribute("aria-expanded") === "true") {
        for (const other of pickers.values()) if (other !== state) dismiss(other);
        const rect = state.trigger.getBoundingClientRect();
        const below = window.innerHeight - rect.bottom;
        const above = below < 240 && rect.top > below;
        state.picker.dataset.placement = above ? "above" : "below";
        state.panel.style.maxHeight = `${Math.max(80, Math.min(360, (above ? rect.top : below) - 16))}px`;
        if (focusSearch) state.panel.querySelector('input[type="search"]')?.focus({ preventScroll: true });
    }
    if (restoreFocus) state.trigger.focus({ preventScroll: true });
}

export function disposePicker(id) {
    const state = pickers.get(id);
    if (!state) return;
    state.cancelHoverClose();
    state.events.abort();
    pickers.delete(id);
}
