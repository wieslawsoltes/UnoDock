# Docking guide geometry, visuals and drop policy (preview 9)

This increment replaces the rectangle-only drag presentation with independently drawn
workspace-edge glyphs and a pane compass. Rendering, hit-testing and release share the
same `DockDropPlan` validation. It is not a certificate of complete AvalonDock parity.

## Observed stock geometry

The pinned original remains `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`. A public-API
probe creates application-owned document/tool layouts and captures the original overlay's
rendered pixels and publicly arranged rectangles. No template definitions, Path.Data,
resources, original implementation bodies or font files are inspected or imported.

Initial native-pointer attempts did not establish an original drag session on the
hosted Windows desktop. The successful observation explicitly injects the public
WM_ENTERSIZEMOVE / WM_MOVING / WM_MOVE / WM_EXITSIZEMOVE protocol into the floating HWND.
Its output is labeled `public-native-message-sequence-not-pointer-acceptance`. This
establishes an observed overlay state, **not original end-to-end input acceptance**.

The captured document-over-document scene has five compass cells in an **88-DIP**
three-by-three arrangement. Each cell is 88/3 DIPs; adjoining cells share exact edge
expressions. The tool-over-document scene adds four workspace glyphs: **32 x 29 DIPs**
at the left/right edges and **29 x 32 DIPs** at the top/bottom edges. It does not show
four additional outer tool glyphs, so those are explicitly opt-in in the port.

The raw XML and provenance are in `contracts/visual-fixtures/guides-*-message.xml` and
`docking-guides-provenance.json`. The successful reference run is
https://github.com/wieslawsoltes/UnoDock/actions/runs/35825988138 at probe revision
`5298acd6024c134925286fc80b83b04608a17304`. The reference ZIP SHA256 is
`828f517484498b3ac29113be7a9804cc9c6427133d1afa926397767ccac5fbe1`.

Live Uno tests compare the realized glyph rectangles against these observations with
one DIP tolerance and emit per-coordinate differences. Independent silver/blue vector
geometry approximates the stock window icons and connected bevelled compass. Capture
geometry is tested separately from raster appearance: these tests do not assert pixel
identity, font equivalence or arbitrary custom-theme compatibility.

## Input policy and migration

The new default is `DockingGuideMode.GuidesOnly`. A drop requires a visible glyph or a
visible tab/caption insertion surface. Blank pane body does not guess an insertion or
split. Existing direct `DockOperations` and `DockDropPlan` calls remain usable.

```csharp
manager.DockingGuideMode = DockingGuideMode.GuidesOnly;     // New default.
manager.DockingGuideMode = DockingGuideMode.GuidesAndEdges; // Glyphs plus preview-8 zones.
manager.DockingGuideMode = DockingGuideMode.EdgesOnly;      // Preview-8 rectangle-only zones.

// Additive, off by default: keep a tool as a tool beside a document pane.
manager.ShowDocumentPaneToolGuides = true;

// Optional larger pointer targets; stock default is 88 / 3 DIPs.
manager.Resources["UnoDock.GuideSize"] = 44d;
manager.Refresh();
```

This is an intentional preview input-policy change. Two historical broad-zone tests
now request `GuidesAndEdges` explicitly; their actual operation/veto assertions remain.
Changing the input mode or extended-target policy cancels an active gesture. Invalid
values, including direct invalid dependency-property writes, roll back before failure.

`GetDockingGuides(content, point)` exposes immutable live target snapshots in the same
surface coordinate space as `GetDropPlan`. `DockGuideTarget` contains the precise
`DetectionRect` and its validated `Plan`. `HitTest` is half-open and checks the current
plan capability. No nearest-target heuristic is used. Plans are re-resolved at release.

Targets are filtered for content ownership, CanMove, CanRepositionItems,
CanDockAsTabbedDocument and mixed-orientation policy. Workspace targets take precedence
over the pane underneath them. Optional outer tool targets execute tool-as-tool plans,
not document-tab insertion. A detached root or changed capability invalidates existing
snapshots without a mutation. `PreviewDock` cancellation remains effective on execution.

## Geometry and rendering

`UnoDock.Core.DockGuideLayout` has no UI dependency. It validates finite rectangles and
dimensions, clips the pane to its hosting client before centering, bounds all glyphs,
and eliminates overlaps. Targets that do not fit are omitted rather than becoming
ambiguous tiny hit regions. `CreateStock` preserves the measured rectangular workspace
aspect ratios at larger density settings; `Create` remains a configurable additive
square-glyph layout for custom presentations.

`OverlayWindow` is non-activating, does not capture pointers, and does not intercept
hit-testing. It retains glyph controls across hover/palette changes. A connected
backplate is used only for a complete adjacent stock compass, never over arbitrary
separated custom rectangles. The preview rectangle is below all guides. Hide/unload
releases plans, target references and glyphs. Duplicate target identities fail before
replacing the current overlay.

The optional `PART_DockingGuideCanvas` in a custom template receives the retained
adorners; reverting to the default template reparents them back. Custom templates are
not replaced or imported from the original. This additive part has no new original-API
attribute mapping; the reference metadata baseline is unchanged.

Palette overrides are `UnoDock.GuideBrush`, `UnoDock.GuideBorderBrush`,
`UnoDock.GuideAccentBrush`, `UnoDock.GuideFillBrush`, `UnoDock.GuideWindowBrush`,
`UnoDock.GuideTitleBrush` and `UnoDock.GuideSelectionBrush`. Explicit dictionaries and
requested/actual themes resolve as a coherent set; light/dark changes do not replace
the glyph containers. The dark styling is independently designed.

## Native windows and cancellation

For native floating targets, guide slots are bounded by the destination native client,
not clipped against the main window. Shared surface-coordinate hit rectangles are
projected into the destination overlay through `ICrossWindowCoordinates`. The source
capture remains on its original control. Unavailable/closed coordinate targets clear
the preview instead of retaining a stale one.

A guide-only release over blank content in another native docking client is not an
outside-workspace release. It leaves the source in place rather than unexpectedly
creating another floating window. Real XTEST coverage checks the visible native guide
rectangles, source parent, floating-window count and adorner cleanup for this case.

Escape/capture cancellation clears the main and floating overlays. Header insertion
continues to work, and the existing pointer-veto and native cross-window docking tests
remain active. Native drag/title-bar behavior, multiple-monitor DPI transitions, mobile
input and browser runtime acceptance are separate platform work.

## Validation and limits

The portable suite adds **23 cases**, including **30,000 deterministic randomized**
configurable/stock guide layouts. The guide host suite has **43 Linux cases** (three
opt-in XTEST sequences) or **40 Windows cases**, including two real native-window
projection/policy cases and two original geometry replays. Seven PNG/XML scenes cover
document/tool guides, active preview, dark, RTL and the two original scene equivalents.

The gallery's **Docking guides** laboratory includes strict/compatibility policies,
stock/large density, document/tool panes, permissions, optional extended targets,
light/dark, RTL, mixed orientations, floating and cancellable preview events.

The implementation metadata comparison remains **977 / 1,031**; **54 signature/type**
and **18 attribute** differences remain. No scanner, mappings or baseline were relaxed.
Default navigator setter semantics and virtualization remain open from preview 8.
The preview rectangle still uses the existing split solver; it does not predict every
minimum-size/custom-layout result exactly. Detailed original input traces, every glyph
hover/policy combination, inherited WPF types, arbitrary templates, accessibility,
performance parity and full platform acceptance remain unfinished.

Local iteration compiles current C# into an actual Uno/X11 host with existing generated
sample XAML/ICU resources. It is not a clean SDK/XAML build. The GitHub workflow performs
that build separately, with warnings-as-errors on all three desktop targets, Linux and
selected Windows runtime execution, browser publishing and native WinUI packaging.
Use the exact commit's CI reports for final acceptance, not only the test matrix above.
