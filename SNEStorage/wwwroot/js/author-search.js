// Enter adds an external author through Blazor instead of submitting the resource.
document.addEventListener("keydown", event => {
    if (event.key === "Enter" && event.target instanceof HTMLInputElement &&
        event.target.hasAttribute("data-resource-author-input")) {
        event.preventDefault();
    }
});

async function search(path, likeString, maxAmount, withAvatar) {
    const url = new URL(path, document.baseURI);
    url.searchParams.set("likeString", likeString);
    url.searchParams.set("maxAmount", String(maxAmount));
    if (typeof withAvatar === "boolean") url.searchParams.set("withAvatar", String(withAvatar));
    const response = await fetch(url, {
        credentials: "same-origin",
        headers: { Accept: "application/json" },
        redirect: "error"
    });
    return {
        status: response.status,
        body: await response.text(),
        contentType: response.headers.get("Content-Type") ?? ""
    };
}

export function searchUsers(likeString, maxAmount) {
    return search("/api/user/search", likeString, Math.min(maxAmount, 20), false);
}

export function searchExternalAuthors(likeString, maxAmount) {
    return search("/api/externa-author", likeString, maxAmount);
}

export function searchResources(likeString, maxAmount) {
    return search("/api/resource/search", likeString, maxAmount);
}
