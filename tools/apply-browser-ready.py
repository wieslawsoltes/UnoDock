"""Integrate native accessibility readiness against exact reviewed anchors."""
from pathlib import Path
p = Path('src/UnoDock.Browser/Assets/host.mjs')
t = p.read_text()
assert not t.startswith("import { activateNativeAccessibility }")
t = "import { activateNativeAccessibility } from './accessibility.mjs';\n" + t
anchor = "let lastChips = '', lastWindows = '', lastClosed = '', latest = null;"
assert t.count(anchor) == 1
t = t.replace(anchor, "const accessibilityDocuments = new WeakSet();\n" + anchor)
anchor = "        document.documentElement.dataset.unoReady = 'true';"
assert t.count(anchor) == 1
t = t.replace(anchor, anchor + "\n        const frame = $('app');\n        const nativeDocument = frame.contentDocument;\n        if (nativeDocument && !accessibilityDocuments.has(nativeDocument)) {\n            accessibilityDocuments.add(nativeDocument);\n            activateNativeAccessibility(frame, message => { errorText = message; render(); });\n        }")
p.write_text(t)
p = Path('tests/browser/workspace.spec.mjs')
t = p.read_text()
old = "const ready = page => page.waitForFunction(() => window.UnoDockBrowser?.applied?.items, null, { timeout: 90000 });"
assert t.count(old) == 1
t = t.replace(old, """async function ready(page) {
    let crash, close;
    const terminated = new Promise((_, reject) => {
        crash = () => reject(new Error('The browser renderer crashed before native workspace readiness.'));
        close = () => reject(new Error('The browser window closed before native workspace readiness.'));
        page.once('crash', crash); page.once('close', close);
    });
    try {
        await Promise.race([terminated, page.waitForFunction(() => window.UnoDockBrowser?.applied?.items, null, { timeout: 90000 })]);
    } finally {
        page.off('crash', crash); page.off('close', close);
    }
}""")
p.write_text(t)
