import { getAccountSession, invalidateAccountSession } from "./account-session.js?v=2";
import { antiforgeryFetch } from "./antiforgery.js?v=1";

const profileRequests = new Map();

export function cancelProfileRequest(key) {
    profileRequests.get(key)?.abort();
    profileRequests.delete(key);
}

export async function getProfile(url, requestKey, notifySession = true) {
    const controller = new AbortController();
    if (requestKey) {
        cancelProfileRequest(requestKey);
        profileRequests.set(requestKey, controller);
    }
    try {
        const requestUrl = new URL(url, document.baseURI);
        if (requestUrl.origin === window.location.origin && requestUrl.pathname === "/api/profile/me" && !requestUrl.search) {
            return JSON.stringify(await getAccountSession(controller.signal));
        }
        const response = await fetch(requestUrl, {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store",
            redirect: "manual",
            headers: { Accept: "application/json" },
            signal: controller.signal
        });
        if (response.status === 401 && notifySession) window.dispatchEvent(new Event("snestorage-session-rejected"));
        return JSON.stringify({ status: response.status, body: await response.text(), cancelled: response.status === 499 });
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError") {
            return JSON.stringify({ status: 0, body: "", cancelled: true });
        }
        if (error instanceof TypeError) {
            // A network failure is a recoverable load failure, not a rejected JS interop call.
            return JSON.stringify({ status: 0, body: "", cancelled: false });
        }
        throw error;
    } finally {
        if (requestKey && profileRequests.get(requestKey) === controller) profileRequests.delete(requestKey);
    }
}

export async function getAccountAvatar(url, requestKey) {
    try {
        const response = JSON.parse(await getProfile(url, requestKey));
        if (response.status !== 200 || !response.body || response.cancelled) return "";

        const profile = JSON.parse(response.body);
        const avatar = profile?.userSummary?.avatar ?? profile?.avatar;
        if (!avatar?.url) return "";

        return accountAvatarUrl(avatar);
    } catch (error) {
        // An optional avatar must not prevent the account menu or navigation from working.
        console.warn("The account avatar could not be loaded.", error);
        return "";
    }
}

export function accountAvatarUrl(avatar) {
    if (!avatar?.url) return "";
    const storedPath = avatar.url.trim().replaceAll("\\", "/");
    const publicPath = storedPath.replace(/^\/*wwwroot\//i, "/");
    const url = new URL(publicPath, document.baseURI);
    if (url.protocol !== "https:" && url.protocol !== "http:") return "";
    if (avatar.id != null) url.searchParams.set("v", String(avatar.id));
    return url.href;
}

let avatarPreviewUrl;

export function createAvatarPreview(inputId) {
    releaseAvatarPreview();

    const file = document.getElementById(inputId)?.files?.[0];
    if (!file) {
        return "";
    }

    avatarPreviewUrl = URL.createObjectURL(file);
    return avatarPreviewUrl;
}

export function releaseAvatarPreview() {
    if (avatarPreviewUrl) {
        URL.revokeObjectURL(avatarPreviewUrl);
        avatarPreviewUrl = undefined;
    }
}

export function clearAvatarInput(inputId) {
    const input = document.getElementById(inputId);
    if (input) {
        input.value = "";
    }

    releaseAvatarPreview();
}

export async function updateProfile(url, fields, fileInputId) {
    const formData = new FormData();
    for (const [name, value] of Object.entries(fields)) {
        formData.append(name, value);
    }

    const file = document.getElementById(fileInputId)?.files?.[0];
    if (file) {
        formData.append("File", file, file.name);
    }

    const response = await antiforgeryFetch(new URL(url, document.baseURI), {
        method: "PUT",
        credentials: "same-origin",
        headers: { Accept: "application/json" },
        body: formData
    });

    if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
    if (response.ok) invalidateAccountSession(true);

    return JSON.stringify({
        status: response.status,
        body: await response.text()
    });
}
