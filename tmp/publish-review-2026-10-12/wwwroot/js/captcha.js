import { antiforgeryFetch } from "./antiforgery.js?v=1";

// Browser challenge and HTTP transport only. Policies and token validation belong to the API.
let scriptLoad;
let host;
let active;

class CaptchaError extends Error {
    constructor(code) { super(code); this.code = code; }
}

function showProviderError(errorCode, action) {
    // Keep diagnostics useful without logging challenge tokens or keys.
    const code = /^\d{6}$/.test(String(errorCode)) ? String(errorCode) : "unknown";
    const configurationError = ["110100", "110110", "110200", "400020", "400021", "400070"].includes(code);
    let message = "Verification could not finish. Try again or cancel to return to your form.";
    if (code === "110200")
        message = "Verification is not configured for this website address. Please contact SNEStorage support.";
    else if (configurationError)
        message = "Verification is currently unavailable due to a website configuration issue. Please try again later.";
    else if (code === "200500")
        message = "Unable to connect to verification. Check your connection and whether your browser is blocking Cloudflare, then try again.";
    else if (["110600", "110620"].includes(code))
        message = "Verification timed out. Please try again.";
    else if (code === "200100")
        message = "Verification could not finish. Check your device's date and time, then reload this page.";
    host.status.textContent = `${message}${code === "unknown" ? "" : ` Error code: ${code}.`}`;
    host.retry.hidden = configurationError || code === "200100";
    console.warn("SNEStorage verification failed", { code, action, hostname: window.location.hostname });
}

function loadTurnstile() {
    if (window.turnstile) return Promise.resolve(window.turnstile);
    if (scriptLoad) return scriptLoad;
    scriptLoad = new Promise((resolve, reject) => {
        let settled = false;
        const script = document.createElement("script");
        script.src = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
        script.async = true;
        const timer = setTimeout(() => failed(), 20000);
        function failed() {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            script.remove();
            scriptLoad = undefined;
            reject(new CaptchaError("CaptchaUnavailable"));
        }
        script.addEventListener("error", failed, { once: true });
        script.addEventListener("load", () => {
            clearTimeout(timer);
            if (settled) return;
            if (window.turnstile) { settled = true; resolve(window.turnstile); }
            else failed();
        }, { once: true });
        document.head.appendChild(script);
    });
    return scriptLoad;
}

export function initialize(id = "captcha-dialog") {
    const dialog = document.getElementById(id);
    if (!(dialog instanceof HTMLDialogElement)) throw new CaptchaError("CaptchaUnavailable");
    if (host?.dialog === dialog) return;
    if (host) dispose();
    const cancel = event => { event.preventDefault(); finish(new CaptchaError("CaptchaCancelled")); };
    const retry = () => {
        if (active?.widgetId == null) return;
        host.status.textContent = "Verifying your browser…";
        host.retry.hidden = true;
        window.turnstile.reset(active.widgetId);
    };
    const closed = () => { if (active && !dialog.open) finish(new CaptchaError("CaptchaCancelled")); };
    const leaving = () => { if (active) finish(new CaptchaError("CaptchaCancelled")); };
    host = { dialog, cancel, retryHandler: retry, closed, leaving,
        container: dialog.querySelector("[data-captcha-widget]"),
        status: dialog.querySelector("[data-captcha-status]"),
        retry: dialog.querySelector("[data-captcha-retry]"),
        cancelButton: dialog.querySelector("[data-captcha-cancel]") };
    dialog.addEventListener("cancel", cancel);
    dialog.addEventListener("close", closed);
    host.cancelButton.addEventListener("click", cancel);
    host.retry.addEventListener("click", retry);
    window.addEventListener("pagehide", leaving);
}

function finish(error, token) {
    const current = active;
    if (!current) return;
    active = undefined;
    clearTimeout(current.timer);
    current.signal?.removeEventListener("abort", current.aborted);
    if (current.widgetId != null) {
        try { window.turnstile?.remove(current.widgetId); } catch { }
    }
    current.dialog.close();
    current.previousFocus?.isConnected && current.previousFocus.focus({ preventScroll: true });
    if (error) current.reject(error);
    else current.resolve(token);
}

async function requestToken(action, signal) {
    if (signal?.aborted) throw new DOMException("Aborted", "AbortError");
    if (active) throw new CaptchaError("CaptchaBusy");
    const sitekey = document.querySelector('meta[name="snestorage-turnstile-sitekey"]')?.content?.trim();
    if (!sitekey) throw new CaptchaError("CaptchaUnavailable");
    initialize();
    return new Promise((resolve, reject) => {
        const current = { resolve, reject, signal, dialog: host.dialog,
            previousFocus: document.activeElement, widgetId: null,
            aborted: () => finish(new DOMException("Aborted", "AbortError")) };
        active = current;
        signal?.addEventListener("abort", current.aborted, { once: true });
        host.status.textContent = "Preparing verification…";
        host.retry.hidden = true;
        host.dialog.showModal();
        current.timer = setTimeout(() => {
            if (active === current) finish(new CaptchaError("CaptchaUnavailable"));
        }, 180000);
        loadTurnstile().then(turnstile => {
            if (active !== current || signal?.aborted) return;
            host.status.textContent = "Verifying your browser…";
            current.widgetId = turnstile.render(host.container, {
                sitekey, action, theme: "dark", language: "en", size: "flexible",
                "response-field": false,
                callback: token => { if (active === current) finish(null, token); },
                "error-callback": errorCode => {
                    if (active !== current) return;
                    showProviderError(errorCode, action);
                },
                "expired-callback": () => {
                    if (active !== current) return;
                    host.status.textContent = "Verification expired. Please try again.";
                    host.retry.hidden = false;
                },
                "timeout-callback": () => {
                    if (active !== current) return;
                    host.status.textContent = "The check timed out. Please try again.";
                    host.retry.hidden = false;
                }
            });
            if (active !== current && current.widgetId != null) turnstile.remove(current.widgetId);
        }).catch(error => { if (active === current) finish(error instanceof CaptchaError ? error : new CaptchaError("CaptchaUnavailable")); });
    });
}

function localFailure(error) {
    return new Response(JSON.stringify({ error: error.code }), {
        status: error.code === "CaptchaUnavailable" ? 503 : 400,
        headers: { "Content-Type": "application/json" }
    });
}

export async function captchaFetch(url, options, action, always = false) {
    try {
        const headers = new Headers(options.headers);
        if (always) headers.set("X-Captcha-Token", await requestToken(action, options.signal));
        const response = await antiforgeryFetch(url, { ...options, headers });
        if (![400, 403].includes(response.status)) return response;
        let rejection;
        try { rejection = await response.clone().json(); } catch { return response; }
        if (!["CaptchaRequired", "CaptchaInvalid"].includes(rejection?.error)) return response;
        if (rejection.action && rejection.action !== action) return localFailure(new CaptchaError("CaptchaUnavailable"));
        // Retry exactly once, only for a CAPTCHA rejection made before the API action ran.
        // Network errors, timeouts, 5xx, and unrelated validation errors never replay a mutation.
        headers.set("X-Captcha-Token", await requestToken(action, options.signal));
        return await antiforgeryFetch(url, { ...options, headers });
    } catch (error) {
        if (error instanceof CaptchaError) return localFailure(error);
        throw error;
    }
}

export function dispose(id = "captcha-dialog") {
    if (!host || host.dialog.id !== id) return;
    if (active) finish(new CaptchaError("CaptchaCancelled"));
    host.dialog.removeEventListener("cancel", host.cancel);
    host.dialog.removeEventListener("close", host.closed);
    host.cancelButton.removeEventListener("click", host.cancel);
    host.retry.removeEventListener("click", host.retryHandler);
    window.removeEventListener("pagehide", host.leaving);
    host = undefined;
}
