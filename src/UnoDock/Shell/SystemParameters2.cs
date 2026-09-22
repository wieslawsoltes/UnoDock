using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace Microsoft.Windows.Shell;

/// <summary>Observable shell metrics for the calling UI thread. Windows values come from
/// system APIs at 96 DPI. On other platforms frame measurements are zero: those window
/// managers do not expose WPF's non-client metrics. App-specific chrome supplies its own sizes.</summary>
public class SystemParameters2 : INotifyPropertyChanged
{
    [ThreadStatic] private static SystemParameters2? _current;
    private readonly DispatcherQueue _dispatcher;
    private readonly UISettings _settings;
#if WINDOWS
    private readonly AccessibilitySettings _accessibility;
#endif
    private Snapshot _value = new();
    private SolidColorBrush _brush = new();
    private bool _queued;
    public static SystemParameters2 Current => _current ??= new();
    private SystemParameters2()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("Shell metrics require a UI dispatcher.");
        _settings = new UISettings();
#if WINDOWS
        _accessibility = new AccessibilitySettings();
        _accessibility.HighContrastChanged += (_, _) => QueueRefresh();
#endif
        _settings.ColorValuesChanged += (_, _) => QueueRefresh();
        Refresh();
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool HighContrast => _value.HighContrast;
    public bool IsGlassEnabled => _value.Glass;
    public double WindowCaptionHeight => _value.Caption;
    public string UxThemeColor => _value.ThemeColor;
    public string UxThemeName => _value.ThemeName;
    public CornerRadius WindowCornerRadius => _value.Corners;
    public global::Windows.UI.Color WindowGlassColor => _value.Color;
    public SolidColorBrush WindowGlassBrush => _brush;
    public Rect WindowCaptionButtonsLocation => _value.Buttons;
    public Size SmallIconSize => _value.Icon;
    public Thickness WindowNonClientFrameThickness => _value.Frame;
    public Thickness WindowResizeBorderThickness => _value.Resize;
    private void QueueRefresh()
    {
        // UISettings can raise on a background thread. Never touch a brush or a DP there.
        _dispatcher.TryEnqueue(() =>
        {
            if (_queued) return;
            _queued = true;
            if (!_dispatcher.TryEnqueue(() => { _queued = false; Refresh(); })) _queued = false;
        });
    }
    public void Refresh()
    {
        if (!_dispatcher.HasThreadAccess) throw new InvalidOperationException("Refresh metrics on the owning UI thread.");
        var next = Read(); var old = _value;
        if (old == next) return;
        _value = next;
        if (old.Color != next.Color) _brush = new SolidColorBrush(next.Color);
        // Publish the entire snapshot before notifying, so reentrant bindings see consistent data.
        Notify(nameof(HighContrast), old.HighContrast != next.HighContrast);
        Notify(nameof(IsGlassEnabled), old.Glass != next.Glass);
        Notify(nameof(WindowCaptionHeight), old.Caption != next.Caption);
        Notify(nameof(UxThemeColor), old.ThemeColor != next.ThemeColor);
        Notify(nameof(UxThemeName), old.ThemeName != next.ThemeName);
        Notify(nameof(WindowCornerRadius), old.Corners != next.Corners);
        Notify(nameof(WindowGlassColor), old.Color != next.Color);
        Notify(nameof(WindowGlassBrush), old.Color != next.Color);
        Notify(nameof(WindowCaptionButtonsLocation), old.Buttons != next.Buttons);
        Notify(nameof(SmallIconSize), old.Icon != next.Icon);
        Notify(nameof(WindowNonClientFrameThickness), old.Frame != next.Frame);
        Notify(nameof(WindowResizeBorderThickness), old.Resize != next.Resize);
        void Notify(string name, bool changed) { if (changed) PropertyChanged?.Invoke(this, new(name)); }
    }
    private Snapshot Read()
    {
#if WINDOWS
        var highContrast = _accessibility.HighContrast;
#else
        var highContrast = false;
        if (OperatingSystem.IsWindows())
        {
            var info = new HighContrastInfo { Size = (uint)Marshal.SizeOf<HighContrastInfo>() };
            highContrast = SystemParametersInfo(0x0042, info.Size, ref info, 0) && (info.Flags & 1) != 0;
        }
#endif
        var color = _settings.GetColorValue(UIColorType.Accent);
        var background = _settings.GetColorValue(UIColorType.Background);
        var next = new Snapshot { HighContrast = highContrast, Color = color,
            ThemeName = highContrast ? "HighContrast" : background.R + background.G + background.B < 384 ? "Dark" : "Light" };
        if (!OperatingSystem.IsWindows()) return next;
        var glass = DwmIsCompositionEnabled(out var enabled) >= 0 && enabled;
        if (DwmGetColorizationColor(out var argb, out _) >= 0)
            color = global::Windows.UI.Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        var x = (double)Metric(32) + Metric(92); var y = (double)Metric(33) + Metric(92);
        var caption = (double)Metric(4); var buttonWidth = (double)Metric(30); var buttonHeight = (double)Metric(31);
        var themeFile = new StringBuilder(1024); var scheme = new StringBuilder(256); var size = new StringBuilder(256);
        var hasTheme = GetCurrentThemeName(themeFile, themeFile.Capacity, scheme, scheme.Capacity, size, size.Capacity) >= 0;
        return next with { Glass = glass && !highContrast, Color = color, Caption = caption,
            Resize = new(x, y, x, y), Frame = new(x, y + caption, x, y),
            Buttons = new(-3 * buttonWidth, 0, 3 * buttonWidth, buttonHeight),
            Icon = new(Metric(49), Metric(50)), ThemeName = hasTheme ? Path.GetFileNameWithoutExtension(themeFile.ToString()) : next.ThemeName,
            ThemeColor = hasTheme ? scheme.ToString() : "", Corners = new(0) };
    }
    [StructLayout(LayoutKind.Sequential)] private struct HighContrastInfo { public uint Size, Flags; public nint Scheme; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfo(uint action, uint size, ref HighContrastInfo info, uint flags);
    private static int Metric(int index) => GetSystemMetricsForDpi(index, 96);
    private sealed record Snapshot
    {
        public bool HighContrast, Glass;
        public double Caption;
        public string ThemeColor = "", ThemeName = "";
        public CornerRadius Corners;
        public global::Windows.UI.Color Color;
        public Rect Buttons;
        public Size Icon;
        public Thickness Frame, Resize;
    }
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("dwmapi.dll", ExactSpelling = true)] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    [DllImport("dwmapi.dll", ExactSpelling = true)] private static extern int DwmGetColorizationColor(out uint color, [MarshalAs(UnmanagedType.Bool)] out bool opaque);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int GetCurrentThemeName(StringBuilder file, int fileLength, StringBuilder color, int colorLength, StringBuilder size, int sizeLength);
}
