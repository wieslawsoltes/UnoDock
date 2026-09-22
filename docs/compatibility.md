# Compatibility contract and remaining boundaries

This preview is **not 100% API or feature compatible**. The migration target is source
compatibility with explicit WPF-to-WinUI mappings, not WPF binary identity. Applications
must validate their own layouts, commands, custom styles, input, accessibility and
windowing paths before replacing an existing docking system.

## Preview 3 evidence

The resolved metadata gate matches 884/1,031 reference entries, with 147 unresolved
signature/type diagnostics and 18 attribute differences. A reference/mapping-bound
baseline rejects newly unresolved entries; it does not approve full parity. The prior increment
added reentrancy-safe transitions/source reconciliation, lazy editor presenters,
shared menus, model diagnostics and a gallery parity lab. Preview 3 adds input-tested Linux/X11
native docking, destination-window previews, topmost-window occlusion, hidden-index
header insertion and stationary-pointer edge scrolling. See interaction.md and parity-progress.md.

## Implemented independently

The solution contains an actual layout/model hierarchy, ownership/cycle validation,
selection/activation, root/sides/hidden/floating collections, nested panels/panes,
document/tool docking, float/dock restoration, auto-hide/pin groups, cancellable
close/hide events, observable MVVM sources, content templates/styles, command adapters,
XML persistence/content callbacks, retained editor presenters, split dividers,
scrollable headers, context menus, in-surface floating hosts, desktop native-window
composition, MRU navigation, independent light/dark palettes and an interactive gallery.

The original public defaults and serialized fixtures have exposed and driven corrections
to horizontal orientation defaults, nullable titles, floating/auto-hide default sizes,
empty-pane capability values, the auto-hide timeout and mixed-orientation policy.
Display-size fallbacks are kept separate from persisted zero-valued model defaults.
Same-pane tab insertion uses boundary indices, avoiding forward-move off-by-one errors.

`CanMove`, `CanRepositionItems` and position-sensitive mixed-orientation checks are shared
between docking operations, command enablement and drag previews. These specific tests
are implemented; exhaustive original behavioral equivalence is not asserted.

## Explicit framework mappings

| WPF contract | UnoDock mapping and consequence |
| --- | --- |
| System.Windows UI types | Microsoft.UI.Xaml types; consumer source and XAML change |
| Window-derived floating/navigator/overlay controls | ContentControl plus native-window composition or in-surface overlays |
| HwndHost auto-hide | Managed ContentControl; no HWND message/native-host contract |
| Thumb inheritance | ContentControl containing WinUI's sealed Thumb |
| ContextMenu / MenuItem | MenuFlyout / MenuFlyoutItem |
| RoutedEvent identity and bubbling | Explicit docking event IDs/handlers, not WPF routed-event infrastructure |
| IValueConverter CultureInfo | CultureInfo overloads and WinUI language-string adapter |
| IMultiValueConverter/MultiBinding | Callable multi-value converter; no native WPF MultiBinding engine |
| Binding.DoNothing | UnsetValue fallback; not identical suppression semantics |
| WPF resources/templates | Require Uno/WinUI XAML translation |
| Microsoft.Windows.Shell, WindowChrome, Freezable and Win32 hooks | No compatibility implementation in this preview |

## Evidence and its limits

The pinned declaration scan has 996 entries. A separate resolved PE metadata scan
records 105 exported types, 1,031 Release API entries and 1,020 Debug entries, including
attributes, enum values, implicit public constructors and inheritance information.
Both profiles are scanned twice and compared byte for byte. This addresses limitations
of the initial syntax-only reference inventory; it does not complete the counterpart
mapping or missing implementation APIs.

The suite contains 61 portable tests, 36 Uno runtime/control tests, 44
interoperability/default/policy tests, 40 drop/menu/automation tests and 33 lifecycle
tests, 25 interaction tests, 1,517 converter/binding cases and 16 coordinate tests
(six cases use opt-in server-generated pointer/keyboard input).
The metadata gate has 23 additional Python regression tests. Interoperability cases consume 13 original public-serializer
layouts and defaults for 13 original types. Original random container IDs are normalized
without breaking PreviousContainerId links. No original algorithm is translated.

Original import checks cover IDs, titles, capabilities, dimensions, timestamps,
selection, hidden/auto-hide restoration, floating child element names and directional
AddToLayout topology with/without Most. Invalid input is checked for atomic failure.
The public-default tests check scalar CLR properties; the fixture also captures
reference dependency-property metadata, whose complete cross-framework mapping is
still outstanding.

## Remaining implementation and acceptance work

* Close the resolved comparison's remaining diagnostics: Windows.Shell/native message
  contracts, legacy protected input/focus/initialization slots, remaining template,
  converter, type-shape and attribute mappings. Drop/overlay contracts and selection/
  invoke peers are implemented in preview 2, but do not imply full WPF infrastructure
  or comprehensive assistive-technology acceptance.
* Native cross-window docking on non-X11 Skia hosts. Linux/X11 client transforms, input
  capture, previews and stacking order are tested; native WinUI has coordinate conversion.
  Skia Win32 client/screen conversion is implemented but still needs native input
  acceptance. macOS/custom hosts need ICrossWindowCoordinates integration.
  Native title-bar dragging, OS snapping, monitor/DPI transitions, owner activation and
  every cross-window target are not validated across all hosts.
* Broader original behavioral fixtures: event ordering/reentrancy, duplicate-content
  policy, custom insertion strategies/source resets, converter edge cases, legacy XML
  versions/custom extensions, and nested docking policy combinations.
* Full keyboard parity, touch/pen delivery, screen-reader traversal, right-to-left
  layout, localization breadth and pixel-level visual comparisons. Portable edge scrolling
  is implemented, but exhaustive gesture/device combinations are not validated.
* Full tab virtualization and workload-based performance parity. Retained content is
  implemented, but that is not a performance-equivalence certificate.
* Android/iOS sample heads and device tests, release signing and notarization.

NuGet publishing supports previews. Stable 1.0+ requires an owner-maintained explicit
compatibility attestation bound to the source-tree fingerprint. No attestation claiming
these boundaries are closed is included.
