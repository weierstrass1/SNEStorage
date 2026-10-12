import { getProfile, cancelProfileRequest } from "./profile.js?v=10";

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
    return read("/api/resource/options", key);
}

export function getResourceSection(resourceId, section, key) {
    if (!sections.includes(section)) throw new Error("Unsupported resource section.");
    return read(`/api/resource/${encodeURIComponent(resourceId)}/${section}`, `${key}-section-${section}`);
}

export function cancelResourceReads(key) {
    cancelProfileRequest(key);
    for (const section of sections) cancelProfileRequest(`${key}-section-${section}`);
}
