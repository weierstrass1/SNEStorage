export function scrollToAnchor() {
    // Called only for a deep link, after its content has rendered.
    try {
        const id = decodeURIComponent(window.location.hash.slice(1));
        if (id) document.getElementById(id)?.scrollIntoView({ block: "start" });
    } catch { /* Ignore a malformed fragment. */ }
}
