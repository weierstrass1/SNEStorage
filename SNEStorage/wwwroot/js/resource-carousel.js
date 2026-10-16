import { loadPreview, releasePreview, getReadyPreview, previewKind } from "./resource-media.js?v=6";

const carousels = new Map();
const panels = ["loading", "error", "image", "audio", "play", "video", "other"];
const labels = { image: "Image", video: "Video", audio: "Audio", spc: "SPC audio", brr: "BRR audio" };

export function initializeCarousel(id, configuration) {
    const root = document.getElementById(id);
    if (!root) return;
    let state = carousels.get(id);
    const previousFile = state?.resourceId === configuration.resourceId ? state.files[state.index]?.fileId : null;
    if (!state || state.root !== root) {
        disposeCarousel(id);
        state = { id, root, files: [], index: 0, revision: 0, disposed: false,
            events: new AbortController(), audioModule: null };
        const find = name => root.querySelector(`[data-media-${name}]`);
        for (const name of [...panels, "stage", "reel", "empty", "content", "label", "position", "total", "indicators", "download"])
            state[name] = find(name);
        state.errorMessage = find("error-message");
        carousels.set(id, state);
        root.addEventListener("click", event => handleClick(state, event), { signal: state.events.signal });
        root.addEventListener("keydown", event => handleKey(state, event), { signal: state.events.signal });
        state.image.addEventListener("error", () => {
            if (state.panel === "image") showError(state, "This image could not be displayed. Please retry.");
        }, { signal: state.events.signal });
        state.video.addEventListener("error", () => {
            if (state.panel === "video") showError(state, "This video could not be played. Please retry.");
        }, { signal: state.events.signal });
    }
    stopMedia(state);
    releasePreview(id);
    state.resourceId = configuration.resourceId;
    state.resourceName = configuration.resourceName;
    state.audioPlayerId = configuration.audioPlayerId;
    state.enabled = configuration.enabled;
    state.files = configuration.files ?? [];
    state.index = Math.max(0, state.files.findIndex(file => file.fileId === previousFile));
    state.root.classList.toggle("has-neighbors", state.files.length > 1);
    state.root.classList.remove("is-sound", "is-video-playing");
    state.empty.hidden = state.files.length > 0;
    state.content.hidden = state.files.length === 0;
    state.indicators.hidden = state.files.length < 2;
    for (const button of root.querySelectorAll("[data-media-direction]")) {
        button.disabled = !state.enabled || state.files.length < 2;
        if (button.classList.contains("media-peek")) button.hidden = state.files.length < 2;
    }
    for (const button of root.querySelectorAll("[data-media-index]")) button.disabled = !state.enabled;
    if (state.enabled && state.files.length) selectSlide(state, state.index);
    else { state.revision++; setPanel(state, "loading"); }
}

function handleClick(state, event) {
    const button = event.target.closest("button");
    if (!button || !state.root.contains(button) || button.disabled || !state.enabled) return;
    if (button.hasAttribute("data-media-direction")) {
        const direction = Number(button.dataset.mediaDirection);
        selectSlide(state, (state.index + direction + state.files.length) % state.files.length, direction);
    } else if (button.hasAttribute("data-media-index")) {
        const index = Number(button.dataset.mediaIndex);
        if (index !== state.index) selectSlide(state, index, Math.sign(index - state.index));
    } else if (button.hasAttribute("data-media-play")) {
        selectSlide(state, state.index, 0, false, true);
    } else if (button.hasAttribute("data-media-retry")) {
        selectSlide(state, state.index, 0, true, state.playRequested);
    }
}

function handleKey(state, event) {
    if (!state.enabled || state.files.length < 2 || event.altKey || event.ctrlKey || event.metaKey ||
        event.target.closest("video, audio, input, select, textarea, .sound-preview")) return;
    const direction = event.key === "ArrowLeft" ? -1 : event.key === "ArrowRight" ? 1 : 0;
    if (!direction) return;
    event.preventDefault();
    selectSlide(state, (state.index + direction + state.files.length) % state.files.length, direction);
}

function neighbor(state, direction) {
    return state.files.length > 1 ? state.files[(state.index + direction + state.files.length) % state.files.length] : null;
}

function selectSlide(state, index, direction = 0, forceReload = false, playVideo = false) {
    if (state.disposed || index < 0 || index >= state.files.length) return;
    const revision = ++state.revision;
    stopMedia(state);
    state.index = index;
    state.playRequested = playVideo;
    state.renderedKey = null;
    const file = state.files[index];
    updateFormat(state, previewKind(file.knownFile));
    state.position.textContent = String(index + 1);
    state.total.textContent = String(state.files.length);
    state.stage.setAttribute("aria-label", `Preview ${index + 1} of ${state.files.length}`);
    for (const button of state.root.querySelectorAll("[data-media-index]")) {
        const active = Number(button.dataset.mediaIndex) === index;
        button.classList.toggle("is-active", active);
        if (active) button.setAttribute("aria-current", "true");
        else button.removeAttribute("aria-current");
    }
    setPanel(state, "loading");
    if (!file.metadataUrl || !file.downloadUrl) { showError(state, "This preview is unavailable."); return; }
    const neighbors = [...new Map([neighbor(state, -1), neighbor(state, 1)]
        .filter(Boolean).map(item => [String(item.fileId), item])).values()];
    const pending = loadPreview(file.metadataUrl, file.downloadUrl, file.fileId, state.id,
        playVideo, file.knownFile, neighbors, () => {
            if (!state.disposed && state.revision === revision) renderPeeks(state);
        }, state.resourceId, forceReload);

    // A warm image and its movement are applied in this click handler, before any await.
    const ready = getReadyPreview(state.id, file.fileId);
    if (ready) renderResponse(state, ready, revision);
    renderPeeks(state);
    animateMovement(state, direction);
    void pending.then(response => {
        if (state.disposed || state.revision !== revision || response.cancelled) return;
        if (response.status !== 200) {
            showError(state, response.message ?? (response.status === 401 ? "Sign in again to view this preview."
                : response.status === 403 ? "You do not have permission to view this preview."
                : response.status === 404 ? "This preview is no longer available."
                : "This preview could not be loaded. Please retry."));
            return;
        }
        renderResponse(state, response, revision);
        renderPeeks(state);
    }).catch(() => {
        if (!state.disposed && state.revision === revision) showError(state, "This preview could not be loaded. Please retry.");
    });
}

function updateFormat(state, kind) {
    state.kind = kind;
    state.label.textContent = labels[kind] ?? "Preview";
    state.root.classList.toggle("is-sound", ["audio", "spc", "brr"].includes(kind));
    state.root.classList.toggle("is-video-playing", kind === "video" && state.playRequested);
}

function setPanel(state, panel) {
    state.panel = panel;
    for (const name of panels) {
        const hidden = name !== panel;
        if (state[name].hidden !== hidden) state[name].hidden = hidden;
    }
    state.stage.setAttribute("aria-busy", String(panel === "loading"));
}

function renderResponse(state, response, revision) {
    const key = `${response.kind}:${response.url ?? ""}:${state.playRequested}`;
    if (state.renderedKey === key) return;
    state.renderedKey = key;
    updateFormat(state, response.kind);
    if (response.kind === "image" && response.url) {
        state.image.src = response.url;
        state.image.alt = `${state.resourceName}, preview ${state.index + 1}`;
        setPanel(state, "image");
    } else if (["audio", "spc", "brr"].includes(response.kind) && response.url) {
        void showSound(state, response, revision);
    } else if (response.kind === "video" && state.playRequested && response.url) {
        state.video.src = response.url;
        setPanel(state, "video");
        void state.video.play().catch(() => { /* Native controls remain available if autoplay is blocked. */ });
    } else if (response.kind === "video") {
        setPanel(state, "play");
    } else {
        state.download.href = state.files[state.index].downloadUrl;
        setPanel(state, "other");
    }
}

async function showSound(state, response, revision) {
    try {
        const module = state.audioModule ?? await import("./audio-preview.js?v=4");
        if (state.disposed || state.revision !== revision) return;
        state.audioModule = module;
        const filename = state.files[state.index].fileName || "Sound preview";
        state.audio.querySelector(".sound-preview")?.setAttribute("aria-label", `Sound preview: ${filename}`);
        module.initializeAudioPreview(state.audioPlayerId, response.url, response.kind, filename);
        setPanel(state, "audio");
    } catch {
        if (!state.disposed && state.revision === revision) showError(state, "The sound player could not start. Please retry.");
    }
}

function renderPeeks(state) {
    for (const [name, direction] of [["previous", -1], ["next", 1]]) {
        const file = neighbor(state, direction);
        const ready = file && getReadyPreview(state.id, file.fileId);
        const image = state.root.querySelector(`[data-media-peek="${name}"]`);
        const placeholder = state.root.querySelector(`[data-media-placeholder="${name}"]`);
        const visible = ready?.kind === "image" && !!ready.url;
        if (visible && image.getAttribute("src") !== ready.url) image.src = ready.url;
        else if (!visible) image.removeAttribute("src");
        image.hidden = !visible;
        placeholder.toggleAttribute("hidden", visible);
    }
}

function animateMovement(state, direction) {
    if (!direction) return;
    state.reel.classList.toggle("moving-next", direction > 0);
    state.reel.classList.toggle("moving-previous", direction < 0);
    for (const element of [state.stage, ...state.root.querySelectorAll(".media-peek")])
        for (const animation of element.getAnimations()) { animation.cancel(); animation.play(); }
}

function showError(state, message) {
    state.errorMessage.textContent = message;
    setPanel(state, "error");
}

function stopMedia(state) {
    state.audioModule?.disposeAudioPreview(state.audioPlayerId);
    state.panel = null;
    state.video.pause();
    state.video.removeAttribute("src");
    state.video.load();
    state.image.removeAttribute("src");
}

export function disposeCarousel(id) {
    const state = carousels.get(id);
    if (!state) return;
    state.disposed = true;
    state.revision++;
    state.events.abort();
    stopMedia(state);
    releasePreview(id);
    carousels.delete(id);
}

new MutationObserver(() => {
    for (const [id, state] of carousels) if (!state.root.isConnected) disposeCarousel(id);
}).observe(document.documentElement, { childList: true, subtree: true });
window.addEventListener("pagehide", event => {
    if (!event.persisted) for (const id of [...carousels.keys()]) disposeCarousel(id);
});
