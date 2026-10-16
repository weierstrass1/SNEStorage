import { captchaFetch } from "./captcha.js?v=3";
import { getAccountSession } from "./account-session.js?v=2";

const requests = new Map();
const result = (status, body) => JSON.stringify({ status, body });

export function openDialog(dialog) {
    if (dialog instanceof HTMLDialogElement && dialog.isConnected && !dialog.open) dialog.showModal();
}

export function closeDialog(dialog) {
    if (dialog instanceof HTMLDialogElement && dialog.open) dialog.close();
}

async function request(resourceId, userId, key, deleting, versionId = null) {
    if (!Number.isSafeInteger(resourceId) || resourceId <= 0 || !userId ||
        versionId !== null && (!Number.isSafeInteger(versionId) || versionId <= 0))
        return result(400, JSON.stringify({ error: "AccountChanged" }));
    if (requests.has(key)) return result(409, JSON.stringify({ error: "RequestInProgress" }));
    const controller = new AbortController();
    requests.set(key, controller);
    let timer;
    try {
        const account = await getAccountSession(controller.signal);
        let identity;
        try { identity = JSON.parse(account.body).userId; } catch { }
        if (account.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        if (account.status !== 200 || identity !== userId)
            return result(account.status === 401 ? 401 : 409, JSON.stringify({ error: "AccountChanged" }));
        const options = { method: deleting ? "DELETE" : "GET", credentials: "same-origin", cache: "no-store",
            redirect: "error", headers: { Accept: "application/json" }, signal: controller.signal };
        // The security challenge can take longer than an ordinary read request.
        timer = setTimeout(() => controller.abort(), deleting ? 180000 : 30000);
        const baseUrl = `/api/resource/${encodeURIComponent(resourceId)}`;
        const url = versionId === null ? baseUrl : deleting ? `${baseUrl}/version/${encodeURIComponent(versionId)}` : `${baseUrl}/versions`;
        const response = deleting ? await captchaFetch(url, options, versionId === null ? "resource_delete" : "resource_deleteVersion", true) : await fetch(url, options);
        if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        return result(response.status, await response.text());
    } catch {
        return result(0, "");
    } finally {
        clearTimeout(timer);
        if (requests.get(key) === controller) requests.delete(key);
    }
}

export function deleteResource(resourceId, userId, key, versionId = null) { return request(resourceId, userId, key, true, versionId); }
export function checkResource(resourceId, userId, key, versionId = null) { return request(resourceId, userId, key, false, versionId); }

export function dispose(key, dialog) {
    requests.get(key)?.abort();
    requests.delete(key);
    closeDialog(dialog);
}
