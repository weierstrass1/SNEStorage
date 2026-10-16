const namePattern = /^[\p{L}\p{Nd}][\p{L}\p{Nd} ]{0,98}[\p{L}\p{Nd}]$/u;

// Browser feedback only; ResourceController and ResourceService validate the submission.
export function validateResourceTextFields(form, originalReleaseNotes = null, original = null) {
    for (const [name, minimum, maximum, message] of [
        ["Name", 2, 100, "Use 2–100 characters: letters, numbers and spaces between words."],
        ["Description", 10, 10000, "Enter a description with 10–10,000 characters."],
        ["ChangeLog", 10, 10000, "Enter release notes with 10–10,000 characters."],
        ["ReleaseNotes", 10, 10000, "Enter release notes with 10–10,000 characters."]
    ]) {
        const field = form.elements.namedItem(name);
        if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) continue;
        const value = field.value.trim();
        field.setCustomValidity("");
        // Unchanged release notes are omitted from the edit DTO.
        if (name === "ReleaseNotes" && originalReleaseNotes !== null &&
            field.value.replaceAll("\r\n", "\n") === originalReleaseNotes.replaceAll("\r\n", "\n")) continue;
        if (value.length < minimum || value.length > maximum ||
            (name === "Name" && !namePattern.test(value))) field.setCustomValidity(message);
    }

    for (const [name, selector, previous, label] of [
        ["Tags", "[data-resource-token-input]", original?.tags, "tag"],
        ["ExternalAuthors", "[data-resource-author-input][name='ExternalAuthors']", original?.externalAuthors, "external author name"]
    ]) {
        const field = form.querySelector(selector);
        if (!(field instanceof HTMLInputElement)) continue;
        field.setCustomValidity("");
        const maximum = Number(field.dataset.itemMaxLength);
        if (!Number.isInteger(maximum) || maximum <= 0) continue;
        // Existing selections are not additions in the edit DTO. Keep legacy tags editable.
        const unchanged = new Set((previous ?? []).map(item => item.name.trim().toLowerCase()));
        const values = Array.from(form.querySelectorAll(`input[name='${name}']`))
            .flatMap(input => name === "Tags" ? input.value.split(",") : [input.value])
            .map(value => value.trim()).filter(Boolean);
        if (values.some(value => value.length > maximum && !unchanged.has(value.toLowerCase())))
            field.setCustomValidity(`Each new ${label} can contain up to ${maximum} characters.`);
    }

    const repository = form.elements.namedItem("RepositoryURL");
    if (!(repository instanceof HTMLInputElement)) return;
    const value = repository.value.trim();
    repository.setCustomValidity("");
    // An empty optional URL clears the repository in an edit.
    if (!value) return;
    let valid = value.length <= 2048 && /^https?:\/\/[^\s<>"\\]+$/i.test(value);
    try {
        const url = new URL(value);
        valid = valid && ["http:", "https:"].includes(url.protocol) &&
            !!url.hostname && !url.username && !url.password;
    } catch { valid = false; }
    if (!valid) repository.setCustomValidity("Enter a valid HTTP or HTTPS repository URL, up to 2,048 characters, without embedded credentials.");
}
