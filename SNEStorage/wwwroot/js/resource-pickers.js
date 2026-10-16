// Browser-owned dropdown state. Form values and search results remain Blazor-owned.
const pickers = new Map();

function stateFor(picker) {
    let state = pickers.get(picker.id);
    if (state?.picker === picker) return state;
    if (state) clearTimeout(state.hoverClose);
    const trigger = picker.querySelector("[data-picker-trigger]");
    const panel = picker.querySelector(".picker-options");
    if (!trigger || !panel) return null;
    state = { picker, trigger, panel, hoverClose: 0 };
    pickers.set(picker.id, state);
    return state;
}

function place(state) {
    const rect = state.trigger.getBoundingClientRect();
    const below = window.innerHeight - rect.bottom;
    const above = below < 240 && rect.top > below;
    state.picker.dataset.placement = above ? "above" : "below";
    state.panel.style.maxHeight = `${Math.max(80, Math.min(360, (above ? rect.top : below) - 12))}px`;
}

function close(state, restoreFocus = false) {
    clearTimeout(state.hoverClose);
    state.picker.dataset.open = "false";
    state.picker.dataset.hover = "false";
    state.panel.hidden = true;
    state.trigger.setAttribute("aria-expanded", "false");
    if (restoreFocus) state.trigger.focus({ preventScroll: true });
}

function open(state, hover = false) {
    for (const other of pickers.values()) if (other !== state) close(other);
    clearTimeout(state.hoverClose);
    state.picker.dataset.open = "true";
    state.picker.dataset.hover = String(hover);
    state.panel.hidden = false;
    state.trigger.setAttribute("aria-expanded", "true");
    place(state);
    if (!hover && state.picker.dataset.focusSearch === "true")
        state.panel.querySelector('input[type="search"]')?.focus({ preventScroll: true });
}

export function closePicker(id, restoreFocus = false) {
    const picker = document.getElementById(id);
    const state = picker && stateFor(picker);
    if (state) close(state, restoreFocus);
}

document.addEventListener("click", event => {
    if (!(event.target instanceof Element)) return;
    const trigger = event.target.closest("[data-picker-trigger]");
    const picker = event.target.closest("[data-resource-picker]");
    if (!picker) return;
    const state = stateFor(picker);
    if (!state) return;
    if (trigger) {
        if (picker.dataset.open === "true" && picker.dataset.hover !== "true") close(state);
        else open(state);
    } else if (event.target.closest("[data-picker-close]")) close(state, true);
}, true);

document.addEventListener("pointerdown", event => {
    for (const state of pickers.values()) {
        if (state.picker.dataset.open === "true" && !state.picker.contains(event.target)) close(state);
    }
    if (event.button !== 0 || !(event.target instanceof Element)) return;
    const label = event.target.closest("[data-resource-picker] .picker-options label");
    const control = label?.control;
    if (!(control instanceof HTMLInputElement) || control.type !== "checkbox" ||
        control.disabled || event.target === control) return;
    // Keep label clicks inside the picker; the subsequent native click selects the checkbox.
    event.preventDefault();
    control.focus({ preventScroll: true });
}, true);

document.addEventListener("pointerover", event => {
    if (event.pointerType !== "mouse" || !(event.target instanceof Element)) return;
    const picker = event.target.closest("[data-resource-picker]");
    if (!picker) return;
    const state = stateFor(picker);
    if (!state) return;
    clearTimeout(state.hoverClose);
    if (event.target.closest("[data-picker-trigger]") && picker.dataset.openOnHover === "true" &&
        picker.dataset.open !== "true") open(state, true);
});

document.addEventListener("pointerout", event => {
    if (!(event.target instanceof Element)) return;
    const picker = event.target.closest("[data-resource-picker]");
    const state = picker && pickers.get(picker.id);
    if (!state || picker.contains(event.relatedTarget) || picker.dataset.hover !== "true") return;
    state.hoverClose = setTimeout(() => {
        if (!picker.contains(document.activeElement)) close(state);
    }, 120);
});

document.addEventListener("focusout", event => {
    if (!(event.target instanceof Element) || !event.relatedTarget) return;
    const picker = event.target.closest("[data-resource-picker]");
    const state = picker && pickers.get(picker.id);
    if (state && !picker.contains(event.relatedTarget)) close(state);
});

document.addEventListener("keydown", event => {
    if (!(event.target instanceof Element)) return;
    const picker = event.target.closest("[data-resource-picker]");
    const state = picker && stateFor(picker);
    if (!state) return;
    if (event.key === "Escape" && picker.dataset.open === "true") {
        event.preventDefault();
        close(state, true);
    } else if (event.key === "ArrowDown" && event.target === state.trigger) {
        event.preventDefault();
        open(state);
        state.panel.querySelector('input:not([disabled]), button:not([disabled]), summary')?.focus({ preventScroll: true });
    }
});

// Form tab changes also dismiss any picker inside the panel being hidden.
document.addEventListener("resource-picker-dismiss", event => {
    if (event.target instanceof Element) closePicker(event.target.id);
}, true);

let placementFrame;
function reposition() {
    if (placementFrame || ![...pickers.values()].some(state => state.picker.dataset.open === "true")) return;
    placementFrame = requestAnimationFrame(() => {
        placementFrame = 0;
        for (const state of pickers.values()) if (state.picker.dataset.open === "true") place(state);
    });
}
window.addEventListener("resize", reposition, { passive: true });
document.addEventListener("scroll", reposition, { capture: true, passive: true });
new MutationObserver(records => {
    if (!pickers.size || !records.some(record => record.removedNodes.length)) return;
    for (const [id, state] of pickers) {
        if (!state.picker.isConnected) {
            clearTimeout(state.hoverClose);
            pickers.delete(id);
        }
    }
}).observe(document.body, { childList: true, subtree: true });
