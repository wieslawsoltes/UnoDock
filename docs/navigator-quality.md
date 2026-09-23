# Navigator rendering, interaction and extension quality (preview 8)

The navigator is independently implemented from public AvalonDock observations and
Uno's public control APIs. The reference probe contains only application-owned test
content, public constructor/property calls, public visual-tree traversal, and pixel
capture. No reference templates, resources, source bodies, IL or fonts are imported.

## What the live tests exposed

`LayoutItem` is a FrameworkElement but is a model/command adapter, not a visual row.
On the pinned Uno host, a plain ItemsControl can treat it as its own container and
insert a zero-height object without materializing the data template. Collection and
selection tests alone did not detect this. The default navigator now uses the public,
additive `NavigatorListBox`, which overrides item-container ownership and creates
actual selectable ListBoxItem containers. Tests verify row height, title realization,
container identity, actual scrolling, and real pointer selection.

Unsupported `ListBox.SelectionMode` and `ListBox.ScrollIntoView` calls are removed.
The ordinary single-selection model is retained. A selected container is measured
relative to its containing ScrollViewer; ChangeView applies the minimal bounded offset.
Deferred work carries session, selection and template-generation identities. A bounded
one-shot LayoutUpdated continuation handles delayed realization. Closure, retemplating,
replacement, removal and superseding selections invalidate pending work; there is no
permanent render-loop or scrolling timer.

## Stable data and reentrancy

The two public item collections and corresponding ItemsSource identities are retained
across ordinary navigation, activation timestamps, title changes, description changes
and unrelated layout refreshes. Structural changes reconcile identities and preserve
the existing session MRU prefix. Only new documents are sorted before appending. The
tool category retains its separately observed model order. Replacing a document array
does not replace an unchanged tool array, and vice versa.

The navigator maintains index maps rather than repeatedly converting/filtering arrays
for each arrow/Tab operation. This reduces application-side allocation and lookup work;
it is not a claim that the underlying ListBox lookup, layout, or rendering is O(1).
Default rows use a nonvirtualizing StackPanel. Complete UI virtualization and large-scale
performance equivalence remain separate work.

Selection publication guards framework feedback without discarding a different request
from an application callback. Queued requests are drained with a bounded convergence
budget. Root/session generations prevent callbacks from initializing or scrolling a
replacement workspace. Observer exceptions restore the transition guards. This does
not make arbitrary recursive application dependency-property callbacks universally safe.

Home/End move to a category boundary; Up/Down navigate within a category; Left/Right
switch categories with RTL-aware meaning. Empty categories preserve a valid selection
in the other category. A pointer commit revalidates the actual clicked row, not merely
the previously selected item. Existing Control-release/Enter commit, Escape cancellation,
MRU stability and editor focus restoration paths are regression-tested as well.

## Compact stock-style appearance

The pinned original's observed labels are `Active Tool Windows` and `Active Files`.
The default presentation now uses its compact two-column arrangement: a three-DIP
outer border, five-DIP inset, 54-DIP details band, approximately 24-DIP rows, and a
42-DIP bottom band. Width follows list/header content instead of a fixed 620-DIP dialog.
The stock light background and border are observed #F0F0F0 and #A0A0A0. Selection,
hover and focus use real framework controls rather than painted-only hit targets.

Explicit dictionary palettes are resolved as a coherent set even when RequestedTheme
or the OS mode disagrees. The Windows screenshots exposed a white-on-light mismatch
that logical selection tests had missed. Four explicit/requested light/dark combinations
now assert foreground, background and every rendered text brush; stock scene captures
also assert their expected text colors before writing images. Test fixtures isolate and
restore the owner's explicit Theme, not only RequestedTheme.

Title and description have independent lines. The captured original overlapped those
two text blocks; reproducing that defect is deliberately not an acceptance criterion.
Long details are trimmed within the width chosen by the lists. Increased font size
expands row and detail heights. Light/dark changes, label changes, accessibility names
and color overrides update existing controls without recreating their containers.

Application overrides are `UnoDock.NavigatorBrush`, `UnoDock.NavigatorBorderBrush`,
`UnoDock.NavigatorSelectionBrush`, `UnoDock.NavigatorSelectionBorderBrush`, and the
existing `UnoDock.FontSize`/foreground/hover palette keys. Call manager.Refresh() after
changing its resource dictionary. Label properties update their accessible names directly.

Reference evidence: original revision `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`, probe
revision `356ac7dd1ccf67baebc9c3c0097cd37b53955372`, Actions run `35817270444`. Four raw
navigator XML observations and provenance are committed under contracts/visual-fixtures.
PNG captures are validation artifacts, never product theme resources. The reference
XML explicitly records that direct public selection assignments close the original
native window; its arranged client visual remains available for observation afterward.

The current port continues to stage programmatic SelectedDocument/SelectedAnchorable
changes until an explicit navigator commit. That observed behavioral difference is
still open; the new screenshots/row tests are not a certificate of full public-setter
or original event-ordering equivalence. Reference geometry is in captured client-root
coordinates; it does not prove native-window RTL coordinate equivalence. Font metrics,
rasterization, native non-client surfaces and custom templates require separate review.

## Custom templates

Original `PART_DocumentListBox` and `PART_AnchorableListBox` names are retained.
Existing plain ListBox parts retain their selection plumbing and are not replaced or
silently restyled. For reliable realized model-adapter rows on Uno, use NavigatorListBox:

```xml
<ControlTemplate
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:dock="using:Xceed.Wpf.AvalonDock.Controls">
  <StackPanel Width="360">
    <dock:NavigatorListBox x:Name="PART_AnchorableListBox" MaxHeight="120" />
    <dock:NavigatorListBox x:Name="PART_DocumentListBox" MaxHeight="320" />
  </StackPanel>
</ControlTemplate>
```

Its inherited ItemTemplate, ItemsPanel and Template are available for application
customization. A custom virtualizing panel must realize its selected container; the
adapter does not guess an unrealized row's pixel offset. Full compatibility with arbitrary
WPF templates, WPF inherited types and routed-event infrastructure is not claimed.

## Validation

`NavigatorQualityTests` has 43 Linux cases (including two opt-in XTEST sequences), or
41 cases on Windows. It checks actual rows, stable collection/container identities,
structural updates, callbacks, scrolling, cancellation, custom parts, colors, density,
labels and five live screenshot scenes. The `Navigator quality` gallery laboratory
contains 3/40/200-document workspaces and live theme/RTL/density controls.

Local iteration uses current C# in a real Uno/X11 host with existing generated sample
resources. It does not replace the clean SDK/XAML CI builds. CI runs the new suite in
both the full Linux execution and the selected Windows acceptance set. Inspect the
exact revision's workflow and artifacts for current results. API accounting remains
separate: no comparator/mapping/baseline rule is relaxed for this visual increment.
