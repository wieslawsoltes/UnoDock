using Microsoft.UI.Xaml.Input;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public class NavigatorWindow : ContentControl
{
    public static readonly DependencyProperty DocumentsProperty = DependencyProperty.Register(nameof(Documents), typeof(LayoutDocumentItem[]), typeof(NavigatorWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty AnchorablesProperty = DependencyProperty.Register(nameof(Anchorables), typeof(IEnumerable<LayoutAnchorableItem>), typeof(NavigatorWindow), new PropertyMetadata(null));
    public static readonly DependencyProperty SelectedDocumentProperty = DependencyProperty.Register(nameof(SelectedDocument), typeof(LayoutDocumentItem), typeof(NavigatorWindow), new PropertyMetadata(null, (d, e) => ((NavigatorWindow)d).OnSelectedDocumentChanged(e)));
    public static readonly DependencyProperty SelectedAnchorableProperty = DependencyProperty.Register(nameof(SelectedAnchorable), typeof(LayoutAnchorableItem), typeof(NavigatorWindow), new PropertyMetadata(null, (d, e) => ((NavigatorWindow)d).OnSelectedAnchorableChanged(e)));
    public static readonly DependencyProperty LayoutDocumentsLabelProperty = DependencyProperty.Register(nameof(LayoutDocumentsLabel), typeof(string), typeof(NavigatorWindow), new PropertyMetadata("Documents"));
    public static readonly DependencyProperty LayoutAnchorablesLabelProperty = DependencyProperty.Register(nameof(LayoutAnchorablesLabel), typeof(string), typeof(NavigatorWindow), new PropertyMetadata("Tools"));
    private readonly DockingManager _manager;
    private readonly ListView _list = new() { DisplayMemberPath = nameof(LayoutItem.Title), SelectionMode = ListViewSelectionMode.Single, IsItemClickEnabled = true, MaxHeight = 380 };
    private LayoutItem[] _ordered = [];
    private bool _selecting;
    public NavigatorWindow(DockingManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        Width = 460; MaxWidth = 700; Padding = new(18); BorderThickness = new(1); IsTabStop = true;
        var grid = new Grid(); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 14) };
        var documents = new TextBlock { FontSize = 20 }; var tools = new TextBlock { FontSize = 20 };
        documents.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = this, Path = new PropertyPath(nameof(LayoutDocumentsLabel)) });
        tools.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = this, Path = new PropertyPath(nameof(LayoutAnchorablesLabel)) });
        heading.Children.Add(documents); heading.Children.Add(new TextBlock { Text = " / ", FontSize = 20 }); heading.Children.Add(tools); grid.Children.Add(heading);
        Grid.SetRow(_list, 1); grid.Children.Add(_list);
        var hint = new TextBlock { Text = "Ctrl+Tab  ·  Shift reverses  ·  Release Ctrl to activate  ·  Esc cancels", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), Opacity = .7 }; Grid.SetRow(hint, 2); grid.Children.Add(hint); Content = grid;
        _list.SelectionChanged += (_, _) => Select(_list.SelectedItem as LayoutItem);
        _list.ItemClick += (_, e) => { Select(e.ClickedItem as LayoutItem); _manager.Surface?.CloseNavigator(true); };
    }
    public LayoutDocumentItem[] Documents => (LayoutDocumentItem[]?)GetValue(DocumentsProperty) ?? [];
    public IEnumerable<LayoutAnchorableItem> Anchorables => (IEnumerable<LayoutAnchorableItem>?)GetValue(AnchorablesProperty) ?? [];
    public LayoutDocumentItem? SelectedDocument { get => (LayoutDocumentItem?)GetValue(SelectedDocumentProperty); set => SetValue(SelectedDocumentProperty, value); }
    public LayoutAnchorableItem? SelectedAnchorable { get => (LayoutAnchorableItem?)GetValue(SelectedAnchorableProperty); set => SetValue(SelectedAnchorableProperty, value); }
    public string LayoutDocumentsLabel { get => (string)GetValue(LayoutDocumentsLabelProperty); set => SetValue(LayoutDocumentsLabelProperty, value); }
    public string LayoutAnchorablesLabel { get => (string)GetValue(LayoutAnchorablesLabelProperty); set => SetValue(LayoutAnchorablesLabelProperty, value); }
    protected void SetDocuments(LayoutDocumentItem[] value) => SetValue(DocumentsProperty, value);
    protected void SetAnchorables(IEnumerable<LayoutAnchorableItem> value) => SetValue(AnchorablesProperty, value);
    protected virtual void OnSelectedDocumentChanged(DependencyPropertyChangedEventArgs e) { if (!_selecting && e.NewValue is LayoutItem item) Select(item); }
    protected virtual void OnSelectedAnchorableChanged(DependencyPropertyChangedEventArgs e) { if (!_selecting && e.NewValue is LayoutItem item) Select(item); }
    internal void Initialize()
    {
        _ordered = _manager.Layout.Descendents().OfType<LayoutContent>().Where(c => c.IsEnabled && c is not LayoutAnchorable { IsHidden: true })
            .OrderByDescending(c => c.LastActivationTimeStamp).ThenBy(c => c.ContentId, StringComparer.Ordinal).Select(_manager.GetLayoutItemFromModel).ToArray();
        SetDocuments(_ordered.OfType<LayoutDocumentItem>().ToArray()); SetAnchorables(_ordered.OfType<LayoutAnchorableItem>().ToArray()); _list.ItemsSource = _ordered;
        Background = DockVisuals.Brush(_manager, "UnoDock.PaneBrush", "LayerFillColorDefaultBrush"); BorderBrush = DockVisuals.Brush(_manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");
        if (_ordered.Length > 0) Select(_ordered[Math.Min(1, _ordered.Length - 1)]);
    }
    private void Select(LayoutItem? item)
    {
        if (_selecting) return; _selecting = true;
        try { SelectedDocument = item as LayoutDocumentItem; SelectedAnchorable = item as LayoutAnchorableItem; _list.SelectedItem = item; if (item != null) _list.ScrollIntoView(item); }
        finally { _selecting = false; }
    }
    internal void Advance(int delta)
    {
        if (_ordered.Length == 0) return; var current = _list.SelectedItem as LayoutItem; var index = Array.IndexOf(_ordered, current);
        Select(_ordered[(index + delta + _ordered.Length) % _ordered.Length]);
    }
    internal void CommitSelection() => (_list.SelectedItem as LayoutItem)?.ActivateCommand?.Execute(null);
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Tab: Advance(InputState.ShiftDown ? -1 : 1); break;
            case Windows.System.VirtualKey.Down: Advance(1); break;
            case Windows.System.VirtualKey.Up: Advance(-1); break;
            case Windows.System.VirtualKey.Enter: _manager.Surface?.CloseNavigator(true); break;
            case Windows.System.VirtualKey.Escape: _manager.Surface?.CloseNavigator(false); break;
            default: return;
        }
        e.Handled = true;
    }
    protected override void OnKeyUp(KeyRoutedEventArgs e)
    { base.OnKeyUp(e); if (e.Key == Windows.System.VirtualKey.Control) { _manager.Surface?.CloseNavigator(true); e.Handled = true; } }
}
