export function watchSession(profilePath, homePath) {
    const profileUrl = new URL(profilePath, document.baseURI);
    const homeUrl = new URL(homePath, document.baseURI);
    let stopped = false;
    let checking = false;
    let activeRequest;

    function stop() {
        stopped = true;
        window.clearInterval(interval);
        window.removeEventListener("focus", checkSession);
        document.removeEventListener("visibilitychange", onVisibilityChange);
        window.removeEventListener("pagehide", stop);
        activeRequest?.abort();
    }

    async function checkSession() {
        if (stopped || checking) {
            return;
        }

        checking = true;
        const request = new AbortController();
        activeRequest = request;
        const timeout = window.setTimeout(() => request.abort(), 10000);

        try {
            // Browser requests carry the latest cookies, including a login from another tab.
            const response = await fetch(profileUrl, {
                method: "GET",
                credentials: "same-origin",
                cache: "no-store",
                redirect: "manual",
                headers: { Accept: "application/json" },
                signal: request.signal
            });

            const contentType = response.headers.get("Content-Type") ?? "";
            const authenticated = response.status === 204 ||
                (response.status === 200 && contentType.includes("application/json"));
            response.body?.cancel().catch(() => {});

            if (!stopped && !response.redirected && authenticated) {
                stop();
                // Reload Home so its Blazor circuit also receives the current authentication state.
                window.location.replace(homeUrl.href);
            }
        } catch {
            // A failed request leaves the page open; a later check can retry.
        } finally {
            window.clearTimeout(timeout);
            activeRequest = undefined;
            checking = false;
        }
    }

    function onVisibilityChange() {
        if (!document.hidden) {
            void checkSession();
        }
    }

    const interval = window.setInterval(checkSession, 5000);
    window.addEventListener("focus", checkSession);
    document.addEventListener("visibilitychange", onVisibilityChange);
    window.addEventListener("pagehide", stop);
    void checkSession();

    return { stop };
}
