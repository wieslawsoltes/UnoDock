using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;

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
