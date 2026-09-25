# Uno semantic theme and native floating-window docking

This increment reviews the docking transaction, floating-host lifetime, drag input,
coordinate and stacking adapters, theme consumers, navigator/menu interactions,
and their acceptance tests. It is not a claim that every repository line has been
independently audited or that the complete AvalonDock port is finished.

## Theme contract

Use `manager.Theme = new FluentTheme()` for an Uno/WinUI semantic theme which
follows the owning manager's actual light/dark theme. Explicit
`new FluentTheme(ElementTheme.Light)` and `new FluentTheme(ElementTheme.Dark)`
remain coherent even when the containing window requests the opposite theme.
The gallery's existing Light and Dark choices use these themes. Its explicit
Generic choice and default sample selection retain the independently authored
classic palette. A null Theme retains the previous automatic legacy palette.

Fluent resolves pane/header/tab surfaces, borders, text, hover/pressed states and
accent from application semantic resources. Manager/ancestor/application
customizations take priority over system fallback. Native floating XamlRoots
follow their docking owner's effective theme. Brushes remain application-owned:
they are neither recolored nor cloned. Replacing a resource followed by Refresh
updates the existing views and does not rebuild editor buffers or docking models.
Explicit `UnoDock.*` brush overrides and explicit theme dictionary edits remain
authoritative. The compact title/tab/rail metrics and existing docking UX remain.

Resource lookup intentionally distinguishes a local dictionary search from Uno's
public TryGetValue system-resource fallback. Consulting the latter at each
ancestor would incorrectly hide application customizations. Legacy chrome keeps
its original inexpensive lookup path instead of walking application dictionaries.

## Moving and docking a floating window

The retained client caption is a whole-window drag handle. Both a floating
document and a tool window containing several split/tabbed panes can be moved,
preview docking guides over the target, and dock on release. Native title-bar
movement uses the same docking intent. Pane tabs retain their separate
single-item interaction. Initial tab tear-off still creates a new native host
on release; this increment does not claim a continuous tab-to-new-HWND transfer
while the original pointer remains held.

The target is recomputed on release; a stale hover cannot commit after the pointer
leaves the client. Control suppresses docking. Escape or canceled capture cancels
the gesture and restores geometry while the original workspace still owns it.
Release, disposal, hidden/replaced roots, and successor gestures invalidate old
work. Native move-loop completion is deferred until the OS exits its modal loop.

All tabs are preflighted, including PreviewDock cancellation, policy and ownership,
before library mutation. Edge docking moves the existing tool subtree intact;
inside docking preserves tab order and editor objects. Every transitioned content
receives its Docked event even if its immediate pane parent remains unchanged.
Application callbacks which transfer later contents during commit are respected,
not stolen back. Arbitrary application side effects are not transactionally
rolled back; whole-group preflight is not a universal database transaction.

The dragged source alone is excluded from native stacking hit testing. Other
application or foreign native windows still occlude a target. The existing
non-hit-testable guide overlay is also rendered over the dragged client, keeping
the compass visible rather than obscured by that client's opaque contents.

## Desktop adapters and units

Windows uses actual HWND screen/client transforms, z-order and cursor state,
scoped owner/tool-window styles, and WM_MOVING/WM_EXITSIZEMOVE lifecycle tracking.
The native WinUI target builds the package; runtime acceptance executes the Uno
Skia Win32 host, not a native WinUI application.

Linux uses checked XCB requests, root stacking and window-manager ancestry,
WM_TRANSIENT_FOR/utility metadata, pointer/button/modifier state and real native
client coordinates. The exercised protocol is X11. XWayland may supply that
protocol, but a pure Wayland compositor requires a host-specific implementation
of ICrossWindowCoordinates; unrestricted global positioning is not claimed.

macOS uses the actual NSWindow/content-view conversion, logical screen points
with bottom-left axes, and explicit conversion to Uno's top-left root coordinates.
The opaque native descriptor returned by the pinned Uno host is inspected only
for its audited handle property; AppWindow.Id is never interpreted as an NSWindow
pointer. Unknown native descriptor types fail explicitly rather than guessing.
Owned utility windows retain/release their AppKit owner relationship. A scoped
Core Foundation common-mode clock continues during native event tracking and
never allows managed exceptions to unwind across its native callback ABI.

Windows/X11 global coordinates are physical pixels; AppKit global coordinates
are logical screen points. Coordinate providers must keep one consistent global
space per desktop backend. Multi-monitor mixed-DPI hardware, macOS Intel execution,
pure Wayland, external UI Automation transports, and every possible third-party
native host remain separate acceptance boundaries.

## Acceptance and evidence

The ordinary CI retains all earlier suites and adds the new group-docking/theme
cases. Native desktop CI additionally exercises dedicated real XTEST and Win32
SendInput gestures. Win32 input covers both client captions and actual OS title
bars; there is no synthetic routed event or direct docking substitute in those
input cases. AppKit tests exercise real native windows, screen coordinates,
stacking, and the clock inside actual event-tracking mode; they are not a claim
of physical macOS pointer-drag automation.

The command-line AppKit test host explicitly activates its application before
checking utility windows which hide when that application is inactive. This is
fixture setup, not a product instruction to steal foreground focus.

Native process exit status is not sufficient evidence: an AppKit host was
observed terminating its native loop with exit zero despite failed assertions.
`tools/verify-native-results.py` requires the complete platform-specific JUnit
suite files, no failed/skipped/duplicate cases, and a minimum executed count.
Its independent Python regressions cover false-green, partial and corrupt results.
Failing runs remain failures and are not replaced by diagnostic reruns. Exact CI
run IDs and counts are recorded in the PR after final validation, not predicted here.

Original reference fixtures, API mappings and comparator thresholds are unchanged.
No original implementation, templates, artwork or fonts were imported. This work
does not claim full AvalonDock API/behavior/pixel equivalence or NuGet publication.
