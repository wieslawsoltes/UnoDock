# Preview 12 validation scope

Base `772d051c0fb6f488f72534f6d710f6d984616256` is the completed preview-11
implementation, verified green in Actions run 35854870484. All auto-hide and previous
preview work is preserved. The menu observation commit is
`9e51875a8732c5dda48bf4c0ccf07a7a695a9d47`; implementation commit
`1ead786e4ee963ed746b7d59c99f3ead020aa275` includes the compact native menus.

The follow-up `627ede151cbe420567c2b744d06fd51c22c72183` fixes reentrant custom-menu
contexts and adds fourteen lifetime regressions. Its six platform jobs passed in
run 35873582410. That checkpoint enabled a test-only root event observer. Subsequent
commit `94deb82ad41e939762bb00622bd1f5747bc3a907` makes tracing explicitly opt-in and
keeps it off in normal acceptance. All six jobs in its run 35875124949 passed. Each
later revision's run must still be checked separately; success is not inferred from
an earlier revision.

## Execution matrix

| Suite | Linux cases | Windows cases |
| --- | ---: | ---: |
| Portable core | 120 | Not scheduled |
| Earlier Uno runtime suites | 2,076 | 300 selected |
| Menu quality | 53 | 50 |
| Menu context lifetime | 14 | 14 |
| **C# per-platform total** | **2,263** | **364** |
| Python metadata comparator | 23 | Not scheduled |

The complete Linux suite enables 42 native XTEST cases, including three menu cases.
Windows includes real HWND filtering and realized controls, but not Linux input
injection. Platform subsets overlap and must not be summed as distinct tests. JSON
and JUnit results are reconciled in delivery verification. The delivered manifest
records the exact source revision, CI run, artifact digests and individual suite counts.

Local current sources passed all 2,143 actual Uno/X11 runtime cases, including the
53 menu cases, fourteen context-lifetime cases and all native input scenarios, both
with and without the optional root observer. The current gallery was built through
the full SDK/MSBuild/XAML pipeline with warnings-as-errors, using restored pinned
packages. This supersedes the earlier cached-generated-XAML iteration limitation.
The portable 120-case suite and 23 Python comparator tests passed locally, and two
resolved metadata inventories reproduced byte-for-byte with unchanged inputs.

CI separately builds current desktop samples with warnings-as-errors on Linux,
Windows and macOS, runs Linux and selected Windows acceptance, publishes the browser
head, and packages generic Uno/native WinUI libraries. Native WinUI runtime, macOS
runtime, browser input and mobile/device acceptance remain separate boundaries.

## Earlier native-input failure and diagnostics

Run 35863975221 failed one existing Linux input-extension case: the release-veto
specimen observed zero protected release callbacks instead of one. All 53 menu cases
passed; Windows passed all 350 then-scheduled cases. The failure was not reproduced
in local full or targeted runs, and its precise cause has not been established.
Neither the test's assertions nor production pointer/capture behavior were weakened
or speculatively changed to make it pass. A capture-order experiment failed to produce
its required ordering and was not added as an acceptance test or used to justify a fix.

`UNODOCK_INPUT_TRACE=1` records at most 256 pointer lifecycle observations on the input
suite's root. It does not synthesize input or change Handled/capture, but registering
observers can still affect timing or framework routing optimizations; normal acceptance
therefore leaves it disabled. If Linux acceptance fails, CI can run a separate diagnostic
pass and upload it separately. It does not replace the failing report, clear the initial
failure, rerun until green, or permit release publication. A successful later run is not
by itself proof that the earlier environment-dependent failure is permanently fixed.

## Public observations and visual review

Original reference `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`; probe
`9e51875a8732c5dda48bf4c0ccf07a7a695a9d47`; run 35859787123; artifact10749980963.
Artifact SHA256: `23d1481107870729b8d15ded09206ac59e987a9362e048f6755ee99110c2ea00`.
Ten public-menu scenarios record order, labels, enabled/collapsed states, row height,
font size and menu bounds. All ten are replayed against actual arranged Uno controls.
The reference workflow repeats the entire XML comparison; run 35863975296 passed it.

Screenshots exposed a segmented icon gutter and absent popup RTL inheritance; both
were corrected in the product. Default light rows now form a continuous gutter; popup
and row FlowDirection are explicit. Six captures cover document, tool, auto-hidden
tool, dark, RTL and large text. Caption/rail improvements from preview11 remain in the
same solution. Font-dependent width/glyph differences and arbitrary menu-template
equivalence are not certified pixel-identical.

The implementation contains no original source bodies, templates, resource definitions,
artwork or font files. Public protocol observations are labeled separately from native
input. Bulk-close reentrancy, worker-command and menu-context tests are independent
robustness acceptance, not original event-ordering equivalence evidence.

## Contracts and artifacts

Resolved comparison: 978/1,031, with 53 unresolved signature/type entries (9 missing,
11 signature differences, 33 type shapes), and 18 separately reported attribute
differences. No scanner, mappings or regression baseline was relaxed. The current CI
comparison remains authoritative for the consumed revision; strict parity is unsatisfied.

CI archives exact source/revision/checksum, metadata comparisons, platform JSON/JUnit,
PNG/XML captures, desktop/browser output and preview12 NuGet/symbol packages. Delivery
verification reconstructs the source Git tree and checks each package's repository
commit. Diagnostic SDK/package caches are not product deliverables. Temporary recovery
and diagnostic-toolchain workflows have been removed. Packages are built, not published
automatically to NuGet.org. No full-compatibility attestation is supplied. See
[menu quality](menu-quality.md) and [compatibility](compatibility.md).
