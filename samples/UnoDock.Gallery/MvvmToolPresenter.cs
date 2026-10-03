using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;

namespace UnoDock.Gallery;

public sealed class MvvmToolPresenter : ContentControl
{
    // The presenter that currently shows each tool view, held for as long as the view lives.
    // On native WinUI a discarded presenter can still own the view natively after its managed
    // wrapper is unreachable, so the owner must stay reachable to release it.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, MvvmToolPresenter> Owners = new();
    public MvvmToolPresenter()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        DataContextChanged += (_, _) => Present();
        Loaded += (_, _) => Present();
    }

    private void Present()
    {
        var view = (DataContext as WorkspaceTool)?.View;
        if (ReferenceEquals(Content, view))
            return;
        if (view != null)
        {
            if (Owners.TryGetValue(view, out var owner) && !ReferenceEquals(owner, this) && ReferenceEquals(owner.Content, view))
                owner.Content = null;
            switch (VisualTreeHelper.GetParent(view))
            {
                case ContentPresenter parent when ReferenceEquals(parent.Content, view):
                    parent.Content = null;
                    break;
                case ContentControl parent when ReferenceEquals(parent.Content, view):
                    parent.Content = null;
                    break;
                case Border parent when ReferenceEquals(parent.Child, view):
                    parent.Child = null;
                    break;
                case Panel parent:
                    parent.Children.Remove(view);
                    break;
            }
        }

        Content = view;
        if (view != null)
            Owners.AddOrUpdate(view, this);
    }
}
