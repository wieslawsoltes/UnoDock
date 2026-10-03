using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;

namespace UnoDock.Gallery;

public sealed class MvvmToolPresenter : ContentControl
{
    // The presenter that currently shows each tool view. Native WinUI connects a
    // ContentControl's element content during layout, so a presenter that has not been laid
    // out yet is not the view's visual parent but still owns it.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, WeakReference<MvvmToolPresenter>> Owners = new();
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
            if (Owners.TryGetValue(view, out var recorded) && recorded.TryGetTarget(out var owner) && !ReferenceEquals(owner, this) && ReferenceEquals(owner.Content, view))
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

        try
        {
            Content = view;
        }
        catch (Exception error) when (view != null && error is System.Runtime.InteropServices.COMException)
        {
            // WIP diagnostics.
            var chain = new List<string>();
            for (DependencyObject? node = VisualTreeHelper.GetParent(view); node != null && chain.Count < 8; node = VisualTreeHelper.GetParent(node))
                chain.Add(node.GetType().Name + (node is ContentPresenter p ? "(content " + (ReferenceEquals(p.Content, view) ? "view" : p.Content?.GetType().Name) + ")" : node is ContentControl c ? "(content " + (ReferenceEquals(c.Content, view) ? "view" : c.Content?.GetType().Name) + ")" : ""));
            var owner = Owners.TryGetValue(view, out var recorded) && recorded.TryGetTarget(out var o) ? o : null;
            throw new InvalidOperationException($"Tool view hosting failed: chain [{string.Join(" < ", chain)}]; logical parent {view.Parent?.GetType().Name ?? "none"}; owner {(owner == null ? "none" : ReferenceEquals(owner, this) ? "this" : "other, content is view: " + ReferenceEquals(owner.Content, view) + ", loaded " + owner.IsLoaded)}; view loaded {view.IsLoaded}", error);
        }

        if (view != null)
            Owners.AddOrUpdate(view, new(this));
    }
}
