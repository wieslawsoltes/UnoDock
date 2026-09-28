import { test } from 'node:test';
import assert from 'node:assert/strict';
import { WorkspaceState } from '../../src/UnoDock.Browser/Assets/session.mjs';
import { acknowledgeEdit } from '../../src/UnoDock.Browser/Assets/edit-acknowledgement.mjs';

function fixture(save = () => {}) {
    const state = new WorkspaceState('edit-acknowledgement', null, save);
    state.ready('main');
    state.create('main', { id: 'text', kind: 'document', title: 'Text', payload: 'initial' });
    return state;
}
const request = (payload, lease = 1) => ({ id: 'text', lease, title: 'Text', payload });

test('edit acknowledgements contain the exact committed record and revision', () => {
    const state = fixture();
    const result = acknowledgeEdit(state, 'main', request('changed'));
    assert.deepEqual(result, state.snapshot('main').items[0]);
    assert.equal(result.payload, 'changed');
    assert.equal(result.revision, 2);
    assert.equal(result.lease, 1);
});

test('acknowledgement records cannot mutate the broker', () => {
    const state = fixture();
    const result = acknowledgeEdit(state, 'main', request('changed'));
    result.payload = 'forged'; result.revision = 9000; result.owner = 'foreign';
    assert.equal(state.owned('main', 'text', 1).payload, 'changed');
    assert.equal(state.owned('main', 'text', 1).revision, 2);
});

test('duplicate native notifications acknowledge without creating revisions or saves', () => {
    let saves = 0;
    const state = fixture(() => saves++);
    const first = acknowledgeEdit(state, 'main', request('changed'));
    const saved = saves, sequence = state.sequence;
    const duplicate = acknowledgeEdit(state, 'main', request('changed'));
    assert.deepEqual(duplicate, first);
    assert.notEqual(duplicate, first);
    assert.equal(saves, saved);
    assert.equal(state.sequence, sequence);
});

test('acknowledgement uses authority revisions after another valid edit', () => {
    const state = fixture();
    state.update('main', 'text', 1, request('another view'));
    const result = acknowledgeEdit(state, 'main', request('latest native input'));
    assert.equal(result.revision, 3);
    assert.equal(result.payload, 'latest native input');
});

test('stale leased input never receives a successful acknowledgement', () => {
    const state = fixture();
    state.register('satellite', 'Satellite'); state.ready('satellite');
    state.transfer('main', 'text', 1, 'satellite');
    assert.throws(() => acknowledgeEdit(state, 'main', request('stale')), /ownership changed/);
    assert.equal(state.snapshot('satellite').items[0].payload, 'initial');
    const current = acknowledgeEdit(state, 'satellite', request('satellite edit', 2));
    assert.equal(current.owner, 'satellite');
    assert.equal(current.revision, 3);
});

test('invalid edits preserve both the authority and previous acknowledgement', () => {
    const state = fixture();
    const accepted = acknowledgeEdit(state, 'main', request('accepted'));
    const sequence = state.sequence;
    assert.throws(() => acknowledgeEdit(state, 'main', { ...request('invalid'), title: 'x'.repeat(201) }), /Invalid title/);
    assert.deepEqual(state.snapshot('main').items[0], accepted);
    assert.equal(state.sequence, sequence);
});

test('storage failure acknowledges the live edit while retaining a visible durability error', () => {
    const state = fixture();
    state.save = () => { throw new Error('storage full'); };
    const result = acknowledgeEdit(state, 'main', request('recoverable live edit'));
    assert.equal(result.payload, 'recoverable live edit');
    assert.match(state.snapshot('main').storageError, /unavailable or full/);
    assert.equal(state.journal().items[0].payload, result.payload);
});

test('interleaved polling and rapid edit acknowledgements preserve input order', () => {
    const state = fixture();
    let text = '';
    for (const character of 'Edited in the real Uno browser TextBox.') {
        text += character;
        const result = acknowledgeEdit(state, 'main', request(text));
        assert.deepEqual(state.snapshot('main').items[0], result);
        assert.equal(result.payload, text);
    }
    assert.equal(state.snapshot('main').items[0].payload, text);
});
