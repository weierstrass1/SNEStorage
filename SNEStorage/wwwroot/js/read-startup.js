// Initial HTTP reads and their module imports share one browser call.
// This transport does not own votes or submissions.
(() => {
    const pending = new Map();
    const methods = new Set(["readNews", "readNewsItem", "findPublicResources", "findResources",
        "beginResourceRead", "getCreateOptions", "readEditorBootstrap", "readPageAccess"]);
    function cancel(key) {
        const job = pending.get(key);
        if (!job) return;
        job.cancelled = true;
        clearTimeout(job.cleanup);
        pending.delete(key);
        job.release();
        if (job.module && job.cancelMethod) job.module[job.cancelMethod]?.(key);
    }
    async function run(resolveModule, method, args, key, cancelMethod, includeModule) {
        if (!methods.has(method)) throw new Error("Unsupported read.");
        const previous = pending.get(key);
        if (previous?.completed) { clearTimeout(previous.cleanup); pending.delete(key); }
        else cancel(key);
        const job = { cancelled: false, module: null, cancelMethod, release: null };
        const cancellation = new Promise(resolve => { job.release = resolve; });
        pending.set(key, job);
        try {
            job.module = await resolveModule();
            if (job.cancelled) return { cancelled: true };
            const response = await Promise.race([job.module[method](...args), cancellation]);
            if (job.cancelled) return { cancelled: true };
            const bytes = new TextEncoder().encode(JSON.stringify(response));
            job.completed = true;
            const module = includeModule ? DotNet.createJSObjectReference(job.module) : null;
            if (bytes.byteLength > 12 * 1024)
                return { module, responseStream: DotNet.createJSStreamReference(bytes), cancelled: false };
            return { module, response, cancelled: false };
        } finally {
            if (pending.get(key) === job) {
                // Disposal can still abort secondary reads while results are in transit.
                if (job.completed) job.cleanup = setTimeout(() => {
                    if (pending.get(key) === job) pending.delete(key);
                }, 30000);
                else pending.delete(key);
            }
        }
    }
    function start(path, method, args, key, cancelMethod) {
        const url = new URL(path, document.baseURI);
        if (url.origin !== location.origin || !url.pathname.startsWith("/js/"))
            throw new Error("Unsupported read module.");
        return run(() => import(url.href), method, args, key, cancelMethod, true);
    }
    function read(module, method, args, key, cancelMethod) {
        return run(() => module, method, args, key, cancelMethod, false);
    }
    window.snestorageReads = Object.freeze({ start, read, cancel });
})();
