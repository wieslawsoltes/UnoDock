using System.Reflection;

namespace UnoDock.Testing;
/// <summary>Finds the native window hosting test content on every host. Uno enumerates its
/// application windows; native WinUI cannot, so it searches the windows registered with
/// UnoDock (the Gallery and test fixtures register theirs, floating windows are registered
/// by the library).</summary>
public static class TestWindows
{
    public static Window For(XamlRoot? root) => Find(root) ?? throw new InvalidOperationException("No window hosts this XamlRoot.");
    public static Window? Find(XamlRoot? root)
    {
        if (root == null)
            return null;
        return All().FirstOrDefault(w => ReferenceEquals(w.Content?.XamlRoot, root));
    }

    public static IEnumerable<Window> All()
    {
#if HAS_UNO
        return Uno.UI.ApplicationHelper.Windows.ToArray();
#else
        var registry = typeof(DockingManager).Assembly.GetType("Microsoft.Windows.Shell.WindowRegistry", true)!;
        return (IEnumerable<Window>)registry.GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
#endif
    }

    /// <summary>The Win32 window handle, or zero on other hosts.</summary>
    public static nint Handle(Window window)
    {
#if HAS_UNO
        return Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is Uno.UI.NativeElementHosting.Win32NativeWindow native ? native.Hwnd : 0;
#else
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
#endif
    }

    /// <summary>Gives a test-only DockingManager subclass the template of another manager. Native
    /// WinUI knows a subclass created only in code as Control, so a template targeting
    /// DockingManager cannot apply to it; there the subclass keeps the equivalent template
    /// UnoDock gives such subclasses. See docs/native-winui.md.</summary>
    public static T UsingTemplateOf<T>(this T manager, DockingManager source) where T : DockingManager
    {
#if HAS_UNO
        manager.Template = source.Template;
#endif
        return manager;
    }
}
