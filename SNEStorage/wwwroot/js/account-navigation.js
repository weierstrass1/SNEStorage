import { accountAvatarUrl } from "./profile.js?v=14";
import { getAccountSession, watchAccountSession } from "./account-session.js?v=2";

export function watchAccount(receiver) {
    let stopped = false;
    let checking = false;
    let checkAgain = false;
    let activeRequest;

    async function report(account) {
        if (stopped) return;
        await receiver.invokeMethodAsync("UpdateAccount", account);
    }

    async function checkSession() {
        if (stopped) return;
        if (checking) { checkAgain = true; return; }
        checking = true;
        const request = new AbortController();
        activeRequest = request;
        try {
            const response = await getAccountSession(request.signal);
            if (stopped || request.signal.aborted) return;
            if (response.cancelled) {
                await report({ authenticated: null, status: response.status || 499 });
                return;
            }
            if (response.status === 401) {
                await report({ authenticated: false, status: 401 });
            } else if (response.status === 200) {
                const profile = JSON.parse(response.body);
                let avatarUrl = "";
                try { avatarUrl = accountAvatarUrl(profile.userSummary?.avatar ?? profile.avatar); } catch { /* An optional image must not hide an account. */ }
                await report({ authenticated: true, status: 200, userId: profile.userId,
                    username: profile.userSummary?.username,
                    usernameColor: profile.userSummary?.usernameColor ?? profile.usernameColor, avatarUrl });
            } else {
                // A missing profile, DB failure or forbidden request doesn't prove the user logged out.
                await report({ authenticated: null, status: response.status });
            }
        } catch {
            // An unavailable API is an unknown state, not proof of login or logout.
            if (!stopped) {
                try { await report({ authenticated: null, status: 0 }); } catch { /* The circuit may have closed. */ }
            }
        } finally {
            activeRequest = undefined;
            checking = false;
            if (checkAgain && !stopped) { checkAgain = false; void checkSession(); }
        }
    }

    function check() { void checkSession(); }
    const monitor = watchAccountSession(check);
    function stop() {
        if (stopped) return;
        stopped = true;
        monitor.stop();
        activeRequest?.abort();
        window.removeEventListener("pagehide", pageHidden);
    }

    function pageHidden(event) { if (!event.persisted) stop(); }
    window.addEventListener("pagehide", pageHidden);
    check();
    return { check, stop };
}
