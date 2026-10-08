import { getProfile, cancelProfileRequest } from "./profile.js?v=7";

export function getPageAccount(key) {
    return getProfile("/api/profile/me", `${key}-account`, false);
}

export function getPageResource(resourceId, key) {
    return getProfile(`/api/resource/${encodeURIComponent(resourceId)}`, `${key}-resource`, false);
}

export function cancelPageAccess(key) {
    cancelProfileRequest(`${key}-account`);
    cancelProfileRequest(`${key}-resource`);
}

export function watchPageAccess(receiver) {
    let stopped = false;
    let timer;
    let channel;
    function check() {
        if (stopped || document.hidden) return;
        window.clearTimeout(timer);
        timer = window.setTimeout(() => {
            if (!stopped) receiver.invokeMethodAsync("RecheckPageAccess").catch(() => stop());
        }, 150);
    }
    function visible() { if (!document.hidden) check(); }
    function stop() {
        stopped = true;
        window.clearTimeout(timer);
        window.removeEventListener("focus", check);
        window.removeEventListener("snestorage-session-rejected", check);
        window.removeEventListener("pagehide", stop);
        document.removeEventListener("visibilitychange", visible);
        channel?.close();
    }
    window.addEventListener("focus", check);
    window.addEventListener("snestorage-session-rejected", check);
    window.addEventListener("pagehide", stop);
    document.addEventListener("visibilitychange", visible);
    try {
        channel = new BroadcastChannel("snestorage-account");
        channel.onmessage = check;
    } catch { /* Focus checks still work when cross-tab messaging is unavailable. */ }
    return { stop };
}
