using UnoDock.Controls;
using System.Globalization;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace UnoDock.Gallery;

/// <summary>A bounded, explicitly described property editor for the sample. No private
/// reflection, arbitrary property execution, or dependency on the original PropertyGrid.</summary>
internal sealed class SamplePropertyInspector : UserControl, IDisposable
{
    private sealed record Field(string Category, string Name, Func<string> Read, Action<string>? Write);
    private sealed record Row(Field Field, FrameworkElement View, TextBox? Editor, TextBlock? ReadOnly, TextBlock Error);
    private readonly DockingManager _manager;
    private readonly TextBlock _heading = new() { FontSize = 12, Margin = new(5, 3, 5, 2), TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBox _search = new() { PlaceholderText = "Search", MinHeight = 0, Height = 25, FontSize = 12, Padding = new(4, 1, 4, 1), Margin = new(4, 2, 4, 4) };
    private readonly StackPanel _rows = new();
    private readonly List<Row> _entries = [];
    private readonly List<(DependencyObject Object, DependencyProperty Property, long Token)> _tokens = [];
    private readonly Dictionary<string, TextBlock> _groups = new(StringComparer.Ordinal);
    private LayoutRoot? _root;
    private LayoutDocument? _document;
    private bool _attached, _disposed, _refreshing, _alphabetical;
    internal string? SelectedContentId => _document?.ContentId;
    internal int VisibleFieldCount => _entries.Count(row => row.View.Visibility == Visibility.Visible);
    internal string? LastError { get; private set; }

    internal SamplePropertyInspector(DockingManager manager)
    {
        _manager = manager;
        MinWidth = 0; IsTabStop = false;
        var host = new Grid();
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star) })
            host.RowDefinitions.Add(new() { Height = height });
        host.Children.Add(_heading);
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(4, 0, 4, 0), Spacing = 3 };
        var grouped = SampleChrome.Button("Categories", () => SetOrder(false)); grouped.Padding = new(3, 1, 3, 1);
        var sorted = SampleChrome.Button("A–Z", () => SetOrder(true)); sorted.Padding = new(3, 1, 3, 1);
        modes.Children.Add(grouped); modes.Children.Add(sorted); Grid.SetRow(modes, 1); host.Children.Add(modes);
        Grid.SetRow(_search, 2); host.Children.Add(_search);
        var scroll = new ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 3); host.Children.Add(scroll); Content = host;
        AutomationProperties.SetAutomationId(_search, "PropertySearch");
        AutomationProperties.SetName(this, "Document properties");
        _search.TextChanged += (_, _) => ApplyFilter();
        Loaded += OnLoaded; Unloaded += OnUnloaded;
        ActualThemeChanged += OnThemeChanged;
    }
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed || _attached) return;
        _attached = true; _manager.LayoutChanged += OnLayoutChanged; _manager.ActiveContentChanged += OnActiveChanged;
        AttachRoot(); SelectDocument();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();
    private void OnLayoutChanged(object? sender, EventArgs args) { AttachRoot(); SelectDocument(); }
    private void OnActiveChanged(object? sender, EventArgs args) => SelectDocument();
    private void AttachRoot()
    {
        if (_root != null) _root.PropertyChanged -= OnRootChanged;
        _root = _manager.Layout; _root.PropertyChanged += OnRootChanged;
    }
    private void OnRootChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(LayoutRoot.LastFocusedDocument) or nameof(LayoutRoot.ActiveContent)) SelectDocument();
    }
    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(LayoutContent.Content) or nameof(LayoutElement.Parent)) SelectDocument(force: true);
        else RefreshValues();
    }
    private void SelectDocument(bool force = false)
    {
        if (!_attached || _disposed) return;
        var document = _manager.Layout.LastFocusedDocument as LayoutDocument;
        if (document?.Root != _manager.Layout)
            document = _manager.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => d.IsVisible);
        if (!force && ReferenceEquals(document, _document)) { RefreshValues(); return; }
        ClearSelection(); _document = document;
        if (document == null) { _heading.Text = "No document selected"; return; }
        document.PropertyChanged += OnDocumentChanged;
        var fields = new List<Field>
        {
            new("Layout", "Title", () => document.Title ?? "", value => document.Title = value),
            new("Layout", "ContentId", () => document.ContentId ?? "", null),
            Bool("CanClose", () => document.CanClose, value => document.CanClose = value),
            Bool("CanFloat", () => document.CanFloat, value => document.CanFloat = value),
            Bool("CanMove", () => document.CanMove, value => document.CanMove = value),
            Bool("IsEnabled", () => document.IsEnabled, value => document.IsEnabled = value),
            new("Layout", "IsFloating", () => document.IsFloating.ToString(), null),
        };
        if (document.Content is FrameworkElement element)
        {
            fields.Add(new("Size", "Width", () => Dimension(element.Width), value => element.Width = ParseDimension(value)));
            fields.Add(new("Size", "Height", () => Dimension(element.Height), value => element.Height = ParseDimension(value)));
            fields.Add(new("Appearance", "Opacity", () => Number(element.Opacity), value => element.Opacity = ParseNumber(value, 0, 1)));
            Observe(element, FrameworkElement.WidthProperty); Observe(element, FrameworkElement.HeightProperty); Observe(element, UIElement.OpacityProperty);
            if (element is Control control)
            {
                fields.Add(new("Appearance", "FontSize", () => Number(control.FontSize), value => control.FontSize = ParseNumber(value, 1, 96)));
                fields.Add(new("Appearance", "Background", () => Color(control.Background), value => control.Background = ParseColor(value)));
                fields.Add(new("Appearance", "Foreground", () => Color(control.Foreground), value => control.Foreground = ParseColor(value)));
                Observe(control, Control.FontSizeProperty); Observe(control, Control.BackgroundProperty); Observe(control, Control.ForegroundProperty);
            }
            if (element is TextBox editor)
            {
                fields.Add(new("Behavior", "IsReadOnly", () => editor.IsReadOnly.ToString(), value => editor.IsReadOnly = ParseBoolean(value)));
                Observe(editor, TextBox.IsReadOnlyProperty);
            }
        }
        foreach (var field in fields) AddRow(field);
        Reorder(); RefreshValues();
        static Field Bool(string name, Func<bool> read, Action<bool> write) => new("Behavior", name, () => read().ToString(), value => write(ParseBoolean(value)));
    }
    private void Observe(DependencyObject value, DependencyProperty property)
    {
        var token = value.RegisterPropertyChangedCallback(property, (_, _) => RefreshValues());
        _tokens.Add((value, property, token));
    }
    private void AddRow(Field field)
    {
        var grid = new Grid { MinHeight = 24, BorderThickness = new(0, 0, 0, 1), BorderBrush = SampleChrome.Color(0xd8d8d8) };
        grid.ColumnDefinitions.Add(new() { Width = new(96) }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = field.Name, FontSize = 12, Margin = new(4, 4, 3, 2), TextTrimming = TextTrimming.CharacterEllipsis });
        TextBox? input = null; TextBlock? readOnly = null;
        var error = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new(4, 0, 4, 3), Visibility = Visibility.Collapsed, Foreground = SampleChrome.Color(0xc52828) };
        if (field.Write != null)
        {
            input = new TextBox { MinHeight = 0, MinWidth = 0, FontSize = 12, Padding = new(3, 1, 3, 1), BorderThickness = new(0), CornerRadius = new(0) };
            Grid.SetColumn(input, 1); grid.Children.Add(input);
            AutomationProperties.SetName(input, field.Name);
            AutomationProperties.SetAutomationId(input, "Property-" + field.Name);
        }
        else
        {
            readOnly = new TextBlock { FontSize = 12, Margin = new(3, 4, 2, 2), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(readOnly, 1); grid.Children.Add(readOnly);
        }
        Grid.SetRow(error, 1); Grid.SetColumnSpan(error, 2); grid.Children.Add(error);
        var row = new Row(field, grid, input, readOnly, error); _entries.Add(row);
        if (input != null)
        {
            input.LostFocus += (_, _) => { if (!_refreshing && !_disposed) Commit(row, input.Text); };
            input.KeyDown += (_, args) =>
            {
                if (args.Key == VirtualKey.Enter) { Commit(row, input.Text); args.Handled = true; }
                else if (args.Key == VirtualKey.Escape) { input.Text = field.Read(); error.Visibility = Visibility.Collapsed; args.Handled = true; }
            };
        }
    }
    internal bool TryEdit(string name, string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var row = _entries.SingleOrDefault(row => row.Field.Name == name);
        return row != null && Commit(row, value);
    }
    private bool Commit(Row row, string value)
    {
        if (_refreshing || row.Field.Write == null || _document?.Root != _manager.Layout) return false;
        try
        {
            _refreshing = true; row.Field.Write(value); LastError = null;
            row.Error.Visibility = Visibility.Collapsed;
            if (row.Editor != null) row.Editor.Text = row.Field.Read();
            return true;
        }
        catch (Exception error)
        {
            LastError = error.Message; row.Error.Text = error.Message; row.Error.Visibility = Visibility.Visible;
            return false;
        }
        finally { _refreshing = false; }
    }
    private void RefreshValues()
    {
        if (_refreshing || _disposed) return;
        _refreshing = true;
        try
        {
            _heading.Text = _document?.Content is { } content ? $"{content.GetType().Name}  {_document.Title}" : "No document selected";
            foreach (var row in _entries)
            {
                if (row.Editor is { FocusState: FocusState.Unfocused } editor) editor.Text = row.Field.Read();
                if (row.ReadOnly != null) row.ReadOnly.Text = row.Field.Read();
            }
        }
        finally { _refreshing = false; }
    }
    internal void Filter(string text) { _search.Text = text; ApplyFilter(); }
    private void SetOrder(bool alphabetical) { _alphabetical = alphabetical; Reorder(); }
    private void Reorder()
    {
        _rows.Children.Clear(); _groups.Clear();
        var ordered = _alphabetical ? _entries.OrderBy(row => row.Field.Name, StringComparer.Ordinal) :
            _entries.OrderBy(row => row.Field.Category, StringComparer.Ordinal).ThenBy(row => row.Field.Name, StringComparer.Ordinal);
        foreach (var row in ordered)
        {
            if (!_alphabetical && !_groups.ContainsKey(row.Field.Category))
            {
                var header = new TextBlock { Text = row.Field.Category, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new(4, 6, 4, 3) };
                _groups.Add(row.Field.Category, header); _rows.Children.Add(header);
            }
            _rows.Children.Add(row.View);
        }
        ApplyFilter();
    }
    private void ApplyFilter()
    {
        var text = _search.Text.Trim();
        foreach (var row in _entries)
            row.View.Visibility = row.Field.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Field.Category.Contains(text, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var group in _groups)
            group.Value.Visibility = _entries.Any(row => row.Field.Category == group.Key && row.View.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnThemeChanged(FrameworkElement sender, object args)
    {
        foreach (var button in this.FindVisualChildren<SampleButton>()) button.Configure(SampleChrome.Default(ActualTheme == ElementTheme.Dark));
    }
    private void ClearSelection()
    {
        _refreshing = true;
        try
        {
            if (_document != null) _document.PropertyChanged -= OnDocumentChanged;
            _document = null;
            foreach (var (value, property, token) in _tokens) value.UnregisterPropertyChangedCallback(property, token);
            _tokens.Clear(); _entries.Clear(); _rows.Children.Clear(); _groups.Clear(); LastError = null;
        }
        finally { _refreshing = false; }
    }
    private void Detach()
    {
        if (!_attached) return;
        _attached = false;
        _manager.LayoutChanged -= OnLayoutChanged; _manager.ActiveContentChanged -= OnActiveChanged;
        if (_root != null) _root.PropertyChanged -= OnRootChanged;
        _root = null; ClearSelection();
    }
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Detach(); Loaded -= OnLoaded; Unloaded -= OnUnloaded; ActualThemeChanged -= OnThemeChanged;
    }
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Dimension(double value) => double.IsNaN(value) ? "Auto" : Number(value);
    private static double ParseDimension(string value) => value.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? double.NaN : ParseNumber(value, 0, 100000);
    private static double ParseNumber(string value, double min, double max)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result) || result < min || result > max)
            throw new ArgumentException($"Enter a number from {min} to {max}.");
        return result;
    }
    private static bool ParseBoolean(string value) => bool.TryParse(value, out var result) ? result : throw new ArgumentException("Enter True or False.");
    private static string Color(Brush? brush) => brush is SolidColorBrush value ? $"#{value.Color.A:X2}{value.Color.R:X2}{value.Color.G:X2}{value.Color.B:X2}" : "(brush)";
    private static SolidColorBrush ParseColor(string value)
    {
        var text = value.Trim().TrimStart('#');
        if ((text.Length != 6 && text.Length != 8) || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            throw new ArgumentException("Enter #RRGGBB or #AARRGGBB.");
        if (text.Length == 6) argb |= 0xff000000;
        return new(Microsoft.UI.ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }
}
