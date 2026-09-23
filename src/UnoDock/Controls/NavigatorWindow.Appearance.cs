using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using Xceed.Wpf.AvalonDock.Internal;
using Xceed.Wpf.AvalonDock.Layout;

namespace Xceed.Wpf.AvalonDock.Controls;

public partial class NavigatorWindow
{
    private readonly Grid _chrome = new();
    private readonly NavigatorDetailsPanel _details = new();
    private readonly TextBlock _documentHeading = new() { FontWeight = Microsoft.UI.Text.FontWeights.Bold };
    private readonly TextBlock _toolHeading = new() { FontWeight = Microsoft.UI.Text.FontWeights.Bold };
    private readonly TextBlock _selectionTitle = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _selectionDescription = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private static ListBox CreateList(string name) => new NavigatorListBox { Name = name };
    private void BuildDefaultChrome()
    {
        // Independently arranged from public stock-theme observations. Content
        // measurement, not a fixed 620-DIP dialog, determines the ordinary width.
        Template = DockChrome.ButtonTemplate;
        IsTabStop = true; Padding = new(5); BorderThickness = new(3); MinWidth = 0; MaxWidth = 760;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch;
        _chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _chrome.RowDefinitions.Add(new() { Height = new(54) });
        _chrome.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _chrome.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _chrome.RowDefinitions.Add(new() { Height = new(42) });
        var details = _details;
        details.Children.Add(_selectionTitle); details.Children.Add(_selectionDescription);
        Grid.SetColumnSpan(details, 2); _chrome.Children.Add(details);
        BindLabel(_toolHeading, nameof(LayoutAnchorablesLabel)); BindLabel(_documentHeading, nameof(LayoutDocumentsLabel));
        _toolHeading.Margin = new(5, 8, 5, 4); _documentHeading.Margin = new(5, 8, 5, 4);
        _toolHeading.TextTrimming = _documentHeading.TextTrimming = TextTrimming.CharacterEllipsis;
        _toolHeading.MaxWidth = _documentHeading.MaxWidth = 340;
        Grid.SetRow(_toolHeading, 1); Grid.SetRow(_documentHeading, 1); Grid.SetColumn(_documentHeading, 1);
        _chrome.Children.Add(_toolHeading); _chrome.Children.Add(_documentHeading);
        Grid.SetRow(_defaultDocuments, 2); Grid.SetColumn(_defaultDocuments, 1); Grid.SetRow(_defaultAnchorables, 2);
        _chrome.Children.Add(_defaultDocuments); _chrome.Children.Add(_defaultAnchorables); Content = _chrome;
        SizeChanged += (_, _) => QueueReveal();
        DockVisuals.SetName(this, "Switch active files and tool windows");
    }
    private void BindLabel(TextBlock target, string property) => target.SetBinding(TextBlock.TextProperty,
        new Binding { Source = this, Path = new PropertyPath(property) });
    internal void UpdateAppearance()
    {
        var palette = DockChrome.Palette(_manager);
        var dark = _manager.ActualTheme == ElementTheme.Dark && _manager.Theme is not Themes.GenericTheme;
        // An explicit dictionary palette can disagree with RequestedTheme/OS mode.
        // Never combine its foreground with unrelated stock-light backgrounds.
        var usePalette = dark || _manager.Theme is Themes.DictionaryTheme;
        Background = Brush("NavigatorBrush", usePalette ? palette.Surface : LightSurface);
        BorderBrush = Brush("NavigatorBorderBrush", usePalette ? palette.Border : LightBorder);
        Foreground = palette.Foreground; FontSize = palette.FontSize;
        _chrome.Background = Background;
        _details.LineHeight = NavigatorListItem.RowHeight(palette.FontSize);
        _chrome.RowDefinitions[0].Height = new(_details.LineHeight * 2 + 6);
        foreach (var text in new[] { _toolHeading, _documentHeading, _selectionTitle, _selectionDescription })
        { text.Foreground = palette.Foreground; text.FontSize = palette.FontSize; }
        var rowPalette = palette with
        {
            Tab = Brush("NavigatorSelectionBrush", usePalette ? palette.Tab : LightSelection),
            Border = Brush("NavigatorSelectionBorderBrush", usePalette ? palette.Border : LightSelectionBorder)
        };
        if (_defaultDocuments is NavigatorListBox documents) documents.Configure(rowPalette, Background);
        if (_defaultAnchorables is NavigatorListBox tools) tools.Configure(rowPalette, Background);
        UpdateSelectedDetails();
        Brush Brush(string key, Brush fallback) => _manager.Resources.TryGetValue("UnoDock." + key, out var b) && b is Brush value ? value : fallback;
    }
    [ThreadStatic] private static Brush? _lightSurface, _lightBorder, _lightSelection, _lightSelectionBorder;
    private static Brush LightSelection => _lightSelection ??= DockChrome.Color(0xeaeaea);
    private static Brush LightSelectionBorder => _lightSelectionBorder ??= DockChrome.Color(0xdadada);
    private static Brush LightSurface => _lightSurface ??= DockChrome.Color(0xf0f0f0);
    private static Brush LightBorder => _lightBorder ??= DockChrome.Color(0xa0a0a0);
    private void UpdateSelectedDetails()
    {
        _selectionTitle.Text = _selected?.Title ?? "";
        _selectionDescription.Text = _selected?.LayoutElement is LayoutDocument document ? document.Description ?? "" : "";
        AutomationProperties.SetName(_defaultDocuments, LayoutDocumentsLabel);
        AutomationProperties.SetName(_defaultAnchorables, LayoutAnchorablesLabel);
    }
}

/// <summary>Ordinary ListBox selection/automation with compact, independently authored chrome.</summary>
public sealed partial class NavigatorListBox : ListBox
{
    [ThreadStatic] private static ControlTemplate? _template;
    [ThreadStatic] private static ItemsPanelTemplate? _panel;
    [ThreadStatic] private static DataTemplate? _itemTemplate;
    private DockPalette _palette = DockChrome.Default(false);
    private Brush _surface = DockChrome.Transparent;
    public NavigatorListBox()
    {
        MinHeight = 0; MaxHeight = 400; MinWidth = 0; MaxWidth = 340;
        Margin = new(5, 0, 5, 5);
        Padding = new(0); BorderThickness = new(0); HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalAlignment = VerticalAlignment.Top;
        // No ListBox.SelectionMode or ScrollIntoView calls: they are not implemented
        // by the pinned Uno ListBox. Its existing single-selection model is retained.
        Template = _template ??= (ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <ScrollViewer x:Name="NavigatorScrollViewer" Padding="1" Background="{TemplateBinding Background}"
                  HorizontalScrollBarVisibility="Disabled" HorizontalScrollMode="Disabled"
                  VerticalScrollBarVisibility="Auto" VerticalScrollMode="Enabled" IsTabStop="False">
                <ItemsPresenter />
              </ScrollViewer>
            </ControlTemplate>
            """);
        ItemsPanel = _panel ??= (ItemsPanelTemplate)XamlReader.Load("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel /></ItemsPanelTemplate>");
        ItemTemplate = _itemTemplate ??= (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
            </DataTemplate>
            """);
    }
    // LayoutItem inherits FrameworkElement, but it is a model adapter, not an
    // item container. Uno's generic ItemsControl otherwise inserts that zero-sized
    // object directly and never invokes ItemTemplate or creates a selectable row.
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListBoxItem;
    protected override DependencyObject GetContainerForItemOverride() => new NavigatorListItem();
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is NavigatorListItem row) row.Configure(_palette, _surface);
    }
    internal void Configure(DockPalette palette, Brush surface)
    {
        var changed = _palette != palette || !ReferenceEquals(_surface, surface);
        _palette = palette; _surface = surface; Background = surface; Foreground = palette.Foreground; FontSize = palette.FontSize;
        if (!changed) return;
        foreach (var row in this.FindVisualChildren<NavigatorListItem>()) row.Configure(palette, surface);
    }
}

internal sealed partial class NavigatorListItem : ListBoxItem
{
    private DockPalette _palette = DockChrome.Default(false);
    private Brush _surface = DockChrome.Transparent;
    private bool _hovered;
    internal NavigatorListItem()
    {
        MinHeight = 0; MinWidth = 0; Height = 24; Padding = new(8, 3, 4, 3); Margin = new(0);
        BorderThickness = new(1); HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Center; Template = DockChrome.ButtonTemplate;
        RegisterPropertyChangedCallback(IsSelectedProperty, (_, _) => Paint());
        GotFocus += (_, _) => Paint(); LostFocus += (_, _) => Paint();
        PointerEntered += (_, _) => { _hovered = true; Paint(); };
        PointerExited += (_, _) => { _hovered = false; Paint(); };
    }
    internal void Configure(DockPalette palette, Brush surface)
    { _palette = palette; _surface = surface; FontSize = palette.FontSize; Foreground = palette.Foreground; Height = RowHeight(palette.FontSize); Paint(); }
    internal static double RowHeight(double fontSize) => Math.Max(24, Math.Ceiling(fontSize * 1.4 + 6));
    private void Paint()
    {
        Background = IsSelected ? _palette.Tab : _hovered ? _palette.Hover : _surface;
        BorderBrush = IsSelected ? _palette.Border : DockChrome.Transparent;
    }
}

// Details occupy the observed 54-DIP band but never widen the content-sized list
// columns. Two independently arranged lines avoid the reference's overlapping text.
internal sealed partial class NavigatorDetailsPanel : Panel
{
    private double _lineHeight = 24;
    internal double LineHeight
    {
        get => _lineHeight;
        set { if (_lineHeight == value) return; _lineHeight = value; InvalidateMeasure(); }
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(new Size(availableSize.Width, LineHeight));
        return new Size(0, LineHeight * 2 + 6);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++)
            Children[i].Arrange(new Rect(4, 3 + i * LineHeight, Math.Max(0, finalSize.Width - 8), LineHeight));
        return finalSize;
    }
}
