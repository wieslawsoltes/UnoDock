using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using UnoDock.Controls;
using Windows.System;

namespace UnoDock.Gallery;

internal sealed partial class SamplePropertyInspector
{
    private readonly SampleInspectorResources _nativeResources = new();
    private readonly ObservableCollection<ListViewItem> _nativeItems = [];
    private readonly Dictionary<FrameworkElement, ListViewItem> _nativeContainers = new(ReferenceEqualityComparer.Instance);
    private readonly ToggleButton _categoryMode, _alphabeticalMode;
    private bool _selectingNativeRow;

    private T NativeTemplate<T>(string name) where T : FrameworkElement =>
        ((DataTemplate)_nativeResources[name]).LoadContent() as T ?? throw new InvalidOperationException("Invalid compiled inspector template: " + name);

    private static T Part<T>(FrameworkElement root, string name) where T : FrameworkElement =>
        root.FindVisualChildren<T>().Single(element => element.Name == name);

    private void AddNativeRow(Field field)
    {
        var kind = field.Write == null ? "ReadOnly" : field.Kind.ToString();
        var grid = NativeTemplate<Grid>("Inspector." + kind);
        var label = Part<TextBlock>(grid, "PropertyName");
        var error = Part<TextBlock>(grid, "PropertyError");
        label.Text = field.Name;
        ToolTipService.SetToolTip(label, field.Description);
        var row = new Row(field, _epoch, grid, label, error);
        _entries.Add(row);
        FrameworkElement input;
        if (field.Write == null)
        {
            input = row.ReadOnly = Part<TextBlock>(grid, "ReadOnlyValue");
        }
        else if (field.Kind == EditorKind.Boolean)
        {
            var check = row.Boolean = Part<CheckBox>(grid, "BooleanEditor");
            check.Checked += (_, _) => Commit(row, "True");
            check.Unchecked += (_, _) => Commit(row, "False");
            input = check;
        }
        else if (field.Kind == EditorKind.Choice)
        {
            var combo = row.Choice = Part<ComboBox>(grid, "ChoiceEditor");
            combo.ItemsSource = field.Choices;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string choice)
                {
                    Commit(row, choice);
                }
            };
            input = combo;
        }
        else
        {
            var text = row.Text = Part<TextBox>(grid, "TextEditor");
            text.LostFocus += (_, _) =>
            {
                if (row.SuppressPresentationBlur)
                {
                    row.SuppressPresentationBlur = false;
                    return;
                }
                if (HasDraft(row) && _synchronizing == 0)
                {
                    Commit(row, text.Text, true);
                }
            };
            text.KeyDown += (_, args) =>
            {
                if (args.Key == VirtualKey.Enter)
                {
                    if (HasDraft(row))
                    {
                        Commit(row, text.Text, true);
                    }
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
                row.Swatch = Part<Border>(grid, "ColorSwatch");
            }
            input = text;
        }

        AutomationProperties.SetName(input, field.Name);
        AutomationProperties.SetHelpText(input, field.Description);
        AutomationProperties.SetAutomationId(input, "Property-" + field.Name);
        input.GotFocus += (_, _) =>
        {
            row.SuppressPresentationBlur = false;
            if (IsCurrent(row))
            {
                ShowDescription(row);
            }
        };
        label.PointerPressed += (_, _) =>
        {
            if (IsCurrent(row))
            {
                ShowDescription(row);
            }
        };
        var container = NativeContainer(grid);
        AutomationProperties.SetName(container, field.Name);
        AutomationProperties.SetHelpText(container, field.Description);
        AutomationProperties.SetAutomationId(container, "PropertyRow-" + field.Name);
    }

    private ListViewItem NativeContainer(FrameworkElement view)
    {
        if (!_nativeContainers.TryGetValue(view, out var container))
        {
            container = new ListViewItem
            {
                Content = view,
                Style = (Style)_nativeResources["Inspector.ContainerStyle"]
            };
            _nativeContainers.Add(view, container);
        }
        return container;
    }

    private void ReorderNativeRows()
    {
        PreserveDraftDuringPresentation();
        var epoch = _epoch;
        _synchronizing++;
        try
        {
            var desired = new List<ListViewItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var ordered = _alphabetical
                ? _entries.OrderBy(row => row.Field.Name, StringComparer.Ordinal)
                : _entries.OrderBy(row => row.Field.Category, StringComparer.Ordinal).ThenBy(row => row.Field.Name, StringComparer.Ordinal);
            foreach (var row in ordered)
            {
                if (!_alphabetical && seen.Add(row.Field.Category))
                {
                    var name = row.Field.Category;
                    if (!_groups.TryGetValue(name, out var group))
                    {
                        var header = NativeTemplate<Border>("Inspector.Category");
                        var button = Part<ToggleButton>(header, "CategoryToggle");
                        AutomationProperties.SetName(button, "Toggle " + name + " properties");
                        AutomationProperties.SetAutomationId(button, "PropertyCategory-" + name);
                        button.Click += (_, _) =>
                        {
                            if (!_disposed && _groups.TryGetValue(name, out var current) && ReferenceEquals(current.Button, button))
                            {
                                ToggleCategory(name);
                            }
                        };
                        group = new Category(header, button, name);
                        _groups.Add(name, group);
                    }
                    var container = NativeContainer(group.View);
                    container.IsTabStop = false;
                    desired.Add(container);
                }
                desired.Add(NativeContainer(row.View));
            }

            // Move retained native containers instead of clearing/recreating the
            // table. Both editor identity and selection survive order changes.
            for (var index = 0; index < desired.Count; index++)
            {
                if (_disposed || epoch != _epoch)
                {
                    return;
                }
                var item = desired[index];
                if (index < _nativeItems.Count && ReferenceEquals(_nativeItems[index], item))
                {
                    continue;
                }
                var previous = _nativeItems.IndexOf(item);
                if (previous >= 0)
                {
                    _nativeItems.Move(previous, index);
                }
                else
                {
                    _nativeItems.Insert(index, item);
                }
            }
            while (_nativeItems.Count > desired.Count && epoch == _epoch && !_disposed)
            {
                _nativeItems.RemoveAt(_nativeItems.Count - 1);
            }
            if (_disposed || epoch != _epoch)
            {
                return;
            }
            FilterNativeRows();
            ApplyColumnWidth();
            UpdateOrderButtons();
            SelectNativeRow(_selected);
        }
        finally
        {
            _synchronizing--;
        }
    }

    private void FilterNativeRows()
    {
        PreserveDraftDuringPresentation();
        var text = _search.Text.Trim();
        _synchronizing++;
        try
        {
            foreach (var row in _entries)
            {
                var match = row.Field.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || row.Field.Category.Contains(text, StringComparison.OrdinalIgnoreCase);
                var visible = match && (text.Length > 0 || _alphabetical || !_collapsed.Contains(row.Field.Category));
                row.View.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                NativeContainer(row.View).Visibility = row.View.Visibility;
            }
            foreach (var group in _groups.Values)
            {
                var expanded = !_collapsed.Contains(group.Name) || text.Length > 0;
                group.Button.IsChecked = expanded;
                group.Button.Content = (expanded ? "−  " : "+  ") + group.Name;
                group.View.Visibility = !_alphabetical && (text.Length == 0 || _entries.Any(row => row.Field.Category == group.Name && row.View.Visibility == Visibility.Visible))
                    ? Visibility.Visible : Visibility.Collapsed;
                NativeContainer(group.View).Visibility = group.View.Visibility;
            }
        }
        finally
        {
            _synchronizing--;
        }
    }

    private void PreserveDraftDuringPresentation()
    {
        foreach (var row in _entries)
        {
            if (row.Text is not { FocusState: not FocusState.Unfocused } text)
            {
                continue;
            }
            row.SuppressPresentationBlur = true;
            var epoch = _epoch;
            DispatcherQueue.TryEnqueue(() =>
            {
                // A stable focused editor still needs its next real user blur.
                // An editor hidden by this presentation keeps only the pending
                // blur suppression; GotFocus resets it on a later interaction.
                if (epoch == _epoch && text.FocusState != FocusState.Unfocused)
                {
                    row.SuppressPresentationBlur = false;
                }
            });
        }
    }

    private void UpdateOrderButtons()
    {
        _categoryMode.IsChecked = !_alphabetical;
        _alphabeticalMode.IsChecked = _alphabetical;
    }

    private void NativeSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_disposed || _synchronizing != 0 || _selectingNativeRow)
        {
            return;
        }
        if (_rows.SelectedItem is ListViewItem { Content: Grid view } &&
            _entries.FirstOrDefault(row => ReferenceEquals(row.View, view)) is { } selected && IsCurrent(selected))
        {
            ShowDescription(selected);
        }
    }

    private void SelectNativeRow(Row? row)
    {
        if (_selectingNativeRow)
        {
            return;
        }
        _selectingNativeRow = true;
        try
        {
            _rows.SelectedItem = row != null && _nativeContainers.TryGetValue(row.View, out var container) && _nativeItems.Contains(container)
                ? container : null;
        }
        finally
        {
            _selectingNativeRow = false;
        }
    }
}
