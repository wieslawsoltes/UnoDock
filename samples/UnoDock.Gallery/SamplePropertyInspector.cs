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
internal sealed class SamplePropertyInspector : UserControl, IDisposable
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
    }

    private sealed record Category(Border View, SampleButton Button, string Name);
    private readonly DockingManager _manager;
    private readonly TextBlock _heading = new()
    {
        FontSize = 12,
        Margin = new(5, 3, 5, 2),
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Search properties",
        MinHeight = 0,
        Height = 25,
        FontSize = 12,
        Padding = new(4, 1, 4, 1)
    };
    private readonly StackPanel _rows = new();
    private readonly Grid _columns = new();
    private readonly TextBlock _descriptionTitle = new()
    {
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
    };
    private readonly TextBlock _description = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        MaxLines = 3
    };
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
        var host = new Grid();
        foreach (var height in new[]
        {
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            new GridLength(1, GridUnitType.Star),
            GridLength.Auto
        }

        )
            host.RowDefinitions.Add(new()
            {
                Height = height
            });
        host.Children.Add(_heading);
        var modes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new(4, 0, 4, 0),
            Spacing = 3
        };
        var grouped = SampleChrome.Button("Categories", () => SetOrder(false));
        grouped.Padding = new(3, 1, 3, 1);
        var sorted = SampleChrome.Button("A–Z", () => SetOrder(true));
        sorted.Padding = new(3, 1, 3, 1);
        modes.Children.Add(grouped);
        modes.Children.Add(sorted);
        Grid.SetRow(modes, 1);
        host.Children.Add(modes);
        var search = new Grid
        {
            Margin = new(4, 2, 4, 4)
        };
        search.ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        search.ColumnDefinitions.Add(new()
        {
            Width = new(23)
        });
        search.Children.Add(_search);
        var clear = SampleChrome.Button("×", () => Filter(""), "Clear property search");
        Grid.SetColumn(clear, 1);
        search.Children.Add(clear);
        Grid.SetRow(search, 2);
        host.Children.Add(search);
        _columns.ColumnDefinitions.Add(new()
        {
            Width = new(_nameWidth)
        });
        _columns.ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        _columns.Children.Add(new TextBlock { Text = "Property", FontSize = 11, Margin = new(4, 3, 3, 3) });
        var valueLabel = new TextBlock
        {
            Text = "Value",
            FontSize = 11,
            Margin = new(4, 3, 3, 3)
        };
        Grid.SetColumn(valueLabel, 1);
        _columns.Children.Add(valueLabel);
        var divider = new Thumb
        {
            Width = 5,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsTabStop = true,
            Background = SampleChrome.Color(0xa0a0a0)
        };
        AutomationProperties.SetName(divider, "Property name column divider");
        divider.DragDelta += (_, e) => ResizeColumn(e.HorizontalChange * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1));
        divider.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right)
            {
                ResizeColumn((e.Key == VirtualKey.Right ? 8 : -8) * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1));
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Home)
            {
                NameColumnWidth = 96;
                e.Handled = true;
            }
        };
        divider.DoubleTapped += (_, _) => NameColumnWidth = 96;
        _columns.Children.Add(divider);
        Grid.SetRow(_columns, 3);
        host.Children.Add(_columns);
        var scroll = new ScrollViewer
        {
            Content = _rows,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 4);
        host.Children.Add(scroll);
        var help = new StackPanel
        {
            Padding = new(5),
            Spacing = 3,
            MinHeight = 66
        };
        help.Children.Add(_descriptionTitle);
        help.Children.Add(_description);
        var helpBorder = new Border
        {
            Child = help,
            BorderThickness = new(0, 1, 0, 0),
            BorderBrush = SampleChrome.Color(0xa0a0a0)
        };
        Grid.SetRow(helpBorder, 5);
        host.Children.Add(helpBorder);
        Content = host;
        AutomationProperties.SetAutomationId(_search, "PropertySearch");
        AutomationProperties.SetName(this, "Document properties");
        _search.TextChanged += (_, _) => ApplyFilter();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnThemeChanged;
        SizeChanged += (_, _) => ApplyColumnWidth();
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

    private void AddRow(Field field)
    {
        var grid = new Grid
        {
            MinHeight = 24,
            BorderThickness = new(0, 0, 0, 1)
        };
        grid.ColumnDefinitions.Add(new()
        {
            Width = new(_nameWidth)
        });
        grid.ColumnDefinitions.Add(new()
        {
            Width = new(1, GridUnitType.Star)
        });
        grid.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        grid.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var label = new TextBlock
        {
            Text = field.Name,
            FontSize = 12,
            Margin = new(4, 4, 3, 2),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTipService.SetToolTip(label, field.Description);
        grid.Children.Add(label);
        var error = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new(4, 0, 4, 3),
            Visibility = Visibility.Collapsed
        };
        var row = new Row(field, _epoch, grid, label, error);
        _entries.Add(row);
        FrameworkElement input;
        if (field.Write == null)
            input = row.ReadOnly = new TextBlock
            {
                FontSize = 12,
                Margin = new(3, 4, 2, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        else if (field.Kind == EditorKind.Boolean)
        {
            var check = row.Boolean = new CheckBox
            {
                MinHeight = 0,
                MinWidth = 0,
                Height = 24,
                Padding = new(2, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            check.Checked += (_, _) => Commit(row, "True");
            check.Unchecked += (_, _) => Commit(row, "False");
            input = check;
        }
        else if (field.Kind == EditorKind.Choice)
        {
            var combo = row.Choice = new ComboBox
            {
                ItemsSource = field.Choices,
                MinHeight = 0,
                MinWidth = 0,
                Height = 24,
                FontSize = 12,
                Padding = new(3, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string choice)
                    Commit(row, choice);
            };
            input = combo;
        }
        else
        {
            var text = row.Text = new TextBox
            {
                MinHeight = 0,
                MinWidth = 0,
                FontSize = 12,
                Padding = new(3, 1, 3, 1),
                BorderThickness = new(0),
                CornerRadius = new(0)
            };
            text.LostFocus += (_, _) =>
            {
                if (HasDraft(row) && _synchronizing == 0)
                    Commit(row, text.Text, true);
            };
            text.KeyDown += (_, args) =>
            {
                if (args.Key == VirtualKey.Enter)
                {
                    if (HasDraft(row))
                        Commit(row, text.Text, true);
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.Escape)
                {
                    Cancel(row);
                    args.Handled = true;
                }
            };
            if (field.Kind == EditorKind.Color)
            {
                var holder = new Grid();
                holder.ColumnDefinitions.Add(new()
                {
                    Width = new(17)
                });
                holder.ColumnDefinitions.Add(new()
                {
                    Width = new(1, GridUnitType.Star)
                });
                row.Swatch = new Border
                {
                    Width = 12,
                    Height = 12,
                    BorderThickness = new(1),
                    BorderBrush = SampleChrome.Color(0x808080),
                    VerticalAlignment = VerticalAlignment.Center
                };
                holder.Children.Add(row.Swatch);
                Grid.SetColumn(text, 1);
                holder.Children.Add(text);
                input = holder;
            }
            else
                input = text;
        }

        var focusTarget = (FrameworkElement?)row.Text ?? row.Boolean ?? (FrameworkElement?)row.Choice ?? input;
        AutomationProperties.SetName(focusTarget, field.Name);
        AutomationProperties.SetHelpText(focusTarget, field.Description);
        AutomationProperties.SetAutomationId(focusTarget, "Property-" + field.Name);
        focusTarget.GotFocus += (_, _) =>
        {
            if (IsCurrent(row))
                ShowDescription(row);
        };
        label.PointerPressed += (_, _) =>
        {
            if (IsCurrent(row))
                ShowDescription(row);
        };
        Grid.SetColumn(input, 1);
        grid.Children.Add(input);
        Grid.SetRow(error, 1);
        Grid.SetColumnSpan(error, 2);
        grid.Children.Add(error);
    }

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

    private void Reorder()
    {
        _synchronizing++;
        try
        {
            _rows.Children.Clear();
            _groups.Clear();
            var ordered = _alphabetical ? _entries.OrderBy(row => row.Field.Name, StringComparer.Ordinal) : _entries.OrderBy(row => row.Field.Category, StringComparer.Ordinal).ThenBy(row => row.Field.Name, StringComparer.Ordinal);
            foreach (var row in ordered)
            {
                if (!_alphabetical && !_groups.ContainsKey(row.Field.Category))
                {
                    var name = row.Field.Category;
                    var button = SampleChrome.Button("−  " + name, () => ToggleCategory(name), "Toggle " + name + " properties");
                    button.HorizontalContentAlignment = HorizontalAlignment.Left;
                    button.Padding = new(4, 1, 3, 1);
                    var header = new Border
                    {
                        Child = button,
                        MinHeight = 23
                    };
                    _groups.Add(name, new(header, button, name));
                    _rows.Children.Add(header);
                }

                _rows.Children.Add(row.View);
            }

            ApplyFilter();
            ApplyColumnWidth();
            Paint();
        }
        finally
        {
            _synchronizing--;
        }
    }

    private void ApplyFilter()
    {
        var text = _search.Text.Trim();
        _synchronizing++;
        try
        {
            foreach (var row in _entries)
            {
                var match = row.Field.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Field.Category.Contains(text, StringComparison.OrdinalIgnoreCase);
                row.View.Visibility = match && (text.Length > 0 || _alphabetical || !_collapsed.Contains(row.Field.Category)) ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var group in _groups.Values)
            {
                group.Button.Content = (_collapsed.Contains(group.Name) && text.Length == 0 ? "+  " : "−  ") + group.Name;
                group.View.Visibility = text.Length == 0 || _entries.Any(row => row.Field.Category == group.Name && row.View.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        finally
        {
            _synchronizing--;
        }
    }

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
        _descriptionTitle.Text = row?.Field.Name ?? "Document properties";
        _description.Text = row?.Field.Description ?? "Select a property to view its description. Enter applies edits; Escape discards a draft.";
    }

    private void OnThemeChanged(FrameworkElement sender, object args) => Paint();
    private void Paint()
    {
        var dark = ActualTheme == ElementTheme.Dark;
        var foreground = SampleChrome.Color(dark ? 0xf0f0f0u : 0x202020u);
        var line = SampleChrome.Color(dark ? 0x505050u : 0xd8d8d8u);
        var category = SampleChrome.Color(dark ? 0x343434u : 0xe8e8e8u);
        Background = SampleChrome.Color(dark ? 0x252526u : 0xffffffu);
        _heading.Foreground = _descriptionTitle.Foreground = _description.Foreground = foreground;
        _columns.Background = category;
        foreach (var row in _entries)
        {
            row.View.BorderBrush = line;
            row.Label.Foreground = foreground;
            if (row.ReadOnly != null)
                row.ReadOnly.Foreground = foreground;
            row.Error.Foreground = SampleChrome.Color(dark ? 0xff9999u : 0xb42318u);
        }

        foreach (var group in _groups.Values)
            group.View.Background = category;
        foreach (var button in this.FindVisualChildren<SampleButton>())
            button.Configure(SampleChrome.Default(dark));
    }

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
            _rows.Children.Clear();
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
