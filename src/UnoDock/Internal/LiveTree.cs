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

    private static bool IsConnected(FrameworkElement element)
    {
#if WINDOWS
        if (element.XamlRoot?.Content is not { } root)
            return false;
        for (DependencyObject? node = element; node != null; node = VisualTreeHelper.GetParent(node))
            if (ReferenceEquals(node, root))
                return true;
#endif
        return false;
    }
}
