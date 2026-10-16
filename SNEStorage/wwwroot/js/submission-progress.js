const submissions = new Map();

export function beginSubmission(formId) {
    if (submissions.has(formId)) return;
    const dialog = document.getElementById(`${formId}-progress`);
    if (!(dialog instanceof HTMLDialogElement)) throw new Error("The submission indicator is unavailable.");
    const preventCancel = event => event.preventDefault();
    dialog.addEventListener("cancel", preventCancel);
    try {
        // Open before CAPTCHA, so its own modal is subsequently placed above this one.
        // Native modal inertness blocks pointer/keyboard input without disabling form
        // fields: disabled fields would disappear from the multipart FormData.
        dialog.showModal();
        submissions.set(formId, { dialog, preventCancel });
    } catch (error) {
        dialog.removeEventListener("cancel", preventCancel);
        throw error;
    }
}

export function endSubmission(formId) {
    const current = submissions.get(formId);
    if (!current) return;
    submissions.delete(formId);
    current.dialog.removeEventListener("cancel", current.preventCancel);
    if (current.dialog.open) current.dialog.close();
}

window.addEventListener("pagehide", () => {
    for (const formId of [...submissions.keys()]) endSubmission(formId);
});
