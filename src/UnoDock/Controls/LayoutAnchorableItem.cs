using Microsoft.UI.Xaml.Data;
using UnoDock.Internal;
using UnoDock.Layout;

namespace UnoDock.Controls;
#if WINDOWS
#else
#endif
public partial class LayoutAnchorableItem : LayoutItem
{
    protected override void Close() => LayoutElement.Close();
    protected override bool CanExecuteDockAsDocumentCommand() => LayoutElement is LayoutAnchorable { CanDockAsTabbedDocument: true } && base.CanExecuteDockAsDocumentCommand();
    protected override void SetDefaultBindings()
    {
        base.SetDefaultBindings();
        BindDefault(CanAutoHideProperty, nameof(LayoutAnchorable.CanAutoHide));
        BindDefault(CanDockAsTabbedDocumentProperty, nameof(LayoutAnchorable.CanDockAsTabbedDocument));
        BindDefault(CanHideProperty, nameof(LayoutAnchorable.CanHide));
    }

    protected override void ClearDefaultBindings() => base.ClearDefaultBindings();
    protected override void InitDefaultCommands()
    {
        base.InitDefaultCommands();
        CommandDefault(HideCommandProperty, () => ((LayoutAnchorable)LayoutElement).Hide(), () => LayoutElement is LayoutAnchorable { CanHide: true, IsHidden: false });
        CommandDefault(AutoHideCommandProperty, () => ((LayoutAnchorable)LayoutElement).ToggleAutoHide(), () => LayoutElement is LayoutAnchorable { CanAutoHide: true, IsFloating: false } && LayoutElement.Parent is LayoutAnchorablePane or LayoutAnchorGroup);
        CommandDefault(DockCommandProperty, () => LayoutElement.Dock(), () => LayoutElement.IsFloating);
    }

    protected override void ClearDefaultCommands() => base.ClearDefaultCommands();
    private bool _syncingVisibility;
    protected override void OnVisibilityChanged()
    {
        if (!_syncingVisibility && LayoutElement is LayoutAnchorable a)
            a.IsVisible = Visibility == Visibility.Visible;
    }

    private protected override void SyncVisibilityFromModel()
    {
        if (LayoutElement is not LayoutAnchorable a)
            return;
        var visibility = a.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        if (Visibility == visibility)
            return;
        _syncingVisibility = true;
        try
        {
            Visibility = visibility;
        }
        finally
        {
            _syncingVisibility = false;
        }
    }
}
