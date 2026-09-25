"""One-use, fail-closed integration of reviewed partial classes into the baseline.
Run only on work/preview21-uno-theme-native-drag. No tests/baselines are rewritten.
"""
from pathlib import Path
import hashlib


def replace(path, old, new):
    p = Path(path)
    text = p.read_text()
    if text.count(old) != 1:
        raise RuntimeError(f"{path}: expected exactly one anchor ({old[:90]!r}), got {text.count(old)}")
    p.write_text(text.replace(old, new))

p = 'src/UnoDock/Internal/DockChrome.cs'
replace(p, 'var dark = manager.ActualTheme == ElementTheme.Dark && manager.Theme is not Themes.GenericTheme;',
'''var dark = DockThemeResources.EffectiveTheme(manager) == ElementTheme.Dark;
        if (manager.Theme is Themes.FluentTheme fluent) fluent.UpdateResources(manager);''')
replace(p, 'Brush B(string key, Brush fallback) => manager.Resources.TryGetValue("UnoDock." + key, out var value) && value is Brush b ? b : fallback;',
'''Brush B(string key, Brush fallback) => DockThemeResources.Brush(manager, key, key switch
        {
            "PaneBrush" => "LayerFillColorDefaultBrush", "HeaderBrush" => "SolidBackgroundFillColorBaseBrush",
            "InactiveTabBrush" => "ControlFillColorSecondaryBrush", "BorderBrush" => "ControlStrokeColorDefaultBrush",
            "ForegroundBrush" => "TextFillColorPrimaryBrush", "HoverBrush" => "SubtleFillColorSecondaryBrush",
            "PressedBrush" => "SubtleFillColorTertiaryBrush", "AccentBrush" => "AccentFillColorDefaultBrush",
            "ActiveTitleBrush" => "ControlFillColorInputActiveBrush", _ => key
        }, fallback);''')

p = 'src/UnoDock/DesktopWindowCoordinates.cs'
replace(p, 'public sealed class DesktopWindowCoordinates :', 'public sealed partial class DesktopWindowCoordinates :')
replace(p, 'private static class Xcb', 'private static partial class Xcb')
replace(p, '/// are device pixels; visual points are device-independent units in the supplied element.',
'''/// use the host's global coordinate space: physical pixels on Windows/X11 and
/// AppKit screen points on macOS. Visual points are device-independent local units.''')
replace(p, 'for native WinUI, Uno Skia Win32 and X11.', 'for native WinUI, Uno Skia Win32, X11 and AppKit.')
replace(p, 'Supply ICrossWindowCoordinates for embedded islands, macOS or other custom hosts.', 'Supply ICrossWindowCoordinates for embedded islands or other custom hosts.')
replace(p, 'var from = GetNative(source); var to = GetNative(destination);',
'''if (OperatingSystem.IsMacOS()) return point => FromScreen(ToScreen(source, point), destination);
        var from = GetNative(source); var to = GetNative(destination);''')
replace(p, 'var native = GetNative(source);\n        var origin = native switch',
'''if (OperatingSystem.IsMacOS()) return Internal.MacDesktopInterop.ToScreen(WindowFor(source) ?? throw Unsupported(), local);
        var native = GetNative(source);
        var origin = native switch''')
replace(p, 'var native = GetNative(destination);\n        var origin = native switch',
'''if (OperatingSystem.IsMacOS())
        {
            point = Internal.MacDesktopInterop.FromScreen(WindowFor(destination) ?? throw Unsupported(), screenPoint);
            return (destination.TransformToVisual(null).Inverse ?? throw new InvalidOperationException("Destination transform is not invertible.")).TransformPoint(point);
        }
        var native = GetNative(destination);
        var origin = native switch''')

p = 'src/UnoDock/DesktopWindowCoordinates.Native.cs'
replace(p, 'if (!W32.GetWindowRect(handle, out var frame) || !W32.ClientToScreen(handle, ref W32.Zero)) return false;',
              'if (!W32.GetWindowRect(handle, out var frame)) return false;')
replace(p, '        internal static NativePoint Zero;\n', '')

p = 'src/UnoDock/Shell/SystemCommands.cs'
replace(p, '    private sealed class State\n',
'''    internal static Window[] Snapshot()
    {
        var result = new HashSet<Window>(ReferenceEqualityComparer.Instance);
        lock (Windows)
            for (var i = Windows.Count - 1; i >= 0; i--)
            {
                if (!Windows[i].TryGetTarget(out var window)) { Windows.RemoveAt(i); continue; }
                if (window.DispatcherQueue.HasThreadAccess && !IsClosed(window)) result.Add(window);
            }
#if !WINDOWS
        foreach (var window in Uno.UI.ApplicationHelper.Windows.ToArray())
            if (window.DispatcherQueue.HasThreadAccess && !IsClosed(window)) result.Add(window);
#endif
        return result.ToArray();
    }
    private sealed class State
''')

p = 'src/UnoDock/Controls/LayoutFloatingWindowControl.cs'
replace(p, 'public abstract class LayoutFloatingWindowControl :', 'public abstract partial class LayoutFloatingWindowControl :')
replace(p, '''        var drag = new Thumb { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Opacity = .01 };
        drag.DragStarted += (_, _) => SetIsDragging(true);
        drag.DragDelta += (_, e) => MoveBy(e.HorizontalChange, e.VerticalChange);
        drag.DragCompleted += (_, _) => SetIsDragging(false);
        _caption.IsHitTestVisible = false; _title.Children.Add(drag); _title.Children.Add(_caption);''',
'''        InitializeCaptionDrag();
        _caption.IsHitTestVisible = false; _title.Children.Add(_dragHandle); _title.Children.Add(_caption);''')
replace(p, '''        // Single-document floating windows have no pane header. Their caption is a
        // docking drag handle; the remaining title area retains window movement.
        _caption.HorizontalAlignment = HorizontalAlignment.Left;
        _caption.PointerPressed += (_, e) =>
        {
            if (Model is LayoutDocumentFloatingWindow { RootDocument: { } document })
                document.Root?.Manager?.BeginDrag(document, _caption, e);
        };''',
'''        // One retained caption handle moves and docks the whole window. Pane
        // tabs retain their separate single-item tear-off interaction.
        _caption.HorizontalAlignment = HorizontalAlignment.Left;''')
replace(p, '''        _caption.IsHitTestVisible = Model is LayoutDocumentFloatingWindow;
        // Document captions are docking drag handles, not native window-move
        // regions. Keep both them and the caption buttons in the client area.
        Microsoft.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(_caption, _caption.IsHitTestVisible);''',
'''        _caption.IsHitTestVisible = false;
        var palette = DockChrome.Palette(manager);
        _frame.RequestedTheme = DockThemeResources.EffectiveTheme(manager);
        _caption.Foreground = palette.Foreground; _caption.FontSize = palette.FontSize;
        _caption.Margin = new Thickness(8, 3, 8, 3);
        foreach (var button in _title.FindVisualChildren<Button>())
        { button.Foreground = palette.Foreground; button.FontSize = palette.FontSize; button.MinHeight = 0; button.MinWidth = 0; button.Padding = new Thickness(7, 2, 7, 2); }''')
replace(p, '''        _frame.Background = DockVisuals.Brush(manager, "UnoDock.PaneBrush", "LayerFillColorDefaultBrush");
        _title.Background = DockVisuals.Brush(manager, "UnoDock.HeaderBrush", "ControlFillColorSecondaryBrush");
        BorderBrush = DockVisuals.Brush(manager, "UnoDock.AccentBrush", "AccentFillColorDefaultBrush");''',
'''        _frame.Background = palette.Surface;
        _title.Background = Contents.Any(c => c.IsActive) ? palette.ActiveTitle : palette.Header;
        BorderBrush = palette.Border;''')
replace(p, 'NativeWindowMessageHook.Attach(_window, FilterMessage, ReportFilterFailure)', 'NativeWindowMessageHook.Attach(_window, FilterNativeDragMessage, ReportFilterFailure)')
replace(p, '        _window.Activate();\n    }\n    private void OnNativeClosing',
'''        _window.Activate();
        try { ConfigureNativeDragHost(); }
        catch (Exception error) { ReportFilterFailure(error); }
    }
    private void OnNativeClosing''')
replace(p, '        if (!ReferenceEquals(sender, _window)) return;\n        _messageHook?.Dispose();',
'''        if (!ReferenceEquals(sender, _window)) return;
        ReleaseNativeDragHost(false);
        _messageHook?.Dispose();''')
replace(p, '    private void OnNativeChanged(AppWindow sender, AppWindowChangedEventArgs e)\n    {',
'''    private void OnNativeChanged(AppWindow sender, AppWindowChangedEventArgs e)
    {
        try { ObserveNativeCaption(e); }
        catch (Exception error) { FailCaptionDrag(error); }''')
replace(p, '    internal void HideHost()\n    {', '    internal void HideHost()\n    {\n        ReleaseNativeDragHost(false);')
replace(p, '        if (_hostDisposed) return; _hostDisposed = true; _closingHost = true;',
'''        if (_hostDisposed) return; _hostDisposed = true; _closingHost = true;
        ReleaseNativeDragHost(true);''')

p = 'src/UnoDock/Internal/DockSurface.cs'
replace(p, '        _docked.Background = DockChrome.Palette(Manager).Header;',
'''        _docked.RequestedTheme = DockThemeResources.EffectiveTheme(Manager);
        _docked.Background = DockChrome.Palette(Manager).Header;''')
replace(p, 'coordinates.TryGetTopmostRoot(this, point, out var hitRoot)', 'coordinates.TryGetTopmostRoot(this, point, out var hitRoot, _floatingDrag?.Window.NativeWindow)')
replace(p, '            if (inSurfaceOnly && window.NativeWindow != null || !Visible(window)',
'            if (ReferenceEquals(window, _floatingDrag?.Window) || inSurfaceOnly && window.NativeWindow != null || !Visible(window)')
replace(p, 'if (_disposed || _dragContent == null || _drag.State != DockDragState.Dragging)',
'if (_disposed || _dragContent == null || (_drag.State != DockDragState.Dragging && _floatingDrag == null))')
replace(p, '''        var source = _dragSource; var input = _dragInput;
        _dragSource = null; _dragInput = null; _dragContent = null;
        if (source != null)''',
'''        var source = _dragSource; var input = _dragInput;
        var floating = _floatingDrag; var generation = _floatingDragGeneration;
        var restore = floating?.IsCurrent == true;
        _floatingDrag = null; _floatingDragGeneration++;
        _dragSource = null; _dragInput = null; _dragContent = null;
        floating?.Dispose();
        ShowDragPreview(null);
        floating?.Window.EndFloatingDragCapture(this, generation, restore);
        if (source != null)''')
replace(p, '        }\n        ShowDragPreview(null);\n    }\n    internal void Reset()', '        }\n    }\n    internal void Reset()')

p = 'src/UnoDock/Internal/DockSurface.Guides.cs'
replace(p, 'if (plan != null) result.Add(new(plan,', 'if (AcceptFloatingPlan(plan)) result.Add(new(plan!,')
replace(p, '        return area == null ? null : LegacyDropPlan(content, point, area, Manager.DockingGuideMode == DockingGuideMode.GuidesOnly);',
'''        var legacy = area == null ? null : LegacyDropPlan(content, point, area, Manager.DockingGuideMode == DockingGuideMode.GuidesOnly);
        return AcceptFloatingPlan(legacy) ? legacy : null;''')
replace(p, '            var targets = guides.Where(g => ReferenceEquals(g.Plan.Target.FindParent<LayoutFloatingWindow>(), window.Model)).ToArray();',
'''            // Render the same non-hit-testable compass over the moving client;
            // otherwise its opaque contents hide the target's native overlay.
            if (ReferenceEquals(window, _floatingDrag?.Window))
            { window.ShowDropGuides(guides, plan, this, Manager); continue; }
            var targets = guides.Where(g => ReferenceEquals(g.Plan.Target.FindParent<LayoutFloatingWindow>(), window.Model)).ToArray();''')

p = 'src/UnoDock/DockingManager.cs'
replace(p, '_transitions.Add(content, true); var before = content.Parent; var root = content.Root;',
'_transitions.Add(content, true); var before = content.Parent; var beforeWindow = content.FindParent<LayoutFloatingWindow>(); var root = content.Root;')
replace(p, 'if (!ReferenceEquals(before, content.Parent)) { if (floating) RaiseFloatedEvent(content); else RaiseDockedEvent(content); }',
'if (!ReferenceEquals(before, content.Parent) || !ReferenceEquals(beforeWindow, content.FindParent<LayoutFloatingWindow>())) { if (floating) RaiseFloatedEvent(content); else RaiseDockedEvent(content); }')

p = 'src/UnoDock/Internal/MacDesktopInterop.cs'
replace(p, '        SendChild(parent, Sel("addChildWindow:ordered:"), child, 1);',
'''        Retain(parent); Retain(child);
        SendChild(parent, Sel("addChildWindow:ordered:"), child, 1);''')
replace(p, '            if (owner != 0) SendVoidObject(owner, Sel("removeChildWindow:"), child);',
'''            if (owner == 0) return;
            try { if (Send(child, Sel("parentWindow")) == owner) SendVoidObject(owner, Sel("removeChildWindow:"), child); }
            finally { Release(child); Release(owner); }''')
replace(p, '    [DllImport(ObjC, EntryPoint = "objc_getClass")]',
'''    [DllImport(ObjC, EntryPoint = "objc_retain")] private static extern nint Retain(nint value);
    [DllImport(ObjC, EntryPoint = "objc_release")] private static extern void Release(nint value);
    [DllImport(ObjC, EntryPoint = "objc_getClass")]''')
print('Reviewed integration completed without modifying any acceptance assertion or reference fixture.')
for path in sorted(Path('src').rglob('*.cs')):
    print(hashlib.sha256(path.read_bytes()).hexdigest(), path)
