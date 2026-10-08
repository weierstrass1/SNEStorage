import { getProfile, cancelProfileRequest } from "./profile.js?v=7";

const writes = new Map();

export function getComments(resourceId, parentCommentId, page, maxAmount, sort, key) {
    const path = parentCommentId == null
        ? `/api/comment/resource/${encodeURIComponent(resourceId)}`
        : `/api/comment/${encodeURIComponent(parentCommentId)}/replies`;
    const url = new URL(path, document.baseURI);
    url.searchParams.set("page", String(page));
    url.searchParams.set("maxAmount", String(maxAmount));
    url.searchParams.set("sort", sort);
    // A comment permission failure does not necessarily mean the session expired.
    return getProfile(url.href, key, false);
}

export async function submitCommentAction(commentId, action, text, key) {
    const methods = { Reply: "POST", Edit: "PUT", Delete: "DELETE" };
    const method = methods[action];
    if (!method) throw new Error("Unsupported comment action.");
    const path = `/api/comment/${encodeURIComponent(commentId)}${action === "Reply" ? "/reply" : ""}`;
    const body = action === "Delete" ? undefined : new FormData();
    body?.set("commentText", text);
    return submit(path, method, body, key);
}

export function submitCommentVote(commentId, value, key) {
    const body = new FormData();
    body.set("value", value);
    return submit(`/api/comment/${encodeURIComponent(commentId)}/like`, "POST", body, key);
}

export function getCommentVote(commentId, key) {
    return getProfile(`/api/comment/${encodeURIComponent(commentId)}/userlike`, `${key}-current-vote`, false);
}

async function submit(path, method, body, key) {
    const controller = new AbortController();
    writes.get(key)?.abort();
    writes.set(key, controller);
    try {
        const response = await fetch(new URL(path, document.baseURI), {
            method, body, credentials: "same-origin", cache: "no-store", redirect: "error",
            headers: { Accept: "application/json" }, signal: controller.signal
        });
        return JSON.stringify({ status: response.status, body: await response.text(), cancelled: response.status === 499 });
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError")
            return JSON.stringify({ status: 0, body: "", cancelled: true });
        if (error instanceof TypeError)
            return JSON.stringify({ status: 0, body: "", cancelled: false });
        throw error;
    } finally {
        if (writes.get(key) === controller) writes.delete(key);
    }
}

export function cancelCommentRequests(key) {
    cancelProfileRequest(key);
    cancelProfileRequest(`${key}-current-vote`);
    writes.get(`${key}-action`)?.abort();
    writes.delete(`${key}-action`);
}
