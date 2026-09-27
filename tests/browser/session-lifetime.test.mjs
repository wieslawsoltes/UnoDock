import { test } from 'node:test';
import assert from 'node:assert/strict';
import { WorkspaceState } from '../../src/UnoDock.Browser/Assets/session.mjs';

function create() {
    const state = new WorkspaceState('lifetime');
    state.ready('main');
    state.create('main', { id: 'editor', kind: 'document', title: 'Draft', payload: 'retained' });
    state.register('popup', 'Popup'); state.ready('popup');
    state.transfer('main', 'editor', 1, 'popup');
    return state;
}

test('reloading a host rotates authority before its replacement editor attaches', () => {
    const state = create();
    const old = state.snapshot('popup').items[0];
    state.ready('popup');
    const replacement = state.snapshot('popup').items[0];
    assert.ok(replacement.lease > old.lease);
    assert.equal(replacement.payload, 'retained');
    assert.throws(() => state.update('popup', 'editor', old.lease, { payload: 'obsolete runtime' }));
    state.update('popup', 'editor', replacement.lease, { payload: 'new runtime' });
    assert.equal(state.items.get('editor').payload, 'new runtime');
});

test('loading a journal never writes a partially restored journal', () => {
    const source = create();
    let writes = 0;
    const restored = new WorkspaceState('restored', source.journal(), () => writes++);
    assert.equal(writes, 0);
    assert.equal(restored.items.get('editor').owner, 'main');
    restored.ready('main');
    assert.equal(writes, 1);
});

test('retiring a host twice does not revoke the recovered main editor', () => {
    const state = create(); state.retire('popup');
    const recovered = state.snapshot('main').items[0];
    state.retire('popup');
    state.update('main', 'editor', recovered.lease, { payload: 'recovered' });
    assert.equal(state.items.get('editor').payload, 'recovered');
});
