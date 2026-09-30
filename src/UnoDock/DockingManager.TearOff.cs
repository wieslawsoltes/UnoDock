namespace UnoDock;

public partial class DockingManager
{
    public static readonly DependencyProperty ContinuousTearOffProperty = DependencyProperty.Register(nameof(ContinuousTearOff), typeof(bool), typeof(DockingManager), new PropertyMetadata(true));
    /// <summary>When true (the default) and floating windows are native, dragging
        /// a tab out of its tab strip, or dragging a tool pane's title, floats the
        /// content immediately into a window that follows the pointer and shows the
        /// docking guides. When false, content floats only when released outside the
        /// workspace. In-surface floating hosts always use the release behavior.</summary>
        public bool ContinuousTearOff
    {
        get => (bool)GetValue(ContinuousTearOffProperty);
        set => SetValue(ContinuousTearOffProperty, value);
    }
    /// <summary>True when newly floated content opens in a native desktop window.</summary>
    internal bool UsesNativeFloatingWindows => FloatingWindowMode != FloatingWindowMode.InSurface && !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();
}
