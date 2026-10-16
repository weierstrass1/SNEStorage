import { readResourceOptions } from "./resource-options.js?v=1";
import { getProfile, cancelProfileRequest } from "./profile.js?v=14";

const initialSections = new Map();
const sections = ["authors", "external-authors", "dependencies", "external-dependencies",
    "versions", "versions/latest", "rating", "previews"];

async function read(url, key) {
    const timeout = setTimeout(() => cancelProfileRequest(key), 30000);
    try { return await getProfile(url, key, false); }
    finally { clearTimeout(timeout); }
}

export function getResource(resourceId, key) {
    return read(`/api/resource/${encodeURIComponent(resourceId)}`, key);
}

export function getResourceOptions(key) {
    return readResourceOptions().then(response => JSON.stringify(response));
}

export function getResourceSection(resourceId, section, key) {
    if (!sections.includes(section)) throw new Error("Unsupported resource section.");
    const initial = initialSections.get(key);
    if (initial?.resourceId === resourceId && initial.reads.has(section)) {
        const pending = initial.reads.get(section);
        initial.reads.delete(section);
        if (!initial.reads.size) initialSections.delete(key);
        return pending;
    }
    return read(`/api/resource/${encodeURIComponent(resourceId)}/${section}`, `${key}-section-${section}`);
}

export function cancelResourceReads(key) {
    initialSections.delete(key);
    cancelProfileRequest(key);
    for (const section of sections) cancelProfileRequest(`${key}-section-${section}`);
}

export function beginOverviewReads(resourceId, key) {
    const reads = new Map();
    initialSections.set(key, { resourceId, reads });
    for (const section of sections) {
        if (section === "versions") continue; // Full history stays lazy.
        reads.set(section, read(`/api/resource/${encodeURIComponent(resourceId)}/${section}`, `${key}-section-${section}`)
            .catch(() => JSON.stringify({ status: 0, body: "", cancelled: false })));
    }
}
