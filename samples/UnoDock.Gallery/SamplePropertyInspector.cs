using UnoDock.Controls;
using System.Globalization;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace UnoDock.Gallery;
/// <summary>Explicitly registered sample properties, not a reflective PropertyGrid clone.
/// Every editor belongs to a selection epoch; obsolete native events cannot write to
/// a former document. Draft text is never written merely because focus moved.</summary>
internal sealed partial class SamplePropertyInspector : UserControl, IDisposable
{
    private enum EditorKind
    {
        Text,
        Boolean,
        Choice,
        Color
    }

    private sealed record Field(string Category, string Name, string Description, Func<string> Read, Action<string>? Write = null, EditorKind Kind = EditorKind.Text, string[]? Choices = null);
    private sealed class Row(Field field, long epoch, Grid view, TextBlock label, TextBlock error)
    {
        internal readonly Field Field = field;
        internal readonly long Epoch = epoch;
        internal readonly Grid View = view;
        internal readonly TextBlock Label = label, Error = error;
        internal TextBox? Text;
        internal CheckBox? Boolean;
        internal ComboBox? Choice;
        internal TextBlock? ReadOnly;
        internal Border? Swatch;
        internal string Displayed = "";
        internal bool SuppressPresentationBlur;
    }

    private sealed record Category(Border View, ToggleButton Button, string Name);
    private readonly DockingManager _manager;
    private readonly TextBlock _heading;
    private readonly TextBox _search;
    private readonly ListView _rows;
    private readonly Grid _columns;
    private readonly TextBlock _descriptionTitle, _description;
    private readonly List<Row> _entries = [];
    private readonly List<(DependencyObject Object, DependencyProperty Property, long Token)> _tokens = [];
    private readonly Dictionary<string, Category> _groups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private LayoutRoot? _root;
    private LayoutContent? _document;
    private object? _content;
    private Row? _selected;
    private bool _attached, _disposed, _alphabetical, _refreshPending;
    private int _synchronizing, _writing;
    private long _epoch;
    private double _nameWidth = 96;
    internal string? SelectedContentId => _document?.ContentId;
    internal int VisibleFieldCount => _entries.Count(row => row.View.Visibility == Visibility.Visible);
    internal string? LastError
    {
        get;
        private set;
    }
    internal string SelectedPropertyName => _selected?.Field.Name ?? "";

    internal double NameColumnWidth
    {
        get => _nameWidth;
        set
        {
            if (!double.IsFinite(value) || value < 64 || value > 400)
                throw new ArgumentOutOfRangeException(nameof(value));
            _nameWidth = value;
            ApplyColumnWidth();
        }
    }

    internal SamplePropertyInspector(DockingManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        MinWidth = 0;
        IsTabStop = false;
        Resources.MergedDictionaries.Add(_nativeResources);
        var host = NativeTemplate<Grid>("Inspector.Shell");
        _heading = Part<TextBlock>(host, "InspectorHeading");
        _search = Part<TextBox>(host, "PropertySearch");
        _rows = Part<ListView>(host, "PropertyRows");
        _columns = Part<Grid>(host, "PropertyColumns");
        _descriptionTitle = Part<TextBlock>(host, "DescriptionTitle");
        _description = Part<TextBlock>(host, "PropertyDescription");
        _categoryMode = Part<ToggleButton>(host, "CategoryMode");
        _alphabeticalMode = Part<ToggleButton>(host, "AlphabeticalMode");
        _rows.ItemsSource = _nativeItems;
        _rows.SelectionChanged += NativeSelectionChanged;
        _categoryMode.Click += (_, _) => SetOrder(false);
        _alphabeticalMode.Click += (_, _) => SetOrder(true);
        Part<Button>(host, "ClearSearch").Click += (_, _) => Filter("");
        var divider = Part<Thumb>(host, "NameColumnDivider");
        divider.DragDelta += (_, args) => ResizeColumn(args.HorizontalChange * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1));
        divider.KeyDown += (_, args) =>
        {
            if (args.Key is VirtualKey.Left or VirtualKey.Right)
            {
                ResizeColumn((args.Key == VirtualKey.Right ? 8 : -8) * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1));
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Home)
            {
                NameColumnWidth = 96;
                args.Handled = true;
            }
        };
        divider.DoubleTapped += (_, _) => NameColumnWidth = 96;
        Content = host;
        AutomationProperties.SetName(this, "Document properties");
        _search.TextChanged += (_, _) => ApplyFilter();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnThemeChanged;
        SizeChanged += (_, _) => ApplyColumnWidth();
        UpdateOrderButtons();
        ShowDescription(null);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed || _attached)
            return;
        _attached = true;
        _manager.LayoutChanged += OnLayoutChanged;
        _manager.ActiveContentChanged += OnActiveChanged;
        AttachRoot();
        SelectDocument();
        Paint();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();
    private void OnLayoutChanged(object? sender, EventArgs args)
    {
        AttachRoot();
        SelectDocument();
    }

    private void OnActiveChanged(object? sender, EventArgs args) => SelectDocument();
    private void AttachRoot()
    {
        if (!_attached || ReferenceEquals(_root, _manager.Layout))
            return;
        if (_root != null)
            _root.PropertyChanged -= OnRootChanged;
        _root = _manager.Layout;
        _root.PropertyChanged += OnRootChanged;
    }

    private void OnRootChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (ReferenceEquals(sender, _root) && args.PropertyName is nameof(LayoutRoot.LastFocusedDocument) or nameof(LayoutRoot.ActiveContent))
            SelectDocument();
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!ReferenceEquals(sender, _document))
            return;
        if (args.PropertyName == nameof(LayoutContent.Content))
            SelectDocument();
        else if (args.PropertyName == nameof(LayoutElement.Parent))
        {
            // Reparenting temporarily removes the root. Wait for the synchronous
            // tree transaction to settle rather than selecting a different document.
            var epoch = _epoch;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (epoch == _epoch)
                    SelectDocument();
            });
        }
        else
            RefreshValues();
    }

    private void SelectDocument()
    {
        if (!_attached || _disposed)
            return;
        var document = CurrentDocument();
        if (ReferenceEquals(document, _document) && ReferenceEquals(document?.Content, _content))
        {
            RefreshValues();
            return;
        }

        ClearSelection();
        _document = document;
        _content = document?.Content;
        if (document == null)
        {
            _heading.Text = "No document selected";
            return;
        }

        document.PropertyChanged += OnDocumentChanged;
        foreach (var field in CreateFields(document))
            AddRow(field);
        Reorder();
        RefreshValues();
        Paint();
    }

    private IEnumerable<Field> CreateFields(LayoutContent document)
    {
        yield return new("Layout", "Title", "Caption displayed by this document's tab and floating window.", () => document.Title ?? "", value => document.Title = value);
        yield return new("Layout", "ContentId", "Stable application identity used when restoring layouts; read-only here.", () => document.ContentId ?? "");
        yield return Bool("CanClose", "Allow the document to be closed.", () => document.CanClose, value => document.CanClose = value);
        yield return Bool("CanFloat", "Allow the document to become a floating window.", () => document.CanFloat, value => document.CanFloat = value);
        if (document is LayoutDocument movable)
            yield return Bool("CanMove", "Allow the document to move between panes.", () => movable.CanMove, value => movable.CanMove = value);
        yield return Bool("IsEnabled", "Enable input and activation for this layout content.", () => document.IsEnabled, value => document.IsEnabled = value);
        yield return new("Layout", "IsFloating", "Whether the document is currently inside a floating host.", () => document.IsFloating.ToString());
        if (_content is not FrameworkElement element)
            yield break;
        yield return new("Size", "Width", "Width in DIPs. Auto leaves sizing to layout.", () => Dimension(element.Width), value => element.Width = ParseDimension(value));
        yield return new("Size", "Height", "Height in DIPs. Auto leaves sizing to layout.", () => Dimension(element.Height), value => element.Height = ParseDimension(value));
        yield return new("Appearance", "Opacity", "Opacity from 0 (transparent) to 1 (opaque).", () => Number(element.Opacity), value => element.Opacity = ParseNumber(value, 0, 1));
        yield return Choice("Layout", "HorizontalAlignment", "Alignment of the retained editor within its pane.", () => element.HorizontalAlignment, value => element.HorizontalAlignment = value);
        yield return Choice("Layout", "VerticalAlignment", "Vertical alignment of the retained editor within its pane.", () => element.VerticalAlignment, value => element.VerticalAlignment = value);
        yield return new("Layout", "Margin", "Outer spacing in DIPs: one number, horizontal/vertical, or left,top,right,bottom.", () => ThicknessText(element.Margin), value => element.Margin = ParseThickness(value, false));
        Observe(element, FrameworkElement.WidthProperty);
        Observe(element, FrameworkElement.HeightProperty);
        Observe(element, UIElement.OpacityProperty);
        Observe(element, FrameworkElement.HorizontalAlignmentProperty);
        Observe(element, FrameworkElement.VerticalAlignmentProperty);
        Observe(element, FrameworkElement.MarginProperty);
        if (element is Control control)
        {
            yield return new("Appearance", "FontSize", "Font size in DIPs, from 1 to 96.", () => Number(control.FontSize), value => control.FontSize = ParseNumber(value, 1, 96));
            yield return new("Appearance", "Background", "Solid color #RRGGBB or #AARRGGBB. Existing non-solid brushes are preserved unless explicitly edited.", () => Color(control.Background), value => control.Background = ParseColor(value), EditorKind.Color);
            yield return new("Appearance", "Foreground", "Text color #RRGGBB or #AARRGGBB.", () => Color(control.Foreground), value => control.Foreground = ParseColor(value), EditorKind.Color);
            yield return new("Layout", "Padding", "Inner spacing in DIPs; values must not be negative.", () => ThicknessText(control.Padding), value => control.Padding = ParseThickness(value, true));
            Observe(control, Control.FontSizeProperty);
            Observe(control, Control.BackgroundProperty);
            Observe(control, Control.ForegroundProperty);
            Observe(control, Control.PaddingProperty);
        }

        if (element is TextBox editor)
        {
            yield return Bool("IsReadOnly", "Prevent text editing while retaining selection and copy.", () => editor.IsReadOnly, value => editor.IsReadOnly = value);
            yield return Bool("AcceptsReturn", "Allow Enter to insert a new line in the editor.", () => editor.AcceptsReturn, value => editor.AcceptsReturn = value);
            yield return Choice("Behavior", "TextWrapping", "Control line wrapping in the document editor.", () => editor.TextWrapping, value => editor.TextWrapping = value);
            Observe(editor, TextBox.IsReadOnlyProperty);
            Observe(editor, TextBox.AcceptsReturnProperty);
            Observe(editor, TextBox.TextWrappingProperty);
        }
    }

    private static Field Bool(string name, string description, Func<bool> read, Action<bool> write) => new("Behavior", name, description, () => read().ToString(), value => write(ParseBoolean(value)), EditorKind.Boolean);
    private static Field Choice<T>(string category, string name, string description, Func<T> read, Action<T> write)
        where T : struct, Enum => new(category, name, description, () => read().ToString(), value =>
    {
        // A named choice is intentional; numeric enum casts must not bypass validation.
        if (!Enum.GetNames<T>().Contains(value, StringComparer.OrdinalIgnoreCase) || !Enum.TryParse<T>(value, true, out var result))
            throw new ArgumentException("Select a named value from the list.");
        write(result);
    }, EditorKind.Choice, Enum.GetNames<T>());
    private void Observe(DependencyObject value, DependencyProperty property)
    {
        var epoch = _epoch;
        var token = value.RegisterPropertyChangedCallback(property, (_, _) =>
        {
            if (epoch == _epoch)
                RefreshValues();
        });
        _tokens.Add((value, property, token));
    }

    private void AddRow(Field field) => AddNativeRow(field);
    private LayoutContent? CurrentDocument()
    {
        var root = _manager.Layout;
        var document = root.LastFocusedDocument;
        return ReferenceEquals(document?.Root, root) ? document : root.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => d.IsVisible);
    }

    private bool IsCurrent(Row row) => !_disposed && _attached && row.Epoch == _epoch && _entries.Contains(row) && ReferenceEquals(_root, _manager.Layout) && ReferenceEquals(_document, CurrentDocument()) && ReferenceEquals(_document?.Root, _root) && ReferenceEquals(_document?.Content, _content);
    internal bool TryEdit(string name, string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var row = _entries.SingleOrDefault(row => row.Field.Name == name);
        return row != null && Commit(row, value);
    }

    private bool Commit(Row row, string value, bool draft = false)
    {
        if (_synchronizing != 0 || _writing != 0 || row.Field.Write == null || !IsCurrent(row))
            return false;
        ShowDescription(row);
        try
        {
            var actual = row.Field.Read();
            if (draft && actual != row.Displayed && value != actual)
                throw new InvalidOperationException("This property changed outside the inspector. Press Escape to reload its current value.");
            // Do not invoke setters for unchanged values (including non-solid brushes).
            if (value != actual)
            {
                _writing++;
                try
                {
                    row.Field.Write(value);
                }
                finally
                {
                    _writing--;
                }
            }

            if (!IsCurrent(row))
                return false; // Application callbacks own a replacement selection.
            LastError = null;
            row.Error.Visibility = Visibility.Collapsed;
            Synchronize(row, discardDraft: true);
            return true;
        }
        catch (Exception error)
        {
            if (IsCurrent(row))
            {
                LastError = error.Message;
                row.Error.Text = error.Message;
                row.Error.Visibility = Visibility.Visible;
                AutomationProperties.SetHelpText(row.View, error.Message);
            }

            return false;
        }
        finally
        {
            if (_refreshPending)
            {
                _refreshPending = false;
                RefreshValues();
            }
        }
    }

    private void Cancel(Row row)
    {
        if (!IsCurrent(row))
            return;
        row.Error.Visibility = Visibility.Collapsed;
        LastError = null;
        Synchronize(row, discardDraft: true);
    }

    private static bool HasDraft(Row row) => row.Text != null && row.Text.Text != row.Displayed;
    private void Synchronize(Row row, bool discardDraft = false)
    {
        // TextChanged can be deferred on native hosts. The DP's current value, not
        // the delivery time of that event, determines whether a draft exists.
        if (!IsCurrent(row) || (!discardDraft && HasDraft(row)))
            return;
        _synchronizing++;
        try
        {
            var value = row.Field.Read();
            row.Displayed = value;
            if (row.Text != null && row.Text.Text != value)
                row.Text.Text = value;
            if (row.Boolean != null)
                row.Boolean.IsChecked = ParseBoolean(value);
            if (row.Choice != null)
                row.Choice.SelectedItem = value;
            if (row.ReadOnly != null)
                row.ReadOnly.Text = value;
            if (row.Swatch != null)
                row.Swatch.Background = value.StartsWith('#') ? ParseColor(value) : null;
        }
        finally
        {
            _synchronizing--;
        }
    }

    private void RefreshValues()
    {
        if (_disposed || !_attached)
            return;
        if (_writing != 0 || _synchronizing != 0)
        {
            _refreshPending = true;
            return;
        }

        _heading.Text = _content is { } content ? $"{content.GetType().Name}  {_document?.Title}" : "No document selected";
        foreach (var row in _entries.ToArray())
            Synchronize(row);
    }

    internal void Filter(string text)
    {
        _search.Text = text;
        ApplyFilter();
    }

    internal void SetOrder(bool alphabetical)
    {
        _alphabetical = alphabetical;
        Reorder();
    }

    internal void ToggleCategory(string category)
    {
        if (!_collapsed.Add(category))
            _collapsed.Remove(category);
        ApplyFilter();
    }

    private void Reorder() => ReorderNativeRows();
    private void ApplyFilter() => FilterNativeRows();
    private void ResizeColumn(double delta)
    {
        if (double.IsFinite(delta))
            NameColumnWidth = Math.Clamp(_nameWidth + delta, 64, Math.Clamp(ActualWidth - 56, 64, 400));
    }

    private void ApplyColumnWidth()
    {
        var width = Math.Min(_nameWidth, Math.Clamp(ActualWidth > 0 ? ActualWidth - 56 : 400, 64, 400));
        _columns.ColumnDefinitions[0].Width = new(width);
        foreach (var row in _entries)
            row.View.ColumnDefinitions[0].Width = new(width);
    }

    private void ShowDescription(Row? row)
    {
        _selected = row;
        SelectNativeRow(row);
        _descriptionTitle.Text = row?.Field.Name ?? "Document properties";
        _description.Text = row?.Field.Description ?? "Select a property to view its description. Enter applies edits; Escape discards a draft.";
    }

    private void OnThemeChanged(FrameworkElement sender, object args) => Paint();
    private void Paint() => UpdateOrderButtons();
    private void ClearSelection()
    {
        _epoch++;
        _synchronizing++;
        try
        {
            if (_document != null)
                _document.PropertyChanged -= OnDocumentChanged;
            _document = null;
            _content = null;
            foreach (var (value, property, token) in _tokens)
                value.UnregisterPropertyChangedCallback(property, token);
            _tokens.Clear();
            _entries.Clear();
            _nativeItems.Clear();
            _nativeContainers.Clear();
            _groups.Clear();
            LastError = null;
            ShowDescription(null);
        }
        finally
        {
            _synchronizing--;
        }
    }

    private void Detach()
    {
        if (!_attached)
            return;
        _attached = false;
        _manager.LayoutChanged -= OnLayoutChanged;
        _manager.ActiveContentChanged -= OnActiveChanged;
        if (_root != null)
            _root.PropertyChanged -= OnRootChanged;
        _root = null;
        _refreshPending = false;
        ClearSelection();
    }

    public new void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Detach();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        ActualThemeChanged -= OnThemeChanged;
        _rows.SelectionChanged -= NativeSelectionChanged;
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Dimension(double value) => double.IsNaN(value) ? "Auto" : Number(value);
    private static double ParseDimension(string value) => value.Trim().Equals("Auto", StringComparison.OrdinalIgnoreCase) ? double.NaN : ParseNumber(value, 0, 100000);
    private static double ParseNumber(string value, double min, double max)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result) || result < min || result > max)
            throw new ArgumentException($"Enter a number from {min} to {max}.");
        return result;
    }

    private static bool ParseBoolean(string value) => bool.TryParse(value, out var result) ? result : throw new ArgumentException("Enter True or False.");
    private static string ThicknessText(Thickness value) => string.Join(",", new[] { value.Left, value.Top, value.Right, value.Bottom }.Select(Number));
    private static Thickness ParseThickness(string value, bool nonnegative)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is not (1 or 2 or 4))
            throw new ArgumentException("Enter one, two or four comma-separated numbers.");
        var values = parts.Select(part => ParseNumber(part, nonnegative ? 0 : -100000, 100000)).ToArray();
        return values.Length switch
        {
            1 => new(values[0]),
            2 => new(values[0], values[1], values[0], values[1]),
            _ => new(values[0], values[1], values[2], values[3])
        };
    }

    private static string Color(Brush? brush) => brush is SolidColorBrush value ? $"#{value.Color.A:X2}{value.Color.R:X2}{value.Color.G:X2}{value.Color.B:X2}" : brush == null ? "(unset)" : "(brush)";
    private static SolidColorBrush ParseColor(string value)
    {
        var text = value.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        if ((text.Length != 6 && text.Length != 8) || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            throw new ArgumentException("Enter #RRGGBB or #AARRGGBB.");
        if (text.Length == 6)
            argb |= 0xff000000;
        return new(Microsoft.UI.ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }
}
