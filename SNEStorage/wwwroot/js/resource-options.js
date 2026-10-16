// Only the public types/categories/visibilities catalog is cached, in this tab's memory.
// No profiles, permissions, resource data or errors are stored here.
const lifetime = 5 * 60 * 1000;
let cached, expires = 0, pending;
export function readResourceOptions() {
    if (cached && performance.now() < expires) return Promise.resolve(cached);
    pending ??= load().finally(() => { pending = undefined; });
    return pending;
}
async function load() {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 30000);
    try {
        const response = await fetch("/api/resource/options", {
            credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, signal: controller.signal
        });
        const result = { status: response.status, body: await response.text(),
            contentType: response.headers.get("Content-Type") ?? "", cancelled: false };
        if (response.ok && result.contentType.includes("json")) {
            try {
                const data = JSON.parse(result.body);
                if (Array.isArray(data.resourceTypes) && data.resourceTypes.length &&
                    Array.isArray(data.categories) && Array.isArray(data.visibilities) && data.visibilities.length) {
                    cached = Object.freeze(result);
                    expires = performance.now() + lifetime;
                }
            } catch { /* A malformed response must remain retryable. */ }
        }
        return result;
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError" || error instanceof TypeError)
            return { status: controller.signal.aborted ? 504 : 0, body: "", contentType: "", cancelled: false };
        throw error;
    } finally { clearTimeout(timeout); }
}
