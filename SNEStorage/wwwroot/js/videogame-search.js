const searches = new Map();

// Searching for a game must not submit the resource form when Enter is pressed.
document.addEventListener("keydown", event => {
    if (event.key === "Enter" && event.target instanceof HTMLInputElement &&
        event.target.hasAttribute("data-videogame-search")) {
        event.preventDefault();
    }
});

export function cancelSearch(key) {
    searches.get(key)?.abort();
    searches.delete(key);
}

export async function searchVideogames(key, likeString, maxAmount) {
    cancelSearch(key);
    const controller = new AbortController();
    searches.set(key, controller);
    const url = new URL("/api/videogames/search", document.baseURI);
    url.searchParams.set("likeString", likeString);
    url.searchParams.set("maxAmount", String(maxAmount));

    try {
        const response = await fetch(url, {
            credentials: "same-origin",
            headers: { Accept: "application/json" },
            redirect: "error",
            signal: controller.signal
        });
        return {
            status: response.status,
            body: await response.text(),
            contentType: response.headers.get("Content-Type") ?? "",
            cancelled: false
        };
    } catch (error) {
        if (controller.signal.aborted) {
            return { status: 0, body: "", contentType: "", cancelled: true };
        }
        throw error;
    } finally {
        if (searches.get(key) === controller) {
            searches.delete(key);
        }
    }
}
