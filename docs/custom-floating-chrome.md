# Custom floating tool and document title bars

Native floating windows now default to a custom UnoDock client caption. This
replaces OS decorations; it is not an extra caption below the system title bar.
Both documents and complete tool groups retain their existing native window,
editor instances, layout ownership, caption-drag docking and Uno semantic theme.

```csharp
using UnoDock;
using UnoDock.Themes;

var manager = new DockingManager
{
    Theme = new FluentTheme(),
    FloatingWindowMode = FloatingWindowMode.Native,
    FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.Custom
};

// Explicit compatibility mode, also changeable while windows are open:
manager.FloatingWindowTitleBarMode = FloatingWindowTitleBarMode.System;
```

In-surface floating windows continue using their managed captions. Invalid enum
values, including direct dependency-property writes, are rejected without leaving
an invalid mode. `LayoutFloatingWindowControl.IsCustomTitleBar` reports whether a
native adapter actually attached; it does not infer success from the requested
setting. Adapter errors are sent through the existing `MessageFilterFailed`
diagnostic event, outside native callbacks. An unsupported backend retains system
decorations rather than pretending that custom chrome was installed.

## Native contracts

Windows removes the overlapped presenter's title bar and border, handles
`WM_NCCALCSIZE` to expose the whole native frame to XAML, and constrains borderless
maximization to the nearest monitor's work area. Caption buttons are real XAML
buttons; no invisible OS caption-button regions are left over them. A per-window
subclass participates in the existing safe native message-hook lifecycle.

X11 preserves the existing `_MOTIF_WM_HINTS` property except for the decoration
flag/value it owns. It communicates with the actual native X11 window through a
checked XCB connection. Returning to System mode restores the previous property
only while it still matches the library's assignment. Pure Wayland is not treated
as X11 merely because it runs on Linux.

AppKit uses full-size content, a hidden transparent native title bar and hidden
standard traffic-light buttons. The titled style and original content view remain
in place to preserve keyboard, accessibility, zoom and miniaturization semantics.
Native movement by the background is disabled so it cannot compete with UnoDock's
caption drag. Frame operations convert AppKit's upward screen Y to a consistent
downward resize axis. Objective-C structure-return conventions are explicit for
ARM64 and x64; x64 runtime acceptance is separate from an ABI-aware implementation.

Decoration state and native resources are scoped to each floating host. A normal
mode change releases its lease and restores owned native state. Closing a native
host releases the lease without briefly repainting OS decorations before closure.
Application changes to native state are not unconditionally overwritten.

## Interaction

The caption exposes Dock, Minimize, Maximize/Restore and Close commands. Double
click toggles maximization; right click opens the existing system-command menu.
Close follows the existing vetoable model/host lifecycle. The caption follows the
manager's palette and active/inactive state. The eight independently hit-tested
resize grips use directional cursors and respect resizable presenter policy,
minimum/maximum dimensions and `ResizeBorderThickness`.

Resize uses the initial native rectangle and pointer position, not accumulated
rounded deltas. Left/top edges keep the opposite edge fixed at the size limit.
Escape restores an owned initial rectangle. Unloading, host closure, a mode
change, loss of capture or invalidated ownership ends the gesture. Late events
from a retired session cannot cancel or mutate a successor session. Native mouse
polling provides release/Escape detection without swallowing another window's
event queue, and touch/pen use the routed pointer capture path.

The existing caption-drag docking path remains responsible for preview guides,
whole-tool-group preflight, tab insertion, Control suppression and Escape.
Single-tab continuous tear-off is not introduced by this chrome change.

## Acceptance

`floating-chrome` supplies independent native geometry/decoration probes, real
host mode-switch and resize tests, plus explicitly enabled XTEST/SendInput cases.
The dedicated Linux job runs under Openbox: its unmodified owner must have an OS
title bar and its custom floating window must have zero frame extents. Bare Xvfb
cannot by itself establish that a window manager removed decorations.

Native WinUI is package/compile checked independently of Uno Skia Win32 runtime
input tests. macOS host/geometry checks are not physical mouse automation. CI
execution counts and the exact tested source revision are recorded in PR #10;
this document does not claim that a pending or failed run passed. Mixed-DPI
multi-monitor hardware, pure Wayland and macOS Intel remain separate validation.
