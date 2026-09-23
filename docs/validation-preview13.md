# Preview 13: dropdown acceptance and validation scope

Base: `233f10d46b8896137e947de22bf0d712b593bb28` (preview 12).
Implementation history is on `work/dropdown-lifecycle-20260923`; the product is integrated
only after real SDK and native-host validation. Consult Build and test for the exact
consumed revision. No local-runtime result is claimed: the local execution service was
unavailable during this continuation, and validation runs in GitHub Actions.

## Registered coverage

The new dropdown suite has **66 common native-host cases** and **six additional Linux
XTEST cases**, totaling **72 Linux / 66 Windows**. It is part of both the ordinary Linux
all-suite run and Windows acceptance. The registered matrix is **2,335 core/Linux cases**
(including 120 portable cases), **430 Windows cases**, and 23 Python comparator cases.
Platform totals overlap. The counts are not a substitute for actual JSON/JUnit results.

Coverage includes retained menu/row contexts, callback-driven replacement and cancellation,
nested context changes, application-owned values, failure cleanup, disable/unload and
reattachment, native closing, checked-state rejection, click overrides, shared ownership,
source-created rows, explicit menu contexts, deferred cancellation, native closing vetoes
and throwing Closing handlers. The six native input cases cover right-button acceptance
and veto, the context-menu key/Escape, both Alt keys, and Shift+F10/Escape.

## Defects exposed by execution

Initial Windows native tests failed repeat opening, disable/re-enable, and shared-menu
handoff, although first openings worked. Public native events showed that IsOpen changes
before Closed completes and immediate ShowAt may be ignored. Per-menu fencing fixed
those tests; a later stronger test also checked that a replacement menu remained open
after the old close chain finished, exposing the need for a UI-thread closing fence.

The initial Linux keyboard case failed even after focus was independently established.
It reported the actual native key as Menu, not Application. The pinned public X11 mapping
uses Menu for XK_Menu and LeftMenu/RightMenu for Alt. The fix is restricted to an actual
X11 host; both Alt keys are independently exercised to prevent a broad shortcut regression.
ContextRequested is also supported rather than relying exclusively on KeyDown delivery.

Native cancellation and exceptions are distinct cases. Closing vetoes preserve the live
menu's ownership/context. A failed Closing callback preserves that scope for cleanup or
retry and cannot leave an unreleasable queue entry blocking all later dropdowns. Original
and cleanup exceptions are retained. Deferred opens are weak and generation checked.

The focused acceptance run `35900472554` established all 68 then-registered Linux cases
passing, including the six native input scenarios, before the last four Closing-exception
regressions were added. Subsequent runs must validate the full 72/66 suite. This record
does not predeclare their result. Temporary branch integration/acceptance workflows are
removed from the final tree; ordinary CI contains the durable execution path.

## API and clean-room accounting

Resolved reference inventory: 1,031 entries at original revision
`2c71faba5eecc1b6ae6cd3d269408e0df37715d8`. The observed comparison remains **978 matched**,
**53 unresolved signature/type entries**, and **18 separately reported attribute differences**.
The added right-button methods are usable protected virtual extension points, not actual
WPF UIElement overrides. No inventory, comparator, type mapping or baseline is relaxed.
The no-regression gate and the unsatisfied full strict-parity gate are different checks.

Behavior is independently implemented from public contracts and observed native host
semantics. No original AvalonDock bodies, templates, resources, artwork or font files are
copied. Robustness tests do not establish original WPF event-ordering equivalence.

## Scope and artifacts

Linux and Windows runtime evidence concerns Uno's Skia X11/Win32 hosts. Native WinUI
packaging, macOS desktop compilation, and browser publishing are separate acceptance
levels. Mobile/touch/pen, cross-root menus, screen readers, arbitrary original templates,
framework inheritance, full event routing and performance equivalence remain open.
The existing compact menus and guide visuals are retained; this increment does not add
a pixel-equivalence claim or new original-image baseline.

CI produces core/API results with an exact source ZIP, source revision/checksum, resolved
inventories/differences, platform-separated JSON/JUnit results and existing PNG/XML scenes,
and UnoDock/UnoDock.Core preview-13 packages with symbols. Built packages are not an
assertion of NuGet.org publication. See [dropdown implementation](dropdown-quality.md).
