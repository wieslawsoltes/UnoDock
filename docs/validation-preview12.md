# Preview 12 validation scope

Base `772d051c0fb6f488f72534f6d710f6d984616256` is the completed preview-11
implementation, verified green in Actions run 35854870484. All auto-hide and previous
preview work is preserved. The menu observation commit is
`9e51875a8732c5dda48bf4c0ccf07a7a695a9d47`.

## Execution matrix

| Suite | Linux cases | Windows cases |
| --- | ---: | ---: |
| Portable core | 120 | Not scheduled |
| Existing Uno runtime suites | 2,076 | 300 selected |
| Menu quality | 53 | 50 |
| **C# per-platform total** | **2,249** | **350** |
| Python metadata comparator | 23 | Not scheduled |

The complete Linux suite enables 42 native XTEST cases, including three menu cases.
Windows includes real HWND filtering and realized controls, but not Linux input
injection. The platform subsets overlap and must not be summed as distinct tests.
Actual JSON/JUnit files and the exact commit's workflow conclusion establish execution;
a configured matrix alone does not. The delivered manifest records the source revision,
CI run, artifact hashes and individual suite counts.

Local iteration passed all 2,129 actual Uno/X11 runtime cases, including 53 new menu
cases and native input. This compiles current C# but reuses existing generated gallery
XAML/ICU resources; it is not a clean SDK/XAML build. CI independently builds complete
current samples with warnings-as-errors on Linux, Windows and macOS, runs Linux and
selected Windows runtime acceptance, publishes the browser head, and packs generic
Uno/native WinUI libraries. Native WinUI runtime, macOS runtime, browser input and
mobile/device acceptance remain separate boundaries.

## Public observations and visual review

Original reference `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`; probe
`9e51875a8732c5dda48bf4c0ccf07a7a695a9d47`; run 35859787123; artifact10749980963.
Artifact SHA256: `23d1481107870729b8d15ded09206ac59e987a9362e048f6755ee99110c2ea00`.
Ten public-menu scenarios record order, labels, enabled/collapsed states, row height,
font size and menu bounds. All ten are replayed against actual arranged Uno controls.
The current reference workflow repeats the entire XML comparison.

Screenshots exposed a segmented icon gutter and absent popup RTL inheritance; both
were corrected in the product. Default light rows now form a continuous gutter; popup
and row FlowDirection are explicit. Six actual captures cover document, tool,
auto-hidden tool, dark, RTL and large text. Caption/rail improvements from preview11
remain in the same solution. Font-dependent width/glyph differences and arbitrary
menu template equivalence are not certified pixel-identical.

The implementation contains no original source bodies, templates, resource definitions,
artwork or font files. Public protocol observations are labeled separately from actual
pointer input. Bulk-close reentrancy/worker-command tests are independent robustness
acceptance, not original event-ordering equivalence evidence.

## Contracts and artifacts

Base resolved comparison: 978/1,031, with 53 unresolved signature/type entries
(9 missing, 11 signature differences, 33 type shapes), and 18 attribute differences
reported separately. No scanner, mappings or regression baseline is relaxed. CI emits
the current count; the full strict parity gate remains unsatisfied.

CI archives exact source/revision/checksum, metadata comparisons, platform JSON/JUnit,
actual PNG/XML captures, desktop/browser output and preview12 NuGet/symbol packages.
Download verification reconstructs the source Git tree and checks package repository
commit metadata. Packages are built, not automatically published to NuGet.org. No
full-compatibility attestation is supplied. See [menu quality](menu-quality.md) and
[compatibility](compatibility.md).
