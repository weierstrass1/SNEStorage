// Per-document browser memory only. Nothing is stored in localStorage or on the server.
const lifetimeMs = 15000;
const requestTimeoutMs = 30000;
const listeners = new Set();
let cached;
let expiresAt = 0;
let inFlight;
let revision = 0;
let lastIdentity;
let channel;
let timer;
let notifyRequired = false;
let suspended = false;

function abortSessionRequest(request, reason) {
    if (!request || request.controller.signal.aborted) return;
    const seconds = ((performance.now() - request.startedAt) / 1000).toFixed(1);
    console.debug(`Account verification cancelled after ${seconds}s: ${reason}.`);
    request.controller.abort();
}

function scheduleCheck(required = false) {
    notifyRequired ||= required;
    window.clearTimeout(timer);
    timer = window.setTimeout(() => {
        const notify = notifyRequired;
        notifyRequired = false;
        if (!notify && (document.hidden || Date.now() < expiresAt || inFlight)) return;
        for (const listener of listeners) listener();
    }, 150);
}

export function invalidateAccountSession(broadcast = false) {
    revision++;
    cached = undefined;
    expiresAt = 0;
    abortSessionRequest(inFlight, "session invalidated");
    inFlight = undefined;
    if (broadcast) channel?.postMessage({ type: "invalidate" });
    scheduleCheck(true);
}

export function watchAccountSession(listener) {
    listeners.add(listener);
    return { stop() { listeners.delete(listener); } };
}

function cancelledResponse() {
    return { status: 0, body: "", cancelled: true };
}

async function loadSession(request) {
    const timeout = window.setTimeout(() => {
        request.timedOut = true;
        console.warn(`Account verification timed out after ${requestTimeoutMs / 1000} seconds: /api/profile/me.`);
        abortSessionRequest(request, "request timeout");
    }, requestTimeoutMs);
    try {
        const response = await fetch(new URL("/api/profile/me", document.baseURI), {
            method: "GET", credentials: "same-origin", cache: "no-store", redirect: "manual",
            headers: { Accept: "application/json" }, signal: request.controller.signal
        });
        const result = { status: response.status, body: await response.text(), cancelled: response.status === 499 };
        if (request.revision !== revision || request.controller.signal.aborted) return cancelledResponse();

        let identity;
        if (response.status === 200) {
            try {
                const profile = JSON.parse(result.body);
                if (!(response.headers.get("Content-Type") ?? "").includes("json") ||
                    typeof profile?.userId !== "string" || !profile.userId.trim()) {
                    return { status: 502, body: "", cancelled: false };
                }
                identity = profile.userId;
            } catch { return { status: 502, body: "", cancelled: false }; }
        } else if (response.status === 401) identity = null;
        else return result; // Failures are not cached or treated as logout.

        // Cache a JSON snapshot, never a mutable profile shared between components.
        cached = result;
        expiresAt = Date.now() + lifetimeMs;
        const changed = lastIdentity !== undefined && lastIdentity !== identity;
        if (lastIdentity !== identity) {
            lastIdentity = identity;
            // Also announces a login/logout completed by a full document navigation.
            channel?.postMessage({ type: "identity", identity });
        }
        if (changed) scheduleCheck(true);
        return result;
    } catch (error) {
        if (request.timedOut && request.revision === revision) return { status: 504, body: "", cancelled: false };
        if (request.controller.signal.aborted || error.name === "AbortError") return cancelledResponse();
        if (error instanceof TypeError) return { status: 0, body: "", cancelled: false };
        throw error;
    } finally {
        window.clearTimeout(timeout);
    }
}

export function getAccountSession(signal) {
    if (signal?.aborted || suspended) return Promise.resolve(cancelledResponse());
    if (cached && Date.now() < expiresAt) return Promise.resolve({ ...cached });
    if (!inFlight) {
        const request = { controller: new AbortController(), revision, startedAt: performance.now() };
        inFlight = request;
        request.promise = loadSession(request).finally(() => {
            if (inFlight === request) inFlight = undefined;
        });
    }
    const request = inFlight;
    // A page only cancels its own wait. Keep the shared request alive for the next page
    // even when it hasn't attached yet; timeout, session changes and pagehide still abort it.
    return new Promise((resolve, reject) => {
        let finished = false;
        function complete(result, error) {
            if (finished) return;
            finished = true;
            signal?.removeEventListener("abort", abort);
            if (error) reject(error);
            else resolve({ ...result });
        }
        function abort() {
            complete(cancelledResponse());
        }
        signal?.addEventListener("abort", abort, { once: true });
        request.promise.then(result => {
            if (finished) return;
            if (request.revision !== revision && !signal?.aborted) {
                // A session change superseded the response. Wait for fresh cookies instead of
                // returning the previous user or displaying a spurious cancellation error.
                getAccountSession(signal).then(next => complete(next), error => complete(undefined, error));
            } else complete(result);
        }, error => complete(undefined, error));
    });
}

try {
    channel = new BroadcastChannel("snestorage-account");
    channel.onmessage = ({ data }) => {
        if (data?.type === "invalidate" || data?.type === "identity" &&
            (typeof data.identity === "string" || data.identity === null) && data.identity !== lastIdentity) {
            // Messages only invalidate; current browser cookies determine the new identity.
            invalidateAccountSession();
        }
    };
} catch { /* Focus checks still work without cross-tab messaging. */ }

window.addEventListener("focus", () => scheduleCheck());
document.addEventListener("visibilitychange", () => { if (!document.hidden) scheduleCheck(); });
window.addEventListener("snestorage-session-rejected", () => invalidateAccountSession());
window.addEventListener("pageshow", event => {
    suspended = false;
    if (event.persisted) invalidateAccountSession();
});
window.addEventListener("pagehide", () => {
    // Do not restore an old account snapshot from the browser's back/forward document cache.
    suspended = true;
    revision++;
    cached = undefined;
    expiresAt = 0;
    abortSessionRequest(inFlight, "document closed or replaced");
    inFlight = undefined;
    window.clearTimeout(timer);
});
