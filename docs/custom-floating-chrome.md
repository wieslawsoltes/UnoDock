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

Windows removes the overlapped presenter's title bar and border and explicitly
clears residual HWND caption style bits. `WM_NCCALCSIZE` exposes the entire frame
to XAML; `WM_NCHITTEST` routes it as client input so invisible native resize or
caption regions cannot intercept XAML controls. Resizable/minimizable/maximizable
style bits remain under presenter policy. The native style-change hook prevents
presenter refresh from reintroducing a caption while the custom lease is active.
Borderless maximization uses the nearest monitor's work area. All hooks belong
to that window, use the existing native callback failure boundary, and are removed
before restoring owned decoration state when switching back to System mode.

X11 preserves the existing `_MOTIF_WM_HINTS` property except for the decoration
flag/value it owns. It communicates with the actual native X11 window through a
checked XCB connection. Returning to System mode restores the previous property
only while it still matches the library's assignment. Pure Wayland is not treated
as X11 merely because it runs on Linux.

The current Uno X11 presenter's `Restore` path reactivates a window but does not
remove its EWMH maximized flags. UnoDock supplies the missing `_NET_WM_STATE_REMOVE`
client message for both maximize axes before calling the presenter's restore.
The window manager remains authoritative for normal geometry and unrelated state.
Restoring a minimized window preserves its previous maximized/restored state.
Caption buttons, double-click and the control's system-command menu share this
corrected action path; they do not fake restored geometry while leaving the WM
maximized.

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
minimum/maximum dimensions and `ResizeBorderThickness`. Resize edges are physical
window edges, not reading-order content. The overlay measures its actual XAML-to-
client X axis and compensates for root RTL mirroring without changing the caption
or editor's flow direction. A zero-thickness side disables that edge and every
corner involving it; adjacent enabled edges remain reachable along their full
length. A press/release without displacement does not clamp a pre-existing frame
that lies outside current size constraints.

Resize uses the initial native rectangle and pointer position, not accumulated
rounded deltas. Left/top edges keep the opposite edge fixed at the size limit.
Escape restores an owned initial rectangle. Unloading, host closure, a mode
change, loss of capture or invalidated ownership ends the gesture. Late events
from a retired session cannot cancel or mutate a successor session. Native mouse
polling provides release/Escape detection without swallowing another window's
event queue, and touch/pen use the routed pointer capture path.

Each resize owns a scoped observer lease. Changes to size constraints, reading
order, resize borders, manager/control availability, source hierarchy or presenter
retire the old session immediately; changing a value back does not revive it.
Policy revocation keeps the latest native frame rather than restoring geometry
computed under a superseded policy. The current policy is also checked before
rollback, because capture loss can be raised before every property observer runs.
All observer, capture and clock cleanup is attempted even when another teardown
step fails. Retained input from another grip cannot act on a successor session
merely because the mouse pointer ID was reused.

Before another native write or an Escape rollback, an internal bounded frame
ledger checks the actual native rectangle against acknowledged and in-flight
requests. A distinct application/window-manager move or resize ends the gesture
without overwriting that frame. Windows requests are canonicalized to pixel edges,
X11 requests to its integer origin/size contract, and AppKit retains fractional
points. X11 may acknowledge position and size separately; the ledger accepts
those intermediate combinations only from adjacent owned requests. At most 64
unacknowledged requests are retained; exceeding that bound cancels instead of
building an unbounded queue. Geometry comparison cannot distinguish an external
assignment which exactly matches an allowed owned rectangle. It is not a claim
of native request provenance or arbitrary window-manager ordering support.

The existing caption-drag docking path remains responsible for preview guides,
whole-tool-group preflight, tab insertion, Control suppression and Escape.
Single-tab continuous tear-off is not introduced by this chrome change.

## Acceptance

The `floating-chrome` selector discovers four application-registered suites:
`floating-chrome-documents`, `floating-chrome-tools`,
`floating-resize-policy-documents` and `floating-resize-policy-tools`. Each runs
exactly once in a fresh native process with independent HWND/AppKit/Xlib geometry
and decoration probes. The original suites retain the complete 47-case actual-host
matrix plus 22 physical input cases on Windows and Linux. The policy suites add
32 actual-host cases plus 20 physical input cases on Windows and Linux. Thus the
dedicated gate requires at least 121 cases on those two platforms and 79 on AppKit;
these are requirements, not a statement that an unobserved run passed.

Policy acceptance verifies physical RTL edge placement independently of the grip's
configured action, all eight physical RTL resize directions, disabled corners,
no-motion behavior, policy/hierarchy/presenter revocation, foreign native geometry
and retained real pointer arguments from a retired grip. The core suite links the
actual internal frame-ledger source and tests delayed/split acknowledgement and
bounded request sequences, including 10,000 deterministic sequences. A click
waits for stable native/client/button geometry before injecting one press/release,
never click retries or direct command invocation.

The evidence gate is mutation-tested for all three platforms. It rejects missing
required names, duplicate/planned-but-unrun suites, failed/skipped cases, conflicting
process/JUnit counts and missing source revisions. Failure removes an old passing
summary. The four-suite total timeout allows additional suites; the existing
per-suite timeout and original acceptance requirements are unchanged. Linux uses
the distribution Openbox configuration explicitly so personal runner rules cannot
silently force test windows to be maximized or undecorated.

The dedicated Linux job runs under Openbox: its unmodified owner must have an OS
title bar and its custom floating window must have zero frame extents. Bare Xvfb
cannot establish that a window manager removed decorations and cannot execute a
window-manager maximize test. Those cases are omitted from the ordinary no-WM
suite but are required by name in the dedicated acceptance gate.

The original single-process chrome matrix reached Xvfb's native client limit
while repeatedly creating and destroying real hosts. Per-kind process isolation
bounds cumulative host-resource retention without raising that limit, suppressing
cases or retrying failures. It is not a claim that an underlying Uno/driver leak
has been diagnosed and repaired. The four-suite execution record and complete
JUnit evidence are checked independently of native process exit status.

Native WinUI is package/compile checked independently of Uno Skia Win32 runtime
input tests. macOS host/geometry checks are not physical mouse automation. CI
execution counts and exact tested source revisions are recorded in the respective
implementation PRs; this document does not claim that a pending or failed run passed. Mixed-DPI
multi-monitor hardware, pure Wayland and macOS Intel remain separate validation.
