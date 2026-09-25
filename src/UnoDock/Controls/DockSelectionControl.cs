using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using UnoDock.Compatibility;

namespace UnoDock.Controls;

/// <summary>Selection event extension for the independent cached tab host.</summary>
public abstract class DockSelectionControl : DockInputControl
{
    public event SelectionChangedEventHandler? SelectionChanged;
    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e) => SelectionChanged?.Invoke(this, e);
}
