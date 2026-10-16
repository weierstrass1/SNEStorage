// Shared only by this browser document. A preview and the playlist must not
// drive the single SPC emulator (or play other audio) at the same time.
let owner;

export function claimAudio(key, interrupt, nativeMedia = () => null) {
    if (owner && owner.key !== key) {
        const previous = owner;
        owner = undefined;
        previous.interrupt();
    }
    owner = { key, interrupt, nativeMedia };
    for (const media of document.querySelectorAll("audio, video"))
        if (media !== nativeMedia()) media.pause();
}

export function releaseAudio(key) {
    if (owner?.key === key) owner = undefined;
}

export function ownsAudio(key) { return owner?.key === key; }

document.addEventListener("play", event => {
    if (!(event.target instanceof HTMLMediaElement) || !owner || event.target === owner.nativeMedia()) return;
    const previous = owner;
    owner = undefined;
    previous.interrupt();
}, true);
