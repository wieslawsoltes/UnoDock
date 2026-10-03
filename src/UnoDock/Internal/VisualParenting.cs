using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;

internal static class VisualParenting
{
#if WINDOWS
    // Native WinUI connects a ContentPresenter's or ContentControl's element content (and a
    // TabViewItem's header) during layout, so until then the element has no visual parent but
    // cannot be given to another host. Hosts are recorded so that Detach can still release it.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, WeakReference<DependencyObject>> Hosts = new();
#endif

    /// <summary>Records the content host that was just given <paramref name="content"/>.</summary>
    internal static void Hosted(DependencyObject host, object? content)
    {
#if WINDOWS
        if (content is UIElement element)
            Hosts.AddOrUpdate(element, new(host));
#endif
    }

    internal static void Detach(UIElement element)
    {
#if WINDOWS
        if (Hosts.TryGetValue(element, out var recorded))
        {
            Hosts.Remove(element);
            if (recorded.TryGetTarget(out var host))
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
    // WIP diagnostics for native WinUI re-parenting failures.
    internal static string Describe(UIElement element, FrameworkElement target)
    {
        var parent = VisualTreeHelper.GetParent(element);
        var host = Hosts.TryGetValue(element, out var recorded) && recorded.TryGetTarget(out var h) ? h : null;
        return $"{element.GetType().Name}; visual parent {parent?.GetType().FullName ?? "none"}; logical parent {(element as FrameworkElement)?.Parent?.GetType().FullName ?? "none"}; same XamlRoot {ReferenceEquals(element.XamlRoot, target.XamlRoot)} (element root {(element.XamlRoot == null ? "none" : "set")}, target root {(target.XamlRoot == null ? "none" : "set")}); loaded {(element as FrameworkElement)?.IsLoaded}; recorded host {host?.GetType().FullName ?? "none"}; content {(element as ContentPresenter)?.Content?.GetType().FullName}";
    }

    private static void Release(DependencyObject host, UIElement element)
    {
        switch (host)
        {
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
#if WINDOWS
            try
            {
                panel.Children.Insert(i, wanted[i]);
            }
            catch (System.Runtime.InteropServices.COMException error)
            {
                // WIP diagnostics for native WinUI re-parenting failures.
                throw new InvalidOperationException("Insert into " + panel.GetType().Name + " failed: " + Describe(wanted[i], panel), error);
            }
#else
            panel.Children.Insert(i, wanted[i]);
#endif
        }
    }
}
