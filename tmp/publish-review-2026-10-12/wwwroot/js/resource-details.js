import { getProfile, cancelProfileRequest } from "./profile.js?v=10";
import { cancelResourceReads } from "./resource-read.js?v=2";
import { captchaFetch } from "./captcha.js?v=3";
export { getResource, getResourceOptions, getResourceSection } from "./resource-read.js?v=2";
export { watchPageAccess } from "./page-access.js?v=5";

export function getResourceAccount(requestKey) {
    return getProfile("/api/profile/me", `${requestKey}-account`, false);
}

const interactionRequests = new Map();

export function rateResource(resourceId, value, requestKey) {
    return submitInteraction(`/api/resource/${encodeURIComponent(resourceId)}/Score/Submit/${encodeURIComponent(value)}`, `${requestKey}-score`, undefined, "resource_addOrEditScore");
}

export function postResourceComment(resourceId, commentText, requestKey) {
    const data = new FormData();
    data.set("commentText", commentText);
    return submitInteraction(`/api/resource/${encodeURIComponent(resourceId)}/Comment/Submit`, `${requestKey}-comment`, data, "resource_addComment");
}

async function submitInteraction(url, requestKey, body, captchaAction) {
    interactionRequests.get(requestKey)?.abort();
    const controller = new AbortController();
    interactionRequests.set(requestKey, controller);
    try {
        const response = await captchaFetch(new URL(url, document.baseURI), {
            method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, body, signal: controller.signal
        }, captchaAction);
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
    cancelResourceContent(requestKey);
    cancelProfileRequest(`${requestKey}-account`);
}

export function cancelResourceContent(requestKey) {
    cancelResourceReads(requestKey);
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
