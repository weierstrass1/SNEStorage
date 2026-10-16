import { accountAvatarUrl } from "./profile.js?v=14";

const batchSize = 20;
let current;
const svgNamespace = "http://www.w3.org/2000/svg";

export function initialize(id) {
    dispose();
    const root = document.getElementById(id);
    if (!root) return;
    const find = name => root.querySelector(`[data-users-${name}]`);
    const state = { root, events: new AbortController(), controller: null, timer: null, generation: 0,
        query: "", page: 0, displayPage: 0, limit: 20, endAt: null, chunks: new Map(),
        ids: new Set(), loading: false, ended: true, error: false,
        input: find("input"), search: find("search"), results: find("results"), count: find("count"),
        loader: find("loading"), empty: find("empty"), emptyTitle: find("empty-title"),
        emptyDescription: find("empty-description"), errorPanel: find("error"), errorMessage: find("error-message"),
        more: find("more"), sentinel: find("sentinel"), pageSize: find("page-size"),
        pagination: find("pagination"), pageLabel: find("page-label"), previous: find("previous"), next: find("next") };
    current = state;
    const on = (element, name, handler) => element.addEventListener(name, handler, { signal: state.events.signal });
    on(state.input, "input", () => search(state, true));
    on(state.input, "keydown", event => { if (event.key === "Enter") { event.preventDefault(); search(state, false); } });
    on(state.search, "click", () => search(state, false));
    on(find("load-more"), "click", () => loadMore(state));
    on(find("retry"), "click", () => loadMore(state));
    on(state.pageSize, "change", () => {
        state.limit = Number(state.pageSize.value);
        search(state, false);
    });
    on(state.previous, "click", () => changePage(state, -1));
    on(state.next, "click", () => changePage(state, 1));
    on(window, "pagehide", () => dispose(id));
    if ("IntersectionObserver" in window) {
        state.observer = new IntersectionObserver(entries => {
            if (entries.some(entry => entry.isIntersecting) && !state.error) loadMore(state);
        }, { rootMargin: "0px 0px 250px 0px" });
    }
    state.cleanup = new MutationObserver(() => { if (!root.isConnected) dispose(id); });
    state.cleanup.observe(document.body, { childList: true, subtree: true });
    state.search.disabled = false;
    search(state, false);
}

function search(state, debounce) {
    clearTimeout(state.timer);
    state.controller?.abort();
    state.generation++;
    state.query = state.input.value.trim();
    state.displayPage = 0;
    state.endAt = null;
    state.chunks.clear();
    resetResults(state);
    if (state.ended) return;
    state.timer = setTimeout(() => loadMore(state), debounce ? 350 : 0);
}

function resetResults(state) {
    state.page = Math.floor(pageStart(state) / batchSize);
    state.ids.clear();
    state.loading = false;
    state.error = false;
    // Empty lists everyone; one letter waits for a second character before filtering.
    state.ended = state.query.length === 1;
    state.results.replaceChildren();
    state.results.setAttribute("aria-busy", "false");
    state.count.textContent = "";
    state.loader.hidden = state.more.hidden = state.errorPanel.hidden = true;
    state.observer?.disconnect();
    state.empty.hidden = !state.ended;
    state.emptyTitle.textContent = "Keep typing";
    state.emptyDescription.textContent = "Enter at least 2 characters to search, or clear the field to browse all users.";
    updatePagination(state);
}

function pageStart(state) { return state.limit ? state.displayPage * state.limit : 0; }
function pageComplete(state) { return state.limit > 0 && state.page * batchSize >= pageStart(state) + state.limit; }
function canNext(state) {
    return pageComplete(state) && (state.endAt === null || pageStart(state) + state.limit < state.endAt);
}
function updatePagination(state) {
    state.pagination.hidden = !state.limit || state.query.length === 1;
    state.pageLabel.textContent = `Page ${state.displayPage + 1}`;
    state.previous.disabled = state.loading || state.displayPage === 0;
    state.next.disabled = state.loading || state.error || !canNext(state);
}
function changePage(state, direction) {
    if (state.loading || direction < 0 && state.displayPage === 0 || direction > 0 && !canNext(state)) return;
    clearTimeout(state.timer);
    state.controller?.abort();
    state.generation++;
    state.displayPage += direction;
    resetResults(state);
    void loadMore(state);
    state.root.querySelector(".users-toolbar").scrollIntoView({ block: "start", behavior: "auto" });
}

async function loadMore(state) {
    if (current !== state || state.loading || state.ended || pageComplete(state) || !state.root.isConnected) return;
    state.observer?.disconnect();
    state.loading = true;
    state.error = false;
    state.loader.hidden = false;
    state.more.hidden = state.errorPanel.hidden = state.empty.hidden = true;
    state.results.setAttribute("aria-busy", "true");
    updatePagination(state);
    const generation = state.generation, controller = new AbortController();
    state.controller = controller;
    const timer = setTimeout(() => controller.abort(), 30000);
    try {
        let users = state.chunks.get(state.page);
        if (!users) {
            const query = new URLSearchParams({ likeString: state.query, maxAmount: String(batchSize),
                page: String(state.page), withAvatar: "true" });
            const response = await fetch(`/api/user/search?${query}`, {
                credentials: "same-origin", cache: "no-store", redirect: "error", signal: controller.signal,
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error(response.status === 429 ? "Too many searches. Wait a moment and try again."
                : "Users could not be loaded. Please try again.");
            users = await response.json();
        }
        if (current !== state || generation !== state.generation) return;
        if (!Array.isArray(users) || users.length > batchSize || users.some(user => typeof user?.id !== "string" || !user.id
            || typeof user.username !== "string" || !user.username.trim()))
            throw new Error("The user list could not be read. Please try again.");
        state.chunks.set(state.page, users);
        const offset = state.page * batchSize, start = pageStart(state);
        if (users.length < batchSize) state.endAt = offset + users.length;
        // A logical page of 50 can end halfway through a batch of 20.
        // Retain the batch so the following page starts at user 51, without omissions.
        const visible = users.slice(Math.max(0, start - offset), state.limit ? Math.max(0, start + state.limit - offset) : undefined);
        const newUsers = visible.filter(user => !state.ids.has(user.id));
        if (visible.length && !newUsers.length && state.ids.size > 0) {
            console.warn("User search pagination did not advance. Check page support on /api/user/search.");
            throw new Error("More results could not be loaded. Please try again later.");
        }
        appendUsers(state, newUsers);
        state.page++;
        state.ended = users.length < batchSize;
        state.count.textContent = state.ids.size
            ? `Showing ${start + 1}–${start + state.ids.size}${state.endAt === null ? "" : ` of ${state.endAt}`} users.` : "";
        if (!state.ids.size) {
            state.emptyTitle.textContent = state.displayPage ? "No more users" : state.query ? "No users found" : "No users yet";
            state.emptyDescription.textContent = state.displayPage ? "Go back to the previous page to see the matches already found."
                : state.query ? "Try another username or a shorter search." : "Community members will appear here once they join SNEStorage.";
            state.empty.hidden = false;
        }
    } catch (error) {
        if (current !== state || generation !== state.generation) return;
        state.error = true;
        state.errorPanel.hidden = false;
        state.errorMessage.textContent = error.name === "AbortError" ? "The search took too long. Please try again."
            : error instanceof TypeError ? "Could not connect. Check your connection and try again."
            : error instanceof SyntaxError ? "The user list could not be read. Please try again." : error.message;
    } finally {
        clearTimeout(timer);
        if (current === state && generation === state.generation) {
            state.loading = false;
            state.controller = null;
            state.loader.hidden = true;
            state.results.setAttribute("aria-busy", "false");
            state.more.hidden = state.error || state.ended || pageComplete(state);
            updatePagination(state);
            if (!state.error && !state.ended && !pageComplete(state)) state.observer?.observe(state.sentinel);
        }
    }
}

function appendUsers(state, users) {
    const fragment = document.createDocumentFragment();
    for (const user of users) {
        if (state.ids.has(user.id)) continue;
        const item = document.createElement("li"), link = document.createElement("a"), name = document.createElement("strong");
        link.className = "user-link";
        link.href = `/Profile/${encodeURIComponent(user.id)}`;
        const icon = document.createElement("span");
        icon.className = "user-avatar-frame"; icon.setAttribute("aria-hidden", "true");
        icon.append(userIcon());
        const avatarUrl = typeof user.avatar?.url === "string" ? accountAvatarUrl(user.avatar) : "";
        if (avatarUrl) {
            const avatar = document.createElement("img");
            avatar.className = "user-avatar";
            avatar.width = avatar.height = 36;
            avatar.alt = "";
            avatar.loading = "lazy";
            avatar.decoding = "async";
            avatar.addEventListener("error", () => icon.replaceChildren(userIcon()), { once: true });
            avatar.src = avatarUrl;
            icon.replaceChildren(avatar);
        }
        name.textContent = user.username;
        name.style.color = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(user.usernameColor ?? "") ? user.usernameColor : "#e6d2fb";
        const arrow = document.createElementNS(svgNamespace, "svg"), path = document.createElementNS(svgNamespace, "path");
        arrow.classList.add("user-arrow"); arrow.setAttribute("viewBox", "0 0 24 24"); arrow.setAttribute("aria-hidden", "true");
        path.setAttribute("d", "M5 12h14m-6-6 6 6-6 6"); arrow.append(path);
        link.append(icon, name, arrow); item.append(link); fragment.append(item);
        state.ids.add(user.id);
    }
    state.results.append(fragment);
}

function userIcon() {
    const icon = document.createElementNS(svgNamespace, "svg"), circle = document.createElementNS(svgNamespace, "circle"), path = document.createElementNS(svgNamespace, "path");
    icon.setAttribute("viewBox", "0 0 24 24");
    circle.setAttribute("cx", "12"); circle.setAttribute("cy", "7"); circle.setAttribute("r", "4");
    path.setAttribute("d", "M4 21v-2a8 8 0 0 1 16 0v2");
    icon.append(circle, path);
    return icon;
}

export function dispose(id) {
    const state = current;
    if (!state || id && state.root.id !== id) return;
    current = null;
    state.generation++;
    clearTimeout(state.timer);
    state.controller?.abort();
    state.events.abort();
    state.observer?.disconnect();
    state.cleanup.disconnect();
}
