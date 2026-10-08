import { getProfile, cancelProfileRequest } from "./profile.js?v=7";
export { watchPageAccess } from "./page-access.js?v=1";

// Keep browser credentials on requests to ResourceController, including private resources.
export function getResource(resourceId, requestKey) {
    return getProfile(`/api/resource/${encodeURIComponent(resourceId)}`, requestKey);
}

export function getResourceOptions(requestKey) {
    return getProfile("/api/resource/options", requestKey);
}

export function getResourceAccount(requestKey) {
    return getProfile("/api/profile/me", `${requestKey}-account`, false);
}

const interactionRequests = new Map();

export function rateResource(resourceId, value, requestKey) {
    return submitInteraction(`/api/resource/${encodeURIComponent(resourceId)}/Score/Submit/${encodeURIComponent(value)}`, `${requestKey}-score`);
}

export function postResourceComment(resourceId, commentText, requestKey) {
    const data = new FormData();
    data.set("commentText", commentText);
    return submitInteraction(`/api/resource/${encodeURIComponent(resourceId)}/Comment/Submit`, `${requestKey}-comment`, data);
}

async function submitInteraction(url, requestKey, body) {
    interactionRequests.get(requestKey)?.abort();
    const controller = new AbortController();
    interactionRequests.set(requestKey, controller);
    try {
        const response = await fetch(new URL(url, document.baseURI), {
            method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, body, signal: controller.signal
        });
        if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        return JSON.stringify({ status: response.status, body: await response.text(), cancelled: response.status === 499 });
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError") return JSON.stringify({ status: 0, body: "", cancelled: true });
        if (error instanceof TypeError) return JSON.stringify({ status: 0, body: "", cancelled: false });
        throw error;
    } finally {
        if (interactionRequests.get(requestKey) === controller) interactionRequests.delete(requestKey);
    }
}

export function cancelResourceInteractions(requestKey) {
    for (const suffix of ["-score", "-comment"]) {
        interactionRequests.get(`${requestKey}${suffix}`)?.abort();
        interactionRequests.delete(`${requestKey}${suffix}`);
    }
}

export function cancelResourceRequest(requestKey) {
    cancelProfileRequest(requestKey);
    cancelProfileRequest(`${requestKey}-account`);
    cancelResourceInteractions(requestKey);
}

document.addEventListener("keydown", event => {
    if (event.target instanceof HTMLElement && event.target.matches(".detail-tabs [role='tab']") &&
        ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) event.preventDefault();
});

export function selectResourceTab(versions, focus) {
    if (versions) {
        for (const video of document.querySelectorAll("#resource-overview-panel video")) video.pause();
    }
    if (focus) document.getElementById(versions ? "resource-versions-tab" : "resource-overview-tab")?.focus();
}
