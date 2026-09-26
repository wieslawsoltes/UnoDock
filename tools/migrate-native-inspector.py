"""One-use, fail-closed migration of the retained inspector to native presenters.
All replacement anchors are resolved before writing. Business rules, registered
fields, value parsing and existing acceptance assertions are not rewritten.
"""
from pathlib import Path
import hashlib

path = Path('samples/UnoDock.Gallery/SamplePropertyInspector.cs')
original = path.read_bytes()
assert hashlib.sha1(f'blob {len(original)}\0'.encode() + original).hexdigest() == '89336bb56782565109fe87fd8e055bfc6300f70a', 'Inspector base changed; review required.'
text = original.decode()

def replace(old, new, count=1):
    global text
    if text.count(old) != count:
        raise RuntimeError(f'Expected {count} exact inspector anchors, found {text.count(old)}: {old[:80]!r}')
    text = text.replace(old, new)

def section(start, end, replacement):
    global text
    if text.count(start) != 1 or text.count(end) != 1:
        raise RuntimeError('Ambiguous inspector section boundary.')
    first = text.index(start)
    last = text.index(end, first)
    text = text[:first] + replacement + '\n\n' + text[last:]

replace('internal sealed class SamplePropertyInspector :', 'internal sealed partial class SamplePropertyInspector :')
replace('        internal string Displayed = "";', '        internal string Displayed = "";\n        internal bool SuppressPresentationBlur;')
replace('private sealed record Category(Border View, SampleButton Button, string Name);', 'private sealed record Category(Border View, ToggleButton Button, string Name);')
section('    private readonly TextBlock _heading = new()', '    private readonly List<Row> _entries = [];', '''    private readonly TextBlock _heading;
    private readonly TextBox _search;
    private readonly ListView _rows;
    private readonly Grid _columns;
    private readonly TextBlock _descriptionTitle, _description;''')
section('    internal SamplePropertyInspector(DockingManager manager)', '    private void OnLoaded', '''    internal SamplePropertyInspector(DockingManager manager)
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
    }''')
section('    private void AddRow(Field field)', '    private LayoutContent? CurrentDocument()', '    private void AddRow(Field field) => AddNativeRow(field);')
section('    private void Reorder()', '    private void ResizeColumn(double delta)', '''    private void Reorder() => ReorderNativeRows();

    private void ApplyFilter() => FilterNativeRows();''')
section('    private void Paint()', '    private void ClearSelection()', '''    private void Paint() => UpdateOrderButtons();''')
replace('        _selected = row;', '        _selected = row;\n        SelectNativeRow(row);')
replace('            _rows.Children.Clear();', '            _nativeItems.Clear();\n            _nativeContainers.Clear();')
replace('        ActualThemeChanged -= OnThemeChanged;', '        ActualThemeChanged -= OnThemeChanged;\n        _rows.SelectionChanged -= NativeSelectionChanged;')
app = Path('samples/UnoDock.Gallery/App.xaml.cs')
app_text = app.read_text()
anchor = '                        ("inspector-quality", true, () => Testing.InspectorQualityTests.Run(output)),'
assert app_text.count(anchor) == 1
app_text = app_text.replace(anchor, anchor + '\n                        ("native-inspector", true, () => Testing.NativeInspectorTests.Run(output)),')
# Native Toggle automation must run the same category operation as a pointer.
native = Path('samples/UnoDock.Gallery/SamplePropertyInspector.Native.cs')
native_text = native.read_text()
old = '''                        button.Click += (_, _) =>
                        {
                            if (!_disposed && _groups.TryGetValue(name, out var current) && ReferenceEquals(current.Button, button))
                            {
                                ToggleCategory(name);
                            }
                        };'''
new = '''                        void ChangeCategory(bool expanded)
                        {
                            if (_synchronizing == 0 && !_disposed && _groups.TryGetValue(name, out var current) &&
                                ReferenceEquals(current.Button, button) && expanded == _collapsed.Contains(name))
                            {
                                ToggleCategory(name);
                            }
                        }
                        button.Checked += (_, _) => ChangeCategory(true);
                        button.Unchecked += (_, _) => ChangeCategory(false);'''
assert native_text.count(old) == 1
native_text = native_text.replace(old, new)
path.write_text(text)
app.write_text(app_text)
native.write_text(native_text)
print('Native templates integrated; field registration, validation, commit/conflict and epoch rules retained.')
