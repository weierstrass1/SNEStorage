// MVC validates this request token together with its HttpOnly antiforgery cookie.
export function antiforgeryFetch(url, options = {}) {
    const endpoint = new URL(url, document.baseURI);
    if (endpoint.origin !== window.location.origin)
        throw new TypeError("Verification tokens must stay on the SNEStorage origin.");

    const headers = new Headers(options.headers);
    const method = (options.method ?? "GET").toUpperCase();
    if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method)) {
        const token = document.querySelector('#request-verification input[name="__RequestVerificationToken"]')?.value;
        if (!token) return Promise.resolve(new Response(JSON.stringify({ error: "AntiforgeryTokenMissing" }), {
            status: 400, headers: { "Content-Type": "application/json" }
        }));
        headers.set("X-CSRF-TOKEN", token);
    }
    return fetch(endpoint, { ...options, headers });
}
