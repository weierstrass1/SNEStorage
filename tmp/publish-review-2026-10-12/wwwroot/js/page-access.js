import { getProfile, cancelProfileRequest } from "./profile.js?v=10";
import { watchAccountSession } from "./account-session.js?v=2";
import { getResource, cancelResourceReads } from "./resource-read.js?v=2";
export { getResourceSection } from "./resource-read.js?v=2";

export function getPageAccount(key) {
    return getProfile("/api/profile/me", `${key}-account`, false);
}

export function getPageResource(resourceId, key) {
    return getResource(resourceId, `${key}-resource`);
}

export function cancelPageAccess(key) {
    cancelProfileRequest(`${key}-account`);
    cancelResourceReads(`${key}-resource`);
}

export function watchPageAccess(receiver) {
    let stopped = false;
    function check() {
        if (!stopped) receiver.invokeMethodAsync("RecheckPageAccess").catch(() => stop());
    }
    const monitor = watchAccountSession(check);
    function stop() {
        stopped = true;
        monitor.stop();
        window.removeEventListener("pagehide", pageHidden);
    }
    function pageHidden(event) { if (!event.persisted) stop(); }
    window.addEventListener("pagehide", pageHidden);
    return { stop };
}
