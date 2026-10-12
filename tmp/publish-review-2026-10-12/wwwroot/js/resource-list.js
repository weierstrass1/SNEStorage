import { getProfile, cancelProfileRequest } from "./profile.js?v=10";

async function read(url, key) {
    let timedOut = false;
    const timeout = setTimeout(() => { timedOut = true; cancelProfileRequest(key); }, 30000);
    try {
        const result = await getProfile(url, key, false);
        return timedOut ? JSON.stringify({ status: 504, body: "", cancelled: false }) : result;
    } finally { clearTimeout(timeout); }
}

function findUrl(filter, authorLike, amount, page) {
    const query = new URLSearchParams();
    function append(name, value) {
        if (value == null || value === "") return;
        if (Array.isArray(value)) value.forEach(item => append(name, item));
        else if (typeof value === "object") Object.entries(value).forEach(([child, item]) => append(`${name}.${child}`, item));
        else query.append(name, String(value));
    }
    Object.entries(filter).forEach(([name, value]) => append(name, value));
    if (authorLike?.trim()) query.set("AuthorLike", authorLike.trim());
    query.set("maxAmount", String(amount));
    query.set("page", String(page));
    return `/api/resource/find?${query}`;
}
export function findResources(key, filter, authorLike, amount, page) {
    return read(findUrl(filter, authorLike, amount, page), `${key}-list`);
}

const publicRequests = new Map();
export function cancelPublicList(key) {
    publicRequests.get(key)?.abort();
    publicRequests.delete(key);
}
export async function findPublicResources(key, filter, amount, page) {
    cancelPublicList(key);
    const controller = new AbortController();
    publicRequests.set(key, controller);
    let timedOut = false;
    const timeout = setTimeout(() => { timedOut = true; controller.abort(); }, 30000);
    try {
        // This public feed must have the same visibility for visitors, authors and staff.
        const response = await fetch(findUrl(filter, null, amount, page), {
            method: "GET", credentials: "omit", cache: "no-store", redirect: "manual",
            headers: { Accept: "application/json" }, signal: controller.signal
        });
        return JSON.stringify({ status: response.status, body: await response.text(), cancelled: false });
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError") {
            return JSON.stringify({ status: timedOut ? 504 : 499, body: "", cancelled: !timedOut });
        }
        if (error instanceof TypeError) return JSON.stringify({ status: 0, body: "", cancelled: false });
        throw error;
    } finally {
        clearTimeout(timeout);
        if (publicRequests.get(key) === controller) publicRequests.delete(key);
    }
}
export function getOptions(key) { return read("/api/resource/options", `${key}-options`); }
export function cancelList(key) { cancelProfileRequest(`${key}-list`); }

const watchers = new Map();
export function observe(key, element, reference, enabled) {
    stopObserver(key);
    if (!enabled || !element) return;
    const state = { pending: false, active: true, observer: null, timer: null };
    const check = () => {
        if (!state.active || state.pending || element.getBoundingClientRect().top > innerHeight + 240) return;
        state.pending = true;
        reference.invokeMethodAsync("LoadMoreAsync").catch(() => {}).finally(() => { state.pending = false; });
    };
    state.observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) check();
    }, { rootMargin: "240px" });
    state.observer.observe(element);
    state.timer = setTimeout(check, 0);
    watchers.set(key, state);
}
function stopObserver(key) {
    const state = watchers.get(key);
    if (!state) return;
    state.active = false;
    clearTimeout(state.timer);
    state.observer.disconnect();
    watchers.delete(key);
}
export function dispose(key) {
    stopObserver(key);
    cancelList(key);
    cancelProfileRequest(`${key}-options`);
}
export function focusResults(id) {
    document.getElementById(id)?.scrollIntoView({ block: "start", behavior: "auto" });
}
