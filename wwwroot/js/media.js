// Kleine Browser-Helfer für Videos (Reels). Global, damit der Pfad unabhängig vom <base href> ist.
window.socialMedia = {
    // Blob-URL für die Vorschau eines gewählten Videos (Stream aus .NET, kein Byte-Array über Interop)
    createObjectUrl: async (streamRef, contentType) => {
        const buffer = await streamRef.arrayBuffer();
        return URL.createObjectURL(new Blob([buffer], { type: contentType }));
    },

    revokeObjectUrl: (url) => {
        if (url) URL.revokeObjectURL(url);
    },

    // Spielt das Video ab, das gerade überwiegend sichtbar ist, pausiert alle anderen
    // und schaltet per Antippen zwischen Play/Pause um.
    observeReels: (container) => {
        if (!container) return;
        window.socialMedia.disposeReels(container);

        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                const video = entry.target;
                if (entry.isIntersecting && entry.intersectionRatio >= 0.6) {
                    video.play().catch(() => { /* Autoplay blockiert – Nutzer kann tippen */ });
                } else {
                    video.pause();
                }
            }
        }, { root: container, threshold: [0, 0.6, 1] });

        const attach = () => container.querySelectorAll('video:not([data-observed])').forEach(video => {
            video.dataset.observed = '1';
            // Das HTML-Attribut "muted" setzt nur defaultMuted – Autoplay braucht die Eigenschaft.
            // Stumm, bis der Nutzer den Ton einschaltet (container.dataset.muted = "false").
            video.muted = container.dataset.muted !== 'false';
            video.addEventListener('click', () => window.socialMedia.togglePlay(video));
            observer.observe(video);
        });
        attach();

        // Später nachgeladene Reels ebenfalls beobachten
        const mutation = new MutationObserver(attach);
        mutation.observe(container, { childList: true, subtree: true });

        container.__reelObserver = observer;
        container.__reelMutation = mutation;
    },

    disposeReels: (container) => {
        if (!container) return;
        container.__reelObserver?.disconnect();
        container.__reelMutation?.disconnect();
        // data-observed bleibt gesetzt, damit Klick-Handler nicht doppelt angehängt werden
        container.querySelectorAll('video').forEach(v => v.pause());
    },

    setMuted: (container, muted) => {
        if (!container) return;
        container.dataset.muted = String(muted);
        container.querySelectorAll('video').forEach(v => v.muted = muted);
    },

    togglePlay: (video) => {
        if (!video) return;
        if (video.paused) video.play().catch(() => { }); else video.pause();
    }
};
