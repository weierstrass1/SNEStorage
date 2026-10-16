// Load formatting once per document; safe text is a fallback only on failure.
import("./markdown.js?v=5").then(({ renderMarkdown }) => start(renderMarkdown), error => {
    console.warn("SNEStorage Markdown could not be loaded.", error);
    start(null);
});

function start(renderMarkdown) {
    // Format each incoming component in the same browser update, without a JS
    // interop round trip per news item, description, biography or comment.
    const rendered = new WeakMap();
    const hostSelector = "[data-markdown-text]";

    function formatHost(host) {
        if (!host.isConnected) return;
        const source = host.querySelector(":scope > [data-markdown-source]")?.textContent ?? "";
        const output = host.querySelector(":scope > [data-markdown-output]");
        if (!output) return;
        const excerpt = host.dataset.markdownExcerpt === "true";
        const previous = rendered.get(host);
        if (previous?.source === source && previous.excerpt === excerpt) return;
        try {
            if (renderMarkdown) {
                renderMarkdown(output, source, excerpt);
                output.classList.remove("markdown-plain");
            } else {
                output.textContent = source;
                output.classList.add("markdown-plain");
            }
        } catch (error) {
            // If formatting fails, display encoded text instead of unsafe HTML.
            output.textContent = source;
            output.classList.add("markdown-plain");
            console.warn("SNEStorage Markdown could not be formatted safely.", error);
        }
        rendered.set(host, { source, excerpt });
        host.setAttribute("data-markdown-ready", "");
        host.setAttribute("aria-busy", "false");
    }

    new MutationObserver(records => {
        const hosts = new Set();
        const added = new Set();
        for (const record of records) {
            const target = record.target instanceof Element ? record.target : record.target.parentElement;
            if (!target || target.closest("[data-markdown-output]")) continue;
            // Text edits matter only inside the encoded source; unrelated
            // counters, player progress and metadata need no Markdown work.
            if (target.matches(hostSelector)) hosts.add(target);
            else if (target.matches("[data-markdown-source]")) {
                const host = target.closest(hostSelector);
                if (host) hosts.add(host);
            }
            for (const node of record.addedNodes)
                if (node instanceof Element && node.isConnected) added.add(node);
        }
        for (const node of added) {
            // A Blazor batch adds both parents and children. Scan the outer
            // subtree once rather than querying it again for each descendant.
            let parent = node.parentElement;
            while (parent && !added.has(parent)) parent = parent.parentElement;
            if (parent) continue;
            if (node.matches(hostSelector)) hosts.add(node);
            node.querySelectorAll(hostSelector).forEach(element => hosts.add(element));
        }
        hosts.forEach(formatHost);
    }).observe(document.documentElement, {
        subtree: true, childList: true, characterData: true, attributes: true,
        attributeFilter: ["data-markdown-excerpt"]
    });

    document.querySelectorAll(hostSelector).forEach(formatHost);
}
