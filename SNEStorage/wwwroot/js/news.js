import { antiforgeryFetch } from "./antiforgery.js?v=1";

const requests = new Map();
const watchers = new Map();

export function createNews(key, dto) {
    return writeNews(key, "POST", "/api/news/create", dto);
}

export function editNews(key, newsId, dto) {
    return writeNews(key, "PUT", `/api/news/${encodeURIComponent(newsId)}/edit`, dto);
}

async function writeNews(key, method, url, dto) {
    const controller = new AbortController();
    requests.set(key, controller);
    const timeout = setTimeout(() => controller.abort(), 30000);
    try {
        const response = await antiforgeryFetch(url, {
            method, credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json", "Content-Type": "application/json" },
            body: JSON.stringify(dto), signal: controller.signal
        });
        if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        return { status: response.status, body: await response.text() };
    } catch (error) {
        if (controller.signal.aborted || error instanceof TypeError) return { status: 0, body: "" };
        throw error;
    } finally {
        clearTimeout(timeout);
        if (requests.get(key) === controller) requests.delete(key);
    }
}

export function readNews(key, amount, page, sort = "RecentFirst") {
    const query = new URLSearchParams({ maxAmount: String(amount), page: String(page), sort });
    return readNewsResponse(key, `/api/news?${query}`);
}

export function readNewsItem(key, newsId) {
    return readNewsResponse(key, `/api/news/${encodeURIComponent(newsId)}`);
}

async function readNewsResponse(key, url) {
    const controller = new AbortController();
    requests.get(key)?.abort();
    requests.set(key, controller);
    const timeout = setTimeout(() => controller.abort(), 30000);
    try {
        const response = await fetch(url, {
            method: "GET", credentials: "omit", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, signal: controller.signal
        });
        return JSON.stringify({ status: response.status, body: await response.text() });
    } catch (error) {
        if (controller.signal.aborted || error instanceof TypeError)
            return JSON.stringify({ status: 0, body: "" });
        throw error;
    } finally {
        clearTimeout(timeout);
        if (requests.get(key) === controller) requests.delete(key);
    }
}

export function observe(key, element, reference, enabled) {
    stopObserver(key);
    if (!enabled || !element || !reference) return;
    const state = { active: true, pending: false, observer: null, timer: null };
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
export function focusNews() {
    const hash = location.hash;
    if (/^#news-\d+$/.test(hash)) document.getElementById(hash.slice(1))?.scrollIntoView({ block: "start" });
}
export function dispose(key) {
    stopObserver(key);
    requests.get(key)?.abort();
    requests.delete(key);
}
