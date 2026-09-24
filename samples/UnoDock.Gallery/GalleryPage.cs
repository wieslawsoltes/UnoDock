using AutomationProperties = Microsoft.UI.Xaml.Automation.AutomationProperties;
using UnoDock.Controls;
using UnoDock.Themes;
using Windows.Storage;

namespace UnoDock.Gallery;

public sealed partial class GalleryPage : Page
{
    public GalleryDockingManager Dock { get; } = new();
    private readonly ObservableCollection<string> _events = [];
    private readonly ObservableCollection<Note> _notes = [];
    private readonly TextBlock _status = new() { Margin = new Thickness(12, 5, 12, 5) };
    private readonly Dictionary<string, object> _content = [];
    private bool _protectDraft = true;
    private int _nextDocument = 4;
    public GalleryPage()
    {
        Dock.ActiveContentChanged += (_, _) => _status.Text = $"{Dock.Layout.LastFocusedDocument?.Title ?? Dock.Layout.ActiveContent?.Title ?? "Ready"}  |  Ctrl+Tab: switch  |  Ctrl+F4: close";
        Dock.DocumentClosing += (_, e) => { if (_protectDraft && e.Document.ContentId == "draft") { e.Cancel = true; Log("Draft close cancelled. Disable protection in Properties to close it."); } };
        Dock.DocumentClosed += (_, e) => Log("Closed " + e.Document.Title);
        Dock.PreviewDock += (_, e) => Log("Dock preview: " + ((DockEventArgs)e).Content.Title);
        Dock.Docked += (_, e) => Log("Docked: " + ((DockEventArgs)e).Content.Title);
        Dock.Floated += (_, e) => Log("Floating: " + ((DockEventArgs)e).Content.Title);
        Dock.RenderingFailed += (_, e) => Log("Rendering failed: " + e.Message);
        BuildSampleShell();
    }
    private void Run(Func<Task> operation) => _ = RunCore(operation);
    private async Task RunCore(Func<Task> operation) { try { await operation(); } catch (Exception e) { Log(e.Message); } }
    private void Log(string value) { _events.Insert(0, $"{DateTime.Now:HH:mm:ss}  {value}"); while (_events.Count > 250) _events.RemoveAt(_events.Count - 1); }
    private LayoutDocument Document(string id, string title, object content)
    { _content[id] = content; return new() { ContentId = id, Title = title, Content = content }; }
    private TextBox Editor(string text) => new() { AcceptsReturn = true, Text = text, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily(OperatingSystem.IsWindows() ? "Consolas" : "DejaVu Sans Mono"), FontSize = 12, Padding = new(5), BorderThickness = new(0), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private void ResetWorkbench()
    {
        using var batch = Dock.BeginLayoutUpdate(); Dock.DocumentsSource = null; Dock.AnchorablesSource = null; Dock.LayoutItemTemplate = null; Dock.LayoutItemContainerStyle = null;
        _content.Clear();
        var welcome = Document("welcome", "Welcome.md", Welcome());
        var code = Document("code", "Workspace.cs", Editor("using UnoDock;\nusing UnoDock.Layout;\n\n// UnoDock namespaces; framework types are Uno / WinUI.\nvar documents = new LayoutDocumentPane();\ndocuments.Children.Add(new LayoutDocument\n{\n    Title = \"Hello, Uno\",\n    ContentId = \"hello\",\n    Content = new TextBox { Text = \"Edit me\" }\n});\n\nvar manager = new DockingManager\n{\n    Layout = new LayoutRoot\n    {\n        RootPanel = new LayoutPanel(documents)\n    }\n};\n"));
        var draft = Document("draft", "Protected draft.txt", Editor("This tab demonstrates cancellable document closing.\n\nDisable protection in the Properties tool to allow closing.\nEdit this text, switch tabs, float it, and dock it again: the same editor instance is preserved."));
        var docs = new LayoutDocumentPane(welcome); docs.Children.Add(code); docs.Children.Add(draft);
        var explorer = new LayoutAnchorable { Title = "Explorer", ContentId = "explorer", Content = Explorer(), CanClose = false };
        var properties = new LayoutAnchorable { Title = "Properties", ContentId = "properties", Content = Properties(), CanClose = false };
        var output = new LayoutAnchorable { Title = "Output", ContentId = "output", Content = new ListView { ItemsSource = _events, FontFamily = new FontFamily("Consolas"), FontSize = 12 }, CanClose = true };
        var inspector = new LayoutAnchorable { Title = "Layout inspector", ContentId = "inspector", Content = Inspector(), CanClose = true };
        foreach (var tool in new[] { explorer, properties, output, inspector }) _content[tool.ContentId!] = tool.Content!;
        var left = new LayoutAnchorablePane(explorer) { DockWidth = new(200), DockMinWidth = 140 };
        var right = new LayoutAnchorablePane(properties) { DockWidth = new(230), DockMinWidth = 180 };
        var bottom = new LayoutAnchorablePane(output) { DockHeight = new(155), DockMinHeight = 80 }; bottom.Children.Add(inspector);
        var center = new LayoutPanel(docs) { Orientation = Orientation.Vertical }; center.Children.Add(bottom);
        var panel = new LayoutPanel(left) { Orientation = Orientation.Horizontal }; panel.Children.Add(center); panel.Children.Add(right);
        Dock.Layout = new LayoutRoot { RootPanel = panel }; code.IsActive = true; _status.Text = "Ready  ·  Drag tabs to dock  ·  All editors retain their state";
        Log("Workspace initialized. This preview has an explicit compatibility report in docs/compatibility.md.");
    }
    private UIElement Welcome()
    {
        var stack = new StackPanel { Padding = new(32), Spacing = 18, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        stack.Children.Add(new TextBlock { Text = "A workspace that moves with you.", FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = "Nested splits, document tabs, dockable tools, floating windows and auto-hide — built on one layout model for Uno Platform.", FontSize = 17, TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        foreach (var text in new[] { "01  Drag a tab to a pane edge to split, or into its center to join a tab group.", "02  Float a document, resize the window, and dock it back without losing the editor instance.", "03  Pin or auto-hide Explorer, Properties and Output. Hover the side buttons to reveal them.", "04  Save your layout, rearrange the workspace, then restore it by content ID.", "05  Open MVVM mode for observable collections, property bindings and data templates.", "06  Use Ctrl+Tab for the MRU navigator, Ctrl+F4 to close, and arrow keys on focused splitters.", "07  Use the Properties tool to test close cancellation, immutable content and capability flags." }) stack.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 14 });
        var note = new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational, Title = "Compatibility status", Message = "This is an independently implemented preview, not a WPF binary replacement. See the repository's API report and platform validation matrix." }; stack.Children.Add(note);
        return new ScrollViewer { Content = stack };
    }
    private UIElement Explorer()
    {
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, ItemsSource = new[] { "◈  UnoDock.Gallery", "    ▾  Workspace", "        Welcome.md", "        Workspace.cs", "        Protected draft.txt", "    ▾  Libraries", "        UnoDock", "        UnoDock.Core", "    ▾  Validation", "        API contracts", "        Core tests", "        Runtime tests", "    ▾ Documentation", "        Migration guide", "        Compatibility status" } };
        list.DoubleTapped += (_, _) => { if (list.SelectedItem is string text) { var doc = Dock.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => text.Trim().Equals(d.Title, StringComparison.Ordinal)); if (doc != null) doc.IsActive = true; } };
        return list;
    }
    private UIElement Properties()
    {
        var stack = new StackPanel { Padding = new(16), Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = "WORKSPACE BEHAVIOR", FontSize = 11, Opacity = .65 });
        Check("Protect draft from closing", true, v => _protectDraft = v);
        Check("Allow mixed orientation", Dock.AllowMixedOrientation, v => Dock.AllowMixedOrientation = v);
        Check("Use in-surface floating windows", OperatingSystem.IsBrowser(), v => { Dock.FloatingWindowMode = v ? FloatingWindowMode.InSurface : FloatingWindowMode.Auto; Log("Floating host mode applies to newly created windows."); });
        Check("Active content may float", true, v => { if (Dock.Layout.ActiveContent is { } c) c.CanFloat = v; });
        Check("Active content may close", true, v => { if (Dock.Layout.ActiveContent is { } c) c.CanClose = v; });
        var delay = new Slider { Minimum = 100, Maximum = 2000, Value = Dock.AutoHideWindowClosingTimer, Header = "Auto-hide close delay (ms)" }; delay.ValueChanged += (_, e) => Dock.AutoHideWindowClosingTimer = (int)e.NewValue; stack.Children.Add(delay);
        var thickness = new Slider { Minimum = 2, Maximum = 14, Value = 6, Header = "Splitter size" }; thickness.ValueChanged += (_, e) => { Dock.GridSplitterWidth = e.NewValue; Dock.GridSplitterHeight = e.NewValue; }; stack.Children.Add(thickness);
        var run = new Button { Content = "Run runtime checks", HorizontalAlignment = HorizontalAlignment.Stretch }; run.Click += async (_, _) => { run.IsEnabled = false; try { var result = await Testing.RuntimeTests.Run(Dock, Path.Combine(ApplicationData.Current.LocalFolder.Path, "test-results")); Log("Runtime tests finished with exit code " + result); } catch (Exception e) { Log(e.Message); } finally { run.IsEnabled = true; } }; stack.Children.Add(run);
        return new ScrollViewer { Content = stack };
        void Check(string text, bool initial, Action<bool> changed) { var check = new CheckBox { Content = text, IsChecked = initial }; check.Checked += (_, _) => changed(true); check.Unchecked += (_, _) => changed(false); stack.Children.Add(check); }
    }
    private UIElement Inspector()
    {
        var grid = new Grid(); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var list = new ListView { FontFamily = new FontFamily("Consolas"), FontSize = 12 }; var refresh = new Button { Content = "Inspect layout tree", Margin = new(8) };
        refresh.Click += (_, _) => list.ItemsSource = Dock.Layout.Descendents().Select(e => e.GetType().Name + (e is LayoutContent c ? "  " + c.ContentId + "  " + c.Title : "")).ToArray();
        grid.Children.Add(refresh); Grid.SetRow(list, 1); grid.Children.Add(list); return grid;
    }
    private void AddDocument()
    {
        if (_mvvmWorkspace?.IsCurrent == true) { _mvvmWorkspace.NewDocument(); return; }
        if (Dock.DocumentsSource != null) { _notes.Add(new("note-" + _nextDocument, "Note " + _nextDocument++, "An observable source insertion.")); return; }
        var number = _nextDocument++; var document = Document("document-" + number, $"Document {number}.txt", Editor("Start writing here…"));
        var pane = Dock.Layout.LastFocusedDocument?.Parent as LayoutDocumentPane ?? Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First(); pane.Children.Add(document); document.IsActive = true;
    }
    private void Split(DockPosition position) { if (Dock.Layout.ActiveContent is { Parent: ILayoutGroup group } content) DockOperations.Dock(content, group, position); }
    private void ShowXml()
    {
        if (_mvvmWorkspace?.IsCurrent == true)
        {
            var xml = _mvvmWorkspace.CaptureLayout();
            var document = _mvvmWorkspace.NewDocument(); document.Name = "Layout.xml"; document.Text = xml;
            return;
        }
        using var text = new StringWriter(); new XmlLayoutSerializer(Dock).Serialize(text);
        var doc = Document("xml-" + _nextDocument++, "Layout.xml", Editor(text.ToString()));
        Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First().Children.Add(doc); doc.IsActive = true;
    }
    private async Task Save() => await SaveWorkspaceLayoutAsync();
    private async Task Restore() => await RestoreWorkspaceLayoutAsync();
    private void BindingDemo()
    {
        StopMvvmWorkspace();
        _mvvmWorkspace = new MvvmWorkspace(Dock, Log);
        Log("MVVM workspace: observable documents and tools, retained buffers, guarded commands and storage.");
    }
    private void Stress()
    {
        if (_mvvmWorkspace?.IsCurrent == true) { _mvvmWorkspace.AddDocuments(1000); return; }
        using var batch = Dock.BeginLayoutUpdate(); using var tree = Dock.Layout.BeginUpdate();
        var pane = Dock.Layout.Descendents().OfType<LayoutDocumentPane>().First();
        for (var i = 0; i < 1000; i++) pane.Children.Add(Document("stress-" + _nextDocument, "Tab " + _nextDocument++, "Lazy document content " + i));
        Log("Added 1,000 model documents in one transaction.");
    }
}
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class Note(string contentId, string title, string text) : INotifyPropertyChanged, IDockContent
{
    private string _title = title, _text = text;
    public string ContentId { get; } = contentId;
    public string Title { get => _title; set { if (_title == value) return; _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
    public string Text { get => _text; set { if (_text == value) return; _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
