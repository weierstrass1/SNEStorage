import { antiforgeryFetch } from "./antiforgery.js?v=1";

const requests = new Map();

async function request(key, url, options = {}) {
    requests.get(key)?.abort();
    const controller = new AbortController();
    requests.set(key, controller);
    const timer = setTimeout(() => controller.abort(), 30000);
    try {
        const response = await antiforgeryFetch(url, {
            ...options, credentials: "same-origin", cache: "no-store", redirect: "error",
            signal: controller.signal,
            headers: { Accept: "application/json", ...options.headers }
        });
        return JSON.stringify({ status: response.status, body: await response.text() });
    } catch {
        return JSON.stringify({ status: 0, body: "" });
    } finally {
        clearTimeout(timer);
        if (requests.get(key) === controller) requests.delete(key);
    }
}

export function getReports(key, query) {
    return request(`${key}:read`, `/api/flags?${query}`);
}

export function getReasons(key) {
    return request(`${key}:reasons`, "/api/flags/options");
}

export function setResolved(key, flagId, resolved) {
    if (!Number.isSafeInteger(flagId) || flagId <= 0 || typeof resolved !== "boolean")
        throw new TypeError("Invalid report update.");
    return request(`${key}:write`, `/api/flags/${flagId}/edit`, {
        method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(resolved)
    });
}

export function dispose(key) {
    for (const suffix of ["read", "write", "reasons"]) {
        const id = `${key}:${suffix}`;
        requests.get(id)?.abort();
        requests.delete(id);
    }
}
