// Align CSS reflections to the browser's document clock, including late Blazor renders.
// The browser animates each frame; no timers, polling, or server calls are needed.
const prefix = "snestorage-quality-";
const aligned = new WeakSet();

function align(animation) {
    if (!animation.animationName?.startsWith(prefix) ||
        animation.timeline !== document.timeline || aligned.has(animation)) return;

    animation.startTime = 0;
    aligned.add(animation);
}

document.addEventListener("animationstart", event => {
    if (!event.animationName.startsWith(prefix) || !(event.target instanceof Element)) return;
    // subtree also includes ::before animations in Chromium/Opera.
    event.target.getAnimations({ subtree: true }).forEach(align);
}, true);

document.getAnimations().forEach(align);
