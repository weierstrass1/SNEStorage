import { captchaFetch } from "./captcha.js?v=3";

const submissions = new Map();
const validationStates = new Map();

export function initializeRegistration(formId, rules) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement)) return;
    const previous = validationStates.get(formId);
    if (previous?.form === form) { previous.rules = rules; return; }
    previous?.dispose();
    const today = new Date();
    form.elements.namedItem("Birthday").max = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
    const state = { form, rules, touched: new Set(), showAll: false, dispose: null };
    const update = event => {
        if (!event.target.name) return;
        if (event.type !== "input") state.touched.add(event.target.name);
        validateRegistration(state);
    };
    const submit = event => {
        state.showAll = true;
        if (validateRegistration(state)) return;
        // Invalid input stays in the browser: no Blazor submit, CAPTCHA, or API request.
        event.preventDefault();
        event.stopImmediatePropagation();
        focusInvalidField(form);
    };
    form.addEventListener("input", update);
    form.addEventListener("change", update);
    form.addEventListener("focusout", update);
    form.addEventListener("submit", submit, true);
    state.dispose = () => {
        form.removeEventListener("input", update);
        form.removeEventListener("change", update);
        form.removeEventListener("focusout", update);
        form.removeEventListener("submit", submit, true);
    };
    validationStates.set(formId, state);
    validateRegistration(state);
}

function validateRegistration(state) {
    const { form, rules } = state;
    const field = name => form.elements.namedItem(name);
    const password = field("Password").value;
    const policy = rules.password;
    const requirements = {
        length: password.length >= policy.requiredLength,
        uppercase: !policy.requireUppercase || /[A-Z]/.test(password),
        lowercase: !policy.requireLowercase || /[a-z]/.test(password),
        digit: !policy.requireDigit || /[0-9]/.test(password),
        symbol: !policy.requireNonAlphanumeric || /[^A-Za-z0-9]/.test(password),
        unique: new Set(password.split("")).size >= policy.requiredUniqueChars
    };
    form.querySelectorAll("[data-password-rule]").forEach(item => {
        item.dataset.met = String(password.length > 0 && requirements[item.dataset.passwordRule]);
    });
    const username = field("User").value;
    const birthday = field("Birthday");
    const avatar = field("Avatar").files?.[0];
    const messages = {
        User: !username.trim() ? "Enter a username." :
            rules.allowedUsernameCharacters && username.split("").some(character => !rules.allowedUsernameCharacters.includes(character)) ?
                "Your username contains characters that are not allowed." : "",
        Email: !field("Email").value.trim() ? "Enter your email address." :
            field("Email").validity.typeMismatch ? "Enter a valid email address." : "",
        Password: !password ? "Enter a password." :
            Object.values(requirements).some(met => !met) ? "Your password must meet all the requirements below." : "",
        PasswordRepeat: !field("PasswordRepeat").value ? "Confirm your password." :
            field("PasswordRepeat").value !== password ? "Passwords do not match." : "",
        Birthday: !birthday.value || birthday.validity.badInput ? "Enter a valid date of birth." :
            birthday.validity.rangeOverflow ? "Your date of birth cannot be in the future." :
            birthday.value < "0001-01-01" ? "Enter a valid date of birth." : "",
        Avatar: !avatar ? "Choose a PNG or JPG avatar." :
            avatar.size === 0 ? "The avatar image is empty. Choose another image." :
            avatar.size > 5 * 1024 * 1024 ? "Your avatar image is too large. Choose an image no larger than 5 MB." :
            !/\.(png|jpe?g)$/i.test(avatar.name) || !["image/png", "image/jpeg"].includes(avatar.type.toLowerCase()) ?
                "Choose a PNG or JPG image for your avatar." : "",
        TimeZoneId: validOption(field("TimeZoneId")) ? "" : "Choose a time zone.",
        EmailVisibilityId: validOption(field("EmailVisibilityId")) ? "" : "Choose an email visibility option."
    };
    for (const [name, message] of Object.entries(messages)) {
        const control = field(name);
        control.setCustomValidity(message);
        const show = state.showAll || state.touched.has(name);
        control.setAttribute("aria-invalid", String(show && !!message));
        const feedback = form.querySelector(`[data-validation-for="${name}"]`);
        if (feedback) feedback.textContent = show ? message : "";
    }
    return Object.values(messages).every(message => !message);
}

function validOption(control) {
    return Number(control.value) > 0 && Array.from(control.options).some(option => option.value === control.value && !option.disabled);
}

function focusInvalidField(form) {
    const invalid = form.querySelector('[aria-invalid="true"]');
    const target = invalid?.name === "Avatar" ? invalid.closest(".avatar-picker")?.querySelector("[data-avatar-choose]") : invalid;
    target?.focus({ preventScroll: true });
    target?.scrollIntoView({ block: "center", behavior: "auto" });
}

export async function submitRegistration(formId) {
    const form = document.getElementById(formId);
    const validation = validationStates.get(formId);
    if (!(form instanceof HTMLFormElement) || !validation)
        return { status: 0, body: "", redirectUrl: "" };
    validation.showAll = true;
    if (!validateRegistration(validation)) {
        focusInvalidField(form);
        return { status: 400, body: "", redirectUrl: "", validationFailed: true };
    }
    const controller = new AbortController();
    submissions.get(formId)?.abort();
    submissions.set(formId, controller);
    try {
        const response = await captchaFetch(form.action, {
            method: "POST", body: new FormData(form), credentials: "same-origin",
            headers: { Accept: "application/json" }, redirect: "follow",
            cache: "no-store", signal: controller.signal
        }, "user_register", true);
        if (response.redirected) {
            const target = new URL(response.url, document.baseURI);
            if (target.origin === window.location.origin &&
                (target.pathname.toLowerCase() === "/register/checkemail" || target.pathname.toLowerCase() === "/register")) {
                // Keep the form when the controller redirects with a registration error.
                if (target.pathname.toLowerCase() === "/register" && target.searchParams.get("result") === "failed")
                    return { status: 400, body: JSON.stringify({ error: "RegistrationFailed" }), redirectUrl: "" };
                if (response.ok) return { status: response.status, body: "", redirectUrl: target.pathname + target.search };
            }
            return { status: 502, body: "", redirectUrl: "" };
        }
        const body = await response.text();
        if (response.ok) {
            try {
                const registration = JSON.parse(body);
                if (typeof registration?.userId === "string" && registration.userId.trim() &&
                    registration.requiresEmailConfirmation === true)
                    return { status: response.status, body, redirectUrl: registration.confirmationEmailSent === false
                        ? "/Register/CheckEmail?delivery=failed" : "/Register/CheckEmail" };
            } catch (error) {
                if (!(error instanceof SyntaxError)) throw error;
            }
        }
        return { status: response.status, body, redirectUrl: "" };
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError") return { status: 0, body: "", cancelled: true };
        if (error instanceof TypeError) return { status: 0, body: "", cancelled: false };
        throw error;
    } finally {
        if (submissions.get(formId) === controller) submissions.delete(formId);
    }
}

export function cancelRegistration(formId) {
    submissions.get(formId)?.abort();
    submissions.delete(formId);
    validationStates.get(formId)?.dispose();
    validationStates.delete(formId);
}

export function focusRegistrationError() {
    const error = document.getElementById("registration-error");
    error?.focus({ preventScroll: true });
    error?.scrollIntoView({ block: "center", behavior: "auto" });
}
