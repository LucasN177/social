// Kleine UI-Helfer (global, damit der Pfad unabhängig vom <base href> ist)
window.socialUi = {
    scrollToBottom: (element) => {
        if (element) element.scrollTop = element.scrollHeight;
    },

    // true, wenn der Nutzer (fast) ganz unten ist – dann bei neuen Nachrichten mitscrollen
    isNearBottom: (element, threshold) => {
        if (!element) return true;
        return element.scrollHeight - element.scrollTop - element.clientHeight <= (threshold ?? 80);
    }
};
