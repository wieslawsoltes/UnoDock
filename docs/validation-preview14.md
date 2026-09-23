# Preview 14: namespace, sample and presentation acceptance

This increment intentionally renames shipped `Xceed.Wpf.AvalonDock.*` types to
`UnoDock.*`. Recompile dependents and update C# and `using:` XAML namespaces. Package
and assembly names stay unchanged. Original public reference data and probe namespaces
are retained, including historical observations; they are not product API.

## Namespace evidence

Eight migration tests verify the shipped namespaces, exact original inventory and
comparator hashes, explicit per-type mappings, unchanged diagnostic IDs and updated
mapping digest. The migration is not counted as an API gain. The existing resolved
metadata gate still reports inherited-framework and protected-contract differences.
See [migration details](namespace-migration.md).

## Classic sample

The default gallery now independently reconstructs the documented Properties / two
Documents / Alarms and Journal arrangement, with Agenda and Contacts on the left
rail. It has native File/Layout/Samples/Diagnostics/View menus, a compact toolbar,
real sample/theme selection, persistent editors, a live opt-in property inspector,
and retained access to all existing quality laboratories.

The inspector is not a full Xceed PropertyGrid implementation. Classic, workspace,
MVVM, narrow, dark, RTL and large-text scenarios are exercised separately. The sample
reference workflow uses the pinned original public controls to reproduce the same
application-owned classic scene. Original sample code/templates/artwork/fonts are
not read or copied into this implementation.

The initial branch acceptance caught a real sizing defect: the default native
MenuBar style's Height overrode the compact row. The fix explicitly sets the bar's
height and uses independently authored native MenuBarItem chrome, retaining its
ContentButton template part and native menu behavior. The existing <=28.1-DIP test
was not widened. Added presentation tests check visible title/container dimensions,
light/dark configurations, actual XTEST command invocation and Escape dismissal.

## Conservative hit geometry

Changing the sample's client dimensions exposed an older rotation-boundary failure.
WinRT Rect stores origins/spans as Single despite its double-facing API. Independent
rounding can put an exact transformed corner just outside a naive bounding rectangle.

Same-root and cross-window DropArea measurements now share a captured four-corner
transform and directed rounding only when needed. Representable rectangles remain
exact; otherwise endpoints round outward and spans upward to the enclosing float.
Nonfinite/unrepresentable regions fail closed. Regression coverage includes 50,000
seeded rotated/reflected point sets, negative coordinates, zero extents, overflow,
public transformed DropArea controls and the unchanged original failing assertion.

## CI and observed evidence

The normal workflow builds Linux/Windows/macOS desktop heads with warnings as errors,
runs all Linux suites and the Windows acceptance subset, builds browser output and
packs generic Uno/native WinUI libraries with symbols. It now runs the namespace
invariants and exports per-suite executed JUnit summaries, including failing assertion
text. Diagnostic reruns never replace a failed primary result.

`PresentationQualityTests` has 15 portable native-host cases plus two opt-in Linux
XTEST cases. `SampleQualityTests` has 28 Windows / 29 Linux cases. These are configured
cases, not a substitute for the exact revision's execution results. JSON/JUnit and
workflow conclusions establish which tests passed. Windows runtime means the Uno
Skia Win32 host; native WinUI package building, macOS building and browser publishing
are not runtime equivalence certificates.

`tools/compare-sample-geometry.py` compares visible panel/group/pane/rail rectangles
from the original observer and real gallery captures at an identical viewport. It
reports missing and extra controls without coordinate fitting. Its one-DIP tolerance
is a geometry criterion, not pixel equivalence or a certification of inspector/font
rendering. The full original API/behavior and arbitrary-template parity gate remains
unsatisfied. No NuGet.org publication is implied by creating packages.
