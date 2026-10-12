const versionPattern = /^[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?$/;
const basePattern = /^[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}$/;
const numberPattern = /^[0-9]{1,4}$/;

// Read the visible controls at submission time, including the last typed character.
export function validateVersionInputs(form, originalVersion = null) {
    for (const group of form.querySelectorAll("[data-version-input]")) {
        const base = group.querySelector("[data-version-base]");
        const release = group.querySelector("[data-version-release]");
        const number = group.querySelector("[data-version-number]");
        const value = group.querySelector("[data-version-value]");
        const suffixNumber = number?.value.trim() ?? "";
        const version = base.value.trim() + (release.value
            ? `-${release.value}${suffixNumber ? `.${suffixNumber}` : ""}` : "");
        value.value = version;
        base.setCustomValidity("");
        number?.setCustomValidity("");
        // Existing labels may predate the format rule. Unchanged labels are omitted by the editor.
        if (originalVersion !== null && version === originalVersion.trim()) continue;
        if (version.length > 25) {
            base.setCustomValidity("The complete version must be no longer than 25 characters.");
        } else if (!basePattern.test(base.value.trim())) {
            base.setCustomValidity("Enter one to three numbers of 1–4 digits, separated by dots, such as 1, 1.2 or 1.2.3. Choose alpha, beta or rc in the Release selector.");
        } else if (number && suffixNumber && !numberPattern.test(suffixNumber)) {
            number.setCustomValidity("Use 1–4 digits, or leave the suffix number empty.");
        } else if (!versionPattern.test(version)) {
            base.setCustomValidity("Enter a version such as 1, 1.2, 1.2.3, 1-alpha.10 or 2.50-beta. Each number can contain 1–4 digits.");
        }
    }
}
