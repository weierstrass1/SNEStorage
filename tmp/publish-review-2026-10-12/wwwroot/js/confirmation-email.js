import { captchaFetch } from "./captcha.js?v=3";

const requests = new Map();

export async function resendConfirmation(requestKey, email) {
    const controller = new AbortController();
    requests.get(requestKey)?.abort();
    requests.set(requestKey, controller);
    try {
        const response = await captchaFetch("/api/account/confirmation/resend", {
            method: "POST", credentials: "same-origin", cache: "no-store",
            headers: { Accept: "application/json", "Content-Type": "application/json" },
            body: JSON.stringify(email.trim()), signal: controller.signal, redirect: "error"
        }, "email_confirmation_resend", true);
        if (response.headers.get("Content-Type")?.includes("text/html"))
            return { status: response.ok ? 502 : response.status, body: "" };
        return { status: response.status, body: await response.text() };
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError" || error instanceof TypeError)
            return { status: 0, body: "" };
        throw error;
    } finally {
        if (requests.get(requestKey) === controller) requests.delete(requestKey);
    }
}

export function cancelResend(requestKey) {
    requests.get(requestKey)?.abort();
    requests.delete(requestKey);
}
