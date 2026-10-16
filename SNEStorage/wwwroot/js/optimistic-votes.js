// Immediate visual feedback only. Blazor retains the API calls, permissions,
// response validation and rollback. Never retry a mutation from this module.
const pending = new WeakMap();
const selections = new WeakMap();
const rootSelector = "[data-vote-feedback]";
const number = new Intl.NumberFormat("en-US", { maximumFractionDigits: 1 });
const integer = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

// If a small vote POST fails after its component has gone, the user still sees
// the error in the persistent layout. A successful background save stays quiet.
export function trackBackgroundVote(kind, id, value) {
    const prefix = `${kind}:${id}:`;
    const origin = [...document.querySelectorAll(rootSelector)].find(root => read(root)?.context?.startsWith(prefix));
    const address = kind === "news" ? `/News/${encodeURIComponent(id)}` : location.pathname + location.search + location.hash;
    return (status, body = "") => {
        if (origin?.isConnected || status >= 200 && status < 300 ||
            value === "Remove" && status === 404 && String(body).includes("LikeNotFound")) return;
        const notice = document.getElementById("vote-background-notice");
        if (!notice) return;
        notice.querySelector("[data-vote-notice-message]").textContent = status === 0 || status === 499 || status >= 500
            ? "We could not confirm your vote. Open its page to check whether it was saved."
            : "Your vote could not be saved. Open its page to check it and try again.";
        notice.querySelector("[data-vote-notice-link]").setAttribute("href", address);
        notice.hidden = false;
    };
}

document.addEventListener("click", event => {
    if (event.target instanceof Element && event.target.closest("[data-vote-notice-dismiss]"))
        document.getElementById("vote-background-notice")?.setAttribute("hidden", "");
});

function read(root) {
    try { return JSON.parse(root.dataset.voteState); } catch { return null; }
}

// A comment can contain replies, each with its own controls.
function elements(root, selector) {
    return [...root.querySelectorAll(selector)].filter(element => element.closest(rootSelector) === root);
}

function text(root, selector, value) {
    for (const element of elements(root, selector)) {
        if (element.textContent === value) continue;
        if (element.childNodes.length === 1 && element.firstChild.nodeType === Node.TEXT_NODE)
            element.firstChild.data = value;
        else element.textContent = value;
    }
}

function paintChoices(root, selected, hovered = selected) {
    for (const button of elements(root, "[data-rating-choice]")) {
        const value = Number(button.dataset.ratingChoice);
        button.classList.toggle("is-filled", value <= hovered);
        button.setAttribute("aria-pressed", String(value === selected));
    }
}

function paint(root, state) {
    if (state.mode === "reaction") {
        const total = state.likes + state.dislikes;
        for (const element of elements(root, "[data-vote-counts]")) element.hidden = total === 0;
        text(root, "[data-vote-likes]", `${state.likes} ${state.likes === 1 ? "like" : "likes"}`);
        text(root, "[data-vote-dislikes]", `${state.dislikes} ${state.dislikes === 1 ? "dislike" : "dislikes"}`);
        const approval = total ? 100 * state.likes / total : 0;
        text(root, "[data-vote-approval]", `${state.decimals === 0 ? Math.round(approval) : number.format(approval)}% approval`);
        for (const button of elements(root, "[data-vote-command]")) {
            const selected = button.dataset.voteCommand === state.vote;
            button.classList.toggle("is-liked", selected && state.vote === "Like");
            button.classList.toggle("is-disliked", selected && state.vote === "Dislike");
            button.setAttribute("aria-pressed", String(selected));
            const action = button.dataset.voteCommand.toLowerCase();
            const label = selected ? `Remove your ${action}` : `${action === "like" ? "Like" : "Dislike"} this ${state.context.startsWith("news:") ? "announcement" : "comment"}`;
            button.setAttribute("aria-label", label);
            if (button.hasAttribute("title")) button.title = label;
        }
    } else if (state.mode === "rating") {
        const average = Math.max(0, Math.min(5, state.average));
        for (const element of elements(root, "[data-vote-fill]")) element.style.width = `${average / 5 * 100}%`;
        for (const element of elements(root, "[data-vote-stars]")) element.setAttribute("aria-label", `${number.format(average)} out of 5 stars`);
        text(root, "[data-vote-average]", `${number.format(average)} / 5`);
        text(root, "[data-vote-total]", state.count ? `${integer.format(state.count)} ${state.count === 1 ? "vote" : "votes"}` : "Not rated");
        paintChoices(root, selections.get(root)?.value ?? state.selected);
    }
}

document.addEventListener("click", event => {
    const button = event.target instanceof Element ? event.target.closest("[data-vote-command], [data-rating-choice]") : null;
    const root = button?.closest(rootSelector);
    if (!(button instanceof HTMLButtonElement) || !root || button.disabled) return;
    if (pending.has(root) || root.dataset.voteBusy === "true") {
        event.preventDefault();
        event.stopImmediatePropagation();
        return;
    }
    const state = read(root);
    if (!state) return;
    if (button.hasAttribute("data-rating-choice")) {
        const selected = Number(button.dataset.ratingChoice);
        selections.set(root, { context: state.context, value: selected });
        paintChoices(root, selected);
        if (root.dataset.voteEnabled === "true")
            for (const save of elements(root, '[data-vote-command="save"]')) save.disabled = false;
        return; // Let Blazor receive the selection, too.
    }
    const preview = { ...state };
    if (state.mode === "reaction") {
        const value = state.vote === button.dataset.voteCommand ? "Remove" : button.dataset.voteCommand;
        preview.likes = Math.max(0, state.likes - (state.vote === "Like" ? 1 : 0) + (value === "Like" ? 1 : 0));
        preview.dislikes = Math.max(0, state.dislikes - (state.vote === "Dislike" ? 1 : 0) + (value === "Dislike" ? 1 : 0));
        preview.vote = value;
    } else if (state.mode === "rating") {
        const selected = selections.get(root)?.value ?? state.selected;
        if (root.dataset.voteEnabled !== "true" || selected < 1 || selected > 5) return;
        const hadRating = state.myRating >= 1 && state.myRating <= 5;
        preview.count = state.count + (hadRating ? 0 : 1);
        preview.average = (state.average * state.count - (hadRating ? state.myRating : 0) + selected) / Math.max(1, preview.count);
        preview.myRating = preview.selected = selected;
        selections.delete(root);
    } else return;
    pending.set(root, { preview, acknowledged: false });
    paint(root, preview);
    for (const control of elements(root, "[data-vote-command], [data-rating-choice]"))
        control.setAttribute("aria-disabled", "true");
    // No disabled attribute here: the original click must reach Blazor.
}, true);

document.addEventListener("pointerover", event => {
    const button = event.target instanceof Element ? event.target.closest("[data-rating-choice]") : null;
    const root = button?.closest(rootSelector);
    if (!root || button.disabled || pending.has(root)) return;
    const state = read(root);
    if (state) paintChoices(root, selections.get(root)?.value ?? state.selected, Number(button.dataset.ratingChoice));
});

document.addEventListener("pointerout", event => {
    const choices = event.target instanceof Element ? event.target.closest("[data-rating-choices]") : null;
    if (!choices || event.relatedTarget instanceof Node && choices.contains(event.relatedTarget)) return;
    const root = choices.closest(rootSelector);
    const state = root && read(root);
    if (state) paintChoices(root, selections.get(root)?.value ?? state.selected);
});

new MutationObserver(records => {
    const roots = new Set(records.map(record => record.target).filter(element => element.matches(rootSelector)));
    for (const root of roots) {
        const selection = selections.get(root);
        const active = pending.get(root);
        // Blazor already rendered confirmed counts/buttons. Only reconcile a
        // locally previewed selection or a vote that is actually pending.
        if (!active && !selection) continue;
        const state = read(root);
        if (!state) continue;
        if (selection && (selection.context !== state.context || selection.value === state.selected)) selections.delete(root);
        if (active) {
            // A complete save may arrive in a single render batch; oldValue still records its busy state.
            active.acknowledged ||= root.dataset.voteBusy === "true" || records.some(record =>
                record.target === root && record.attributeName === "data-vote-busy" && record.oldValue === "true");
            if (active.preview.context !== state.context || active.acknowledged && root.dataset.voteBusy !== "true") {
                pending.delete(root);
                for (const button of elements(root, "[data-vote-command], [data-rating-choice]")) button.removeAttribute("aria-disabled");
            } else { paint(root, active.preview); continue; }
        }
        paint(root, state);
    }
}).observe(document.documentElement, {
    subtree: true, attributes: true, attributeOldValue: true,
    attributeFilter: ["data-vote-state", "data-vote-busy"]
});
