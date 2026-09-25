using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UnoDock.Internal;
using UnoDock.Layout;
using UnoDock.Compatibility;

namespace UnoDock.Controls;
public class LayoutAnchorableControl : LayoutDocumentControl
{
    public new LayoutAnchorable? Model { get => base.Model as LayoutAnchorable; set => base.Model = value; }

    public LayoutAnchorableControl()
    {
    }

    protected override void OnGotKeyboardFocus(DockKeyboardFocusChangedEventArgs e) => base.OnGotKeyboardFocus(e);
}
