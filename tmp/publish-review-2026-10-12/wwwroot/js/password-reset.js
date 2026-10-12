import { captchaFetch } from "./captcha.js?v=3";

const submissions = new Map();

export async function submitPassword(formId, requestEmail) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement) || !form.reportValidity())
        return { status: 400, body: "" };

    // Form fields can be disabled by the busy state; read their current values directly.
    const value = name => form.elements.namedItem(name)?.value ?? "";
    const payload = requestEmail ? value("Email").trim() : {
        userId: value("UserId"), token: value("Token"),
        newPassword: value("NewPassword"), confirmPassword: value("ConfirmPassword")
    };
    const controller = new AbortController();
    submissions.get(formId)?.abort();
    submissions.set(formId, controller);
    try {
        const response = await captchaFetch(`/api/account/password/${requestEmail ? "request" : "change"}`, {
            method: "POST", credentials: "same-origin", cache: "no-store",
            headers: { Accept: "application/json", "Content-Type": "application/json" },
            body: JSON.stringify(payload), signal: controller.signal, redirect: "error"
        }, requestEmail ? "password_reset_request" : "password_change", requestEmail);
        if (response.headers.get("Content-Type")?.includes("text/html"))
            return { status: 502, body: "" };
        return { status: response.status, body: await response.text() };
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError" || error instanceof TypeError)
            return { status: 0, body: "" };
        throw error;
    } finally {
        if (submissions.get(formId) === controller) submissions.delete(formId);
    }
}

export function cancelPassword(formId) {
    submissions.get(formId)?.abort();
    submissions.delete(formId);
}
