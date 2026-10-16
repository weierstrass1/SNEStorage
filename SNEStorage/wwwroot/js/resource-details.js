import { getProfile, cancelProfileRequest } from "./profile.js?v=14";
import { cancelResourceReads, beginOverviewReads, getResource as getInitialResource } from "./resource-read.js?v=4";
import { trackBackgroundVote } from "./optimistic-votes.js?v=2";
export { getResource, getResourceOptions, getResourceSection } from "./resource-read.js?v=4";
export { watchPageAccess } from "./page-access.js?v=7";

export function getResourceAccount(requestKey) {
    return getProfile("/api/profile/me", `${requestKey}-account`, false);
}

// Cancel obsolete reads locally and start HTTP in the same interop call.
export async function beginResourceRead(resourceId, requestKey) {
    cancelResourceContent(requestKey);
    const result = await getInitialResource(resourceId, requestKey);
    const response = JSON.parse(result);
    if (response.status === 200 && !response.cancelled && JSON.parse(response.body)?.id === resourceId)
        beginOverviewReads(resourceId, requestKey);
    return result;
}

const interactionRequests = new Map();

export function rateResource(resourceId, value, requestKey) {
    const finished = trackBackgroundVote("rating", resourceId, value);
    return submitInteraction(`/api/resource/${encodeURIComponent(resourceId)}/Score/Submit/${encodeURIComponent(value)}`, `${requestKey}-score`, undefined, "resource_addOrEditScore", true)
        .then(json => { const response = JSON.parse(json); finished(response.status, response.body); return json; }, error => { finished(0); throw error; });
}

export function postResourceComment(resourceId, commentText, requestKey) {
    const data = new FormData();
    data.set("commentText", commentText);
    return submitInteraction(`/api/resource/${encodeURIComponent(resourceId)}/Comment/Submit`, `${requestKey}-comment`, data, "resource_addComment");
}

async function submitInteraction(url, requestKey, body, captchaAction, persistOnNavigation = false) {
    const controller = new AbortController();
    if (!persistOnNavigation) {
        interactionRequests.get(requestKey)?.abort();
        interactionRequests.set(requestKey, controller);
    }
    const timeout = persistOnNavigation ? setTimeout(() => controller.abort(), 180000) : null;
    try {
        const { captchaFetch } = await import("./captcha.js?v=3");
        const response = await captchaFetch(new URL(url, document.baseURI), {
            method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, body, signal: controller.signal, keepalive: persistOnNavigation
        }, captchaAction);
        if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        return JSON.stringify({ status: response.status, body: await response.text(), cancelled: response.status === 499 });
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError") return JSON.stringify({ status: 0, body: "", cancelled: true });
        if (error instanceof TypeError) return JSON.stringify({ status: 0, body: "", cancelled: false });
        throw error;
    } finally {
        clearTimeout(timeout);
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
