# Multi-pane docking and sizing

The layout remains an AvalonDock-style model (root, panels, pane groups and panes)
rendered by Uno controls. DockWidth/DockHeight are GridLengths and DockMinWidth /
DockMinHeight remain minimum constraints, not alternative preferred dimensions.
The original two-pane splitter protocol observations and their assertions are
unchanged by this increment.

## Mixed fixed/star resizing

A star weight competes with every star in its grid, not just the two panes beside
the splitter. Converting one resized star to `newPixels / pairPixels` discarded its
relationship to untouched siblings; a 30-DIP resize could move an unrelated pane
by hundreds of DIPs. The resize now captures the entire displayed axis and uses
an unaffected, unconstrained star's weight-to-arranged-pixel scale to calculate
new endpoint weights. Unrelated sibling weights are never rewritten.

If every outside star is at its minimum, the scale keeps those minima binding.
An unconstrained star/star pair keeps the existing weight-plus-displacement rule
so ordinary layout rounding does not introduce a drag-start jump. When endpoint
minima mean its weights no longer describe the observed pixel ratio, the pair is
rebased to the requested actual sizes. Zero-weight stars with nonzero minima can
therefore be expanded. The two-pane mixed-unit reference behavior is retained
when there are no competing outside stars.

The preview still defers writes until release. Absolute range automation uses
actual requested pixels; pointer/keyboard operations use displacement. Unrepresentable
new star weights are rejected before publishing either model endpoint. This is
not a new layout allocator or a change to Uno's impossible-minimum arrangement.

## Resize validity and callbacks

The snapshot includes each displayed pane's length, minimum, arranged size and
parent version. A batched sibling edit or detach-and-return cannot silently reuse
old ratios even before the root's deferred Updated event is delivered. Size changes
also invalidate an unfinished preview before another move/release.

After model setters invoke application callbacks, peer length/minimum/ownership
checks participate in the existing two-endpoint transaction. A competing sibling
edit aborts publication and restores only still-owned endpoint values. The
application edit itself survives. Existing exception aggregation, cancellation,
root replacement, RTL and native capture behavior remain unchanged.

## Orthogonal edge docking

A wrapper inserted to split a pane on the perpendicular axis now inherits the
replaced slot's DockWidth, DockHeight and minima. It no longer substitutes default
1-star/25-DIP metadata and redistributes unrelated outer panes. Inner content,
pane identity, compatible group types and the ownership collection remain as
before. Both pixel-sized and star-sized outer slots are covered.

## Validation

The `docking-sizing` actual-host suite adds 52 named cases across horizontal and
vertical axes and left-to-right/right-to-left layouts. It covers mixed endpoints,
minimum-bound stars, zero star weights, outside constraints, absolute automation,
batched peer changes, parent-version ABA, callback edits and orthogonal slot
retention. Internal splitter protocol tests are identified as such; automation
cases call the real native range provider. Existing native pointer, original
splitter, accessibility, docking-guide and full runtime suites remain enabled.

A complete local before/after run of the same 52 assertions produced 48 failures
on the merged predecessor and none after these fixes; four already-correct
minimum-bound-peer cases passed on both. The tests reuse one native window so
native window startup does not become a per-case timing precondition. Earlier
interrupted local runs are not counted as completed evidence.

This increment does not assert complete WPF, AvalonDock binary/pixel or arbitrary
layout parity. Native WinUI runtime, physical AppKit pointer input, pure Wayland,
mixed-DPI hardware and general large-data-table behavior remain separate coverage.
No packages are published by these changes.

Public contract references:
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/AvalonDock.html
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Layout.LayoutPositionableGroup%601~DockMinWidth.html
