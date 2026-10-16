import { captchaFetch } from "./captcha.js?v=3";
import { getAccountSession } from "./account-session.js?v=2";

let catalog;
let catalogExpires = 0;
const normalize = text => text.trim().replace(/\s+/g, " ").toLowerCase();

function failure(status, body) {
    let value;
    try { value = JSON.parse(body); } catch { value = body; }
    const code = (typeof value === "string" ? value.split("-").pop() : value?.error) || "";
    const messages = {
        RepeatedFlag: "You had already reported this item. No additional report was created.",
        ReasonNotFound: "This reason is no longer available. Close the report and try again.",
        ResourceNotFound: "This resource is no longer available.",
        CommentNotFound: "This comment is no longer available.",
        CaptchaCancelled: "Verification was cancelled. Your selected reason has been kept.",
        CaptchaInvalid: "Verification expired. Please try again.",
        CaptchaUnavailable: "Verification is unavailable. Please try again later.",
        CaptchaBusy: "Complete the verification already open, then try again.",
        AntiforgeryTokenMissing: "Reload the page before reporting this item."
    };
    const message = messages[code] || (status === 401 ? "Please log in again to submit a report."
        : status === 403 ? "You do not have permission to report this item."
        : status === 429 ? "Too many requests. Please wait before trying again."
        : status === 404 ? "This item or reporting action is not available."
        : "The report could not be completed. Please try again later.");
    return Object.assign(new Error(message), { status, code });
}

async function readJson(path, signal) {
    const response = await fetch(path, { credentials: "same-origin", cache: "no-store", redirect: "error",
        headers: { Accept: "application/json" }, signal });
    const body = await response.text();
    if (!response.ok) throw failure(response.status, body);
    try { return JSON.parse(body); }
    catch { throw new Error("The reporting options could not be read. Please try again."); }
}

export async function getReportReasons(signal) {
    if (catalog && Date.now() < catalogExpires) return catalog;
    const [options, rules] = await Promise.all([
        readJson("/api/flags/options", signal),
        readJson("/Content/report-reasons.json?v=1", signal)
    ]);
    if (!options || typeof options !== "object" || !Array.isArray(rules?.reasons))
        throw new Error("The reporting options could not be read. Please try again.");
    const contexts = new Map(rules.reasons.filter(rule => typeof rule.description === "string")
        .map(rule => [normalize(rule.description), rule]));
    catalog = Object.values(options).flatMap(reason => {
        if (!Number.isSafeInteger(reason?.id) || reason.id <= 0 || typeof reason.description !== "string"
            || typeof reason.reasonCategory?.name !== "string") return [];
        const rule = contexts.get(normalize(reason.description));
        // Unknown reasons are not guessed from IDs or keywords. Add context metadata to the JSON first.
        if (!rule || !Array.isArray(rule.contexts)) return [];
        return [{ id: reason.id, description: reason.description, category: reason.reasonCategory.name,
            priority: reason.priority, contexts: rule.contexts,
            requiresPreviews: rule.requiresPreviews === true, requiresNoVersions: rule.requiresNoVersions === true }];
    });
    catalogExpires = Date.now() + (catalog.length ? 300000 : 0);
    return catalog;
}

export async function getReportContext(target, signal) {
    if (target.context !== "resource") return target;
    const result = { ...target };
    const tasks = [];
    if (result.hasPreviews === null) tasks.push(readJson(`/api/resource/${target.id}/previews`, signal)
        .then(items => { if (!Array.isArray(items)) throw new Error("Resource previews could not be checked."); result.hasPreviews = items.length > 0; }));
    if (result.hasVersions === null) tasks.push(readJson(`/api/resource/${target.id}/versions`, signal)
        .then(items => { if (!Array.isArray(items)) throw new Error("Resource versions could not be checked."); result.hasVersions = items.length > 0; })
        .catch(error => { if (error.code === "VersionNotFound") result.hasVersions = false; else throw error; }));
    await Promise.all(tasks);
    return result;
}

export async function submitReport(target, reasonId, signal) {
    const account = await getAccountSession(signal);
    if (signal.aborted) throw new DOMException("Aborted", "AbortError");
    if (account.status === 401) throw failure(401, "");
    if (account.status !== 200) throw new Error("Your account could not be verified. Please try again.");
    let identity;
    try { identity = JSON.parse(account.body).userId; } catch { }
    if (!identity || identity !== target.userId)
        throw new Error("Your account has changed. Close this report and reload the page before continuing.");
    const body = target.context === "resource" ? { reasonID: reasonId, resourceID: target.id }
        : { reasonID: reasonId, commentID: target.id };
    const response = await captchaFetch(`/api/flags/${target.context}_flag/create`, {
        method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal,
        headers: { Accept: "application/json", "Content-Type": "application/json" }, body: JSON.stringify(body)
    }, "create_flag", true);
    const text = await response.text();
    if (!response.ok) {
        const error = failure(response.status, text);
        if (error.code === "ReasonNotFound") { catalog = undefined; catalogExpires = 0; }
        throw error;
    }
    if (!/^(?:[1-9]\d*|"[1-9]\d*")$/.test(text.trim()))
        throw Object.assign(new Error("We could not confirm the report. Please do not send it again immediately."), { uncertain: true });
}
