import { test } from 'node:test';
import assert from 'node:assert/strict';
import { WorkspaceState } from '../../src/UnoDock.Browser/Assets/session.mjs';

function setup() {
    const state = new WorkspaceState('destination-test');
    state.ready('main');
    state.register('satellite', 'Satellite');
    state.ready('satellite');
    state.create('main', { id: 'document', kind: 'document', title: 'Document', payload: 'unsaved native text' });
    state.create('satellite', { id: 'tool', kind: 'tool', title: 'Tool', payload: 'tool text', zone: 'left' });
    return state;
}
const record = (state, id) => ({ ...state.items.get(id) });

test('a failed native destination rejects transfer without altering source ownership', () => {
    const state = setup();
    state.setAccepting('satellite', false);
    const before = record(state, 'document'), sequence = state.sequence;
    assert.throws(() => state.transfer('main', 'document', before.lease, 'satellite'), /renderer is not ready/);
    assert.deepEqual(record(state, 'document'), before);
    assert.equal(state.sequence, sequence);
    assert.equal(state.snapshot('main').windows.find(window => window.id === 'satellite').ready, false);
    assert.equal(state.windows.get('satellite').ready, true, 'The prior host generation must remain recorded.');
});

test('availability changes do not journal content or churn leases on repeated reports', () => {
    const state = setup(), before = record(state, 'tool');
    let writes = 0;
    state.save = () => writes++;
    const initial = state.sequence;
    state.setAccepting('satellite', false);
    for (let i = 0; i < 20; i++) state.setAccepting('satellite', false);
    state.setAccepting('satellite', true);
    for (let i = 0; i < 20; i++) state.setAccepting('satellite', true);
    assert.equal(state.sequence, initial + 2);
    assert.equal(writes, 0);
    assert.deepEqual(record(state, 'tool'), before);
});

test('suspending transfers does not let a reloaded host reuse its former lease', () => {
    const state = setup(), previous = record(state, 'tool');
    state.setAccepting('satellite', false);
    state.ready('satellite');
    state.setAccepting('satellite', true);
    assert.deepEqual(record(state, 'tool'), { ...previous, lease: previous.lease + 1, revision: previous.revision + 1 });
    assert.throws(() => state.update('satellite', 'tool', previous.lease, { payload: 'stale editor' }), /stale request/);
});

test('a new destination cannot be marked accepting before its native handshake', () => {
    const state = setup();
    state.register('starting', 'Starting');
    const sequence = state.sequence;
    assert.throws(() => state.setAccepting('starting', true), /readiness handshake/);
    assert.throws(() => state.setAccepting('missing', false), /not registered/);
    assert.throws(() => state.setAccepting('satellite', 'true'), /Invalid/);
    assert.equal(state.sequence, sequence);
    assert.equal(state.snapshot('main').windows.find(window => window.id === 'starting').ready, false);
});

test('dock-all rejects an unavailable primary before changing any source record', () => {
    const state = setup();
    state.transfer('main', 'document', 1, 'satellite', 'top');
    state.setAccepting('main', false);
    const before = state.snapshot('satellite'), sequence = state.sequence;
    assert.throws(() => state.transferAll('satellite', 'main'), /renderer is not ready/);
    assert.deepEqual(state.snapshot('satellite'), before);
    assert.equal(state.sequence, sequence);
});

test('dock-all publishes one complete ownership set while retaining zones and payloads', () => {
    const state = setup();
    state.transfer('main', 'document', 1, 'satellite', 'bottom');
    const before = state.snapshot('satellite').items, sequence = state.sequence;
    const observed = [];
    state.save = () => observed.push(state.snapshot('main').items);
    assert.equal(state.transferAll('satellite', 'main'), 2);
    assert.equal(state.sequence, sequence + 1);
    assert.equal(observed.length, 1);
    assert.equal(observed[0].length, 2);
    for (const previous of before) {
        assert.deepEqual(record(state, previous.id), { ...previous, owner: 'main', lease: previous.lease + 1, revision: previous.revision + 1 });
        assert.throws(() => state.owned('satellite', previous.id, previous.lease), /stale request/);
    }
    assert.deepEqual(state.snapshot('satellite').items, []);
});

test('dock-all retains complete in-memory ownership after a persistence failure', () => {
    const state = setup();
    state.transfer('main', 'document', 1, 'satellite');
    let writes = 0;
    state.save = () => { writes++; throw new Error('quota'); };
    assert.equal(state.transferAll('satellite', 'main'), 2);
    assert.equal(writes, 1);
    assert.equal(state.snapshot('main').items.length, 2);
    assert.equal(state.snapshot('satellite').items.length, 0);
    assert.match(state.storageError, /storage/);
});

test('dock-all validates the entire source placement set before publication', () => {
    const state = setup();
    state.transfer('main', 'document', 1, 'satellite');
    state.items.get('document').zone = 'invalid internal placement';
    const before = state.snapshot('satellite'), sequence = state.sequence;
    assert.throws(() => state.transferAll('satellite', 'main'), /Invalid docking zone/);
    assert.deepEqual(state.snapshot('satellite'), before);
    assert.equal(state.sequence, sequence);
});

test('dock-all skips closed content and treats empty or same-host requests as no-ops', () => {
    const state = setup();
    state.close('satellite', 'tool', 1);
    const closed = record(state, 'tool'), sequence = state.sequence;
    assert.equal(state.transferAll('satellite', 'main'), 0);
    assert.equal(state.transferAll('main', 'main'), 0);
    assert.deepEqual(record(state, 'tool'), closed);
    assert.equal(state.sequence, sequence);
    assert.throws(() => state.transferAll('missing', 'main'), /Source window/);
});

test('browser-close recovery retains content even when the primary renderer failed', () => {
    const state = setup();
    state.setAccepting('main', false);
    const previous = record(state, 'tool');
    state.retire('satellite');
    assert.deepEqual(record(state, 'tool'), { ...previous, owner: 'main', lease: previous.lease + 1, revision: previous.revision + 1 });
    assert.equal(state.windows.has('satellite'), false);
    assert.equal(state.snapshot('main').windows[0].ready, false);
    state.ready('main');
    assert.equal(state.snapshot('main').items.length, 2);
});

test('availability is not persisted into a restored workspace journal', () => {
    const state = setup();
    state.setAccepting('main', false);
    state.setAccepting('satellite', false);
    const restored = new WorkspaceState('new-session', state.journal());
    assert.equal(restored.snapshot('main').windows[0].ready, false);
    restored.ready('main');
    assert.equal(restored.snapshot('main').windows[0].ready, true);
    assert.equal(restored.snapshot('main').items.length, 2);
});
