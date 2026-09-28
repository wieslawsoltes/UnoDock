import { test } from 'node:test';
import assert from 'node:assert/strict';
import { activateNativeAccessibility } from '../../src/UnoDock.Browser/Assets/accessibility.mjs';

function fixture() {
    let calls = 0;
    const host = {
        managedIsAutoEnableAccessibility() { return true; },
        managedEnableAccessibility() { calls++; }
    };
    const frame = { contentDocument: {}, contentWindow: { Uno: { UI: { Runtime: { Skia: { Accessibility: host } } } } } };
    return { host, frame, calls: () => calls };
}
const immediate = { delay: async () => {}, attempts: 3 };

test('native accessibility uses the pinned export and enables exactly once', async () => {
    const f = fixture();
    const promise = activateNativeAccessibility(f.frame, assert.fail, immediate);
    assert.equal(f.calls(), 0, 'Managed initialization must unwind before accessibility is entered.');
    assert.equal(await promise, true);
    assert.equal(f.calls(), 1);
});

test('accessibility activation does not wait for requestAnimationFrame', async () => {
    const f = fixture();
    f.frame.contentWindow.requestAnimationFrame = () => assert.fail('Background animation frames are not a readiness clock.');
    assert.equal(await activateNativeAccessibility(f.frame, assert.fail, immediate), true);
});

test('a replaced native document revokes pending activation', async () => {
    const f = fixture();
    const result = await activateNativeAccessibility(f.frame, assert.fail, {
        ...immediate, delay: async () => { f.frame.contentDocument = {}; }
    });
    assert.equal(result, false);
    assert.equal(f.calls(), 0);
});

test('unknown native exports report a bounded failure', async () => {
    const f = fixture(); const messages = []; let waits = 0;
    delete f.host.managedIsAutoEnableAccessibility;
    f.host.managedIsAutoEnableAccessibilityEnabled = () => true;
    const result = await activateNativeAccessibility(f.frame, message => messages.push(message), {
        attempts: 3, delay: async () => { waits++; }
    });
    assert.equal(result, false);
    assert.equal(f.calls(), 0);
    assert.equal(waits, 4);
    assert.equal(messages.length, 1);
    assert.match(messages[0], /did not complete/);
});

test('late native registration is discovered without duplicate enablement', async () => {
    const f = fixture(); delete f.host.managedIsAutoEnableAccessibility; let waits = 0;
    const result = await activateNativeAccessibility(f.frame, assert.fail, {
        attempts: 3, delay: async () => {
            if (++waits === 2) f.host.managedIsAutoEnableAccessibility = () => true;
        }
    });
    assert.equal(result, true);
    assert.equal(f.calls(), 1);
});

test('disabled auto-accessibility is not silently overridden', async () => {
    const f = fixture(); const messages = [];
    f.host.managedIsAutoEnableAccessibility = () => false;
    assert.equal(await activateNativeAccessibility(f.frame, message => messages.push(message), immediate), false);
    assert.equal(f.calls(), 0);
    assert.equal(messages.length, 1);
});

test('native failures are visible and no substitute tree is generated', async () => {
    const f = fixture(); const messages = [];
    f.host.managedEnableAccessibility = () => { throw new Error('native failure'); };
    assert.equal(await activateNativeAccessibility(f.frame, message => messages.push(message), immediate), false);
    assert.match(messages[0], /native failure/);
    assert.deepEqual(f.frame.contentDocument, {});
});
