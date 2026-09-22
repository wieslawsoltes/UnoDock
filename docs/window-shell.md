# Window shell adapters

The independently implemented `Microsoft.Windows.Shell` namespace supplies the three
original public type names `SystemCommands`, `SystemParameters2`, and `WindowChrome`.
It adapts the public contract to Uno/WinUI instead of importing WPF implementation code.
This is source-level migration with declared type mappings, not a WPF binary replacement.

## Explicit command targets and lifetimes

```csharp
using Microsoft.Windows.Shell;

// Use the specific docking host, never Window.Current or ambient focus:
SystemCommands.MaximizeWindowCommand.Execute(null, floatingControl);
SystemCommands.RestoreWindow(floatingControl);
SystemCommands.CloseWindow(floatingControl);

// Window overloads also support ordinary Uno/WinUI native windows:
using var registration = SystemCommands.RegisterWindow(window);
SystemCommands.CreateSystemMenu(window).ShowAt(menuButton);
```

Close, maximize, minimize, restore and system-menu commands have stable identity and
`CanExecuteChanged` notification. Native commands use the target's OverlappedPresenter
and live-window state. Managed floating commands preserve content and restored bounds.
Floating close follows model cancellation rather than destroying the OS window first.
All execution and window registration must use the owning UI thread. CanExecute is
false for absent, unsupported, foreign-thread or previously observed closed targets.

The registry has weak window keys and weak lookup entries. Registration leases control
visual-root lookup; disposing the final lease does not discard the closed-state guard.
A closed observed window cannot become command-enabled merely by registering or querying
it again. Direct native-target commands also observe close lifecycle. This cannot infer
the past lifecycle of an arbitrary already-destroyed window never seen by the adapter.
Windows without registration can be targeted directly; visual-root routing on native
WinUI requires registration. This adapter does not implement WPF CommandBindings,
InputGestures, implicit command-target focus routing or the full routed-command engine.

## Chrome configuration

```csharp
var chrome = new WindowChrome
{
    CaptionHeight = 38,
    ResizeBorderThickness = new Microsoft.UI.Xaml.Thickness(8),
    CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
    GlassFrameThickness = new Microsoft.UI.Xaml.Thickness(0),
    ShowSystemMenu = true
};
WindowChrome.SetWindowChrome(floatingControl, chrome); // Managed frame configuration
WindowChrome.SetIsHitTestVisibleInChrome(captionButton, true);

// For native non-client behavior, explicitly attach to the native Window instead:
WindowChrome.SetWindowChrome(window, chrome);
var applied = WindowChrome.GetAppliedCapabilities(window);
WindowChrome.SetWindowChrome(window, null);
```

A ContentControl attachment configures a compositional/managed frame; it does not
silently change to native ownership when an application reparents that control. The
Window overload owns native caption regions and DWM margins while attached. Avoid
concurrently configuring those native properties through a second title-bar framework.
Detachment restores the previous ExtendsContentIntoTitleBar flag and removes the
adapter's glass; arbitrary preexisting custom drag rectangles or DWM margins are not
recoverable through the supported public APIs and are not claimed to be preserved.

On Windows hosts advertising title-bar customization, native caption regions are
computed in DIPs and quantized to physical pixels. Interactive descendants marked with
IsHitTestVisibleInChrome are subtracted. Moving/resizing those descendants, changing
XamlRoot scale, changing settings or activating the window invalidates regions; identical
quantized results do not reissue the native update. Document floating captions are
explicit client-area docking handles and are excluded along with their action buttons.
Noninteractive caption areas remain window-move regions.

The core geometry implementation uses clipped rectangle subtraction and deterministic
interval unions, with vertically coalesced output. It is not a raster mask, an approximate
bounding box or a sampling-based exclusion algorithm. Point probes are tests of the
algorithm, not the algorithm itself. Hit tests give interactive controls precedence,
then corners/edges, then caption; rectangles use half-open bounds.

Managed resize/move starts from initial bounds and applies cumulative displacement,
keeping the opposite edge fixed. Minimum/maximum sizes are enforced. Pointer release
commits; Escape, capture cancellation/loss, reattachment or chrome detachment restores
the initial rectangle. State is cleared before releasing capture to tolerate reentrant
capture-loss callbacks. Maximized frames do not start managed resize gestures.

Dependency-property writes reject invalid/non-finite caption, resize and corner values;
direct invalid SetValue writes roll back. Negative glass components normalize to the
complete-frame sentinel. Settings support cloning and sharing among hosts. Cleanup
restores only the last values actually applied by this adapter, not a newly queued value;
independent application overrides are retained. Pending dispatch work cannot modify an
already-detached host.

## OS measurements and capability boundaries

SystemParameters2 publishes a coherent per-UI-thread snapshot and notifies only changed
properties. Windows measurements use 96-DPI system metrics; colors/composition/theme
come from UISettings and Win32/DWM/theme APIs. UISettings notifications are marshaled
onto the dispatcher. Native WinUI high-contrast notifications also refresh the snapshot.
An explicit Refresh is available; complete WPF WM_SETTINGCHANGE/theme-message monitoring
is not implemented on every host. Non-Windows native-frame measurements are zero when
not available, not invented Windows-style frame dimensions.

The reported Windows caption-button rectangle derives from nominal system button metrics;
it is not a per-window measurement of modern title-bar button layout. Native corner radius
is not claimed to match WPF theme/glass heuristics. Applications needing exact reserved
caption bounds must use their window's title-bar geometry. Glass is Windows-only and
composition-dependent. Native arbitrary corner shapes and OS snapping/DPI transitions
still require platform-specific acceptance. `ShowSystemMenu` controls the managed caption
menu; it does not remove every OS-provided non-client system-menu gesture.

WindowChrome is a DependencyObject with Clone, not Freezable. Freezing, full animation,
thread transfer, WPF dependency-property inheritance and HWND-hosting remain unfinished.
Preview 5 adds floating-window FilterMessage subscriptions on Windows; see
[window lifecycle](window-lifecycle.md) for native callback safety and validation. Native non-client integration is
not advertised on platforms lacking title-bar customization. Mac/mobile/browser OS menus
and native metrics are not emulated by claiming unsupported capabilities.

## Gallery and regression evidence

The toolbar's **Window shell** command creates a laboratory document. It creates managed
or native floating specimens, edits caption/border/corner/glass settings, toggles close
protection and applies/detaches chrome. Commands target the specimen; capability and
measurement labels distinguish native behavior from managed behavior or unavailable data.

There are 36 core chrome tests and 36 Uno-host shell cases. Core coverage includes all
resize corners/edges, anchoring, clamps, invalid coordinates, half-open hit tests and
75,000 randomized caption/exclusion point probes. Host cases cover command identity,
thread affinity, cancelled close, closed-window lifetime, shared attachment, rollback,
queued detachment and caption exclusions. Two opt-in XTEST scenarios use actual pointer
events to resize a managed left border and cancel it by detaching chrome. They run with
the existing native docking/drag suite on a dedicated Linux/Xvfb display.

Windows native code is build-validated by the native WinUI package target; a successful
build is not a claim of Windows pointer, accessibility, glass-rendering or OS-menu runtime
acceptance. The dedicated pipeline artifacts record test results for the consumed commit.

## Public design references

- https://learn.microsoft.com/dotnet/api/system.windows.shell.windowchrome
- https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindowtitlebar.setdragrectangles
- https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.overlappedpresenter
- https://learn.microsoft.com/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea
- https://platform.uno/docs/articles/features/windows-ui-xaml-window.html
- Pinned declaration and PE contracts in contracts/avalondock-metadata-release.json.
