namespace UnoDock.Controls;

public partial class LayoutAnchorableItem
{
    public static readonly DependencyProperty CanAutoHideProperty = DependencyProperty.Register(nameof(CanAutoHide), typeof(bool), typeof(LayoutAnchorableItem), new PropertyMetadata(true, (owner, args) => ((LayoutAnchorableItem)owner).OnAdapterPropertyChanged(nameof(CanAutoHide), args)));
    public bool CanAutoHide
    {
        get => (bool)GetValue(CanAutoHideProperty);
        set => SetValue(CanAutoHideProperty, value);
    }

    public static readonly DependencyProperty CanDockAsTabbedDocumentProperty = DependencyProperty.Register(nameof(CanDockAsTabbedDocument), typeof(bool), typeof(LayoutAnchorableItem), new PropertyMetadata(true, (owner, args) => ((LayoutAnchorableItem)owner).OnAdapterPropertyChanged(nameof(CanDockAsTabbedDocument), args)));
    public bool CanDockAsTabbedDocument
    {
        get => (bool)GetValue(CanDockAsTabbedDocumentProperty);
        set => SetValue(CanDockAsTabbedDocumentProperty, value);
    }
}
