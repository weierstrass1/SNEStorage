// Presentation only: detect when more comments are needed and reveal a confirmed post.
export function createCommentFeed(root, sentinel, receiver) {
    let enabled = false;
    let stopped = false;
    let requesting = false;
    let generation = 0;
    let frame = 0;

    function nearEnd() {
        if (!root.isConnected || !sentinel.isConnected || sentinel.getClientRects().length === 0) return false;
        const bounds = sentinel.getBoundingClientRect();
        return bounds.top <= window.innerHeight + 200 && bounds.bottom >= -200;
    }

    function schedule() {
        if (stopped || !enabled || requesting || frame) return;
        frame = requestAnimationFrame(async () => {
            frame = 0;
            if (stopped || !enabled || requesting || !nearEnd()) return;
            requesting = true;
            // One request per render. The component disables the observer while it is loading.
            enabled = false;
            try {
                await receiver.invokeMethodAsync("LoadMoreComments", generation);
            } catch {
                stop();
            } finally {
                requesting = false;
                schedule();
            }
        });
    }

    const observer = typeof IntersectionObserver === "function"
        ? new IntersectionObserver(entries => {
            if (entries.some(entry => entry.isIntersecting)) schedule();
        }, { rootMargin: "200px 0px" })
        : null;
    observer?.observe(sentinel);

    function stop() {
        stopped = true;
        enabled = false;
        observer?.disconnect();
        if (frame) cancelAnimationFrame(frame);
        frame = 0;
    }

    return {
        update(canLoadMore, version) {
            if (stopped) return;
            enabled = Boolean(canLoadMore) && observer !== null;
            generation = version;
            schedule();
        },
        stop
    };
}

export function revealComment(root, commentId) {
    // Search within the owning feed, so an expanded nested thread cannot target another list.
    const comment = root.querySelector(`[data-comment-id="${CSS.escape(String(commentId))}"]`);
    if (!comment) return;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    comment.scrollIntoView({ behavior: reducedMotion ? "instant" : "smooth", block: "center" });
    comment.focus({ preventScroll: true });
    comment.classList.remove("comment-new-highlight");
    // Restart the short highlight when the same comment is revealed again.
    void comment.offsetWidth;
    comment.classList.add("comment-new-highlight");
}
