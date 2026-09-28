// Activate Uno's native semantic tree; never synthesize a substitute DOM editor.
// The initial asynchronous boundary lets the managed report call unwind before
// entering managed accessibility. Background popups need not receive animation frames.
export async function activateNativeAccessibility(frame, failed = () => {}, options = {}) {
    const delay = options.delay ?? (milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds)));
    const attempts = options.attempts ?? 40;
    try {
        const scope = frame.contentWindow;
        const document = frame.contentDocument;
        if (!scope || !document) return false;
        await delay(0);
        for (let attempt = 0; attempt < attempts; attempt++) {
            if (frame.contentWindow !== scope || frame.contentDocument !== document) return false;
            const host = scope.Uno?.UI?.Runtime?.Skia?.Accessibility;
            // This is the export name in the pinned, published Uno runtime.
            // Unknown host shapes fail visibly instead of silently faking readiness.
            if (typeof host?.managedEnableAccessibility === 'function' &&
                typeof host?.managedIsAutoEnableAccessibility === 'function' &&
                host.managedIsAutoEnableAccessibility()) {
                host.managedEnableAccessibility();
                return true;
            }
            await delay(100);
        }
        failed('Native Uno accessibility initialization did not complete.');
    } catch (error) {
        failed('Native Uno accessibility failed: ' + error.message);
    }
    return false;
}
