window.SNEStorageMedia = {
    captureFirstFrames: async (container) => {
        const input = container?.querySelector('input[type="file"]');
        const files = Array.from(input?.files ?? []);
        const captures = [];

        for (let index = 0; index < files.length; index++) {
            const file = files[index];
            if (!file.type.startsWith("video/")) continue;

            const video = document.createElement("video");
            const objectUrl = URL.createObjectURL(file);
            video.muted = true;
            video.playsInline = true;
            video.preload = "auto";
            video.src = objectUrl;

            try {
                await new Promise((resolve, reject) => {
                    const timeout = window.setTimeout(() => reject(new Error("Video metadata timed out.")), 15000);
                    video.addEventListener("loadeddata", () => {
                        window.clearTimeout(timeout);
                        resolve();
                    }, { once: true });
                    video.addEventListener("error", () => {
                        window.clearTimeout(timeout);
                        reject(new Error("Video frame could not be decoded."));
                    }, { once: true });
                    video.load();
                });

                if (Number.isFinite(video.duration) && video.duration > 0.1) {
                    await new Promise((resolve) => {
                        const target = Math.min(0.1, video.duration / 2);
                        const timeout = window.setTimeout(resolve, 3000);
                        video.addEventListener("seeked", resolve, { once: true });
                        video.addEventListener("seeked", () => window.clearTimeout(timeout), { once: true });
                        video.currentTime = target;
                    });
                }

                const canvas = document.createElement("canvas");
                const scale = Math.min(1, 320 / Math.max(video.videoWidth, video.videoHeight));
                canvas.width = Math.max(1, Math.round(video.videoWidth * scale));
                canvas.height = Math.max(1, Math.round(video.videoHeight * scale));
                canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);
                captures.push({ index, dataUrl: canvas.toDataURL("image/jpeg", 0.55) });
            } catch (error) {
                console.warn("Unable to capture a preview frame", file.name, error);
            } finally {
                video.pause();
                video.removeAttribute("src");
                video.load();
                URL.revokeObjectURL(objectUrl);
            }
        }

        return captures;
    }
};
