const selections = new Map();
const maxCount = 20;
const maxBytes = 5 * 1024 * 1024;
const imageExtensions = new Set(["png", "jpg", "jpeg", "gif", "webp", "avif"]);
const videoExtensions = new Set(["mp4", "webm", "ogv"]);

function result(items, errors = []) {
    return { items: items.map(item => ({ id: item.id, name: item.file.name,
        size: item.file.size, url: item.url, video: item.video })), errors };
}

export function addPreviews(inputId, capacity = maxCount) {
    const input = document.getElementById(inputId);
    if (!(input instanceof HTMLInputElement)) throw new Error("Preview input is unavailable.");
    const items = selections.get(inputId) ?? [];
    const errors = [];
    for (const file of input.files ?? []) {
        const extension = file.name.split(".").pop().toLowerCase();
        if (!imageExtensions.has(extension) && !videoExtensions.has(extension)) {
            errors.push(`${file.name}: choose a PNG, JPG, GIF, WEBP, AVIF, MP4, WEBM, or OGV file.`);
        } else if (file.size === 0 || file.size > maxBytes) {
            errors.push(`${file.name}: each preview must be nonempty and no larger than 5 MB.`);
        } else if (items.some(item => item.file.name === file.name && item.file.size === file.size &&
            item.file.lastModified === file.lastModified)) {
            errors.push(`${file.name}: this file is already selected.`);
        } else if (items.length >= Math.min(maxCount, capacity)) {
            errors.push(`${file.name}: a resource can have up to 20 previews.`);
        } else {
            items.push({ id: crypto.randomUUID(), file, url: URL.createObjectURL(file),
                video: videoExtensions.has(extension) });
        }
    }
    selections.set(inputId, items);
    input.value = "";
    return result(items, errors);
}

export function removePreview(inputId, id) {
    const items = selections.get(inputId) ?? [];
    const removed = items.find(item => item.id === id);
    if (removed) URL.revokeObjectURL(removed.url);
    const remaining = items.filter(item => item.id !== id);
    selections.set(inputId, remaining);
    return result(remaining);
}

export function previewCount(inputId) {
    return (selections.get(inputId) ?? []).length;
}

export function appendPreviews(data, inputId, capacity = maxCount) {
    data.delete("Previews");
    const items = selections.get(inputId) ?? [];
    if (items.length > Math.min(maxCount, capacity) || items.some(item => item.file.size === 0 || item.file.size > maxBytes)) {
        throw new Error("Choose up to 20 previews, no larger than 5 MB each.");
    }
    for (const item of items) data.append("Previews", item.file, item.file.name);
}

export function disposePreviews(inputId) {
    for (const item of selections.get(inputId) ?? []) URL.revokeObjectURL(item.url);
    selections.delete(inputId);
}
