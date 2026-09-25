"""One-use continuation from the previously committed, verified integration prefix.
Validate all remaining anchors in memory before writing any file.
"""
from pathlib import Path
pending = {}
def replace(path, old, new):
    text = pending.get(path, Path(path).read_text())
    if text.count(old) != 1:
        raise RuntimeError(f'{path}: expected one anchor, found {text.count(old)}: {old[:80]!r}')
    pending[path] = text.replace(old, new)

p = 'src/UnoDock/Controls/LayoutFloatingWindowControl.cs'
replace(p, '        else if (!_window.AppWindow.IsVisible && !_minimized) _window.Activate();',
'''        else if (!_window.AppWindow.IsVisible && !_minimized) _window.Activate();
        try { ConfigureNativeDragHost(); }
        catch (Exception error) { ReportFilterFailure(error); }''')
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
'''            // Keep the same non-hit-testable guides visible over the moving client.
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
for path, content in pending.items():
    Path(path).write_text(content)
    print(path)
print('All remaining integration anchors applied; no existing test assertions were changed.')
