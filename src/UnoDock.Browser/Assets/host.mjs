import { activateNativeAccessibility } from './accessibility.mjs';
import { acknowledgeEdit } from './edit-acknowledgement.mjs';
import { WorkspaceState } from './session.mjs';
const MIME = 'application/x-unodock-transfer';
const JOURNAL = 'unodock.browser.workspace.v1';
const MAX_WINDOWS = 8;
const here = new URL(location.href);
const fragment = new URLSearchParams(location.hash.slice(1));
const windowId = fragment.get('window') || 'main';
const capability = fragment.get('cap') || crypto.randomUUID();
let active = '', requestedActive = '', applied = null, errorText = '', dragging = false, rootHub;
const $ = id => document.getElementById(id);
const uid = () => crypto.randomUUID();

class WindowHub {
    constructor() {
        this.session = uid();
        this.handles = new Map([['main', { ref: window, cap: capability, pending: null, born: Date.now() }]]);
        this.drags = new Map();
        let journal = null;
        try { const raw = localStorage.getItem(JOURNAL); if (raw) journal = JSON.parse(raw); }
        catch { errorText = 'The recovery journal could not be read. Your browser may have storage disabled.'; }
        try { this.state = new WorkspaceState(this.session, journal); }
        catch { this.state = new WorkspaceState(this.session); errorText = 'The existing journal is invalid and was not loaded. Export it before resetting browser storage.'; }
        this.state.save = data => localStorage.setItem(JOURNAL, JSON.stringify(data));
        if (!journal && this.state.items.size === 0) {
            for (const item of [
                { id: 'welcome', title: 'Welcome.md', kind: 'document', payload: '# Your workspace, across windows\n\nEdit this native Uno TextBox, float it into a real browser window, then dock it back.\n\nDrag a content chip in the browser transfer strip to another window. Within each Uno canvas, normal tab dragging, splits and auto-hide remain available.' },
                { id: 'architecture', title: 'Architecture.cs', kind: 'document', payload: '// One Uno runtime per browser window.\n// One authoritative content owner per lease.\n// Application payloads cross runtimes; UIElements do not.\n\npublic sealed record WorkspaceDocument(string Id, string Text);' },
                { id: 'notes', title: 'Notes', kind: 'tool', zone: 'right', payload: 'A dockable tool window.\n\nChanges are journaled on every edit. Closing a satellite returns its content to the primary workspace.' },
                { id: 'output', title: 'Output', kind: 'tool', zone: 'bottom', payload: 'UnoDock browser workbench\nReady. No server or account required.' }
            ]) this.state.create('main', item);
        }
    }
    check(id, cap, ref) {
        // A child can retain a function from a document which has navigated away.
        // WindowProxy identity alone must not authorize that retired broker.
        if (window.closed || window.UnoDockBrowserHub !== this) throw Error('The primary workspace session has ended. Recover the journal in a main workspace.');
        const handle = this.handles.get(id);
        if (!handle || handle.cap !== cap || handle.ref !== ref || ref.closed) throw Error('This window does not own a registered workspace host.');
        return handle;
    }
    connect(ref, id, cap) {
        this.check(id, cap, ref);
        return { call: request => this.execute(id, cap, ref, request) };
    }
    sweep() {
        for (const [id, handle] of this.handles) {
            if (id === 'main') continue;
            let inaccessible = false;
            try {
                const starting = handle.pending && handle.ref.location.href === 'about:blank' && Date.now() - handle.born <= 90000;
                inaccessible = handle.ref.closed || (!starting && handle.ref.location.origin !== location.origin);
            }
            catch { inaccessible = true; }
            if (inaccessible || (handle.pending && Date.now() - handle.born > 90000)) {
                this.state.retire(id); this.handles.delete(id);
                try { handle.ref.close(); } catch {}
            }
        }
        for (const [id, ticket] of this.drags) if (ticket.expires < Date.now()) this.drags.delete(id);
    }
    execute(id, cap, ref, request) {
        const handle = this.check(id, cap, ref);
        this.sweep();
        switch (request.op) {
            case 'read': return this.state.snapshot(id);
            case 'ready': {
                this.state.ready(id);
                const pending = handle.pending; handle.pending = null;
                if (pending) {
                    try { this.state.transfer(pending.owner, pending.id, pending.lease, id, pending.zone); }
                    catch { /* A newer ownership request retired this pending opening. */ }
                }
                return this.state.snapshot(id);
            }
            case 'update': return acknowledgeEdit(this.state, id, request);
            case 'close': this.state.close(id, request.id, request.lease); break;
            case 'reopen': this.state.reopen(id, request.id); break;
            case 'create': return this.state.create(id, { id: uid(), kind: request.kind, type: request.type || 'text', title: request.title, payload: request.payload || '', zone: request.zone });
            case 'theme': this.state.setTheme(request.theme); break;
            case 'move': this.state.transfer(id, request.id, request.lease, request.destination, request.zone || 'center'); break;
            case 'float': {
                const item = this.state.owned(id, request.id, request.lease);
                for (const [existingId, existing] of this.handles) {
                    if (existing.pending?.owner === id && existing.pending.id === item.id && existing.pending.lease === item.lease) {
                        existing.ref.focus(); return existingId;
                    }
                }
                if (this.handles.size >= MAX_WINDOWS) throw Error('Eight workspace windows are already open. Reuse a window with Move.');
                const childId = uid(), childCap = uid();
                const url = new URL(here); url.hash = new URLSearchParams({ window: childId, cap: childCap, session: this.session }).toString();
                // The caller is the user-activated window. No blank-window pool,
                // popup-blocker bypass or unsolicited reopening is used.
                const child = ref.open(url.href, `unodock-${childId}`, 'popup,width=960,height=700,resizable=yes,scrollbars=yes');
                if (!child) throw Error('Popup blocked. Allow popups for this site, then click Float again. The content has not moved.');
                this.state.register(childId, item.title);
                this.handles.set(childId, { ref: child, cap: childCap, born: Date.now(), pending: { owner: id, id: item.id, lease: item.lease, zone: item.zone } });
                return childId;
            }
            case 'dockAll':
                for (const item of [...this.state.items.values()]) if (item.owner === id && !item.closed) this.state.transfer(id, item.id, item.lease, 'main', item.zone);
                break;
            case 'drag': {
                const item = this.state.owned(id, request.id, request.lease);
                const ticket = uid(); this.drags.set(ticket, { owner: id, id: item.id, lease: item.lease, expires: Date.now() + 60000 });
                return ticket;
            }
            case 'cancelDrag': this.drags.delete(request.ticket); break;
            case 'drop': {
                const ticket = this.drags.get(request.ticket); this.drags.delete(request.ticket);
                if (!ticket || ticket.expires < Date.now()) throw Error('This drag has expired or belongs to another workspace.');
                this.state.transfer(ticket.owner, ticket.id, ticket.lease, id, request.zone); break;
            }
            case 'export': return this.state.journal();
            default: throw Error('Unknown browser workspace operation.');
        }
        return true;
    }
}

let connection;
try {
    if (windowId === 'main') {
        rootHub = new WindowHub();
    } else {
        if (!window.opener || window.opener.closed) throw Error('The primary workspace is closed. Recover its journal in a new main workspace.');
        rootHub = window.opener.UnoDockBrowserHub;
        if (!rootHub || rootHub.session !== fragment.get('session')) throw Error('The workspace session has ended. Recover the journal in a new main workspace.');
    }
    // Children opened from children retain the same authority, not a chain of brokers.
    window.UnoDockBrowserHub = rootHub;
    connection = rootHub.connect(window, windowId, capability);
} catch (error) { errorText = error.message; }

function execute(request) {
    if (!connection) throw Error(errorText || 'Workspace connection is unavailable.');
    try { return connection.call(request); }
    catch (error) { errorText = error.message; throw error; }
}
function safe(request) {
    try { const value = execute(request); if (request.op !== 'read' && request.op !== 'cancelDrag') errorText = ''; render(); return value; }
    catch { render(); return null; }
}
function selected(snapshot) { return snapshot.items.find(item => item.id === active) || snapshot.items[0]; }
window.UnoDockBrowser = Object.freeze({
    call(json) {
        try {
            const request = typeof json === 'string' ? JSON.parse(json) : json;
            if (request.op === 'activate') { active = request.id; return JSON.stringify({ ok: true }); }
            const value = execute(request);
            if (request.op === 'read' || request.op === 'ready') value.active = requestedActive;
            return JSON.stringify({ ok: true, value });
        } catch (error) { return JSON.stringify({ ok: false, error: error.message }); }
    },
    report(json) {
        applied = typeof json === 'string' ? JSON.parse(json) : json;
        document.documentElement.dataset.unoReady = 'true';
        const frame = $('app');
        const nativeDocument = frame.contentDocument;
        if (nativeDocument && !accessibilityDocuments.has(nativeDocument)) {
            accessibilityDocuments.add(nativeDocument);
            activateNativeAccessibility(frame, message => { errorText = message; render(); });
        }
        if (applied.active === requestedActive) requestedActive = '';
        if (!requestedActive && applied.active) active = applied.active;
        render();
    },
    get applied() { return applied; },
    snapshot() { return execute({ op: 'read' }); },
    get windowId() { return windowId; }
});

const accessibilityDocuments = new WeakSet();
let lastChips = '', lastWindows = '', lastClosed = '', latest = null;
function render() {
    try { latest = execute({ op: 'read' }); }
    catch { latest = null; }
    $('status').textContent = errorText || latest?.storageError || (applied ? 'Changes saved locally · Drag a chip between windows to dock' : 'Starting the Uno WebAssembly renderer…');
    $('status').dataset.error = String(Boolean(errorText || latest?.storageError));
    $('recovery').hidden = Boolean(latest);
    if (!latest) { $('app').style.pointerEvents = 'none'; return; }
    document.documentElement.dataset.theme = latest.theme;
    const item = selected(latest);
    $('float').disabled = !item || !applied;
    $('move').disabled = !item || !applied;
    $('return').hidden = windowId === 'main';
    $('window-title').textContent = windowId === 'main' ? 'Main workspace' : (item?.title || 'Floating workspace');
    document.title = `${$('window-title').textContent} — UnoDock`;
    const signature = JSON.stringify(latest.items.map(x => [x.id, x.title, x.lease, x.id === active]));
    if (!dragging && signature !== lastChips) {
        lastChips = signature; $('chips').replaceChildren();
        for (const value of latest.items) {
            const chip = document.createElement('button');
            chip.type = 'button'; chip.className = 'content-chip'; chip.draggable = true;
            chip.textContent = value.title; chip.dataset.content = value.id;
            chip.setAttribute('aria-pressed', String(value.id === item?.id));
            chip.title = 'Drag to another browser window, or select and use Float / Move';
            chip.onclick = () => { active = requestedActive = value.id; safe({ op: 'read' }); };
            let ticket;
            chip.ondragstart = event => {
                ticket = safe({ op: 'drag', id: value.id, lease: value.lease });
                if (!ticket) { event.preventDefault(); return; }
                dragging = true; event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData(MIME, ticket);
                event.dataTransfer.setData('text/plain', `UnoDock content: ${value.title}`);
            };
            chip.ondragend = () => { dragging = false; if (ticket) safe({ op: 'cancelDrag', ticket }); $('zones').hidden = true; };
            $('chips').append(chip);
        }
    }
    const windows = latest.windows.filter(x => x.ready && x.id !== windowId);
    const windowSignature = JSON.stringify(windows);
    if (windowSignature !== lastWindows) {
        lastWindows = windowSignature; const previous = $('destination').value; $('destination').replaceChildren();
        for (const value of windows) $('destination').add(new Option(value.title, value.id));
        if (windows.some(x => x.id === previous)) $('destination').value = previous;
    }
    $('move').disabled ||= windows.length === 0;
    const closedSignature = JSON.stringify(latest.closed);
    if (closedSignature !== lastClosed) {
        lastClosed = closedSignature; $('closed').replaceChildren(new Option('Reopen closed content…', ''));
        for (const value of latest.closed) $('closed').add(new Option(value.title, value.id));
    }
}
$('float').onclick = () => { const item = selected(latest); if (item) safe({ op: 'float', id: item.id, lease: item.lease }); };
$('move').onclick = () => { const item = selected(latest); if (item) safe({ op: 'move', id: item.id, lease: item.lease, destination: $('destination').value, zone: $('placement').value }); };
$('return').onclick = () => { if (safe({ op: 'dockAll' })) window.close(); };
$('theme').onclick = () => safe({ op: 'theme', theme: latest?.theme === 'dark' ? 'light' : 'dark' });
$('new-document').onclick = () => safe({ op: 'create', kind: 'document', title: 'Untitled.txt', payload: '' });
$('new-tool').onclick = () => safe({ op: 'create', kind: 'tool', title: 'Notes tool', payload: '' });
$('closed').onchange = () => { if ($('closed').value) safe({ op: 'reopen', id: $('closed').value }); };
$('export').onclick = () => {
    const journal = safe({ op: 'export' }); if (!journal) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(journal, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'unodock-workspace.json'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
};
$('recover').onclick = () => { const url = new URL(location.href); url.hash = ''; location.replace(url); };
for (const target of document.querySelectorAll('[data-zone]')) {
    target.ondragover = event => { if (event.dataTransfer.types.includes(MIME)) { event.preventDefault(); event.dataTransfer.dropEffect = 'move'; target.classList.add('over'); } };
    target.ondragleave = () => target.classList.remove('over');
    target.ondrop = event => { event.preventDefault(); target.classList.remove('over'); $('zones').hidden = true; safe({ op: 'drop', ticket: event.dataTransfer.getData(MIME), zone: target.dataset.zone }); };
}
document.addEventListener('dragenter', event => { if (event.dataTransfer?.types.includes(MIME)) $('zones').hidden = false; });
document.addEventListener('dragover', event => { if (event.dataTransfer?.types.includes(MIME)) event.preventDefault(); });
document.addEventListener('keydown', event => { if (event.key === 'Escape') $('zones').hidden = true; });
document.addEventListener('dragleave', event => { if (!event.relatedTarget && (event.clientX <= 0 || event.clientY <= 0)) $('zones').hidden = true; });
$('app').addEventListener('load', () => {
    try {
        const document = $('app').contentDocument;
        // Cross-document drags enter the actual Uno frame rather than the outer
        // shell. Reveal browser-owned targets before accepting the drop.
        for (const eventName of ['dragenter', 'dragover']) document?.addEventListener(eventName, event => {
            if (event.dataTransfer?.types.includes(MIME)) {
                event.preventDefault(); $('zones').hidden = false;
            }
        });
    } catch { errorText = 'The Uno application must be hosted on the same origin.'; }
});
if (connection) $('app').src = '../gallery/?browser-workspace=1';
render();
setInterval(render, 250);
