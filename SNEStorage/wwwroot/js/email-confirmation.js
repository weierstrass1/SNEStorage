import { antiforgeryFetch } from "./antiforgery.js?v=1";

const requests = new Map();

export async function confirmEmail(key, url) {
    cancelConfirmation(key);
    const controller = new AbortController();
    requests.set(key, controller);
    const timeout = setTimeout(() => controller.abort(), 30000);
    try {
        const response = await antiforgeryFetch(url, {
            method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, signal: controller.signal
        });
        return response.headers.get("Content-Type")?.includes("text/html") ? 502 : response.status;
    } catch (error) {
        if (controller.signal.aborted || error instanceof TypeError) return 0;
        throw error;
    } finally {
        clearTimeout(timeout);
        if (requests.get(key) === controller) requests.delete(key);
    }
}

export function cancelConfirmation(key) {
    requests.get(key)?.abort();
    requests.delete(key);
}
