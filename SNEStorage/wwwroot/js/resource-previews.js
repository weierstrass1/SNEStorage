import { validateUpload, uploadLimits } from "./upload-validation.js?v=2";
import { audioKind, normalizeAudioFile } from "./audio-formats.js?v=1";

const selections = new Map();
const maxCount = uploadLimits.previews;
const maxBytes = uploadLimits.mediaBytes;

function result(items, errors = []) {
    return { items: items.map(item => ({ id: item.id, name: item.file.name,
        size: item.file.size, url: item.url, video: item.video, kind: item.kind })), errors };
}

export async function addPreviews(inputId, capacity = maxCount) {
    const input = document.getElementById(inputId);
    if (!(input instanceof HTMLInputElement)) throw new Error("Preview input is unavailable.");
    let selection = selections.get(inputId);
    if (!selection) {
        selection = { items: [], pending: 0, disposed: false };
        selections.set(inputId, selection);
    }
    const items = selection.items;
    const files = Array.from(input.files ?? []);
    input.value = "";
    const errors = [];
    selection.pending++;
    try {
        for (const original of files) {
            const file = normalizeAudioFile(original);
            if (items.length >= Math.min(maxCount, capacity)) {
                errors.push(`${file.name}: a resource can have up to 20 previews. Remove an existing or new preview first.`);
                continue;
            }
            const message = await validateUpload(file, "preview");
            if (selection.disposed) return result([]);
            if (message) errors.push(`${file.name}: ${message}`);
            else if (items.some(item => item.file.name === file.name && item.file.size === file.size &&
                item.file.lastModified === file.lastModified)) errors.push(`${file.name}: this file is already selected.`);
            else if (items.length >= Math.min(maxCount, capacity)) errors.push(`${file.name}: a resource can have up to 20 previews.`);
            else items.push({ id: crypto.randomUUID(), file, url: URL.createObjectURL(file),
                video: file.type.startsWith("video/"), kind: audioKind(file.name, file.type) ?? "" });
        }
    } finally {
        selection.pending--;
    }
    return result(items, errors);
}

export function removePreview(inputId, id) {
    const items = selections.get(inputId)?.items ?? [];
    const index = items.findIndex(item => item.id === id);
    if (index >= 0) URL.revokeObjectURL(items.splice(index, 1)[0].url);
    return result(items);
}

export function previewCount(inputId) {
    return selections.get(inputId)?.items.length ?? 0;
}

export async function validatePreviews(inputId, capacity = maxCount) {
    const selection = selections.get(inputId);
    if (selection?.pending) return "Wait until the selected previews have finished checking before submitting.";
    const items = selection?.items ?? [];
    if (items.length > Math.min(maxCount, capacity)) return "Keep up to 20 previews in total. Remove an existing or new preview before saving.";
    for (const item of items) {
        const message = await validateUpload(item.file, "preview");
        if (message) return `${item.file.name}: ${message}`;
    }
    return "";
}

export function appendPreviews(data, inputId, capacity = maxCount) {
    data.delete("Previews");
    const selection = selections.get(inputId);
    const items = selection?.items ?? [];
    if (selection?.pending || items.length > Math.min(maxCount, capacity) || items.some(item => item.file.size === 0 || item.file.size > maxBytes)) {
        throw new Error("Choose up to 20 previews, no larger than 5 MB each.");
    }
    for (const item of items) data.append("Previews", item.file, item.file.name);
}

export function disposePreviews(inputId) {
    const selection = selections.get(inputId);
    if (selection) selection.disposed = true;
    for (const item of selection?.items ?? []) URL.revokeObjectURL(item.url);
    selections.delete(inputId);
}
