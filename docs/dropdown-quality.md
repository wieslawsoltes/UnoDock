# Dropdown contracts and opening lifetimes

Preview 13 continues the independent port using the committed public metadata inventory
and public API documentation. No original implementation bodies, templates, resources,
artwork or fonts are used. These are product regressions and explicit Uno adapters,
not a certificate of original WPF event ordering or whole-product parity.

## Implementation

DropDownButton and DropDownControlArea now share an opening-session controller. It records
menu/root/context ownership before assigning a temporary row DataContext, because that
assignment can invoke application callbacks which replace the menu, close it, disable
its trigger, or request another context. Requests are serialized, version checked and
drained with a finite convergence budget. A replaced menu is not opened by stale work.

Context changes on the trigger update an active opening while retaining its menu rows.
Closed menus are not populated with temporary contexts. Application row local values
and bindings remain governed by MenuContext's ownership rules. On failure, cleanup
attempts both context release and native hiding and preserves multiple exceptions.

A weak shared-menu owner registry coordinates transfer between dropdown triggers.
Ownership is held through cleanup and hiding, so a second trigger opened by a cleanup
callback cannot have its newly assigned context cleared by the previous trigger. One
bounded dispatcher retry handles this reentrant handoff; there is no polling timer.
Closed events check the current opening identity and native IsOpen state. Unload and
disable invalidate the current trigger's opening, not a later opening from another owner.

The additional OpenDropDown/CloseDropDown APIs use that same controller. ToggleButton
checked state follows the current opening and is revalidated after application Checked
callbacks. IsChecked alone is not an imperative opening API; use OpenDropDown or native
click input. ContextMenuEx and MenuItemEx retain their existing source/template behavior.

## Input extension points

DropDownControlArea exposes protected virtual OnMouseRightButtonDown and
OnPreviewMouseRightButtonUp with the existing DockMouseButtonEventArgs adapter. They
receive the actual pointer identifier, changed button, native event and coordinates.
The release stage opens the menu only when not handled; a subclass may omit the base
call or set Handled to prevent the default opening. Right-button transitions delivered
as PointerMoved during a chord are included. Mouse RightTapped is suppressed when that
same pointer protocol has already handled opening. Touch/pen RightTapped retains the
native fallback path.

The context-menu key and Shift+F10 open at the focused area; Escape dismisses an opening.
These are control-local compatibility stages, not a fabricated WPF tunnel. The methods
are virtual additions on Uno UserControl rather than overrides of WPF's UIElement
methods. This remaining signature distinction stays visible in the API comparison.

## Sample

Open **Menu quality**, then **Dropdown contracts**. Two compact dropdown buttons and a
right-click area share a real native menu. The laboratory demonstrates context transfer,
live context change, right-release vetoes, enabling/disabling, RTL, document float/dock,
and closing the containing document. The log and retained editor show the lifecycle.
This adds interaction coverage; it does not claim new pixel-identical screenshot evidence.

## Acceptance

DropDownQualityTests runs inside the actual Uno gallery on Linux and the Windows Skia
host. It covers scoped contexts, live changes, application-owned row values, callbacks
which replace or close menus during preparation/opening, failure cleanup, shared-menu
transfer, repeated openings, disable/unload and reattachment, detached triggers, checked
state rejection and click overrides. Linux additionally opts into XTEST right-button
normal/veto sequences and the native context-menu-key/Escape sequence.

Use the workflow result and JSON/JUnit artifact for the exact consumed revision. The
local execution service was unavailable during this continuation; SDK/XAML builds and
runtime execution are performed by GitHub Actions, not claimed from a local harness.
No dependency mapping, metadata comparator or regression baseline is relaxed.

## Remaining boundaries

The shared owner registry coordinates these dropdown triggers; an arbitrary application
calling ShowAt directly on the same menu bypasses that registry. Native cancellation of
FlyoutBase.Hide by application Closing handlers follows the platform, and is not a WPF
ContextMenu-equivalence guarantee. Arbitrary custom templates, complete routed-event and
command semantics, original dropdown event ordering, cross-root context menus, mobile
input and screen-reader acceptance require further evidence. Full API/behavior/visual
parity remains unverified; NuGet publishing is a separate release action.

## Public contract references

- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Controls.DropDownButton~DropDownContextMenu.html
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Controls.DropDownControlArea~OnPreviewMouseRightButtonUp.html
- https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.primitives.flyoutbase.showat

The pinned reference inventory, rather than documentation for separately licensed PLUS
features, defines the requested compatibility surface.
