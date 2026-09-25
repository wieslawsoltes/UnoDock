using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;

namespace UnoDock.Gallery;
public sealed class MvvmToolPresenter : ContentControl
{
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
    }
}
