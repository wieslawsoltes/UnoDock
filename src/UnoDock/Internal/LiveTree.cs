namespace UnoDock.Internal;

internal static class LiveTree
{
    /// <summary>Native WinUI raises Unloaded on a later turn: an element removed and added again
        /// in the meantime then receives Unloaded while it is back in the live tree. Such an event
        /// is stale and must not tear the element down. See docs/native-winui.md.</summary>
        internal static bool IsStaleUnload(FrameworkElement element) => IsConnected(element);
    /// <summary>Loaded, or on native WinUI connected under its window's content: there IsLoaded
        /// stays false after a stale Unloaded and until Loaded is raised a turn after layout.</summary>
        internal static bool IsLive(FrameworkElement element) => element.IsLoaded || IsConnected(element);
    // Under the window's content, or inside one of its open popups (menus, flyouts).
    private static bool IsConnected(FrameworkElement element)
    {
#if WINDOWS
        if (element.XamlRoot is not { Content: { } root } xamlRoot)
            return false;
        var path = new List<DependencyObject>();
        for (DependencyObject? node = element; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, root))
                return true;
            path.Add(node);
        }

        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            if (path.Any(node => ReferenceEquals(node, popup) || ReferenceEquals(node, popup.Child)))
                return true;
#endif
        return false;
    }
}
