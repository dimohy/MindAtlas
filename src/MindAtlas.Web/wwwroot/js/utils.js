// Utility JS functions for MindAtlas
window.mindAtlasUtils = {
    downloadFile: function (filename, content) {
        const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },
    // Suppress the textarea's default newline insertion when the user
    // presses Ctrl+Enter (which we use as the submit shortcut). Plain
    // Enter is left untouched so multi-line editing keeps working.
    suppressCtrlEnterNewline: function (selector) {
        const el = document.querySelector(selector);
        if (!el || el.__suppressCtrlEnter) return;
        el.__suppressCtrlEnter = true;
        el.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && e.ctrlKey) e.preventDefault();
        });
    }
};

