# Dropdown contracts and opening lifetimes

Preview 13 continues the independent port using the committed public metadata inventory
and public API documentation. No original implementation bodies, templates, resources,
artwork or fonts are used. These are product regressions and explicit Uno adapters,
not a certificate of original WPF event ordering or whole-product parity.

## Opening and context ownership

DropDownButton and DropDownControlArea share an opening-session controller. It records
menu/root/context ownership before assigning a temporary row DataContext, because that
assignment can invoke application callbacks which replace the menu, close it, disable
its trigger, or request another context. Requests are serialized, version checked and
drained with a finite convergence budget. A replaced menu is not opened by stale work.

Trigger context changes update an active opening while retaining its menu rows. Closed
menus are not populated with temporary trigger contexts. Application row local values
and bindings remain governed by MenuContext's ownership rules. ContextMenuEx source rows
are handled after their creation; a non-null explicit MenuDataContext has precedence over
trigger refreshes. On failure, cleanup attempts both context release and native hiding,
preserving multiple exceptions instead of discarding the original error.

A weak shared-menu registry coordinates transfer between dropdown triggers. Ownership
is held through native hiding and context cleanup, so a trigger opened by a cleanup
callback cannot lose its new context to the previous owner. A reentrant preparation
handoff has one bounded dispatcher retry; ordinary close handoff waits on native events.

## Native close sequencing

Actual Win32 regression tests exposed a platform race: IsOpen can become false before
native Closed dispatch finishes, and an immediate ShowAt can be ignored. The native
close chain can also dismiss another menu opened during that callback interval.

A UI-thread close queue now fences both same-menu reopening and replacement menus until
Closed has returned. Its waiting owner and menu references are weak. Only the latest
pending request wins; cancellation, disable, unload, menu replacement and later context
requests are checked through generation and current-state validation. A superseded
waiter cannot revive its old request through a DataContext refresh. No polling timer is
used. Opening cancelled before actual native showing gets a single dispatcher fence
because it has no native Closed event to await.

Closing handlers may veto Hide. When the native event reports cancellation and the
menu remains open, the controller retains that opening, its row contexts and checked
state. A second trigger cannot take ownership of the vetoed menu. Such a veto also
means disabling/unloading the trigger cannot promise unconditional native dismissal;
the application's explicit Closing decision wins until the menu actually closes.

The additive OpenDropDown/CloseDropDown APIs use the same controller as input. Checked
state is revalidated after application callbacks. IsChecked alone is not an imperative
opening API; use OpenDropDown or native click input.

## Input extension points

DropDownControlArea exposes protected virtual OnMouseRightButtonDown and
OnPreviewMouseRightButtonUp using the existing DockMouseButtonEventArgs adapter. They
receive the actual pointer identifier, changed button, native event and coordinates.
The release stage opens the menu only when not handled; a subclass may omit the base
call or set Handled to prevent default opening. Right-button transitions delivered as
PointerMoved during a chord are included. The corresponding mouse RightTapped and
ContextRequested paths cannot bypass the right-release veto. Touch/pen retain native
context-request fallback paths, but device acceptance is separate.

The pinned X11 host reports XK_Menu as VirtualKey.Menu, unlike its LeftMenu/RightMenu Alt keys. A host-checked adapter recognizes that distinction; native tests verify both Alt keys remain inert.

The context-menu key and Shift+F10 are supported through keyboard and native
ContextRequested paths; Escape dismisses the opening. These are control-local
compatibility stages, not a fabricated WPF tunnel. The mouse methods are virtual
additions on Uno UserControl rather than overrides of WPF UIElement methods. That
signature distinction remains visible in the API comparison.

## Sample

Open **Menu quality**, then **Dropdown contracts**. Two compact dropdown buttons and a
right-click area share a real native menu. The laboratory demonstrates context transfer,
live context changes, right-release vetoes, enabling/disabling, RTL, document float/dock,
and closing the containing document. Its bounded log and retained editor show the
lifecycle. This adds interaction coverage, not new pixel-equivalent screenshot evidence.

## Acceptance

DropDownQualityTests and DropDownTransitionTests run inside the real Uno gallery on
Linux and the Windows Skia host. The scheduled suite contains 62 common UI cases and
six opt-in Linux XTEST cases. Coverage includes context ownership and reentrancy,
preparation/opening callbacks, failure cleanup, shared-menu transfer, repeated opening,
disable/unload/reattachment, native close vetoes, cross-menu replacement, stale queued
requests, source-created rows, explicit menu contexts, checked-state rejection and
click overrides. Native input covers right-button acceptance/veto and the context-menu
keyboard path. The suite is also included in full Linux and Windows-acceptance runs.

Use the workflow conclusion and JSON/JUnit artifact for the exact consumed revision;
counts here describe registered tests, not an assertion about an unobserved run. Local
execution was unavailable during this continuation. Clean SDK/XAML builds and native
runtime execution are performed by GitHub Actions, not claimed from a local harness.
No dependency mapping, metadata comparator or regression baseline is relaxed.

## Remaining boundaries

The registry coordinates these dropdown triggers; arbitrary application ShowAt calls
bypass its ownership protocol. Full WPF ContextMenu event ordering, routed-command and
event infrastructure, arbitrary templates, cross-root menu migration, mobile/touch,
screen-reader traversal and pixel-level appearance require further acceptance. Existing
compact-menu visuals are retained, not newly certified equivalent. Full API, behavior
and visual parity remains unverified. NuGet publishing is a separate release action.

## Public contract references

- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Controls.DropDownButton~DropDownContextMenu.html
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Controls.DropDownControlArea~OnPreviewMouseRightButtonUp.html
- https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.primitives.flyoutbase.showat
- https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.contextrequested

The pinned AvalonDock inventory defines the requested surface, not separately licensed
PLUS features. Public Uno hosting code was inspected to diagnose native event timing;
no original AvalonDock algorithms were translated.
