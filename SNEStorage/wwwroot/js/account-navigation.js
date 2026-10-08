import { accountAvatarUrl } from "./profile.js?v=7";

export function watchAccount(profilePath, receiver) {
    const profileUrl = new URL(profilePath, document.baseURI);
    let channel;
    try {
        if (typeof BroadcastChannel === "function") channel = new BroadcastChannel("snestorage-account");
    } catch { /* Focus/navigation checks still work when cross-tab messaging is unavailable. */ }
    let stopped = false;
    let checking = false;
    let checkAgain = false;
    let activeRequest;
    let lastIdentity;

    async function report(account) {
        if (stopped) return;
        await receiver.invokeMethodAsync("UpdateAccount", account);
        if (account.authenticated != null) {
            const identity = JSON.stringify([account.authenticated, account.username ?? ""]);
            if (identity !== lastIdentity) {
                lastIdentity = identity;
                channel?.postMessage("changed");
            }
        }
    }

    async function checkSession() {
        if (stopped) return;
        if (checking) { checkAgain = true; return; }
        checking = true;
        const request = new AbortController();
        activeRequest = request;
        const timeout = window.setTimeout(() => request.abort(), 10000);
        try {
            // Fresh browser cookies determine the current API session, not the circuit's original user.
            const response = await fetch(profileUrl, {
                credentials: "same-origin", cache: "no-store", redirect: "manual",
                headers: { Accept: "application/json" }, signal: request.signal
            });
            if (response.status === 401) {
                await response.body?.cancel();
                await report({ authenticated: false, status: 401 });
            } else if (response.status === 200 && (response.headers.get("Content-Type") ?? "").includes("json")) {
                const profile = await response.json();
                if (!profile?.userId) { await report({ authenticated: null, status: 502 }); return; }
                let avatarUrl = "";
                try { avatarUrl = accountAvatarUrl(profile.userSummary?.avatar ?? profile.avatar); } catch { /* An optional image must not hide an account. */ }
                await report({ authenticated: true, status: 200,
                    username: profile.userSummary?.username,
                    usernameColor: profile.userSummary?.usernameColor ?? profile.usernameColor, avatarUrl });
            } else {
                // A missing profile, DB failure or forbidden request doesn't prove the user logged out.
                await response.body?.cancel();
                await report({ authenticated: null, status: response.status });
            }
        } catch {
            // An unavailable API is an unknown state, not proof of login or logout.
            if (!stopped) {
                try { await report({ authenticated: null, status: 0 }); } catch { /* The circuit may have closed. */ }
            }
        } finally {
            window.clearTimeout(timeout);
            activeRequest = undefined;
            checking = false;
            if (checkAgain && !stopped) { checkAgain = false; void checkSession(); }
        }
    }

    function check() { void checkSession(); }
    function visible() { if (!document.hidden) check(); }
    function stop() {
        if (stopped) return;
        stopped = true;
        activeRequest?.abort();
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
    if (channel) channel.onmessage = check;
    check();
    return { check, stop };
}
