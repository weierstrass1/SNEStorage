import { Marked } from "../lib/markdown/marked.esm.js";
import DOMPurify from "../lib/markdown/purify.es.js";

function escapeHtml(text) {
    return String(text ?? "").replace(/[&<>"']/g, character =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[character]);
}

// One isolated parser; never register extensions again when a comment rerenders.
const parser = new Marked({
    gfm: true,
    breaks: true,
    async: false,
    renderer: {
        html({ text }) { return escapeHtml(text); },
        image({ text }) { return escapeHtml(text); },
        checkbox({ checked }) { return checked ? "&#9745; " : "&#9744; "; }
    }
});

const sanitizeOptions = {
    ALLOWED_TAGS: ["p", "br", "strong", "em", "del", "a", "code", "pre", "blockquote",
        "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6", "hr",
        "table", "thead", "tbody", "tr", "th", "td"],
    ALLOWED_ATTR: ["href", "title", "start"],
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false,
    RETURN_DOM_FRAGMENT: true
};

export function renderMarkdown(element, source, excerpt = false) {
    if (!(element instanceof HTMLElement) || !element.isConnected)
        return;
    if (!DOMPurify.isSupported)
        throw new Error("Safe Markdown rendering is unavailable.");

    const fragment = DOMPurify.sanitize(parser.parse(String(source ?? "")), sanitizeOptions);
    if (excerpt) {
        // CSS limits rendered lines at the current width/font size; no character truncation.
        // Use plain text from sanitized markup so headings, code and tables cannot expand a preview.
        const copy = fragment.cloneNode(true);
        for (const block of copy.querySelectorAll("p,li,blockquote,h1,h2,h3,h4,h5,h6,pre,tr")) block.append("\n");
        for (const cell of copy.querySelectorAll("th,td")) cell.append(" ");
        for (const lineBreak of copy.querySelectorAll("br,hr")) lineBreak.replaceWith("\n");
        const plainText = (copy.textContent ?? "").replace(/\r\n?/g, "\n")
            .replace(/[^\S\n]+/g, " ").replace(/\n{3,}/g, "\n\n").trim();
        element.replaceChildren(document.createTextNode(plainText));
        return;
    }
    for (const link of fragment.querySelectorAll("a")) {
        const href = link.getAttribute("href");
        if (!href) continue;
        try {
            const url = new URL(href, document.baseURI);
            if (!["http:", "https:", "mailto:"].includes(url.protocol) || url.username || url.password) {
                link.removeAttribute("href");
                continue;
            }
            link.setAttribute("target", "_blank");
            link.setAttribute("rel", "noopener noreferrer");
        } catch {
            link.removeAttribute("href");
        }
    }

    for (const table of fragment.querySelectorAll("table")) {
        const scroll = document.createElement("div");
        scroll.className = "markdown-table-scroll";
        scroll.tabIndex = 0;
        scroll.setAttribute("role", "region");
        scroll.setAttribute("aria-label", "Scrollable table");
        table.replaceWith(scroll);
        scroll.append(table);
    }

    // Never send generated HTML back over SignalR or mutate the component's siblings.
    element.replaceChildren(fragment);
}
