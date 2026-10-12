import { getAccountSession, watchAccountSession } from "./account-session.js?v=2";

export function watchSession(homePath) {
    const homeUrl = new URL(homePath, document.baseURI);
    let stopped = false;
    let checking = false;
    let activeRequest;

    function stop() {
        stopped = true;
        window.clearInterval(interval);
        monitor.stop();
        window.removeEventListener("pagehide", pageHidden);
        activeRequest?.abort();
    }

    async function checkSession() {
        if (stopped || checking || document.hidden) {
            return;
        }

        checking = true;
        const request = new AbortController();
        activeRequest = request;

        try {
            const response = await getAccountSession(request.signal);
            if (!stopped && !response.cancelled && response.status === 200) {
                stop();
                // Reload Home so its Blazor circuit also receives the current authentication state.
                window.location.replace(homeUrl.href);
            }
        } catch {
            // A failed request leaves the page open; a later check can retry.
        } finally {
            activeRequest = undefined;
            checking = false;
        }
    }

    const monitor = watchAccountSession(() => { void checkSession(); });
    const interval = window.setInterval(checkSession, 5000);
    function pageHidden(event) { if (!event.persisted) stop(); }
    window.addEventListener("pagehide", pageHidden);
    void checkSession();

    return { stop };
}
