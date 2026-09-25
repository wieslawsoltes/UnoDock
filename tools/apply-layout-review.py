"""One-use integration of separately reviewed model partials into the pinned source.
Validate every replacement in memory before writing. Removed after integration.
"""
from pathlib import Path
pending = {}

def replace(path, old, new):
    text = pending.get(path, Path(path).read_text(encoding='utf-8'))
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected exactly one integration anchor: {old[:70]!r}')
    pending[path] = text.replace(old, new)

def remove_method(path, signature, replacement=''):
    text = pending.get(path, Path(path).read_text(encoding='utf-8'))
    start = text.index(signature)
    brace = text.index('{', start)
    depth = 1; end = brace + 1
    # These two pinned method bodies contain no braces in string literals.
    while depth:
        if text[end] == '{': depth += 1
        elif text[end] == '}': depth -= 1
        end += 1
    pending[path] = text[:start] + replacement + text[end:]

root = 'src/UnoDock/Layout/Root.cs'
content = 'src/UnoDock/Layout/Content.cs'
replace(root, 'public class LayoutRoot :', 'public partial class LayoutRoot :')
replace(content, 'public abstract class LayoutContent :', 'public abstract partial class LayoutContent :')
remove_method(content, '    internal void SetActive(bool value)')
remove_method(root, '    private void RepairActivation()')
text = pending[root]
start = text.index('    public LayoutContent? ActiveContent\n')
end = text.index('    public IEnumerable<ILayoutElement> Children', start)
pending[root] = text[:start] + '''    public LayoutContent? ActiveContent
    {
        get => _active;
        set => RequestActiveContent(value);
    }
''' + text[end:]
for name in ['Added', 'Removed']:
    event = 'Element' + name
    remove_method(root, '    internal void ' + name + '(LayoutElement element)', f'''    internal void {name}(LayoutElement element)
    {{
        // Snapshot before invoking application callbacks; they may edit descendants.
        var elements = new[] {{ element }}.Concat(element.Descendents().OfType<LayoutElement>()).ToArray();
        using var notifications = new LayoutMutationScope();
        foreach (var child in elements) notifications.Run(() => {event}?.Invoke(this, new(child)));
        notifications.Run(Invalidate);
    }}''')
for side in ['Top', 'Right', 'Bottom', 'Left']:
    replace(root, f'; value.SetSide(AnchorSide.{side});', ';')
app = 'samples/UnoDock.Gallery/App.xaml.cs'
replace(app, 'var suites = new (string Name, bool Windows, Func<Task<int>> Run)[]\n                    {', '''var suites = new (string Name, bool Windows, Func<Task<int>> Run)[]
                    {
                        ("layout-mutation-invariants", true, () => Testing.LayoutMutationInvariantTests.Run(output)),''')
for path, text in pending.items():
    Path(path).write_text(text, encoding='utf-8')
print('Applied model activation, ownership-event, and test-registry integration.')
