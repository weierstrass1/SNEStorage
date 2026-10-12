const requests = new Map();

export async function readDocument(baseUri, requestKey, documentPath) {
    cancelRead(requestKey);
    const controller = new AbortController();
    requests.set(requestKey, controller);
    const timeout = setTimeout(() => controller.abort(), 15000);
    try {
        // Revalidate the public document rather than saving account data or a server cache.
        const response = await fetch(new URL(documentPath, baseUri), {
            cache: "no-cache",
            signal: controller.signal,
            headers: { Accept: "application/json" }
        });
        if (!response.ok) throw new Error(`Public document returned HTTP ${response.status}.`);
        const document = await response.json();
        if (!Array.isArray(document.sections) || document.sections.length === 0)
            throw new Error("The public document needs at least one section.");
        return document;
    } finally {
        clearTimeout(timeout);
        if (requests.get(requestKey) === controller) requests.delete(requestKey);
    }
}

export function cancelRead(requestKey) {
    requests.get(requestKey)?.abort();
    requests.delete(requestKey);
}

export function scrollToAnchor() {
    // Deep links may arrive before the asynchronously loaded rules are rendered.
    try {
        const id = decodeURIComponent(window.location.hash.slice(1));
        if (id) document.getElementById(id)?.scrollIntoView({ block: "start" });
    } catch { /* Ignore a malformed fragment. */ }
}
