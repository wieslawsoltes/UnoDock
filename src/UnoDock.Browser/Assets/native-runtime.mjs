// Observe the actual iframe runtime. This does not create semantic controls,
// replay input, or convert a failed render into a successful model projection.
export function observeNativeRuntime(frame, changed = () => {}) {
    let scope = null, document = null, generation = 0, disposed = false;
    let state = 'starting', message = '', release = () => {};

    function publish(next, reason = '') {
        if (state === next && message === reason) return;
        state = next; message = reason;
        changed({ state, message, generation });
    }
    function describe(value, fallback) {
        try {
            const text = typeof value?.message === 'string' ? value.message : String(value ?? '');
            return (text || fallback).slice(0, 2000);
        } catch { return fallback; }
    }
    function current(token) {
        try { return !disposed && token === generation && frame.contentWindow === scope && frame.contentDocument === document; }
        catch { return false; }
    }
    function fail(reason) {
        if (disposed || state === 'failed') return;
        publish('failed', describe(reason, 'The native renderer stopped unexpectedly.'));
    }
    function bind() {
        if (disposed) return false;
        let nextScope, nextDocument;
        try { nextScope = frame.contentWindow; nextDocument = frame.contentDocument; }
        catch { fail('The native renderer is no longer on the workspace origin.'); return false; }
        if (!nextScope || !nextDocument) return false;
        if (scope === nextScope && document === nextDocument) return true;
        release(); scope = nextScope; document = nextDocument;
        const token = ++generation;
        const error = event => {
            // Resource load events have no message and must not be confused with
            // unhandled script/runtime exceptions. Do not prevent native logging.
            if (current(token) && typeof event.message === 'string' && event.message)
                fail(event.error ?? event.message);
        };
        const rejection = event => {
            if (current(token)) fail(describe(event.reason, 'Unhandled native runtime rejection.'));
        };
        nextScope.addEventListener('error', error);
        nextScope.addEventListener('unhandledrejection', rejection);
        release = () => {
            nextScope.removeEventListener('error', error);
            nextScope.removeEventListener('unhandledrejection', rejection);
        };
        publish('starting');
        return true;
    }
    function report() {
        if (!bind() || state === 'failed') return false;
        publish('ready');
        return true;
    }
    function dispose() {
        if (disposed) return;
        disposed = true; generation++;
        frame.removeEventListener('load', bind);
        release(); scope = document = null;
    }
    frame.addEventListener('load', bind);
    return Object.freeze({
        bind, report, fail, dispose,
        get state() { return state; },
        get message() { return message; },
        get generation() { return generation; }
    });
}
