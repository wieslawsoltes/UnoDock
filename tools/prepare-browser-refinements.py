"""Additional exact browser integration anchors; removed after successful preparation."""
from pathlib import Path
pending = {}
def replace(path, old, new):
    text = pending.get(path, Path(path).read_text())
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected unique anchor: {old[:100]!r}')
    pending[path] = text.replace(old, new)
replace('src/UnoDock.Browser/Assets/host.mjs',
        '    check(id, cap, ref) {\n        const handle = this.handles.get(id);',
        '''    check(id, cap, ref) {
        // A child can retain a function from a document which has navigated away.
        // WindowProxy identity alone must not authorize that retired broker.
        if (window.closed || window.UnoDockBrowserHub !== this) throw Error('The primary workspace session has ended. Recover the journal in a main workspace.');
        const handle = this.handles.get(id);''')
replace('src/UnoDock.Browser/Assets/host.mjs',
        "if (connection) $('app').src = '../gallery/?browser-workspace=1';",
        '''$('app').addEventListener('load', () => {
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
if (connection) $('app').src = '../gallery/?browser-workspace=1';''')
replace('src/UnoDock.Browser/Assets/host.mjs',
        "    try { const value = execute(request); errorText = ''; render(); return value; }",
        "    try { const value = execute(request); if (request.op !== 'read' && request.op !== 'cancelDrag') errorText = ''; render(); return value; }")
replace('README.md', '\n tests/                 Runtime, browser, source, and compatibility tests', '\ntests/                  Runtime, browser, source, and compatibility tests')
for path, text in pending.items():
    Path(path).write_text(text)
    print(path)
