// Copyright (c) UnoDock contributors. MIT. No DOM or runtime dependency.
const zones = new Set(['center', 'left', 'right', 'top', 'bottom']);
const kinds = new Set(['document', 'tool']);
const maximumPayload = 1024 * 1024;
function text(value, limit, name) {
    if (typeof value !== 'string' || value.length > limit) throw new TypeError(`Invalid ${name}.`);
    return value;
}
function zone(value) {
    if (!zones.has(value)) throw new TypeError('Invalid docking zone.');
    return value;
}
export class WorkspaceState {
    constructor(session, restored = null, save = () => {}) {
        this.session = text(session, 80, 'session');
        this.sequence = 0;
        this.theme = 'light';
        this.items = new Map();
        this.windows = new Map([['main', { id: 'main', title: 'Main workspace', ready: false }]]);
        this.save = save;
        this.storageError = '';
        if (restored !== null) {
            if (restored.schema !== 1 || !Array.isArray(restored.items) || restored.items.length > 200) throw new TypeError('Unsupported workspace journal.');
            this.theme = restored.theme === 'dark' ? 'dark' : 'light';
            for (const item of restored.items) {
                this.create('main', item);
                if (item.closed) this.items.get(item.id).closed = true;
            }
        }
    }
    commit() {
        this.sequence++;
        try { this.save(this.journal()); this.storageError = ''; }
        catch { this.storageError = 'Browser storage is unavailable or full. Export the workspace to keep a backup.'; }
    }
    journal() {
        return { schema: 1, session: this.session, theme: this.theme,
            items: [...this.items.values()].map(({ id, kind, type, title, payload, zone, closed }) => ({ id, kind, type, title, payload, zone, closed })) };
    }
    register(id, title) {
        text(id, 80, 'window ID');
        if (this.windows.has(id)) throw new Error('Window already registered.');
        this.windows.set(id, { id, title: text(title, 200, 'window title'), ready: false });
        this.commit();
    }
    ready(id) {
        const window = this.windows.get(id);
        if (!window) throw new Error('Window is not registered.');
        window.ready = true;
        this.commit();
    }
    create(owner, value) {
        if (!this.windows.has(owner) || this.items.size >= 200) throw new Error('Cannot create content in this workspace.');
        const id = text(value.id, 80, 'content ID');
        if (!id || this.items.has(id)) throw new Error('Content ID must be unique.');
        if (!kinds.has(value.kind)) throw new TypeError('Unknown content kind.');
        const item = { id, kind: value.kind, type: text(value.type ?? 'text', 100, 'content type'),
            title: text(value.title, 200, 'title'), payload: text(value.payload, maximumPayload, 'payload'),
            owner, zone: zone(value.zone ?? (value.kind === 'tool' ? 'right' : 'center')), lease: 1, revision: 1, closed: false };
        this.items.set(id, item);
        this.commit();
        return id;
    }
    owned(owner, id, lease) {
        const item = this.items.get(id);
        if (!item || item.closed || item.owner !== owner || item.lease !== lease) throw new Error('Content ownership changed; the stale request was rejected.');
        return item;
    }
    update(owner, id, lease, value) {
        const item = this.owned(owner, id, lease);
        const payload = text(value.payload, maximumPayload, 'payload');
        const title = text(value.title ?? item.title, 200, 'title');
        if (payload === item.payload && title === item.title) return;
        item.payload = payload;
        item.title = title;
        item.revision++;
        this.commit();
    }
    transfer(owner, id, lease, destination, placement = 'center') {
        const item = this.owned(owner, id, lease);
        if (!this.windows.get(destination)?.ready) throw new Error('The destination is not ready.');
        placement = zone(placement);
        item.owner = destination;
        item.zone = placement;
        item.lease++;
        item.revision++;
        this.commit();
    }
    close(owner, id, lease) {
        const item = this.owned(owner, id, lease);
        item.closed = true;
        item.lease++;
        this.commit();
    }
    reopen(owner, id) {
        const item = this.items.get(id);
        if (!this.windows.has(owner) || !item?.closed) throw new Error('No closed content with this identity.');
        item.closed = false;
        item.owner = owner;
        item.lease++;
        this.commit();
    }
    retire(id) {
        if (id === 'main' || !this.windows.has(id)) return;
        for (const item of this.items.values()) {
            if (item.owner === id) { item.owner = 'main'; item.lease++; item.revision++; }
        }
        this.windows.delete(id);
        this.commit();
    }
    setTheme(value) {
        if (value !== 'light' && value !== 'dark') throw new TypeError('Invalid theme.');
        this.theme = value;
        this.commit();
    }
    snapshot(owner) {
        if (!this.windows.has(owner)) throw new Error('Window is no longer registered.');
        return { schema: 1, session: this.session, sequence: this.sequence, windowId: owner,
            theme: this.theme, storageError: this.storageError,
            items: [...this.items.values()].filter(item => item.owner === owner && !item.closed).map(item => ({ ...item })),
            windows: [...this.windows.values()].map(value => ({ ...value })),
            closed: [...this.items.values()].filter(item => item.closed).map(({ id, title }) => ({ id, title })) };
    }
}
