// The pinned Uno Skia WASM host can exhaust its early accessibility retries
// before Window.RootElement is registered. Resume its own exported activation
// after the application reports readiness; never synthesize semantic nodes.
export function activateNativeAccessibility(frame, onFailure = () => {}) {
    const owner = frame.contentDocument;
    let attempts = 0;
    const activate = () => {
        if (!owner || frame.contentDocument !== owner || !frame.isConnected) return;
        const root = owner.getElementById('uno-semantics-root');
        if (root?.childElementCount) {
            owner.documentElement.dataset.unoAccessibility = 'ready';
            return;
        }
        const accessibility = frame.contentWindow?.Uno?.UI?.Runtime?.Skia?.Accessibility;
        try {
            // Respect the application's explicit native opt-in. This adapter is
            // version-scoped, and absence of native support is not a false pass.
            if (accessibility?.managedIsAutoEnableAccessibility?.() === true) {
                accessibility.managedEnableAccessibility();
            }
        } catch (error) {
            onFailure(`Native accessibility activation failed: ${error.message}`);
            return;
        }
        if (++attempts < 40) frame.contentWindow.setTimeout(activate, 100);
        else onFailure('Native browser accessibility did not become ready. Reload the workspace to retry.');
    };
    frame.contentWindow?.requestAnimationFrame(activate);
}
