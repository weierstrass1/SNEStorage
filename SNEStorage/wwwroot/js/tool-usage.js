import { antiforgeryFetch } from "./antiforgery.js?v=1";

// A failed statistics request must never prevent a local tool from working.
export function attachToolUsage(element, toolId) {
    const endpoint = new URL(`api/tools/${encodeURIComponent(toolId)}/usage`, document.baseURI);
    const lifetime = new AbortController();
    const numberFormat = new Intl.NumberFormat();
    let available = null;
    let lastCount = 0;

    async function request(method) {
        if (!element || lifetime.signal.aborted) return;
        const signal = AbortSignal.any([lifetime.signal, AbortSignal.timeout(8000)]);
        try {
            const response = await antiforgeryFetch(endpoint, {
                method, signal, credentials: "same-origin", cache: "no-store",
                headers: { Accept: "application/json" }
            });
            if (!response.ok) {
                if (response.status === 404) available = false;
                throw new Error("The usage total is unavailable.");
            }
            const result = await response.json();
            if (!Number.isSafeInteger(result.count) || result.count < 0)
                throw new Error("Invalid usage total.");
            if (lifetime.signal.aborted) return;
            available = true;
            lastCount = Math.max(lastCount, result.count);
            element.textContent = `Uses: ${numberFormat.format(lastCount)}`;
            element.title = "Total successful uses reported across all visitors.";
        } catch {
            if (lifetime.signal.aborted) return;
            if (element.textContent === "Uses: …") element.textContent = "Uses: —";
            element.title = method === "POST"
                ? "Your file is ready, but this use could not be confirmed in the global total."
                : "The global usage total is temporarily unavailable.";
        }
    }

    void request("GET");
    return {
        recordSuccess() {
            if (available !== false) void request("POST");
        },
        dispose() { lifetime.abort(); }
    };
}
