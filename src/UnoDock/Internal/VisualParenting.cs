using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;

internal static class VisualParenting
{
#if WINDOWS
    // The host (panel, ContentPresenter, ContentControl or TabViewItem header) UnoDock last gave
    // each element, held for as long as the element lives. Native WinUI connects content during
    // layout and reports no parent for an element whose host has left the live tree, yet still
    // refuses that element to another host; Detach releases it from the recorded host.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, DependencyObject> Hosts = new();
#endif

    /// <summary>Records the host that was just given <paramref name="content"/>.</summary>
    internal static void Hosted(DependencyObject host, object? content)
    {
#if WINDOWS
        if (content is UIElement element)
            Hosts.AddOrUpdate(element, host);
#endif
    }

    internal static void Detach(UIElement element)
    {
#if WINDOWS
        if (Hosts.TryGetValue(element, out var host))
        {
            Hosts.Remove(element);
            Release(host, element);
        }
#endif
        var parent = VisualTreeHelper.GetParent(element);
        switch (parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Border border when ReferenceEquals(border.Child, element):
                border.Child = null;
                break;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, element):
                presenter.Content = null;
                break;
            case ContentControl control when ReferenceEquals(control.Content, element):
                control.Content = null;
                break;
        }
    }

#if WINDOWS
    private static void Release(DependencyObject host, UIElement element)
    {
        switch (host)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, element):
                presenter.Content = null;
                break;
            case ContentControl control when ReferenceEquals(control.Content, element):
                control.Content = null;
                break;
            case TabViewItem item when ReferenceEquals(item.Header, element):
                item.Header = null;
                break;
        }
    }

#endif
    internal static void ReconcilePanel(Panel panel, IReadOnlyList<UIElement> wanted)
    {
        var keep = new HashSet<UIElement>(wanted, ReferenceEqualityComparer.Instance);
        for (var i = panel.Children.Count - 1; i >= 0; i--)
            if (!keep.Contains(panel.Children[i]))
                panel.Children.RemoveAt(i);
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < panel.Children.Count && ReferenceEquals(panel.Children[i], wanted[i]))
                continue;
            Detach(wanted[i]);
            panel.Children.Insert(i, wanted[i]);
            Hosted(panel, wanted[i]);
        }
    }
}
