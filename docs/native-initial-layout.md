# Initial native floating-window layout

The final XAML reconciliation includes a Linux/X11 initial-layout correction.
After native window activation and synchronous owner/chrome initialization,
`LayoutFloatingWindowControl.QueueInitialNativeLayout` queues one refresh for
that exact host. A closed, disposed or replaced host is not refreshed.

`DesktopWindowCoordinates.RefreshInitialNativeLayout` reads actual geometry and
attributes with checked XCB requests, then sends one StructureNotify notification
from its independent X connection. This addresses a host startup ordering in
which synchronous requests have moved ConfigureNotify into Xlib's buffered queue
before the event thread polls the socket. The Uno handler obtains live geometry;
this notification does not move, resize, activate or reparent a window and does
not synthesize pointer input. Native call failures use the existing diagnostic
path. Other desktop backends are unchanged.

The correction has its own actual-host initial-layout cases and remains subject
to the complete ordinary, native docking, custom chrome and both compiled-XAML
acceptance suites. Creating a workflow run, compiling a project, or satisfying an
assertion in isolation is not evidence that all platform acceptance completed.

## Integration boundary

The reconciled XAML implementation retains the `ChromeDensity` and per-item
binding definitions from the merged density work. Consumer dictionaries and
compiled chrome templates are additional presentation capabilities, not a
parallel ownership model or binding implementation. The source-backed property
inspector migration is a separate change so its behavior can be checked against
this reviewed baseline.

Windows input acceptance runs Uno Skia Win32. Native WinUI is compile/package
validated; AppKit tests do not claim physical pointer automation. Pure Wayland,
mixed-DPI hardware and untrusted runtime XAML remain outside this acceptance
contract. No release or package publication is performed by this change.
