# Preview 8 validation scope and reproducibility

Navigator implementation: `b0f3df961b4fbfaeb247c188cdbd618914fb0420`.
Palette correction: `f9bb6f38f9eaf4c007a5322d87a150177904e973`.

The implementation was integrated only after a clean SDK/XAML desktop build with
`-warnaserror`, portable tests, generated-adapter reproduction, and two byte-identical
metadata inventories passed. The corresponding integration runs are
[35819746345](https://github.com/wieslawsoltes/UnoDock/actions/runs/35819746345) and
[35820648037](https://github.com/wieslawsoltes/UnoDock/actions/runs/35820648037).
These integration jobs are not substitutes for the full platform workflow.

The one-use integration workflow is removed. The ordinary `Build and test` workflow
now enforces warnings-as-errors for all three desktop builds and runs the actual
Linux runtime suite and selected Windows runtime acceptance tests. Inspect the run
for the exact revision being consumed; the validation delivery records that revision,
run ID, artifact digests and source/package attribution in its manifest.

## Executable test matrix

| Suite | Linux cases | Windows cases |
| --- | ---: | ---: |
| Portable core | 97 | Not scheduled |
| Runtime/control | 36 | Not scheduled |
| Original-layout interoperability | 44 | Not scheduled |
| Drop/menu/automation | 40 | Not scheduled |
| Lifecycle/source handling | 33 | Not scheduled |
| Interaction | 25 | Not scheduled |
| Converter replay/binding | 1,517 | Not scheduled |
| Window coordinates | 16 | Not scheduled |
| Shell/chrome | 36 | Not scheduled |
| Window lifecycle/navigation | 49 | 47 |
| Input extensions | 43 | 35 |
| Navigator quality | 43 | 41 |
| Original scene visual parity | 27 | 27 |
| **C# cases per platform** | **2,006** | **150** |

The 23 Python metadata-comparator tests remain enabled. The full Linux execution
includes 22 opt-in XTEST input scenarios; the Windows set excludes Linux-only input
cases and includes two real HWND-filter tests. Platform cases overlap and must not
be summed as distinct tests. Test executables return nonzero on failure and emit
JSON/JUnit results; the table describes the test matrix, not a replacement for those
execution reports.

The current local candidate passed all 1,909 Uno/X11 runtime cases, including all
43 navigator-quality cases, with zero failures. That iteration host compiles current
C# against existing generated gallery XAML/ICU resources. The clean SDK/XAML builds
and Windows acceptance are performed separately by CI, not inferred from this harness.

## Visual evidence and the regression found in it

Five navigator PNG/XML scenes cover ordinary selection, tool selection, 40-document
scrolling, RTL and dark appearance. Tests verify actual ListBoxItem container height,
realized title text, retained container/collection identities, selected-row reveal,
bounded deferred work, focus/input and custom named template parts. Screenshots are
also inspected rather than treating collection/selection counts as visual correctness.

The initial Windows run at `b0f3df9` passed its logical tests but revealed pale text on
a stock light background in the navigator captures. The owner had an explicit dark
palette despite a light RequestedTheme. The correction resolves foreground/background
from the same explicit dictionary palette. Four explicit/requested light/dark cases
check actual navigator-owned text and background brushes, and stock screenshot fixtures
isolate and restore the explicit Theme. This is a product correction plus strengthened
tests, not just a changed screenshot baseline.

The original navigator observations were produced through public constructors,
properties, visual-tree geometry and rendering, using application-owned sample content.
Reference revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Probe revision: `356ac7dd1ccf67baebc9c3c0097cd37b53955372`.
Reference run: [35817270444](https://github.com/wieslawsoltes/UnoDock/actions/runs/35817270444).
Artifact SHA256: `fadf7c05743487c97212ac5ecbbbe0466ff8425feef57e58933ce1ef63df58bc`.

Raw public observation XML and provenance are checked in. The implementation uses its
own templates and layout code; no original implementation bodies, templates, resources,
artwork or font files are imported. The original captured title/description overlap is
not reproduced. Text-dependent width, glyph rasterization and native non-client details
are not asserted pixel-equivalent. The original direct selection setter closes its host;
the port still stages selection until explicit commit, an open behavioral difference.

## API accounting and acceptance boundaries

Resolved reference entries: 1,031. Matched: **977**, comprising 881 declared members,
24 inherited counterparts and 72 type shapes. Unresolved: **54**, comprising 9 missing
members, 12 signature differences and 33 type-shape differences. The **18 attribute
differences** are reported separately. No reference mapping, comparator or regression
baseline was relaxed. The no-regression gate is distinct from the unsatisfied strict
full-parity gate.

Runtime evidence applies to Uno's Skia X11 and Win32 hosts. Native WinUI is package-built,
not comprehensively runtime-accepted; macOS is build-only, and browser publishing is
not browser runtime/input acceptance. Default navigator rows are not virtualized.
Original ListBox template-part names and selection plumbing are retained, but reliable
visual rows for FrameworkElement model adapters on Uno require NavigatorListBox.
Arbitrary WPF templates, event ordering, inherited framework types, full accessibility,
mobile/touch, multiple-DPI monitors and workload-level performance parity remain open.

## Deliverables

CI produces exact checked-out source/revision/checksum, resolved inventories/differences,
platform-separated JSON/JUnit tests and PNG/XML captures, desktop/browser builds, and
UnoDock/UnoDock.Core `0.1.0-preview.8` packages plus symbols. Source verification rebuilds
Git blob/tree hashes from the ZIP. Package verification checks the nuspec repository
commit against the source revision. Downloaded artifact SHA256 digests are checked.

Packages are built, not automatically published to NuGet.org. No full-compatibility
attestation is supplied. See [navigator implementation](navigator-quality.md) and
[compatibility boundaries](compatibility.md).
