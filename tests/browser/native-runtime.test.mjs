import { test } from 'node:test';
import assert from 'node:assert/strict';
import { observeNativeRuntime } from '../../src/UnoDock.Browser/Assets/native-runtime.mjs';

class Events {
    listeners = new Map();
    addEventListener(name, callback) {
        if (!this.listeners.has(name)) this.listeners.set(name, new Set());
        this.listeners.get(name).add(callback);
    }
    removeEventListener(name, callback) { this.listeners.get(name)?.delete(callback); }
    emit(name, value = {}) { for (const callback of [...(this.listeners.get(name) ?? [])]) callback(value); }
    callbacks(name) { return [...(this.listeners.get(name) ?? [])]; }
}
function fixture() {
    const frame = Object.assign(new Events(), { contentWindow: new Events(), contentDocument: {} });
    const notifications = [];
    const runtime = observeNativeRuntime(frame, value => notifications.push(value));
    runtime.bind();
    return { frame, runtime, notifications };
}

test('native monitor requires a real report before marking the renderer ready', () => {
    const f = fixture();
    assert.equal(f.runtime.state, 'starting');
    assert.equal(f.runtime.report(), true);
    assert.equal(f.runtime.state, 'ready');
    f.runtime.dispose();
});
test('unhandled native error retires stale readiness without suppressing the error', () => {
    const f = fixture(); f.runtime.report();
    f.frame.contentWindow.emit('error', { message: 'bounds fault', error: new Error('bounds fault'), preventDefault: assert.fail });
    assert.equal(f.runtime.state, 'failed');
    assert.match(f.runtime.message, /bounds fault/);
    assert.equal(f.runtime.report(), false);
});
test('the first native failure remains visible through subsequent error cascades', () => {
    const f = fixture(); f.runtime.report();
    f.frame.contentWindow.emit('error', { message: 'first failure' });
    f.frame.contentWindow.emit('unhandledrejection', { reason: new Error('runtime already exited') });
    assert.equal(f.runtime.message, 'first failure');
    assert.equal(f.notifications.filter(x => x.state === 'failed').length, 1);
});
test('resource-load events do not retire an otherwise healthy runtime', () => {
    const f = fixture(); f.runtime.report();
    f.frame.contentWindow.emit('error', { target: { tagName: 'IMG' } });
    assert.equal(f.runtime.state, 'ready');
});
test('a replaced iframe document requires fresh readiness and rejects captured old events', () => {
    const f = fixture(); f.runtime.report();
    const old = f.frame.contentWindow, captured = old.callbacks('error')[0];
    f.frame.contentDocument = {}; // A real navigation retains WindowProxy identity.
    f.frame.emit('load');
    assert.equal(f.runtime.state, 'starting');
    captured({ message: 'obsolete fault' });
    assert.equal(f.runtime.state, 'starting');
    assert.equal(f.runtime.report(), true);
    assert.equal(old.callbacks('error').length, 1);
});
test('runtime recovery does not clear or modify broker-owned content', () => {
    const f = fixture(), content = { id: 'notes', payload: 'recoverable', lease: 4 };
    f.runtime.report(); f.runtime.fail('native fault');
    f.frame.contentWindow = new Events(); f.frame.contentDocument = {};
    f.frame.emit('load');
    assert.equal(f.runtime.report(), true);
    assert.deepEqual(content, { id: 'notes', payload: 'recoverable', lease: 4 });
});
test('disposed monitors detach and cannot be revived by captured callbacks', () => {
    const f = fixture(), scope = f.frame.contentWindow;
    const callback = scope.callbacks('error')[0];
    f.runtime.dispose(); f.runtime.dispose();
    callback({ message: 'late' });
    assert.equal(scope.callbacks('error').length, 0);
    assert.equal(scope.callbacks('unhandledrejection').length, 0);
    assert.equal(f.frame.callbacks('load').length, 0);
    assert.equal(f.runtime.report(), false);
});
test('unhandled rejection is visible even when the reason has hostile conversion', () => {
    const f = fixture();
    f.frame.contentWindow.emit('unhandledrejection', { reason: { toString() { throw Error('conversion'); } } });
    assert.equal(f.runtime.state, 'failed');
    assert.match(f.runtime.message, /Unhandled native runtime rejection/);
});
test('failure messages are bounded and do not create substitute DOM content', () => {
    const f = fixture();
    f.runtime.fail('x'.repeat(5000));
    assert.equal(f.runtime.message.length, 2000);
    assert.deepEqual(f.frame.contentDocument, {});
});
