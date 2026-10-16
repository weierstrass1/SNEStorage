// Files stay in IndexedDB in the visitor's browser. No HTTP upload or .NET interop.
const databaseName = "snestorage-spc-library";
let databasePromise;

function openLibrary() {
    if (!globalThis.indexedDB) return Promise.reject(new Error("Local storage is unavailable in this browser."));
    databasePromise ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        let blocked = false;
        request.onupgradeneeded = () => {
            const db = request.result;
            db.createObjectStore("tracks", { keyPath: "id" });
            db.createObjectStore("files", { keyPath: "id" });
            db.createObjectStore("settings");
        };
        request.onerror = () => reject(request.error);
        request.onblocked = () => {
            blocked = true;
            reject(new Error("Close other SNEStorage tabs and try opening your local library again."));
        };
        request.onsuccess = () => {
            const db = request.result;
            if (blocked) { db.close(); return; }
            db.onversionchange = () => { db.close(); databasePromise = undefined; };
            resolve(db);
        };
    }).catch(error => { databasePromise = undefined; throw error; });
    return databasePromise;
}

async function transaction(stores, mode, execute) {
    const db = await openLibrary();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(stores, mode);
        let result;
        tx.oncomplete = () => resolve(result);
        tx.onabort = tx.onerror = () => reject(tx.error ?? new Error("Your local library could not be saved."));
        try { execute(tx, value => { result = value; }); }
        catch (error) { tx.abort(); reject(error); }
    });
}

export function readLibrary() {
    return transaction(["tracks", "settings"], "readonly", (tx, result) => {
        const snapshot = { tracks: [], settings: {} };
        tx.objectStore("tracks").getAll().onsuccess = event => { snapshot.tracks = event.target.result; };
        tx.objectStore("settings").get("player").onsuccess = event => { snapshot.settings = event.target.result ?? {}; };
        result(snapshot);
    });
}

export function readTrackFile(id) {
    return transaction(["files"], "readonly", (tx, result) => {
        tx.objectStore("files").get(id).onsuccess = event => result(event.target.result?.blob);
    });
}

export function addTrack(track, file) {
    return transaction(["tracks", "files"], "readwrite", tx => {
        tx.objectStore("tracks").put(track);
        tx.objectStore("files").put({ id: track.id, blob: file });
    });
}

export function saveTrackOrder(tracks) {
    return transaction(["tracks"], "readwrite", tx => {
        for (const track of tracks) tx.objectStore("tracks").put(track);
    });
}

export function deleteTrack(id) {
    return transaction(["tracks", "files"], "readwrite", tx => {
        tx.objectStore("tracks").delete(id);
        tx.objectStore("files").delete(id);
    });
}

export function clearTracks() {
    return transaction(["tracks", "files"], "readwrite", tx => {
        tx.objectStore("tracks").clear();
        tx.objectStore("files").clear();
    });
}

export function saveSettings(settings) {
    return transaction(["settings"], "readwrite", tx => tx.objectStore("settings").put(settings, "player"));
}
