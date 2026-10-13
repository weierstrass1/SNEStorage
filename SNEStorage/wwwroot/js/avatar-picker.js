const pickers = new Map();
const maxImageSize = 5 * 1024 * 1024;
const template = `<cropper-canvas>
    <cropper-image initial-fit="cover" translatable scalable></cropper-image>
    <cropper-handle action="move" plain></cropper-handle>
    <cropper-selection aspect-ratio="1" initial-coverage="1" precise style="pointer-events:none"></cropper-selection>
</cropper-canvas>`;

export function initializeAvatarPicker(id) {
    const root = document.getElementById(id);
    if (!root || pickers.get(id)?.root === root) return;
    disposeAvatarPicker(id);
    const find = name => root.querySelector(`[data-avatar-${name}]`);
    const state = {
        root, input: root.querySelector('input[type="file"]'), dialog: find("dialog"),
        stage: find("stage"), container: find("cropper"), preview: find("preview"),
        loading: find("loading"), zoomControls: find("zoom-controls"), zoom: find("zoom"),
        zoomValue: find("zoom-value"), error: find("error"), cropError: find("crop-error"),
        apply: find("apply"), reset: find("reset"), choose: find("choose"), clear: find("clear"),
        initialAvatarUrl: find("preview").getAttribute("src"),
        listeners: new AbortController(), generation: 0, committed: null, source: null,
        candidate: null, sourceUrl: null, previewUrl: null, cropper: null, image: null,
        baseline: 1, internalChange: false, disposed: false
    };
    pickers.set(id, state);
    const on = (element, event, handler) => element.addEventListener(event, handler, { signal: state.listeners.signal });
    on(state.choose, "click", () => state.committed ? openEditor(state, state.source) : state.input.click());
    on(find("replace"), "click", () => state.input.click());
    if (state.clear) on(state.clear, "click", () => {
        state.committed = state.source = state.savedTransform = null;
        if (state.previewUrl) URL.revokeObjectURL(state.previewUrl);
        state.previewUrl = null;
        if (state.initialAvatarUrl) state.preview.src = state.initialAvatarUrl;
        else state.preview.removeAttribute("src");
        state.preview.hidden = !state.initialAvatarUrl;
        state.root.querySelector("[data-avatar-placeholder]").toggleAttribute("hidden", !!state.initialAvatarUrl);
        state.clear.hidden = true;
        showMessage(state.error, "");
        setInputFile(state, null);
        state.choose.focus({ preventScroll: true });
    });
    on(state.input, "change", event => {
        if (state.internalChange) return;
        // The original file must not become the submitted avatar until its crop is confirmed.
        event.stopImmediatePropagation();
        const file = state.input.files?.[0];
        setInputFile(state, state.committed);
        if (!file) return;
        const message = file.size === 0 ? "This image is empty. Choose another image." :
            file.size > maxImageSize ? "Your image is too large. Choose a PNG or JPG no larger than 5 MB." :
            !/\.(png|jpe?g)$/i.test(file.name) || !["image/png", "image/jpeg"].includes(file.type.toLowerCase()) ?
                "Choose a PNG or JPG image." : "";
        showMessage(state.error, message);
        if (!message) openEditor(state, file);
    });
    root.querySelectorAll("[data-avatar-cancel]").forEach(button => on(button, "click", () => closeEditor(state)));
    on(state.dialog, "cancel", event => { event.preventDefault(); closeEditor(state); });
    on(state.reset, "click", () => state.image?.$setTransform(state.initialTransform));
    on(state.zoom, "input", () => {
        if (!state.image) return;
        const current = Math.hypot(...state.image.$getTransform().slice(0, 2));
        const factor = Number(state.zoom.value) * state.baseline / current;
        state.image.$zoom(factor >= 1 ? factor - 1 : 1 - 1 / factor);
    });
    on(state.stage, "keydown", event => {
        if (!state.image) return;
        const step = event.shiftKey ? 30 : 10;
        const directions = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] };
        if (!directions[event.key]) return;
        event.preventDefault();
        state.image.$move(...directions[event.key]);
    });
    on(state.apply, "click", () => applyCrop(state));
}

async function openEditor(state, file) {
    if (!file || state.disposed) return;
    closeEditor(state, false);
    const generation = state.generation;
    state.candidate = file;
    state.sourceUrl = URL.createObjectURL(file);
    state.loading.hidden = false;
    state.stage.hidden = true;
    state.zoomControls.hidden = true;
    state.apply.disabled = state.reset.disabled = true;
    showMessage(state.cropError, "");
    state.dialog.showModal();
    try {
        const { default: Cropper } = await import("../lib/cropperjs/cropper.esm.min.js");
        if (state.disposed || generation !== state.generation) return;
        const sourceImage = new Image();
        sourceImage.src = state.sourceUrl;
        // Decode before opening the cropper so corrupt images produce a useful message.
        await sourceImage.decode();
        if (state.disposed || generation !== state.generation) return;
        state.stage.hidden = false;
        state.cropper = new Cropper(sourceImage, { container: state.container, template });
        state.image = state.cropper.getCropperImage();
        await state.image.$ready();
        if (state.disposed || generation !== state.generation) return;
        state.image.$center("cover");
        state.initialTransform = state.image.$getTransform();
        state.baseline = Math.hypot(...state.initialTransform.slice(0, 2));
        state.zoom.value = "1";
        state.zoomValue.value = "100%";
        state.image.addEventListener("transform", event => {
            const ratio = Math.hypot(...event.detail.matrix.slice(0, 2)) / state.baseline;
            const minimum = Number(state.zoom.min);
            const maximum = Number(state.zoom.max);
            if (ratio < minimum - .0001 || ratio > maximum + .0001) { event.preventDefault(); return; }
            const zoom = Math.min(maximum, Math.max(minimum, ratio));
            state.zoom.value = String(zoom);
            state.zoomValue.value = `${Math.round(zoom * 100)}%`;
        });
        if (file === state.source && state.savedTransform) state.image.$setTransform(state.savedTransform);
        state.loading.hidden = true;
        state.zoomControls.hidden = false;
        state.apply.disabled = state.reset.disabled = false;
        state.stage.focus({ preventScroll: true });
    } catch {
        if (state.disposed || generation !== state.generation) return;
        state.loading.hidden = true;
        state.stage.hidden = true;
        showMessage(state.cropError, "We could not open this image. Try choosing another PNG or JPG.");
    }
}

async function applyCrop(state) {
    if (!state.cropper || state.apply.disabled) return;
    const generation = state.generation;
    state.apply.disabled = state.reset.disabled = true;
    state.apply.textContent = "Preparing avatar…";
    showMessage(state.cropError, "");
    try {
        const canvas = await state.cropper.getCropperSelection().$toCanvas({ width: 512, height: 512 });
        const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/png"));
        if (state.disposed || generation !== state.generation) return;
        if (!blob) throw new Error("Image export failed");
        const file = new File([blob], "avatar.png", { type: "image/png" });
        state.savedTransform = state.image.$getTransform();
        state.source = state.candidate;
        state.committed = file;
        if (state.previewUrl) URL.revokeObjectURL(state.previewUrl);
        state.previewUrl = URL.createObjectURL(blob);
        state.preview.src = state.previewUrl;
        state.preview.hidden = false;
        state.root.querySelector("[data-avatar-placeholder]").setAttribute("hidden", "");
        if (state.clear) state.clear.hidden = false;
        showMessage(state.error, "");
        setInputFile(state, file);
        closeEditor(state);
    } catch {
        if (state.disposed || generation !== state.generation) return;
        showMessage(state.cropError, "We could not prepare your avatar. Try again or choose another image.");
        state.apply.disabled = state.reset.disabled = false;
    } finally {
        if (!state.disposed && generation === state.generation) state.apply.textContent = "Use this avatar";
    }
}

function setInputFile(state, file) {
    const transfer = new DataTransfer();
    if (file) transfer.items.add(file);
    state.input.files = transfer.files;
    state.internalChange = true;
    try { state.input.dispatchEvent(new Event("change", { bubbles: true })); }
    finally { state.internalChange = false; }
}

function showMessage(element, message) {
    element.textContent = message;
    element.hidden = !message;
}

function closeEditor(state, restoreFocus = true) {
    state.generation++;
    if (state.dialog.open) state.dialog.close();
    state.container.replaceChildren();
    state.cropper = state.image = state.candidate = null;
    state.apply.textContent = "Use this avatar";
    if (state.sourceUrl) URL.revokeObjectURL(state.sourceUrl);
    state.sourceUrl = null;
    if (restoreFocus && !state.disposed) state.choose.focus({ preventScroll: true });
}

export function disposeAvatarPicker(id) {
    const state = pickers.get(id);
    if (!state) return;
    state.disposed = true;
    state.listeners.abort();
    closeEditor(state, false);
    if (state.previewUrl) URL.revokeObjectURL(state.previewUrl);
    pickers.delete(id);
}
