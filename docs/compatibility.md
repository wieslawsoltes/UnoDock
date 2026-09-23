# Compatibility contract and remaining boundaries

**Preview 10 is not a certified 100% API, behavioral or visual replacement.** The
migration target is source compatibility with explicit WPF-to-Uno/WinUI mappings,
not WPF binary identity. Current sources and all earlier preview increments are
committed; historical candidate/unpushed notes do not describe this checkpoint.

## Implemented and exercised

The independent implementation includes nested document/tool models, root ownership
and cycle validation, selection and activation, docking/floating/auto-hide restoration,
cancellable operations, MVVM source handling, retained content/focus, commands, XML
persistence, native/in-surface hosts, scrolling headers, MRU navigation, input extension
hooks and public window lifecycle hooks. Native X11/Win32 coordinates and Win32 message
filtering have targeted real-host acceptance tests.

Stock-style pane chrome and original geometry replays are described in
[visual parity](visual-parity.md). The [navigator](navigator-quality.md) creates real
selectable containers with bounded reveal and stable identities. [Docking guides](docking-guides.md)
use retained independently drawn glyphs and the same validated plan for preview and
execution, including projection into native floating clients. [Splitters](splitter-quality.md)
now defer pane resizing until commit, render a moving ghost, preserve sizing units,
validate reentrant callbacks and cancel safely on capture/lifecycle invalidation.

## Resolved API accounting

The pinned reference is `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`. Release metadata
contains 105 public/protected type names and 1,031 declared entries; Debug and source
profiles are recorded independently. All reference type names have counterparts,
which does not imply equivalent inheritance or behavior.

The preview-9 baseline matches **977/1,031**: 881 declared members, 24 inherited
counterparts and 72 type shapes. Remaining diagnostics are 9 missing members,
12 signature differences and 33 type-shape differences; 18 attribute differences are
reported separately. The current CI comparison is the authoritative count for a
revision. No scanner/mapping/baseline rule is relaxed for visual or splitter increments.
The regression gate rejects newly unresolved contracts; the full strict gate remains
unsatisfied.

## Explicit framework mappings

| Original contract | Port boundary |
| --- | --- |
| System.Windows UI types | Microsoft.UI.Xaml equivalents; consumer source/XAML changes |
| Window-derived controls | Composed window lifecycle/native or in-surface hosting |
| WPF Thumb inheritance | WinUI sealed Thumb composition; public drag/cancel forwarding |
| HwndHost auto-hide | Managed control, not a general HWND hosting contract |
| ContextMenu/MenuItem | MenuFlyout/MenuFlyoutItem |
| WPF routed commands/events | Explicit-target commands and control-local compatibility input stages |
| IMultiValueConverter/MultiBinding | Independent converter-binding adapter, not a complete WPF binding engine |
| Binding.DoNothing | Distinct adapter sentinel; native WinUI bindings do not acquire WPF semantics |
| Resources/templates | Independently implemented Uno XAML; arbitrary WPF templates require migration |
| Microsoft.Windows.Shell | Targeted system commands/metrics and managed/native chrome |
| WindowChrome/Freezable | DependencyObject/Clone adapter, not general freezing/animation/thread transfer |
| FilterMessage | Implemented native Win32 subclass hook for floating hosts; not all HWND APIs |

See [input extensions](input-extensions.md), [window lifecycle](window-lifecycle.md)
and [window shell](window-shell.md) for exact extension and lifetime semantics.

## Evidence and limitations

The matrix has 120 portable core cases, 2,012 Uno/Linux runtime cases, a 243-case
Windows subset and 23 Python comparator cases. Linux includes 32 opt-in XTEST input
sequences. Actual JSON/JUnit and workflow conclusions establish what executed and
passed on the exact revision; counts are not an equivalence score.

Original public probes supply 13 XML layouts, defaults, converter observations, pane
and guide geometry, navigator client rendering and 32 splitter protocol scenarios.
The observation method is labeled: synthetic routed events or Win32 moving messages
are not represented as original end-to-end pointer input. Input tests on the port
are reported separately. [Provenance](clean-room.md) records the declaration/metadata/
public-observation boundary; this is not a legal opinion or a staffed two-team claim.

Open differences include the navigator's staged direct selection setters (the original
setter closes the host), cancellation behavior in the synthetic splitter completion
protocol, inherited type shapes, protected hooks and attribute contracts. The port
intentionally provides safe cancellation rather than reproducing unsafe observed
synthetic behavior. Default navigator rows are not virtualized. Guide half-pane previews
are not certified against every final constraint-resolved split rectangle.

Further acceptance is required for arbitrary event ordering and templates, custom
serialization extensions, nested/multi-star sizing, all drag/drop combinations,
Windows native WinUI runtime, macOS runtime, browser input, mobile/touch/pen, full
screen-reader and range-provider behavior, RTL variants, multi-monitor/DPI changes,
IME, localization, signing/notarization and workload-level performance equivalence.

Preview NuGet packages may be built. Publishing requires configured credentials or
trusted publishing and its own successful workflow. Stable 1.0+ requires an explicit
source-tree-bound full-compatibility attestation; none is supplied. See
[publishing](publishing.md) and [preview-10 validation](validation-preview10.md).
