"""One-use exact integration. All anchors are verified before source writes."""
from pathlib import Path
pending = {}
def replace(path, old, new):
    text = pending.get(path, Path(path).read_text())
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected unique anchor: {old[:100]!r}')
    pending[path] = text.replace(old, new)
replace('samples/UnoDock.Gallery/App.xaml.cs',
        '    protected override void OnLaunched(LaunchActivatedEventArgs args)\n    {',
        '    protected override void OnLaunched(LaunchActivatedEventArgs args)\n    {\n        if (TryLaunchBrowserWorkspace())\n            return;')
replace('samples/UnoDock.Gallery/UnoDock.Gallery.csproj',
        '    <ProjectReference Include="../../src/UnoDock/UnoDock.csproj" />',
        '    <ProjectReference Include="../../src/UnoDock/UnoDock.csproj" />\n    <ProjectReference Include="../../src/UnoDock.Browser/UnoDock.Browser.csproj" />')
replace('src/UnoDock.Browser/Assets/session.mjs', '        this.save = save;', '        this.save = () => {};')
replace('src/UnoDock.Browser/Assets/session.mjs', '    }\n    commit() {', '        this.save = save;\n    }\n    commit() {')
replace('src/UnoDock.Browser/BrowserDockingSession.cs',
        '                Float(entry.Item.Id);', '                Float(entry.Item.Id);')
replace('src/UnoDock.Browser/BrowserDockingSession.cs',
        '            Request(new() { Op = "float", Id = contentId, Lease = entry.Item.Lease });',
        '            using var response = Request(new() { Op = "float", Id = contentId, Lease = entry.Item.Lease });')
replace('src/UnoDock.Browser/BrowserDockingSession.cs',
        '_factory.Create(item, (title, payload) => Publish(item.Id, title, payload))',
        '_factory.Create(item, (title, payload) => Publish(item.Id, item.Lease, title, payload))')
replace('src/UnoDock.Browser/BrowserDockingSession.cs',
        '    private void Publish(string id, string title, string payload)\n    {\n        if (_applying || _disposed || !_entries.TryGetValue(id, out var entry))',
        '    private void Publish(string id, long lease, string title, string payload)\n    {\n        if (_applying || _disposed || !_entries.TryGetValue(id, out var entry) || entry.Item.Lease != lease)')
replace('src/UnoDock.Browser/Assets/host.mjs',
        "let active = '', applied = null, errorText = '', dragging = false, rootHub;",
        "let active = '', requestedActive = '', applied = null, errorText = '', dragging = false, rootHub;")
replace('src/UnoDock.Browser/Assets/host.mjs',
        "            const value = execute(request); return JSON.stringify({ ok: true, value });",
        "            const value = execute(request);\n            if (request.op === 'read' || request.op === 'ready') value.active = requestedActive;\n            return JSON.stringify({ ok: true, value });")
replace('src/UnoDock.Browser/Assets/host.mjs',
        '        if (applied.active) active = applied.active;',
        '        if (applied.active === requestedActive) requestedActive = \'\';\n        if (!requestedActive && applied.active) active = applied.active;')
replace('src/UnoDock.Browser/Assets/host.mjs',
        "            chip.onclick = () => { active = value.id; safe({ op: 'read' }); };",
        "            chip.onclick = () => { active = requestedActive = value.id; safe({ op: 'read' }); };")
for path, text in pending.items():
    Path(path).write_text(text)
    print(path)
