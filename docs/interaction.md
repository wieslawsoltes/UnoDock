# Interaction compatibility — preview 3

This increment implements native Linux/X11 content dragging and portable header
scrolling. It does not certify full AvalonDock feature or API equivalence.

## Coordinates and hit testing

`DesktopWindowCoordinates` is the default `ICrossWindowCoordinates` provider. Same-root
transforms use the XAML visual tree. Native WinUI delegates to the existing
`ContentCoordinateConverter` adapter. Uno Linux/X11 resolves each `XamlRoot` through
`ApplicationHelper.Windows` and obtains its client ID from public `WindowHelper` /
`X11NativeWindow` APIs. It never treats `AppWindow.Id` as a native X11 window ID.

The converter snapshots source-to-root and root-to-destination transforms and queries
client-origin translation using checked XCB requests on a separately owned connection.
For a source point p, source/destination scales s/t and client-origin displacement d
in physical pixels, the intermediate destination point is `(s*p + d)/t`. Negative
monitor positions and fractional scale factors remain signed doubles. Bounds transform
all four corners, not just the two diagonal points. Native errors, detached roots,
nonfinite results and overflow fail closed rather than choosing an unrelated target.

X11 topmost-window detection descends the server's actual child stacking order at the
queried point. It handles window-manager reparenting without guessing decoration
sizes and rejects occluded panes, unrelated windows and excessive hierarchy depth.
Native X11 protocol coordinates are signed 16-bit; out-of-range requests are rejected,
not wrapped. Windows in another X11 screen are not cross-screen targets.

The adapter owns a lazy, reusable checked XCB connection and releases every reply/error
allocation. DockingManager disposes its default adapter but never an application-owned
replacement. No global Xlib error handler is installed, and stale window IDs cannot
terminate the process through Xlib's default handler. The optional
`IScreenWindowCoordinates` contract also exposes physical screen projections. Uno Skia
Win32 uses ClientToScreen on the public native HWND; Linux requires `libxcb.so.1`.

## Capture, preview and scrolling

The manager retains a single pointer owner for the whole drag. Captured events from
another native root are translated into manager-surface coordinates before hit testing.
Dropping re-evaluates the current plan instead of executing a stale painted preview.
Tool tabs can move into and out of native tool windows. A single floating document's
caption is its content-docking handle; the remaining chrome and OS title bar retain
window movement. OS title-bar drop/snapping integration is not implemented here.

A tab strip is an insertion surface and takes precedence over the pane's top split
region. Hidden document models still count toward the destination model index. Body
center drops append rather than using a header insertion index. In-surface activation
changes Canvas.ZIndex without reparenting controls or losing pointer capture.

The edge-scroll velocity is quadratic in edge proximity, capped at 900 logical units
per second. A dispatcher timer advances it even when no new pointer event arrives;
time steps are capped at 50 milliseconds after a stall. The edge zones shrink on
narrow viewports so they cannot overlap, and logical offsets reverse for RTL. Scroll
offsets clamp to the extent. Only the header viewport scrolls; editor-body drags do not.

Release, Escape, capture loss, pointer cancellation, source unload, layout reset and
manager disposal stop the timer and clear preview/capture state. Native feedback is
painted in the destination window, not outside the main window's compositor bounds.
Closing an owned X11 float unmaps its client before Uno's asynchronous rendering
teardown; Uno still owns actual destruction of render resources. This avoids a closing
window remaining a visible input occluder without destroying an in-use render surface.

## Tests

`InteractionGeometryTests` adds 16 portable cases, including 10,000 randomized
coordinate roundtrips and 10,000 randomized scrolling bound/direction checks.
`InteractionTests` adds 25 cases for headers, hidden indices, stack order, the public
SingleContentLayoutItem setter, native coordinates/moves/occlusion, preview placement,
close unmapping and input/capture cleanup. Five use XTEST-generated server input:
main-to-native tool drag, native-document-to-main drag, Escape cancellation,
stationary-hover scrolling and source removal during capture.

Native input injection is explicitly opt-in and belongs on a dedicated test display:

```sh
UNODOCK_SELFTEST=1 UNODOCK_NATIVE_INPUT_TESTS=1 \
  xvfb-run -a -s '-screen 0 2560x1440x24' \
  dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

The Linux CI and publishing-validation jobs enable this flag and install `libxtst6`.
Without it, normal self-tests do not synthesize input into the user's desktop. Browser,
Windows and macOS builds remain distinct from Linux runtime/input execution. Native
X11/WM variations, mixed physical-monitor DPI, touch and pen still need device testing.

## Remaining platform boundaries

Native WinUI and Skia Win32 coordinate conversion are implemented but have not received
the same input acceptance tests. macOS, embedded/non-X11 Linux and custom hosts require
`ICrossWindowCoordinates`; unsupported targets are omitted safely. Cross-manager
transfers remain rejected by the shared-root ownership policy. Full header
virtualization, arbitrary transformed tab insertion, WM snapping/title-bar dragging,
mobile heads and complete accessibility/input parity remain separate work.

## Primary contracts used

- Uno windowing and public native-window APIs:
  https://platform.uno/docs/articles/features/windows-ui-xaml-window.html
- XCB API / checked coordinate queries:
  https://xcb.freedesktop.org/manual/group__XCB____API.html
- XTEST server input semantics:
  https://xorg.freedesktop.org/archive/current/doc/libXtst/xtestlib.html

No AvalonDock implementation bodies, templates, artwork or resources were copied.
