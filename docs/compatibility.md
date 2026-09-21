# Compatibility contract and remaining boundaries

This preview is **not 100% API or feature compatible**. The intended migration target
is source compatibility with explicit WPF-to-WinUI mappings, not WPF binary identity.
Do not replace a production docking system without exercising the application's
layout fixtures, commands, custom styles, input, accessibility, and windowing paths.

## Implemented independently

The primary model hierarchy, ownership and cycle validation, selection/activation,
root/sides/hidden/floating collections, nested panels and panes, document/tool docking,
float/dock restoration, auto-hide groups, cancellable close/hide and transition events,
observable MVVM sources, content templates/styles, command adapters, XML layout
persistence/content callbacks, retained editors, split dividers, scrollable headers,
context menus, in-surface floating hosts, desktop native-window composition, navigator,
light/dark resource palettes, and the gallery exist as real implementations.

Behavior tests distinguish portable kernel assertions from tests run in a real Uno
application. Neither compilation nor the names in the API inventory prove all of the
above behave exactly as AvalonDock on every platform.

## Explicit platform mappings

| WPF contract | UnoDock mapping and consequence |
| --- | --- |
| System.Windows UI types | Microsoft.UI.Xaml types; consumer source and XAML must change |
| Window-derived floating/navigator/overlay controls | ContentControl plus native-window composition, or in-surface overlays |
| HwndHost auto-hide | Managed ContentControl; no HWND message/native-host contract |
| Thumb inheritance | ContentControl containing WinUI's sealed Thumb |
| ContextMenu / MenuItem | MenuFlyout / MenuFlyoutItem |
| RoutedEvent identity and bubbling | Explicit docking event IDs/handlers, not WPF routed event infrastructure |
| IValueConverter CultureInfo | Both CultureInfo overloads and WinUI language-string adapter |
| IMultiValueConverter/MultiBinding | Callable multi-value converter, no native WPF MultiBinding engine |
| Binding.DoNothing | UnsetValue fallback semantics; not identical to WPF suppression semantics |
| WPF resource and template syntax | Must be translated to Uno/WinUI XAML |
| Microsoft.Windows.Shell, WindowChrome, Freezable, Win32 hooks | No compatibility implementation in this preview |

## Remaining verification and implementation work

* Complete semantic API comparison: the baseline scanner is a syntax declaration
  inventory. It does not resolve framework inheritance, implicit constructors,
  attributes, all conditional-compilation profiles, or implicit enum ordinals.
  The exact declaration difference report is deliberately conservative; inherited
  equivalents may be reported as differences and require review.
* Additional original public controls, drop/overlay APIs, WPF-specific protected
  overrides, template-part/state contracts, automation peers, attached-property
  metadata, shell/chrome APIs, and custom theme compatibility are not all present.
* Native cross-window docking is incomplete. Uno flags ContentCoordinateConverter
  unimplemented on its backends; that implementation is therefore compiled only for
  native WinUI. The Uno/Skia default has no coordinate adapter. Native title-bar drag,
  OS snapping, monitor changes, DPI transitions, owner activation, and all drop targets
  require platform-specific validation and additional host integration.
* `AllowMixedOrientation` policy, `AnchorableShowStrategy.Most`, duplicate-content
  policy details, source resets with custom insertion strategies, and some legacy
  converter edge cases are not yet certified equivalent.
* XML roundtrips are tested within UnoDock. A corpus of original AvalonDock-produced
  fixtures, including legacy versions and every custom extension, is still needed
  before claiming lossless original-format interoperability.
* Full keyboard parity, native window chrome, touch/pen edge cases, screen-reader
  traversal, right-to-left layout, localization coverage, drag auto-scroll, and pixel
  comparisons remain acceptance work.
* Content caching is implemented; full tab virtualization and workload-based
  performance parity with WPF AvalonDock have not been established.
* Desktop/browser sample heads exist. Mobile sample heads, hardware/device tests,
  release signing, and notarization are not included.

The publishing workflow supports preview releases. Stable 1.0+ releases require an
explicit owner-maintained compatibility attestation; the repository does not supply
an attestation claiming these boundaries are closed.
