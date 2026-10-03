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

    /// <summary>Runs <paramref name="host"/>, which gives a detached element a new parent. WIP:
    /// records whether native WinUI accepts the element on a later turn.</summary>
    internal static void Rehost(UIElement element, FrameworkElement target, Action host)
    {
        try
        {
            host();
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ArgumentException)
        {
            var before = Describe(element, target);
            Probe(element, target, before);
            throw new InvalidOperationException("Re-host failed: " + before, error);
        }
    }

    private static async void Probe(UIElement element, FrameworkElement target, string before)
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            await Task.Delay(40 * attempt);
            var parent = VisualTreeHelper.GetParent(element);
            if (parent != null)
            {
                Console.Error.WriteLine($"UNODOCK-REHOST attempt {attempt}: now parented by {parent.GetType().FullName} | {before}");
                return;
            }

            var probe = new Grid();
            try
            {
                probe.Children.Add(element);
                probe.Children.Remove(element);
                Console.Error.WriteLine($"UNODOCK-REHOST attempt {attempt}: accepted by a new panel | {before}");
                return;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"UNODOCK-REHOST attempt {attempt}: still rejected {error.HResult:X8} | {before}");
            }
        }
    }

    internal static string Describe(UIElement element, FrameworkElement target)
    {
        var parent = VisualTreeHelper.GetParent(element);
        var host = Hosts.TryGetValue(element, out var recorded) && recorded.TryGetTarget(out var h) ? h : null;
        var text = $"{element.GetType().Name}; visual parent {parent?.GetType().FullName ?? "none"}; logical parent {(element as FrameworkElement)?.Parent?.GetType().FullName ?? "none"}; same XamlRoot {ReferenceEquals(element.XamlRoot, target.XamlRoot)} (element root {(element.XamlRoot == null ? "none" : "set")}, target root {(target.XamlRoot == null ? "none" : "set")}); loaded {(element as FrameworkElement)?.IsLoaded}; recorded host {host?.GetType().FullName ?? "none"}; transitions {(target as Panel)?.ChildrenTransitions?.Count}";
        if (element is ContentPresenter { Content: UIElement content })
            text += $"; content {content.GetType().Name} parent {VisualTreeHelper.GetParent(content)?.GetType().FullName ?? "none"} logical {(content as FrameworkElement)?.Parent?.GetType().FullName ?? "none"} host {(Hosts.TryGetValue(content, out var c) && c.TryGetTarget(out var ch) ? ch.GetType().FullName + (ReferenceEquals(ch, element) ? " (this)" : " (other)") : "none")}";
        return text;
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
            var element = wanted[i];
            var index = i;
            Rehost(element, panel, () => panel.Children.Insert(index, element));
#else
            panel.Children.Insert(i, wanted[i]);
#endif
        }
    }
}
