import { getResource, getResourceOptions, getResourceSection, getResourceAccount, cancelResourceRequest } from "./resource-details.js?v=14";
import { appendPreviews, previewCount, validatePreviews } from "./resource-previews.js?v=4";
import { basicUploadError } from "./upload-validation.js?v=2";
import { captchaFetch } from "./captcha.js?v=3";
import { validateVersionInputs } from "./resource-version.js?v=2";
import { validateResourceTextFields } from "./resource-form-validation.js?v=2";

export { getResource, getResourceOptions, getResourceSection, getResourceAccount };

const submissions = new Map();
const contentFlags = ["IncludesPorn", "IncludesGore", "IncludesPolitics", "IncludesSlurs", "IncludesSensitiveContent"];

document.addEventListener("keydown", event => {
    if (event.target instanceof Element && event.target.closest("[data-resource-editor] [role='tab']") &&
        ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) event.preventDefault();
    if (event.key === "Enter" && event.target instanceof HTMLInputElement &&
        event.target.hasAttribute("data-resource-token-input") && event.target.closest("[data-resource-editor]")) {
        event.preventDefault();
    }
});

async function invalidInput(form, mode, previewInputId, original) {
    for (const field of form.elements) {
        if (typeof field.setCustomValidity === "function") field.setCustomValidity("");
    }
    validateResourceTextFields(form, mode === "EditVersion" ? original?.version?.releaseNotes ?? "" : null, original);
    for (const field of form.querySelectorAll("input[name^='ExternalDependencies[']")) {
        if (field.required && !field.value.trim()) field.setCustomValidity("Complete this dependency field before saving.");
    }
    validateVersionInputs(form, mode === "EditVersion" ? original?.version?.label ?? null : null);
    const invalid = Array.from(form.elements).find(field => typeof field.checkValidity === "function" && !field.checkValidity());
    if (invalid) {
        const label = invalid.labels?.[0]?.textContent.trim() || "this field";
        const message = invalid.validity.customError ? invalid.validationMessage
            : invalid.validity.valueMissing ? `Complete ${label} before saving.`
            : invalid.type === "url" ? `Enter a valid HTTP or HTTPS URL for ${label}.`
            : `Check ${label} before saving.`;
        invalid.setCustomValidity(message);
        return { validationMessage: message, validationTab: Number(invalid.closest("[data-editor-panel]")?.dataset.editorPanel ?? 0) };
    }
    if (mode === "Resource") {
        if (!form.elements.namedItem("VideogameId")?.value || !Number(form.elements.namedItem("VisibilityId")?.value))
            return { validationMessage: "Choose a video game and a visibility setting.", validationTab: 0 };
        if (!form.querySelector("input[name='Types']:checked")) return { validationMessage: "Select at least one resource type.", validationTab: 0 };
        if (form.querySelectorAll("input[name='KeptPreviewIDs']").length + previewCount(previewInputId) > 20)
            return { validationMessage: "Keep up to 20 previews in total. Remove an existing or new preview before saving.", validationTab: 1 };
        const capacity = Math.max(0, 20 - form.querySelectorAll("input[name='KeptPreviewIDs']").length);
        const previewError = await validatePreviews(previewInputId, capacity);
        if (previewError) return { validationMessage: previewError, validationTab: 1 };
    }
    if (mode === "CreateVersion") {
        const file = form.elements.namedItem("File")?.files?.[0];
        const archiveError = basicUploadError(file, "archive");
        if (archiveError) {
            form.elements.namedItem("File")?.focus();
            return { validationMessage: archiveError };
        }
    }
    return null;
}

// The edit DTO contains additions plus the IDs of original join rows to remove.
function appendSelectionChanges(data, original) {
    const tagNames = new Map();
    for (const value of data.getAll("Tags")) {
        if (typeof value !== "string") continue;
        for (const text of value.split(",")) {
            const tag = text.trim();
            if (tag) tagNames.set(tag.toLowerCase(), tag);
        }
    }
    const previousTags = original?.tags ?? [];
    const originalNames = new Set(previousTags.map(tag => tag.name.trim().toLowerCase()));
    data.delete("Tags");
    data.delete("RemoveTagIDs");
    for (const [key, name] of tagNames) {
        if (!originalNames.has(key)) data.append("Tags", name);
    }
    for (const tag of previousTags) {
        if (!tagNames.has(tag.name.trim().toLowerCase())) data.append("RemoveTagIDs", String(tag.id));
    }

    const typeIds = new Set(data.getAll("Types").map(String));
    const previousTypes = original?.types ?? [];
    const originalTypeIds = new Set(previousTypes.map(type => String(type.typeId)));
    data.delete("Types");
    data.delete("RemovedTypeIDs");
    for (const id of typeIds) {
        if (!originalTypeIds.has(id)) data.append("Types", id);
    }
    for (const type of previousTypes) {
        if (!typeIds.has(String(type.typeId))) data.append("RemovedTypeIDs", String(type.id));
    }

    appendListChanges(data, "AuthorsIDs", "RemoveAuthorsIDs", original?.authors ?? [], item => item.userId);
    appendListChanges(data, "ExternalAuthors", "RemoveExternalAuthorsIDs", original?.externalAuthors ?? [], item => item.name, true);
    appendListChanges(data, "DependencyIDs", "RemoveDependencyIDs", original?.dependencies ?? [], item => String(item.resourceId));

    const kept = new Set(data.getAll("KeptPreviewIDs").map(String));
    data.delete("KeptPreviewIDs");
    for (const id of original?.previewIds ?? []) {
        if (!kept.has(String(id))) data.append("RemovePreviewIDs", String(id));
    }
    appendExternalDependencyChanges(data, original?.externalDependencies ?? []);
}

function appendListChanges(data, field, removalField, previous, valueOf, ignoreCase = false) {
    const keyOf = value => ignoreCase ? value.trim().toLowerCase() : value.trim();
    const selected = new Map(data.getAll(field).filter(value => typeof value === "string" && value.trim())
        .map(value => [keyOf(value), value.trim()]));
    const originalValues = new Set(previous.map(item => keyOf(valueOf(item))));
    data.delete(field);
    data.delete(removalField);
    for (const [key, value] of selected) if (!originalValues.has(key)) data.append(field, value);
    for (const item of previous) if (!selected.has(keyOf(valueOf(item)))) data.append(removalField, String(item.id));
}

const dependencyFields = ["DependencyName", "DependencyURL", "Mirror1", "Mirror2", "Mirror3", "Mirror4"];
function dependencyValues(item) {
    return dependencyFields.map(field => String(item[field[0].toLowerCase() + field.slice(1)] ?? "").trim());
}

// ExternalDependencyDTO has no edit ID: changed rows are removed and added with their new values.
function appendExternalDependencyChanges(data, previous) {
    const entries = new Map();
    for (const [key, value] of data) {
        const match = /^ExternalDependencies\[(\d+)\]\.(\w+)$/.exec(key);
        if (!match || !dependencyFields.includes(match[2]) || typeof value !== "string") continue;
        if (!entries.has(Number(match[1]))) entries.set(Number(match[1]), {});
        entries.get(Number(match[1]))[match[2][0].toLowerCase() + match[2].slice(1)] = value;
    }
    for (const key of Array.from(data.keys())) if (key.startsWith("ExternalDependencies[")) data.delete(key);
    const remaining = [...previous];
    const additions = [];
    for (const [, item] of [...entries].sort((a, b) => a[0] - b[0])) {
        const values = dependencyValues(item);
        const signature = JSON.stringify(values);
        const match = remaining.findIndex(original => JSON.stringify(dependencyValues(original)) === signature);
        if (match >= 0) remaining.splice(match, 1);
        else additions.push(values);
    }
    for (const item of remaining) data.append("RemoveExternalDependencyIDs", String(item.id));
    additions.forEach((values, index) => dependencyFields.forEach((field, offset) =>
        data.append(`ExternalDependencies[${index}].${field}`, values[offset])));
}

export async function submitEditor(formId, resourceId, versionId, mode, original, requestKey, previewInputId) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement)) return { status: 0, body: "", cancelled: false, validationMessage: "The form is unavailable. Please reload the page." };
    const validation = await invalidInput(form, mode, previewInputId, original);
    if (validation) return { status: 0, body: "", cancelled: false, ...validation };

    const data = new FormData(form);
    for (const name of ["Name", "Description", "RepositoryURL", "Version"]) {
        const value = data.get(name);
        if (typeof value === "string") data.set(name, value.trim());
    }
    const root = `/api/resource/${encodeURIComponent(resourceId)}`;
    let url, method;
    if (mode === "Resource") {
        url = `${root}/edit`;
        method = "PUT";
        data.set("Id", String(resourceId));
        for (const name of contentFlags) data.set(name, form.elements.namedItem(name)?.checked ? "true" : "false");
        const isAuthor = form.elements.namedItem("IAmAuthor")?.checked === true;
        const userId = original?.currentUserId;
        const wasAuthor = (original?.authors ?? []).some(author => author.userId === userId);
        // Include self in the selection diff so unchecking removes the original join row.
        if (isAuthor && userId) data.append("AuthorsIDs", userId);
        if (isAuthor === wasAuthor) data.delete("IAmAuthor");
        else data.set("IAmAuthor", isAuthor ? "true" : "false");
        if (data.get("RepositoryURL") === (original?.repositoryURL ?? "").trim()) data.delete("RepositoryURL");
        const keptCount = data.getAll("KeptPreviewIDs").length;
        appendSelectionChanges(data, original);
        appendPreviews(data, previewInputId, Math.max(0, 20 - keptCount));
    } else if (mode === "CreateVersion") {
        url = `${root}/version/create/submit`;
        method = "POST";
        data.set("SealOfQualityRequested", form.elements.namedItem("SealOfQualityRequested")?.checked ? "true" : "false");
    } else if (mode === "EditVersion" && versionId > 0) {
        url = `${root}/version/${encodeURIComponent(versionId)}/edit`;
        method = "PUT";
        data.set("SealOfQualityRequested", form.elements.namedItem("SealOfQualityRequested")?.checked ? "true" : "false");
        const previous = original?.version;
        if (previous) {
            if (data.get("Version") === (previous.label ?? "").trim()) data.delete("Version");
            const notes = String(data.get("ReleaseNotes") ?? "").replaceAll("\r\n", "\n");
            if (notes === (previous.releaseNotes ?? "").replaceAll("\r\n", "\n")) data.delete("ReleaseNotes");
            if (form.elements.namedItem("SealOfQualityRequested")?.checked === previous.sealOfQualityRequested)
                data.delete("SealOfQualityRequested");
        }
    } else {
        return { status: 0, body: "", cancelled: false, validationMessage: "The requested editor is unavailable." };
    }

    const controller = new AbortController();
    submissions.set(requestKey, controller);
    try {
        const captchaAction = mode === "Resource" ? "resource_edit"
            : mode === "CreateVersion" ? "resource_createVersion" : "resource_editVersion";
        const response = await captchaFetch(url, {
            method, credentials: "same-origin", headers: { Accept: "application/json" },
            body: data, redirect: "error", signal: controller.signal
        }, captchaAction, true);
        if (response.status === 401) window.dispatchEvent(new Event("snestorage-session-rejected"));
        return { status: response.status, body: await response.text(), cancelled: response.status === 499, validationMessage: "" };
    } catch (error) {
        if (controller.signal.aborted || error.name === "AbortError") return { status: 0, body: "", cancelled: true, validationMessage: "" };
        if (error instanceof TypeError) return { status: 0, body: "", cancelled: false, validationMessage: "" };
        throw error;
    } finally {
        if (submissions.get(requestKey) === controller) submissions.delete(requestKey);
    }
}

export function cancelEditorRequest(requestKey) {
    cancelResourceRequest(requestKey);
    submissions.get(requestKey)?.abort();
    submissions.delete(requestKey);
}

export function focusInvalidEditor(formId) {
    const form = document.getElementById(formId);
    if (!(form instanceof HTMLFormElement)) return;
    const invalid = Array.from(form.elements).find(field => typeof field.checkValidity === "function" && !field.checkValidity());
    if (invalid) {
        for (let parent = invalid.parentElement; parent && parent !== form; parent = parent.parentElement)
            if (parent instanceof HTMLDetailsElement) parent.open = true;
        invalid.focus();
        invalid.reportValidity();
    } else {
        form.querySelector("[data-editor-panel]:not([hidden])")?.focus({ preventScroll: true });
    }
}

export function syncEditorTabs(formId, activeTab, focusTab) {
    const form = document.getElementById(formId);
    if (!form) return;
    for (const panel of form.querySelectorAll("[data-editor-panel]")) {
        if (Number(panel.dataset.editorPanel) === activeTab) continue;
        for (const video of panel.querySelectorAll("video")) video.pause();
        for (const picker of panel.querySelectorAll("[data-resource-picker]")) picker.dispatchEvent(new Event("resource-picker-dismiss"));
    }
    const tab = form.querySelectorAll("[role='tab']")[activeTab];
    if (focusTab) tab?.focus({ preventScroll: true });
    if (tab && tab.parentElement) {
        const list = tab.parentElement;
        const left = tab.offsetLeft - list.offsetLeft;
        if (left < list.scrollLeft) list.scrollLeft = left;
        else if (left + tab.offsetWidth > list.scrollLeft + list.clientWidth)
            list.scrollLeft = left + tab.offsetWidth - list.clientWidth;
    }
}
