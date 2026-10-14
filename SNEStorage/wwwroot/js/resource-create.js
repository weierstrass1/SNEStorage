import { appendPreviews, validatePreviews } from "./resource-previews.js?v=4";
import { basicUploadError } from "./upload-validation.js?v=2";
import { captchaFetch } from "./captcha.js?v=3";
import { validateVersionInputs } from "./resource-version.js?v=2";
import { validateResourceTextFields } from "./resource-form-validation.js?v=1";

const uploads = new Map();

async function readResponse(response) {
    if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
    return {
        status: response.status,
        body: await response.text(),
        contentType: response.headers.get("Content-Type") ?? ""
    };
}

// Enter commits a tag through Blazor instead of submitting the entire form.
document.addEventListener("keydown", event => {
    if (event.key === "Enter" && event.target instanceof HTMLInputElement &&
        event.target.hasAttribute("data-resource-token-input")) {
        event.preventDefault();
    }
    if (event.target instanceof HTMLElement && event.target.hasAttribute("data-resource-tab") &&
        ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) event.preventDefault();
});

function invalidField(form) {
    return Array.from(form.elements).find(field => typeof field.checkValidity === "function" && !field.checkValidity());
}

function invalidMessage(field) {
    const label = field.labels?.[0]?.textContent.replace(/\s*\*\s*/g, "").trim() || "this field";
    if (field.validity.customError) return field.validationMessage;
    if (field.validity.valueMissing) return `Complete ${label} before submitting.`;
    if (field.type === "url") return `Enter a valid HTTP or HTTPS URL for ${label}.`;
    return `Check ${label} before submitting.`;
}

export async function validateResourceForm(formId) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement)) return { valid: false, tab: 0, message: "The form is unavailable." };
    // Check trimmed text as well as native constraints, including values restored by Blazor.
    validateResourceTextFields(form);
    validateVersionInputs(form);
    const invalid = invalidField(form);
    if (invalid) return { valid: false,
        tab: Number(invalid.closest("[data-resource-tab-panel]")?.dataset.resourceTabPanel ?? 0),
        message: invalidMessage(invalid) };
    const archive = form.elements.namedItem("File")?.files?.[0];
    const archiveError = basicUploadError(archive, "archive");
    if (archiveError) return { valid: false, tab: 1, message: archiveError };
    const previewError = await validatePreviews("resource-previews");
    if (previewError) return { valid: false, tab: 2, message: previewError };
    return { valid: true, tab: 0, message: "" };
}

export function focusInvalidField(formId) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement)) return;
    const invalid = invalidField(form);
    if (!invalid) return;
    const details = invalid.closest("details");
    if (details) details.open = true;
    invalid.focus();
    invalid.reportValidity();
}

export function focusTab(id) { document.getElementById(id)?.focus(); }

export function syncTabs(formId, tabId) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement)) return;
    for (const video of form.querySelectorAll("[data-resource-tab-panel][hidden] video")) video.pause();
    for (const picker of form.querySelectorAll("[data-resource-tab-panel][hidden] [data-resource-picker]"))
        picker.dispatchEvent(new Event("resource-picker-dismiss"));
    const tab = document.getElementById(tabId);
    const tabList = tab?.parentElement;
    if (tab && tabList) {
        const left = tab.offsetLeft - tabList.offsetLeft;
        if (left < tabList.scrollLeft) tabList.scrollLeft = left;
        else if (left + tab.offsetWidth > tabList.scrollLeft + tabList.clientWidth)
            tabList.scrollLeft = left + tab.offsetWidth - tabList.clientWidth;
    }
}

export async function getCreateOptions() {
    const response = await fetch("/api/resource/options", {
        credentials: "same-origin",
        headers: { Accept: "application/json" },
        redirect: "error"
    });
    return readResponse(response);
}

export async function submitResource(formId) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement) || !(await validateResourceForm(formId)).valid) {
        return { status: 0, body: "", contentType: "" };
    }

    const data = new FormData(form);
    for (const name of ["Name", "Description", "Version", "ChangeLog", "RepositoryURL"]) {
        const value = data.get(name);
        if (typeof value === "string") data.set(name, value.trim());
    }
    appendPreviews(data, "resource-previews");
    // An unchecked checkbox does not appear in FormData, including IAmAuthor (whose DTO default is true).
    for (const name of ["IAmAuthor", "SealOfQualityRequested", "IncludesPorn", "IncludesGore", "IncludesPolitics", "IncludesSlurs", "IncludesSensitiveContent"]) {
        data.set(name, form.elements.namedItem(name)?.checked ? "true" : "false");
    }

    // Include a tag still being typed as well as the tags already added to the visual list.
    const tags = data.getAll("Tags").filter(value => typeof value === "string")
        .flatMap(value => value.split(",")).map(value => value.trim()).filter(Boolean);
    data.delete("Tags");
    const seen = new Set();
    for (const tag of tags) {
        const key = tag.toLowerCase();
        if (!seen.has(key)) {
            data.append("Tags", tag);
            seen.add(key);
        }
    }

    // Author names are whole names, not comma-separated tags. Include any name still being typed.
    const externalAuthors = data.getAll("ExternalAuthors").filter(value => typeof value === "string")
        .map(value => value.trim()).filter(Boolean);
    data.delete("ExternalAuthors");
    const authorNames = new Set();
    for (const author of externalAuthors) {
        const key = author.toLowerCase();
        if (!authorNames.has(key)) {
            data.append("ExternalAuthors", author);
            authorNames.add(key);
        }
    }
    const authorIds = new Set(data.getAll("AuthorsIDs").filter(value => typeof value === "string" && value.length > 0));
    data.delete("AuthorsIDs");
    for (const id of authorIds) data.append("AuthorsIDs", id);
    const dependencyIds = new Set(data.getAll("DependencyIDs").filter(value => typeof value === "string" && value.length > 0));
    data.delete("DependencyIDs");
    for (const id of dependencyIds) data.append("DependencyIDs", id);

    const controller = new AbortController();
    uploads.set(formId, controller);
    try {
        const response = await captchaFetch(form.action, {
            method: "POST",
            credentials: "same-origin",
            headers: { Accept: "application/json" },
            body: data,
            redirect: "error",
            signal: controller.signal
        }, "resource_create", true);
        return await readResponse(response);
    } finally {
        if (uploads.get(formId) === controller) {
            uploads.delete(formId);
        }
    }
}

export function cancelSubmission(formId) {
    uploads.get(formId)?.abort();
    uploads.delete(formId);
}
