#!/usr/bin/env python3
"""One-use checked integration against the reviewed pre-refactor source layout."""
from pathlib import Path

pending = {}

def replace(path, old, new):
    text = pending.get(path, Path(path).read_text())
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected one exact anchor, found {text.count(old)}')
    pending[path] = text.replace(old, new)

def between(path, start, end, replacement):
    text = pending.get(path, Path(path).read_text())
    if text.count(start) != 1 or text.count(end) != 1:
        raise RuntimeError(f'{path}: non-unique source boundary')
    a = text.index(start)
    b = text.index(end, a)
    pending[path] = text[:a] + replacement + text[b:]

between('src/UnoDock/Layout/Tree.cs', '    internal void AssignParent(', '    protected virtual void OnParentChanging(', '')
between('src/UnoDock/Layout/Tree.cs', 'internal static class LayoutTree', 'public abstract class LayoutGroupBase', '')
replace('src/UnoDock/Layout/Tree.cs', '''        ComputeVisibility(); OnChildrenCollectionChanged(); Notify(nameof(ChildrenCount));
        NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged);''', '''        LayoutMutation.Execute(mutation =>
        {
            mutation.Run(ComputeVisibility);
            mutation.Run(OnChildrenCollectionChanged);
            mutation.Run(() => Notify(nameof(ChildrenCount)));
            mutation.Run(() => NotifyChildrenTreeChanged(ChildrenTreeChange.DirectChildrenChanged));
        });''')
replace('src/UnoDock/Layout/Root.cs', 'public class LayoutRoot :', 'public partial class LayoutRoot :')
between('src/UnoDock/Layout/Root.cs', '    public LayoutContent? ActiveContent\n', '    public IEnumerable<ILayoutElement> Children\n', '''    public LayoutContent? ActiveContent
    {
        get => _active;
        set => ChangeActiveContent(value);
    }
''')
between('src/UnoDock/Layout/Root.cs', '    internal void Added(', '    internal AnchorSide SideOf(', '''    internal void Added(LayoutElement element) => PublishElementChange(element, true);
    internal void Removed(LayoutElement element) => PublishElementChange(element, false);
    private void RepairActivation() => RepairActiveContent();
''')
replace('src/UnoDock/Layout/Content.cs', 'public abstract class LayoutContent :', 'public abstract partial class LayoutContent :')
between('src/UnoDock/Layout/Content.cs', '    internal void SetActive(', '    public bool IsSelected\n', '')
p = 'samples/UnoDock.Gallery/App.xaml.cs'
replace(p, '                    var suites = new (string Name, bool Windows, Func<Task<int>> Run)[]\n                    {', '''                    var suites = new (string Name, bool Windows, Func<Task<int>> Run)[]
                    {
                        ("layout-mutation-invariants", true, () => Testing.LayoutMutationInvariantTests.Run(output)),''')
for path, text in pending.items():
    Path(path).write_text(text)
    print(path)
