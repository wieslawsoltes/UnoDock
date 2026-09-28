import { test } from 'node:test';
import assert from 'node:assert/strict';
import { WorkspaceState } from '../../src/UnoDock.Browser/Assets/session.mjs';
const content = (id = 'a') => ({ id, kind: 'document', title: 'Notes', payload: 'original' });
function setup(save) {
    const state = new WorkspaceState('test', null, save);
    state.ready('main'); state.register('popup', 'Popup'); state.ready('popup'); state.create('main', content());
    return state;
}
test('one authoritative owner and increasing lease', () => {
    const s = setup(); s.transfer('main', 'a', 1, 'popup', 'left');
    assert.equal(s.snapshot('main').items.length, 0);
    assert.deepEqual(s.snapshot('popup').items.map(x => [x.id, x.lease, x.zone]), [['a', 2, 'left']]);
});
test('stale editor cannot overwrite a moved item', () => {
    const s = setup(); s.transfer('main', 'a', 1, 'popup');
    assert.throws(() => s.update('main', 'a', 1, { payload: 'stale' }));
    assert.equal(s.items.get('a').payload, 'original');
});
test('ABA return rejects the first lease', () => {
    const s = setup(); s.transfer('main', 'a', 1, 'popup'); s.transfer('popup', 'a', 2, 'main');
    assert.throws(() => s.update('main', 'a', 1, { payload: 'stale' }));
    s.update('main', 'a', 3, { payload: 'fresh' }); assert.equal(s.items.get('a').payload, 'fresh');
});
test('failed destination does not relinquish the source', () => {
    const s = setup(); s.register('loading', 'Loading');
    assert.throws(() => s.transfer('main', 'a', 1, 'loading'));
    assert.equal(s.items.get('a').owner, 'main'); assert.equal(s.items.get('a').lease, 1);
});
test('invalid payload or zone does not partially mutate', () => {
    const s = setup();
    assert.throws(() => s.update('main', 'a', 1, { title: 'changed', payload: 42 }));
    assert.throws(() => s.transfer('main', 'a', 1, 'popup', '__proto__'));
    assert.equal(s.items.get('a').title, 'Notes'); assert.equal(s.items.get('a').owner, 'main');
});
test('popup close returns all kinds, retaining edits', () => {
    const s = setup(); s.create('popup', { ...content('tool'), kind: 'tool' });
    s.transfer('main', 'a', 1, 'popup'); s.update('popup', 'a', 2, { payload: 'edited' }); s.retire('popup');
    assert.equal(s.snapshot('main').items.length, 2); assert.equal(s.items.get('a').payload, 'edited');
    assert.throws(() => s.update('popup', 'a', 2, { payload: 'late' }));
});
test('journal restores content, not foreign runtime identities', () => {
    const s = setup(); s.transfer('main', 'a', 1, 'popup');
    const restored = new WorkspaceState('new', s.journal());
    assert.equal(restored.items.get('a').owner, 'main'); assert.equal(restored.items.get('a').payload, 'original');
});
test('snapshots cannot mutate the broker', () => {
    const s = setup(); s.snapshot('main').items[0].payload = 'foreign';
    assert.equal(s.items.get('a').payload, 'original');
});
test('close is retained and can be reopened without duplicate identity', () => {
    const s = setup(); s.close('main', 'a', 1); assert.equal(s.snapshot('main').items.length, 0);
    s.reopen('popup', 'a'); assert.equal(s.items.size, 1); assert.equal(s.snapshot('popup').items[0].id, 'a');
});
test('storage failure is visible and does not corrupt the live model', () => {
    const s = setup(() => { throw Error('quota'); });
    s.update('main', 'a', 1, { payload: 'live' });
    assert.match(s.snapshot('main').storageError, /storage/); assert.equal(s.items.get('a').payload, 'live');
});
test('multiple popup transfers preserve cardinality', () => {
    const s = setup(); s.register('two', 'Two'); s.ready('two');
    for (let i = 0; i < 100; i++) {
        const x = s.items.get('a'); s.transfer(x.owner, 'a', x.lease, ['main', 'popup', 'two'][i % 3]);
        assert.equal([...s.windows.keys()].reduce((n, id) => n + s.snapshot(id).items.length, 0), 1);
    }
});
test('journals reject duplicate IDs and invalid schema', () => {
    assert.throws(() => new WorkspaceState('x', { schema: 2, items: [] }));
    assert.throws(() => new WorkspaceState('x', { schema: 1, items: [content(), content()] }));
});
