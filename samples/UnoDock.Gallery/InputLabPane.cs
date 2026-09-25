using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Data;
using UnoDock.Compatibility;
using UnoDock.Controls;

namespace UnoDock.Gallery;
internal sealed class InputLabPane : LayoutDocumentPaneControl
{
    private readonly InputLabState _state;
    private readonly Action<string> _record;
    internal InputLabPane(LayoutDocumentPane model, InputLabState state, Action<string> record, bool bindSelection) : base(model)
    {
        _state = state; _record = record;
        if (bindSelection) SetBinding(SelectedIndexProperty, new Binding { Source = state, Path = new(nameof(state.Index)), Mode = BindingMode.TwoWay });
    }
    protected override LayoutTabItemBase CreateTabItem(LayoutContent model) => new InputLabTab(_state, _record);
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        _record($"Selection hook: index={SelectedIndex}, item={(SelectedItem as LayoutContent)?.Title}");
        if (_state.Redirect && SelectedIndex == 1 && Items.Count() > 2) SelectedIndex = 2;
    }
}
