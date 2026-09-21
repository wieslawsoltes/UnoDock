"""Apply reviewed independent interoperability corrections to the recovered source."""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
CHANGES = json.loads(r'''
[
["src/UnoDock/Layout/Content.cs","new PropertyMetadata(\"\", (d, e)","new PropertyMetadata(null, (d, e)"],
["src/UnoDock/Layout/Content.cs","private double _left = 80, _top = 80, _width = 640, _height = 480;","private double _left, _top, _width, _height;"],
["src/UnoDock/Layout/Content.cs","private int _previousIndex;","private int _previousIndex = -1;"],
["src/UnoDock/Layout/Content.cs","public string Title { get => (string?)GetValue(TitleProperty) ?? \"\"; set => SetValue(TitleProperty, value ?? \"\"); }","public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }"],
["src/UnoDock/Layout/Content.cs","private double _autoWidth = 320, _autoHeight = 240, _autoMinWidth = 100, _autoMinHeight = 100;","private double _autoWidth, _autoHeight, _autoMinWidth = 100, _autoMinHeight = 100;"],
["src/UnoDock/Layout/Panes.cs","_floatingWidth = 640, _floatingHeight = 480;","_floatingWidth, _floatingHeight;"],
["src/UnoDock/Layout/Panes.cs","_reposition = true, _duplicates;","_reposition = true, _duplicates = true;"],
["src/UnoDock/Layout/Panes.cs","Children.Count > 0 && Children.All","Children.All"],
["src/UnoDock/Layout/Tree.cs","private bool _visible;","private bool _visible = true;"],
["tools/generate-property-adapters.py","'AllowMixedOrientation': 'true', 'AllowMovingFloatingWindowWithKeyboard': 'true'","'AllowMixedOrientation': 'false', 'AllowMovingFloatingWindowWithKeyboard': 'false'"],
["tools/generate-property-adapters.py","'AutoHideWindowClosingTimer': '400'","'AutoHideWindowClosingTimer': '1500'"],
["src/UnoDock/Controls/LayoutFloatingWindowControl.cs","Math.Max(160, p.FloatingWidth), Math.Max(100, p.FloatingHeight)","p.FloatingWidth > 0 ? Math.Max(160, p.FloatingWidth) : 640, p.FloatingHeight > 0 ? Math.Max(100, p.FloatingHeight) : 480"],
["src/UnoDock/Layout/Root.cs","public LayoutContent? LastFocusedDocument { get; private set; }","public LayoutContent? LastFocusedDocument { get; internal set; }"],
["src/UnoDock/Layout/Root.cs","{ LastFocusedDocument.IsLastFocusedDocument = false; LastFocusedDocument = null; }","{ LastFocusedDocument.IsLastFocusedDocument = false; LastFocusedDocument = null; Notify(nameof(LastFocusedDocument)); }"],
["src/UnoDock/Layout/LayoutXml.cs","Put(\"IsMaximized\", content.IsMaximized); Put(\"LastActivationTimeStamp\", content.LastActivationTimeStamp);","Put(\"IsMaximized\", content.IsMaximized); Put(\"IsLastFocusedDocument\", content.IsLastFocusedDocument); Put(\"LastActivationTimeStamp\", content.LastActivationTimeStamp?.ToString(\"MM/dd/yyyy HH:mm:ss\", Invariant));"],
["src/UnoDock/Layout/LayoutXml.cs","Capture(floating.RootDocument, \"RootDocument\")","Capture(floating.RootDocument)"],
["src/UnoDock/Layout/LayoutXml.cs","Capture(floating.RootPanel, \"RootPanel\")","Capture(floating.RootPanel)"],
["src/UnoDock/Layout/LayoutXml.cs","content.Title = S(\"Title\") ?? \"\";","content.Title = S(\"Title\");"],
["src/UnoDock/Layout/LayoutXml.cs","if (S(\"LastActivationTimeStamp\") is { } dt) content.LastActivationTimeStamp = XmlConvert.ToDateTime(dt, XmlDateTimeSerializationMode.RoundtripKind);","if (S(\"LastActivationTimeStamp\") is { } dt) content.LastActivationTimeStamp = ParseTimestamp(dt);\n            B(\"IsLastFocusedDocument\", v => content.IsLastFocusedDocument = v);"],
["src/UnoDock/Layout/LayoutXml.cs","foreach (var child in node.Children) group.InsertChildAt(group.ChildrenCount, Create(child));","var selectedIndex = -1;\n            foreach (var child in node.Children)\n            {\n                var item = Create(child);\n                // Restore explicit selection only after collection insertion has\n                // completed its first-child selection initialization.\n                if (item is LayoutContent { IsSelected: true }) selectedIndex = group.ChildrenCount;\n                group.InsertChildAt(group.ChildrenCount, item);\n            }\n            if (group is ILayoutContentSelector selection && selectedIndex >= 0)\n                selection.SelectedContentIndex = selectedIndex;"],
["src/UnoDock/Layout/LayoutXml.cs","var active = root.Descendents()","var lastFocused = root.Descendents().OfType<LayoutContent>().FirstOrDefault(c => c.IsLastFocusedDocument);\n        root.LastFocusedDocument = lastFocused;\n        foreach (var c in root.Descendents().OfType<LayoutContent>())\n            if (!ReferenceEquals(c, lastFocused)) c.IsLastFocusedDocument = false;\n        var active = root.Descendents()"],
["src/UnoDock/Layout/LayoutXml.cs","    private static string Length(GridLength length)","    private static DateTime ParseTimestamp(string text)\n    {\n        // Accept the observed original invariant format and earlier UnoDock ISO layouts.\n        if (DateTime.TryParseExact(text, \"MM/dd/yyyy HH:mm:ss\", Invariant, DateTimeStyles.None, out var value)) return value;\n        try { return XmlConvert.ToDateTime(text, XmlDateTimeSerializationMode.RoundtripKind); }\n        catch (FormatException e) { throw new XmlException(\"Invalid LastActivationTimeStamp.\", e); }\n    }\n    private static string Length(GridLength length)"],
["src/UnoDock/Layout/DockOperations.cs","var existing = root.RootPanel.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.GetSide() == side);","var existing = strategy.HasFlag(AnchorableShowStrategy.Most) ? null : root.RootPanel.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(p => p.GetSide() == side);"],
["src/UnoDock/Layout/DockOperations.cs","existing = new() { DockWidth = new(280), DockHeight = new(220) };","existing = new();"],
["src/UnoDock/Layout/DockOperations.cs","if (content.Root is not LayoutRoot root) return;","if (!CanMove(content) || content.Root is not LayoutRoot root) return;"],
["src/UnoDock/Layout/DockOperations.cs","if (content.Root is not LayoutRoot root || content is LayoutAnchorable { CanDockAsTabbedDocument: false }) return;","if (!CanMove(content) || content.Root is not LayoutRoot root || content is LayoutAnchorable { CanDockAsTabbedDocument: false }) return;"],
["src/UnoDock/Layout/DockOperations.cs","if (insertionIndex >= 0 && target is ILayoutPane pane) pane.MoveChild(from, Math.Clamp(insertionIndex, 0, target.ChildrenCount - 1));","if (insertionIndex >= 0 && target is ILayoutPane pane)\n                {\n                    var boundary = Math.Clamp(insertionIndex, 0, target.ChildrenCount);\n                    var destination = boundary > from ? boundary - 1 : boundary;\n                    if (destination != from) pane.MoveChild(from, destination);\n                }"],
["src/UnoDock/Controls/LayoutPaneControls.cs","#if WINDOWS\nusing DockPointerDeviceType = Microsoft.UI.Input.PointerDeviceType;\n#else\nusing DockPointerDeviceType = Windows.Devices.Input.PointerDeviceType;\n#endif","using DockPointerDeviceType = Microsoft.UI.Input.PointerDeviceType;"],
["src/UnoDock/Layout/Root.cs","private bool _visible;","private bool _visible = true;"],
["src/UnoDock/Controls/LayoutItem.cs","LayoutElement.Title = Title ?? \"\";","LayoutElement.Title = Title;"],
["src/UnoDock/Layout/DockOperations.cs","    public static void Dock(LayoutContent content, ILayoutGroup target, DockPosition position, int insertionIndex = -1)","    /// <summary>Checks shared docking policy without mutating either tree.</summary>\n    public static bool CanDock(LayoutContent content, ILayoutGroup target, DockPosition position)\n    {\n        ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(target);\n        if (!CanMove(content) || target.Root is not LayoutRoot root || !ReferenceEquals(content.Root, root)) return false;\n        if (position == DockPosition.Inside) return CanContain(target, content);\n        if (position is not (DockPosition.Left or DockPosition.Right or DockPosition.Top or DockPosition.Bottom)) return false;\n        if (target is not ILayoutPanelElement || target.Parent is not ILayoutGroup) return false;\n        var orientation = position is DockPosition.Left or DockPosition.Right ? Orientation.Horizontal : Orientation.Vertical;\n        return content is not LayoutDocument || root.Manager?.AllowMixedOrientation != false ||\n            target.Parent is not LayoutDocumentPaneGroup group || group.ChildrenCount <= 1 || group.Orientation == orientation;\n    }\n    public static void Dock(LayoutContent content, ILayoutGroup target, DockPosition position, int insertionIndex = -1)"],
["src/UnoDock/Layout/DockOperations.cs","if (!CanMove(content) || target.Root is not LayoutRoot root || !ReferenceEquals(content.Root, root)) return;","if (!CanDock(content, target, position) || target.Root is not LayoutRoot root) return;"],
["src/UnoDock/Controls/LayoutItem.cs","() => Split(DockPosition.Bottom), CanSplit)","() => Split(DockPosition.Bottom), () => CanSplit(DockPosition.Bottom))"],
["src/UnoDock/Controls/LayoutItem.cs","() => Split(DockPosition.Right), CanSplit)","() => Split(DockPosition.Right), () => CanSplit(DockPosition.Right))"],
["src/UnoDock/Controls/LayoutItem.cs","private bool CanSplit() => LayoutElement.Parent is LayoutDocumentPane { ChildrenCount: > 1 } && DockOperations.CanMove(LayoutElement);","private bool CanSplit(DockPosition position) => LayoutElement.Parent is LayoutDocumentPane { ChildrenCount: > 1 } pane && DockOperations.CanDock(LayoutElement, pane, position);"],
["src/UnoDock/Controls/LayoutItem.cs","&& CanSplit())","&& CanSplit(position))"],
["src/UnoDock/Internal/DockSurface.cs","Math.Max(model.AutoHideWidth, model.AutoHideMinWidth)","Math.Max(model.AutoHideWidth > 0 ? model.AutoHideWidth : 300, model.AutoHideMinWidth)"],
["src/UnoDock/Internal/DockSurface.cs","Math.Max(model.AutoHideHeight, model.AutoHideMinHeight)","Math.Max(model.AutoHideHeight > 0 ? model.AutoHideHeight : 240, model.AutoHideMinHeight)"],
["src/UnoDock/Internal/DockSurface.cs","_preview.Visibility = target == null ? Visibility.Collapsed : Visibility.Visible;\n        if (target != null)","var allowed = target is { } drop && (drop.Id == \"root\" ? _drag.Position != DockPosition.Inside :\n            _dropGroups.TryGetValue(drop.Id, out var group) && DockOperations.CanDock(_dragContent, group, _drag.Position));\n        _preview.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;\n        if (allowed && target != null)"],
["samples/UnoDock.Gallery/UnoDock.Gallery.csproj","    <Compile Include=\"../../tests/UnoDock.Runtime.Tests/RuntimeTests.cs\" Link=\"Testing/RuntimeTests.cs\" />","    <Compile Include=\"../../tests/UnoDock.Runtime.Tests/RuntimeTests.cs\" Link=\"Testing/RuntimeTests.cs\" />\n    <Compile Include=\"../../tests/UnoDock.Runtime.Tests/InteropTests.cs\" Link=\"Testing/InteropTests.cs\" />\n    <EmbeddedResource Include=\"../../contracts/reference-fixtures/*.xml\" LogicalName=\"ReferenceFixtures.%(Filename)%(Extension)\" />"],
["samples/UnoDock.Gallery/App.xaml.cs","exitCode = await Testing.RuntimeTests.Run(gallery.Dock,\n                        Environment.GetEnvironmentVariable(\"UNODOCK_TEST_RESULTS\") ?? \"artifacts/test-results\");","var output = Environment.GetEnvironmentVariable(\"UNODOCK_TEST_RESULTS\") ?? \"artifacts/test-results\";\n                    exitCode = await Testing.RuntimeTests.Run(gallery.Dock, output);\n                    exitCode |= await Testing.InteropTests.Run(output);"],
["src/UnoDock/Layout/Panes.cs","private Orientation _orientation;","private Orientation _orientation = Orientation.Horizontal;"]
]
''')
pending = {}
for name, old, new in CHANGES:
    path = ROOT / name
    value = pending.get(path, path.read_text(encoding="utf-8"))
    if old in value:
        pending[path] = value.replace(old, new)
    elif new not in value:
        raise SystemExit("Unexpected base source in " + name)
for path, value in pending.items():
    path.write_text(value, encoding="utf-8")
print(f"Updated {len(pending)} source files using {len(CHANGES)} reviewed replacements.")
