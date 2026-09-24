"""One-use preview20 source migration; never edits original expectations or API mappings."""
from pathlib import Path
import hashlib
import json
import re
import sys
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent

def replace(path, old, new):
    file = ROOT / path
    text = file.read_text()
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected one exact migration anchor, found {text.count(old)}')
    file.write_text(text.replace(old, new))

def migrate_previews(path):
    file = ROOT / path
    text = file.read_text()
    pattern = re.compile(r'\b(?P<receiver>(?:\w+\.)*\w+)\.Selected(?P<kind>Document|Anchorable)\s*=(?!=)\s*')
    edits = []
    for match in pattern.finditer(text):
        start = match.end(); end = start; stack = []; quote = None
        while end < len(text):
            char = text[end]
            if quote:
                if char == '\\': end += 2; continue
                if char == quote: quote = None
            elif char in ('"', "'"): quote = char
            elif char in '([{': stack.append(char)
            elif char in ')]}':
                if not stack: break
                if '([{'.index(stack.pop()) != ')]}'.index(char): raise RuntimeError('Unbalanced assignment')
            elif not stack and char in ';,': break
            end += 1
        rhs = text[start:end].rstrip()
        if not rhs or quote or stack: raise RuntimeError(f'{path}: unsupported assignment')
        original = text[match.start():end]
        replacement = f"{match['receiver']}.Preview{match['kind']}({rhs})"
        edits.append((match.start(), end, replacement))
        print(f'Preview-only test migration in {path}: {original} -> {replacement}')
    for start, end, replacement in reversed(edits): text = text[:start] + replacement + text[end:]
    # A method call cannot narrow a nullable property for Roslyn as assignment did.
    # This changes only annotations, not test execution or assertions.
    text = text.replace('nav.SelectedDocument.LayoutElement', 'nav.SelectedDocument!.LayoutElement')
    text = text.replace('nav.SelectedAnchorable.LayoutElement', 'nav.SelectedAnchorable!.LayoutElement')
    text = text.replace('navigator selection dependency properties are mutually exclusive', 'navigator preview operations publish mutually exclusive dependency properties')
    file.write_text(text)
    if not edits: raise RuntimeError(f'No preview migration in expected file {path}')

protected = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT / 'contracts').rglob('*') if p.is_file()}
archive = Path(sys.argv[1]).read_bytes()
assert hashlib.sha256(archive).hexdigest() == 'dbc5bab116edbcb388fcb662f07446896cf3de3fb4f83e533c8243cb40d8a1a5'
with zipfile.ZipFile(Path(sys.argv[1])) as z:
    xml = z.read('navigator-selection-observations.xml')
    provenance = json.loads(z.read('provenance.json').decode('utf-8-sig'))
assert provenance['probeRevision'] == 'b007cd14d9165107d37044faec1ab49b3b3e94bc'
assert provenance['referenceRevision'] == '2c71faba5eecc1b6ae6cd3d269408e0df37715d8'
assert provenance['byteIdentical'] is True and provenance['cases'] == 28
assert hashlib.sha256(xml).hexdigest() == provenance['sha256']
assert len(ET.fromstring(xml).findall('Case')) == 28
(ROOT / 'contracts/visual-fixtures/navigator-selection-observations.xml').write_bytes(xml)
provenance.update(runId=36032890549, artifactId=10823916978,
                  artifactSha256='dbc5bab116edbcb388fcb662f07446896cf3de3fb4f83e533c8243cb40d8a1a5')
(ROOT / 'contracts/visual-fixtures/navigator-selection-provenance.json').write_text(json.dumps(provenance, indent=2) + '\n')

path = 'src/UnoDock/Controls/NavigatorWindow.cs'
replace(path, '''    protected virtual void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e)
    {
        // Ignore only the value currently being published, not arbitrary callbacks.
        // An observer assigning a different value while we publish queues a real request.
        if (!ReferenceEquals(e.NewValue, _selected as LayoutDocumentItem)) Select(e.NewValue as LayoutItem);
    }
    protected virtual void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(e.NewValue, _selected as LayoutAnchorableItem)) Select(e.NewValue as LayoutItem);
    }''', '''    protected virtual void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e) => DirectSelectionChanged(e, true);
    protected virtual void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e) => DirectSelectionChanged(e, false);''')
replace(path, '''        finally { _selecting = false; _hasPendingSelection = false; _pendingSelection = null; }
    }
    private void PublishSelection()''', '''        finally { _selecting = false; _hasPendingSelection = false; _pendingSelection = null; }
        DrainDirectSelection();
    }
    private void PublishSelection()''')
replace('src/UnoDock/Controls/NavigatorWindow.Selection.cs',
    'var published = document ? _selected as LayoutDocumentItem : _selected as LayoutAnchorableItem as LayoutItem;',
    'LayoutItem? published = document ? _selected as LayoutDocumentItem : _selected as LayoutAnchorableItem;')
for path in ['tests/UnoDock.VisualTests/NavigatorQualityTests.cs', 'tests/UnoDock.VisualTests/NavigatorCommitTests.cs',
             'tests/UnoDock.VisualTests/NavigatorSampleTests.cs', 'tests/UnoDock.Runtime.Tests/WindowLifecycleTests.cs']:
    migrate_previews(path)
replace('tests/UnoDock.VisualTests/NavigatorQualityTests.cs',
    '        return await tests.Run(output, "navigator-quality");',
    '        NavigatorSelectionTests.Register(tests, host, output);\n        return await tests.Run(output, "navigator-quality");')
replace('tests/UnoDock.VisualTests/NavigatorSelectionTests.cs', 'UnoDock.VisualValidation.VisualCapture.Save', 'VisualCapture.Save')
replace('tests/UnoDock.VisualTests/NavigatorSelectionTests.cs',
    'Host.GetLayoutItemFromModel(tool ? ToolModels[2] : Docs[2] as LayoutContent ?? throw new InvalidOperationException())',
    'Host.GetLayoutItemFromModel(tool ? (LayoutContent)ToolModels[2] : Docs[2])')

path = 'samples/UnoDock.Gallery/NavigatorLab.cs'
replace(path, '        Add("3 documents", "three", () => Populate(3));',
    '''        Add("Assign document", "assign-document", () => AssignSelection(false));
        Add("Assign tool", "assign-tool", () => AssignSelection(true));
        Add("3 documents", "three", () => Populate(3));''')
replace(path, 'Edits remain in their existing buffers."',
    'Assign document/tool uses the public selection property: an allowed document hides, a tool closes, and a veto keeps the list open. Edits remain in their existing buffers."')
replace(path, '        SampleButton Add(string title, string id, Action action)',
    '''        void AssignSelection(bool tool)
        {
            var navigator = manager.FindVisualChildren<NavigatorWindow>().FirstOrDefault();
            if (navigator == null) { manager.OpenNavigator(); navigator = manager.FindVisualChildren<NavigatorWindow>().FirstOrDefault(); }
            if (navigator == null) return;
            if (tool) navigator.SelectedAnchorable = navigator.Anchorables.LastOrDefault();
            else navigator.SelectedDocument = navigator.Documents.FirstOrDefault(item => !ReferenceEquals(item, navigator.SelectedDocument));
            UpdateStatus();
        }
        SampleButton Add(string title, string id, Action action)''')
replace('Directory.Build.props', '0.1.0-preview.19', '0.1.0-preview.20')
p = ROOT / 'README.md'; text = p.read_text().replace('Status: 0.1.0-preview.19.', 'Status: 0.1.0-preview.20.')
text = text.replace('# UnoDock\n', '''# UnoDock

## Preview 20: direct navigator selection

`SelectedDocument` and `SelectedAnchorable` assignments now request activation rather
than just highlighting a row. The observed document path hides without Closing/Closed;
the tool path uses cancellable Closing followed by Closed. Command vetoes keep the
navigator visible. For programmatic preview without activation, use the additive
`PreviewDocument(item)` / `PreviewAnchorable(item)` methods. Keyboard preview is unchanged.
See [exact behavior, independent observations and safety differences](docs/navigator-selection.md).

''', 1); p.write_text(text)
p = ROOT / 'docs/navigator-activation.md'; text = p.read_text()
p.write_text('''> Preview20 update: the historical direct-setter boundary below is superseded for
> active-session assignment by [navigator-selection.md](navigator-selection.md).
> Explicit keyboard preview/commit remains distinct from property assignment.

''' + text)
for p, digest in protected.items(): assert hashlib.sha256(p.read_bytes()).hexdigest() == digest, p
print(f'Preserved {len(protected)} existing contract files byte-for-byte; added verified raw observations.')
