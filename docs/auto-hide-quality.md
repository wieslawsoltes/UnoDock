# Auto-hide flyout behavior, layout and resizing (preview 11)

## Public observations and appearance

The independently authored `AutoHideWindowObservations` program constructs the pinned
original's public models, raises a public MouseEnter event on its realized anchor and
observes the public flyout/model values. It does not inspect method bodies, native
window internals, template definitions, resources, artwork or fonts. The eight raw
observations and exact reference/probe/artifact identifiers are recorded in
`contracts/visual-fixtures/auto-hide-window-observations.xml` and its provenance JSON.

For Left/Right, the original observed width is `max(AutoHideWidth, AutoHideMinWidth) + 6`;
for Top/Bottom the height uses the analogous values. The six-DIP splitter is outside
that stored content dimension. Unspecified dimensions (zero) use the model minimum
(100 DIPs in these scenes), not the former port's arbitrary 300/240-DIP fallbacks.
Hover opens the tool without making it active. All eight cases are replayed against
actual arranged Uno controls. The observation protocol is not original native-pointer
acceptance, and does not measure original animation or hover-delay timing.

The independent flyout now reserves a separate splitter row or column outside the
editor. Caption, content border and preview use the same compact palette/density
system as the rest of the workbench. The caption grows for larger fonts, respects
custom title templates and updates capability buttons without replacing the retained
editor presenter. Six PNG captures exercise four edges, RTL and an independent dark
palette, plus six client-only captures; these are scene-specific evidence, not a
pixel-identity assertion. The additional public screen probe captures the original
HwndHost pixels through a screen copy of its publicly reported bounds; it does not
inspect native internals. The original inactive caption has a compact gray presentation
and a dropdown beside its pin/close buttons. The independently drawn port now includes
that functional dropdown, a 16-DIP default caption, and explicit palette/density keys
`UnoDock.AutoHideTitleBrush` and `UnoDock.AutoHideTitleHeight`. Larger fonts expand the
caption, and explicit theme dictionaries keep ownership of their colors.

Screenshot review found two gaps missed by geometry checks: side-rail background relied
on parent window pixels, and the fixture's text-before-AcceptsReturn initialization lost
all but its first line on Uno. The docking grid now paints the resolved rail background;
owned sample text initializes multiline mode before content. Tests check opaque rail
pixels, all six text ink bands, the actual caption extent/color and override behavior,
and dropdown invocation through its automation peer. This does not force a style or
font onto application-owned editor controls.

## Deferred resizing and native input

`LayoutAutoHideWindowControl` uses the same composed `LayoutGridResizerControl` native
input/capture protocol as pane splitters. A gesture captures the model, root, manager,
host surface, side, flow direction, viewport, original stored size and minimum extent.
Only a bounded, noninteractive ghost moves during preview. It is hosted in the flyout
layer rather than clipped inside the original flyout, so it can travel across the
available client area. Editor and document geometry remain unchanged during the drag.

The result is solved from the initial extent, not accumulated changes or refreshed
measurements. All four sides and RTL are exercised. Minimums are respected unless the
available viewport itself is smaller; display clipping never silently rewrites saved
sizes. Zero displacement preserves an unspecified zero. A committed size survives
close/reopen and uses the model's ordinary XML persistence path.

Normal release commits one dimension inside a root-update batch. Escape, explicit
CancelDrag, capture loss, disabled/unloaded controls, a changed viewport or flow,
root replacement, a removed/disabled/pinned tool and competing dimension/minimum edits
invalidate the snapshot without applying it. Native completion uses the shared
release-evidence and generation checks, including the one-dispatch-turn deferral
needed for Uno capture-loss ordering. No polling loop is introduced.

Session state, subscriptions and the ghost are released before notifying model
observers. A throwing observer rolls back only a value still owned by that operation;
an independent application replacement wins. Original and rollback exceptions are
retained. This is not a transaction for arbitrary external application side effects.
Keyboard resizing consumes the movement axis only, through the shared native splitter.

## Hover, focus, menus and lifecycle

Pointer hover opens without stealing model activation. Click and keyboard invocation
remain explicit activation paths. Focus entering the editor activates the tool. The
close timer now calls the actual protected `HasFocusWithinCore` extension point and
retains the flyout while it contains focus, the pointer, an active resize or an open
context menu. Focus leaving and menu closure rearm expiry when appropriate; an old
queued focus callback cannot affect a newer opening. The menu is retained across
ordinary title/model refreshes, and retention begins at Opening, before its animation.

Shared application menus resolve their context from the current flyout model and
release only temporary item data contexts. Application-owned values and bindings are
not overwritten. Reusing the host for another tool clears the old model association.

A host-close callback can reopen or redirect the retained control without the old
operation clearing its content or removing it from the surface. A thrown close observer
still leaves the empty view detached. Title-template selectors that replace the root
or redirect to another tool cannot continue rendering stale chrome. The authoritative
AutoHideWindow property is published after the internal state is ready, and cleared on
closure/unload. This changes the older preview's stale closed-host property behavior.

Applications can add their own focus-retention policy using the additive factory:

```csharp
public sealed class Workbench : DockingManager
{
    protected override LayoutAutoHideWindowControl CreateAutoHideWindowControl()
        => new OwnedAutoHideWindow();
}

public sealed class OwnedAutoHideWindow : LayoutAutoHideWindowControl
{
    public bool KeepOpenForOwnedPopup { get; set; }
    protected override bool HasFocusWithinCore()
        => KeepOpenForOwnedPopup || base.HasFocusWithinCore();
}
```

## Tests, gallery and limits

The new suite contains 64 Linux / 57 Windows cases: eight original observation replays,
four-sided LTR/RTL geometry, dedicated gutter separation, deferred commit, bounds,
clamp reversal, no-op semantics, stale model/root/viewport/capture cancellation,
throwing/reentrant callbacks, template redirection, shared menus, actual editor focus,
timer retention, keyboard axes, larger text and six captures. Seven Linux-only XTEST
cases exercise native hover/click, LTR/RTL resize release, Escape, capture loss,
disabling and unloading. Tests use actual arranged controls; public synthetic calls
in the remaining protocol tests are not presented as native pointer injection.

The gallery's **Auto-hide quality** document includes all four rails, editable tools,
minimum/default size settings, RTL, light/dark palettes, cancellation and XML inspection.
The library adds one resolved signature match by implementing the protected bounded
MeasureOverride on the flyout; the mappings/scanner/baseline are unchanged.

Remaining boundaries include original hover/animation timing, native HWND/HwndHost
inheritance and hosting hooks, arbitrary external popup ownership, touch/pen gestures,
UI Automation range providers, all mixed-DPI/native-monitor transitions and exact
pixel rendering. Native WinUI is package-built; Linux X11 and the selected Windows
Win32 suites are the runtime acceptance hosts. macOS and browser build/publish results
are distinct from device/input acceptance. The implementation is not a full-parity
certificate or a replacement for application-specific migration validation.
