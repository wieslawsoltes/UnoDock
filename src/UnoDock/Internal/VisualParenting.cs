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

    /// <summary>Runs <paramref name="host"/>, which gives a detached element a new parent. WIP:
    /// native WinUI rejects an element that still carries the XamlRoot of the tree it left;
    /// this records which re-basing lets the host succeed.</summary>
    internal static void Rehost(UIElement element, FrameworkElement target, Action host)
    {
        try
        {
            host();
            return;
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ArgumentException)
        {
            var before = Describe(element, target);
            var attempts = new List<string>();
            foreach (var root in new[] { target.XamlRoot, null })
            {
                if (root == null && attempts.Count > 0 && target.XamlRoot == null)
                    continue;
                var failures = SetRoot(element, root);
                try
                {
                    host();
                    System.Diagnostics.Trace.WriteLine("UnoDock re-host succeeded after setting XamlRoot " + (root == null ? "null" : "to the target") + ": " + before);
                    Console.Error.WriteLine("UNODOCK-REHOST OK root=" + (root == null ? "null" : "target") + " setter-failures=" + failures + " | " + before);
                    return;
                }
                catch (Exception retry) when (retry is System.Runtime.InteropServices.COMException or ArgumentException)
                {
                    attempts.Add((root == null ? "null" : "target") + " setter-failures=" + failures + " -> " + retry.HResult.ToString("X8"));
                }
            }

            throw new InvalidOperationException("Re-host failed: " + before + "; attempts: " + string.Join(", ", attempts), error);
        }
    }

    private static int SetRoot(DependencyObject node, XamlRoot? root)
    {
        var failures = 0;
        if (node is UIElement element && !ReferenceEquals(element.XamlRoot, root))
        {
            try
            {
                element.XamlRoot = root;
            }
            catch (Exception)
            {
                failures++;
            }
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            failures += SetRoot(VisualTreeHelper.GetChild(node, i), root);
        if (node is ContentPresenter { Content: UIElement content } && VisualTreeHelper.GetParent(content) == null)
            failures += SetRoot(content, root);
        return failures;
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
