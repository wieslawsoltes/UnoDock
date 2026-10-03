using Microsoft.UI.Xaml.Input;

namespace UnoDock.Internal;

internal static class VisualParenting
{
#if WINDOWS
    // The content host (ContentPresenter, ContentControl or TabViewItem header) last given each
    // element, held for as long as the element lives. On native WinUI a discarded host can still
    // own the element natively after its managed wrapper is unreachable, so it must stay
    // reachable for Detach to release the element.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, DependencyObject> Hosts = new();
#endif

    /// <summary>Records the content host that was just given <paramref name="content"/>.</summary>
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
                // WIP diagnostics.
                string probe;
                try
                {
                    var grid = new Grid();
                    grid.Children.Add(wanted[i]);
                    grid.Children.Remove(wanted[i]);
                    probe = "accepted by a new panel";
                }
                catch (Exception failure)
                {
                    probe = "rejected by a new panel " + failure.HResult.ToString("X8");
                }

                throw new InvalidOperationException($"Insert of {wanted[i].GetType().Name} into {panel.GetType().Name} failed ({probe}); visual parent {VisualTreeHelper.GetParent(wanted[i])?.GetType().Name ?? "none"}; element root {(wanted[i].XamlRoot == null ? "none" : "set")}; panel root {(panel.XamlRoot == null ? "none" : "set")}; panel loaded {panel.IsLoaded}; element loaded {(wanted[i] as FrameworkElement)?.IsLoaded}", error);
            }
#else
            panel.Children.Insert(i, wanted[i]);
#endif
        }
    }
}
