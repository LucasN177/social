// Kleine UI-Helfer (global, damit der Pfad unabhängig vom <base href> ist)
window.socialUi = {
    scrollToBottom: (element) => {
        if (element) element.scrollTop = element.scrollHeight;
    },

    // true, wenn der Nutzer (fast) ganz unten ist – dann bei neuen Nachrichten mitscrollen
    isNearBottom: (element, threshold) => {
        if (!element) return true;
        return element.scrollHeight - element.scrollTop - element.clientHeight <= (threshold ?? 80);
    },

    // Infinite Scroll: ruft dotNetRef.OnSentinelVisible auf, sobald das Element in die Nähe
    // des sichtbaren Bereichs kommt. Erneutes Aufrufen beobachtet neu – der Observer meldet
    // dann sofort den aktuellen Stand, auch wenn das Element nach dem Nachladen noch sichtbar ist.
    observeSentinel: (element, dotNetRef, rootMargin) => {
        if (!element) return;
        element.__sentinelObserver?.disconnect();
        const observer = new IntersectionObserver((entries) => {
            if (entries.some(e => e.isIntersecting)) {
                dotNetRef.invokeMethodAsync('OnSentinelVisible').catch(() => { /* Komponente bereits entfernt */ });
            }
        }, { rootMargin: rootMargin || '600px 0px' });
        observer.observe(element);
        element.__sentinelObserver = observer;
    },

    disposeSentinel: (element) => {
        element?.__sentinelObserver?.disconnect();
        if (element) element.__sentinelObserver = null;
    },

    // Online/Offline melden (OfflineBanner). Liefert den aktuellen Stand.
    watchConnection: (dotNetRef) => {
        window.socialUi.unwatchConnection();
        const notify = () => dotNetRef.invokeMethodAsync('SetOnline', navigator.onLine).catch(() => { });
        window.addEventListener('online', notify);
        window.addEventListener('offline', notify);
        window.socialUi.__connectionHandler = notify;
        return navigator.onLine;
    },

    unwatchConnection: () => {
        const handler = window.socialUi.__connectionHandler;
        if (!handler) return;
        window.removeEventListener('online', handler);
        window.removeEventListener('offline', handler);
        window.socialUi.__connectionHandler = null;
    }
};
