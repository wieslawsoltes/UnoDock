using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using UnoDock;
using UnoDock.Controls;

namespace Microsoft.Windows.Shell;

[Flags]
public enum WindowChromeCapabilities { None = 0, ManagedFrame = 1, NativeCaption = 2, NativeResize = 4, Glass = 8, SystemMenu = 16 }

/// <summary>Independently implemented non-client configuration. Native Windows supports caption
/// regions, interactive exclusions, resizing and DWM glass. In-surface hosts apply a managed
/// frame. This is a DependencyObject, not a general implementation of WPF Freezable.</summary>
public partial class WindowChrome : DependencyObject, INotifyPropertyChanged
{
    private static readonly ConditionalWeakTable<Window, Attachment> Native = new();
    private static readonly ConditionalWeakTable<ContentControl, Attachment> Managed = new();
    public static readonly DependencyProperty CaptionHeightProperty = Property(nameof(CaptionHeight), typeof(double), 32d);
    public static readonly DependencyProperty CornerRadiusProperty = Property(nameof(CornerRadius), typeof(CornerRadius), new CornerRadius(0));
    public static readonly DependencyProperty GlassFrameThicknessProperty = Property(nameof(GlassFrameThickness), typeof(Thickness), new Thickness(0));
    public static readonly DependencyProperty ResizeBorderThicknessProperty = Property(nameof(ResizeBorderThickness), typeof(Thickness), new Thickness(5));
    public static readonly DependencyProperty ShowSystemMenuProperty = Property(nameof(ShowSystemMenu), typeof(bool), true);
    public static readonly DependencyProperty IsHitTestVisibleInChromeProperty = DependencyProperty.RegisterAttached("IsHitTestVisibleInChrome", typeof(bool), typeof(WindowChrome),
        new PropertyMetadata(false, (d, _) => { if (d is FrameworkElement e) FindAttachment(e)?.Invalidate(); }));
    public static readonly DependencyProperty WindowChromeProperty = DependencyProperty.RegisterAttached("WindowChrome", typeof(WindowChrome), typeof(WindowChrome),
        new PropertyMetadata(null, (d, e) =>
        {
            if (d is not ContentControl control) return;
            if (Managed.TryGetValue(control, out var previous)) { previous.Dispose(); Managed.Remove(control); }
            if (e.NewValue is WindowChrome chrome) Managed.Add(control, new Attachment(control, null, chrome));
        }));
    private bool _reverting;
    public event PropertyChangedEventHandler? PropertyChanged;
    public WindowChrome() { }
    public double CaptionHeight { get => (double)GetValue(CaptionHeightProperty); set { Validate(CaptionHeightProperty, value); SetValue(CaptionHeightProperty, value); } }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set { Validate(CornerRadiusProperty, value); SetValue(CornerRadiusProperty, value); } }
    public Thickness GlassFrameThickness { get => (Thickness)GetValue(GlassFrameThicknessProperty); set { Validate(GlassFrameThicknessProperty, value); SetValue(GlassFrameThicknessProperty, NormalizeGlass(value)); } }
    public Thickness ResizeBorderThickness { get => (Thickness)GetValue(ResizeBorderThicknessProperty); set { Validate(ResizeBorderThicknessProperty, value); SetValue(ResizeBorderThicknessProperty, value); } }
    public bool ShowSystemMenu { get => (bool)GetValue(ShowSystemMenuProperty); set => SetValue(ShowSystemMenuProperty, value); }
    public static Thickness GlassFrameCompleteThickness => new(-1);
    public static bool GetIsHitTestVisibleInChrome(UIElement inputElement)
    { ArgumentNullException.ThrowIfNull(inputElement); return (bool)inputElement.GetValue(IsHitTestVisibleInChromeProperty); }
    public static void SetIsHitTestVisibleInChrome(UIElement inputElement, bool hitTestVisible)
    { ArgumentNullException.ThrowIfNull(inputElement); inputElement.SetValue(IsHitTestVisibleInChromeProperty, hitTestVisible); }
    public static WindowChrome? GetWindowChrome(DockWindowControl window) => GetWindowChrome((ContentControl)window);
    public static void SetWindowChrome(DockWindowControl window, WindowChrome? chrome) => SetWindowChrome((ContentControl)window, chrome);
    public static WindowChrome? GetWindowChrome(ContentControl window)
    { ArgumentNullException.ThrowIfNull(window); return (WindowChrome?)window.GetValue(WindowChromeProperty); }
    public static void SetWindowChrome(ContentControl window, WindowChrome? chrome)
    { ArgumentNullException.ThrowIfNull(window); window.SetValue(WindowChromeProperty, chrome); }
    public static WindowChrome? GetWindowChrome(Window window)
    { ArgumentNullException.ThrowIfNull(window); Verify(window); return Native.TryGetValue(window, out var attachment) ? attachment.Chrome : null; }
    public static void SetWindowChrome(Window window, WindowChrome? chrome)
    {
        ArgumentNullException.ThrowIfNull(window); Verify(window);
        if (Native.TryGetValue(window, out var previous))
        {
            if (ReferenceEquals(previous.Chrome, chrome)) return;
            previous.Dispose(); Native.Remove(window);
        }
        if (chrome != null) Native.Add(window, new Attachment(null, window, chrome));
    }
    public static WindowChromeCapabilities GetAppliedCapabilities(Window window)
    { ArgumentNullException.ThrowIfNull(window); Verify(window); return Native.TryGetValue(window, out var a) ? a.Capabilities : WindowChromeCapabilities.None; }
    public static WindowChromeCapabilities GetAppliedCapabilities(ContentControl window)
    { ArgumentNullException.ThrowIfNull(window); return Managed.TryGetValue(window, out var a) ? a.Capabilities : WindowChromeCapabilities.None; }
    public WindowChrome Clone() => new() { CaptionHeight = CaptionHeight, CornerRadius = CornerRadius,
        GlassFrameThickness = GlassFrameThickness, ResizeBorderThickness = ResizeBorderThickness, ShowSystemMenu = ShowSystemMenu };
    private static void Verify(Window window)
    { if (!window.DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Window chrome requires the owning UI thread."); }
    private static DependencyProperty Property(string name, Type type, object value) => DependencyProperty.Register(name, type, typeof(WindowChrome),
        new PropertyMetadata(value, (d, e) => ((WindowChrome)d).Changed(e, name)));
    private void Changed(DependencyPropertyChangedEventArgs e, string name)
    {
        if (_reverting) return;
        try { Validate(e.Property, e.NewValue); }
        catch
        {
            _reverting = true;
            try { SetValue(e.Property, e.OldValue); } finally { _reverting = false; }
            throw;
        }
        if (e.Property == GlassFrameThicknessProperty && !Equals(e.NewValue, NormalizeGlass((Thickness)e.NewValue)))
        { SetValue(e.Property, NormalizeGlass((Thickness)e.NewValue)); return; }
        PropertyChanged?.Invoke(this, new(name));
    }
    private static void Validate(DependencyProperty property, object value)
    {
        if (value is double d && (!double.IsFinite(d) || d < 0)) throw new ArgumentOutOfRangeException(nameof(value));
        if (value is CornerRadius c && new[] { c.TopLeft, c.TopRight, c.BottomLeft, c.BottomRight }.Any(v => !double.IsFinite(v) || v < 0))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value is Thickness t && new[] { t.Left, t.Top, t.Right, t.Bottom }.Any(v => !double.IsFinite(v) || property != GlassFrameThicknessProperty && v < 0))
            throw new ArgumentOutOfRangeException(nameof(value));
    }
    private static Thickness NormalizeGlass(Thickness t) => t.Left < 0 || t.Top < 0 || t.Right < 0 || t.Bottom < 0 ? GlassFrameCompleteThickness : t;
    private static Attachment? FindAttachment(FrameworkElement element)
    {
        for (DependencyObject? cursor = element; cursor != null; cursor = VisualTreeHelper.GetParent(cursor))
            if (cursor is ContentControl c && Managed.TryGetValue(c, out var managed)) return managed;
        var window = WindowRegistry.Find(element);
        return window != null && Native.TryGetValue(window, out var native) ? native : null;
    }
    private sealed partial class Attachment : IDisposable
    {
        private readonly ContentControl? _control;
        private readonly Window? _window;
        private readonly IDisposable? _registration;
        private readonly PropertyChangedEventHandler _chromeChanged;
        private FrameworkElement? _root;
        private XamlRoot? _xamlRoot;
        private bool _disposed, _queued, _extendedBefore, _changedNative;
        private Thickness _savedBorder;
        private CornerRadius _savedCorner;
        private double _savedCaption;
        private Thickness _appliedBorder;
        private CornerRadius _appliedCorner;
        private double _appliedCaption;
        private bool _managedApplied;
        private global::Windows.Graphics.RectInt32[]? _nativeRegions;
        private Thickness? _nativeGlass;
        private double _nativeScale;
        internal WindowChrome Chrome { get; }
        internal WindowChromeCapabilities Capabilities { get; private set; }
        internal Attachment(ContentControl? control, Window? window, WindowChrome chrome)
        {
            _control = control; _window = window; Chrome = chrome;
            var weak = new WeakReference<Attachment>(this);
            _chromeChanged = (_, _) => { if (weak.TryGetTarget(out var self)) self.Invalidate(); };
            chrome.PropertyChanged += _chromeChanged;
            if (window != null)
            {
                _registration = WindowRegistry.Register(window);
                window.Activated += Activated; window.Closed += Closed;
            }
            if (control is LayoutFloatingWindowControl floating)
            { _savedBorder = floating.ResizeBorderThickness; _savedCorner = floating.CornerRadius; _savedCaption = floating.ChromeCaptionHeight; }
            BindRoot(); Invalidate();
        }
        private void Activated(object sender, WindowActivatedEventArgs e) { BindRoot(); Invalidate(); }
        private void Closed(object sender, WindowEventArgs e) { Dispose(); if (_window != null) Native.Remove(_window); }
        private void Loaded(object sender, RoutedEventArgs e) { BindRoot(); Invalidate(); }
        // LayoutUpdated covers moving/resizing interactive descendants, not just
        // changes to the root's extent. Apply caches quantized rectangles to avoid
        // feeding identical geometry back into the native layout loop.
        private void LayoutUpdated(object? sender, object e) => Invalidate();
        private void SizeChanged(object sender, SizeChangedEventArgs e) => Invalidate();
        private void RootChanged(XamlRoot sender, XamlRootChangedEventArgs e) => Invalidate();
        private void BindRoot()
        {
            var root = _control ?? _window?.Content as FrameworkElement;
            if (!ReferenceEquals(_root, root))
            {
                if (_root != null) { _root.Loaded -= Loaded; _root.SizeChanged -= SizeChanged; _root.RightTapped -= RightTapped; _root.PointerPressed -= PointerPressed; _root.LayoutUpdated -= LayoutUpdated; DetachGestures(_root); }
                _root = root;
                if (_root != null) { _root.Loaded += Loaded; _root.SizeChanged += SizeChanged; _root.RightTapped += RightTapped; _root.PointerPressed += PointerPressed; if (_window != null) _root.LayoutUpdated += LayoutUpdated; AttachGestures(_root); }
            }
            if (!ReferenceEquals(_xamlRoot, root?.XamlRoot))
            {
                if (_xamlRoot != null) _xamlRoot.Changed -= RootChanged;
                _xamlRoot = root?.XamlRoot;
                if (_xamlRoot != null) _xamlRoot.Changed += RootChanged;
            }
        }
        internal void Invalidate()
        {
            if (_disposed || _queued) return;
            var queue = _control?.DispatcherQueue ?? _window?.DispatcherQueue;
            if (queue == null) return;
            _queued = true;
            if (!queue.TryEnqueue(() => { _queued = false; if (!_disposed) Apply(); })) _queued = false;
        }
        private void Apply()
        {
            BindRoot();
            if (_control is LayoutFloatingWindowControl floating)
            {
                floating.ResizeBorderThickness = Chrome.ResizeBorderThickness; floating.CornerRadius = Chrome.CornerRadius;
                floating.ChromeCaptionHeight = Chrome.CaptionHeight;
                _appliedBorder = Chrome.ResizeBorderThickness; _appliedCorner = Chrome.CornerRadius;
                _appliedCaption = Chrome.CaptionHeight; _managedApplied = true;
                Capabilities = WindowChromeCapabilities.ManagedFrame | WindowChromeCapabilities.SystemMenu;
            }
            if (_window == null || _root?.IsLoaded != true || !AppWindowTitleBar.IsCustomizationSupported()) return;
            var title = _window.AppWindow.TitleBar;
            if (!_changedNative) { _extendedBefore = title.ExtendsContentIntoTitleBar; _changedNative = true; }
            if (!title.ExtendsContentIntoTitleBar) title.ExtendsContentIntoTitleBar = true;
            var scale = DesktopWindowCoordinates.Scale(_root);
            var regions = ChromeGeometry.CaptionRegions(new(0, 0, _root.ActualWidth, _root.ActualHeight), Chrome.CaptionHeight, Exclusions());
            // Round inward to avoid making an interactive pixel draggable through quantization.
            var pixels = regions.Select(r =>
            {
                var x = checked((int)Math.Ceiling(r.X * scale)); var y = checked((int)Math.Ceiling(r.Y * scale));
                var right = checked((int)Math.Floor(r.Right * scale)); var bottom = checked((int)Math.Floor(r.Bottom * scale));
                return new global::Windows.Graphics.RectInt32 { X = x, Y = y, Width = Math.Max(0, right - x), Height = Math.Max(0, bottom - y) };
            }).Where(r => r.Width > 0 && r.Height > 0).ToArray();
            if (_nativeRegions == null || !pixels.SequenceEqual(_nativeRegions))
            { title.SetDragRectangles(pixels); _nativeRegions = pixels; }
            if (_nativeGlass != Chrome.GlassFrameThickness || _nativeScale != scale)
            {
                NativeChrome.SetGlass(_window, Chrome.GlassFrameThickness, scale);
                _nativeGlass = Chrome.GlassFrameThickness; _nativeScale = scale;
            }
            Capabilities = WindowChromeCapabilities.NativeCaption | WindowChromeCapabilities.NativeResize | WindowChromeCapabilities.SystemMenu;
            if (SystemParameters2.Current.IsGlassEnabled) Capabilities |= WindowChromeCapabilities.Glass;
        }
        private List<DockRect> Exclusions()
        {
            var result = new List<DockRect>(); if (_root == null) return result;
            var stack = new Stack<DependencyObject>(); stack.Push(_root);
            while (stack.TryPop(out var node))
            {
                if (node is UIElement { Visibility: Visibility.Collapsed }) continue;
                if (node is FrameworkElement element && GetIsHitTestVisibleInChrome(element))
                {
                    var rect = element.TransformToVisual(_root).TransformBounds(new(0, 0, element.ActualWidth, element.ActualHeight));
                    result.Add(new(rect.X, rect.Y, rect.Width, rect.Height)); continue;
                }
                for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(node, i));
            }
            return result;
        }
        private ChromeHit Hit(Point p)
        {
            var border = Chrome.ResizeBorderThickness;
            var presenter = _window?.AppWindow.Presenter as OverlappedPresenter;
            return ChromeGeometry.HitTest(new(0, 0, _root!.ActualWidth, _root.ActualHeight), new(p.X, p.Y), Chrome.CaptionHeight,
                border.Left, border.Top, border.Right, border.Bottom, presenter?.IsResizable ?? true,
                presenter?.State == OverlappedPresenterState.Maximized || _control is LayoutFloatingWindowControl { IsMaximized: true }, Exclusions());
        }
        private void RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (!Chrome.ShowSystemMenu || _root?.IsLoaded != true || Hit(e.GetPosition(_root)) != ChromeHit.Caption) return;
            SystemCommands.CreateSystemMenu((object?)_window ?? _control!).ShowAt(_root, new FlyoutShowOptions { Position = e.GetPosition(_root) }); e.Handled = true;
        }
        private void PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Handled || _root?.IsLoaded != true) return;
            var point = e.GetCurrentPoint(_root);
            if (!point.Properties.IsLeftButtonPressed && e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse) return;
            var hit = Hit(point.Position); if (hit == ChromeHit.Client) return;
            if (_control is LayoutFloatingWindowControl { NativeWindow: null } managed)
            { BeginManagedDrag(managed, hit, e); return; }
            if (_window == null || !OperatingSystem.IsWindows()) return;
            using var coordinates = new DesktopWindowCoordinates();
            if (NativeChrome.BeginOperation(_window, hit, coordinates.ToScreen(_root, point.Position))) e.Handled = true;
        }
        public void Dispose()
        {
            if (_disposed) return; EndManagedDrag(restore: true); _disposed = true;
            Chrome.PropertyChanged -= _chromeChanged;
            if (_root != null) { _root.Loaded -= Loaded; _root.SizeChanged -= SizeChanged; _root.RightTapped -= RightTapped; _root.PointerPressed -= PointerPressed; _root.LayoutUpdated -= LayoutUpdated; DetachGestures(_root); }
            if (_xamlRoot != null) _xamlRoot.Changed -= RootChanged;
            if (_managedApplied && _control is LayoutFloatingWindowControl floating)
            {
                if (floating.ResizeBorderThickness == _appliedBorder) floating.ResizeBorderThickness = _savedBorder;
                if (floating.CornerRadius == _appliedCorner) floating.CornerRadius = _savedCorner;
                if (floating.ChromeCaptionHeight == _appliedCaption) floating.ChromeCaptionHeight = _savedCaption;
            }
            if (_window != null)
            {
                _window.Activated -= Activated; _window.Closed -= Closed;
                if (_changedNative && !WindowRegistry.IsClosed(_window))
                {
                    if (_window.AppWindow.TitleBar.ExtendsContentIntoTitleBar) _window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = _extendedBefore;
                    NativeChrome.SetGlass(_window, new(0), 1);
                }
            }
            _registration?.Dispose(); Capabilities = WindowChromeCapabilities.None; _root = null; _xamlRoot = null;
        }
    }
}
