// Theme switcher — toggles the `data-theme` attribute on <html>.
// 'auto' resolves to the current OS preference.
(function () {
    const root = document.documentElement;
    const mq = window.matchMedia('(prefers-color-scheme: dark)');

    function resolve(theme) {
        if (theme === 'auto') return mq.matches ? 'dark' : 'light';
        return theme === 'dark' ? 'dark' : 'light';
    }

    let currentRequested = 'auto';

    function apply(theme) {
        currentRequested = theme || 'auto';
        root.setAttribute('data-theme', resolve(currentRequested));
    }

    // Follow OS changes when in auto mode.
    mq.addEventListener('change', () => {
        if (currentRequested === 'auto') apply('auto');
    });

    window.mindAtlasTheme = { apply };
})();
