# Preview 9 validation checkpoint

Implementation commit: `a90b329cf94a40a375c1af9e32c75268d19d1e25`.
The source patch was verified by SHA256 and Git's full-index checks before a clean
SDK/XAML desktop build with warnings-as-errors, portable tests, deterministic API
comparison, generated-adapter reproduction and real Uno/X11 guide/input tests.
That integration run is https://github.com/wieslawsoltes/UnoDock/actions/runs/35828496118.
All of its validation stages succeeded. The one-use integration workflow is removed
from the final source tree; ordinary CI performs the complete platform matrix.

## Test matrix

| Suite | Linux | Windows |
| --- | ---: | ---: |
| Portable core | 120 | Not scheduled |
| Actual Uno runtime suites excluding guides | 1,909 | 150 selected cases |
| Docking guides, rendering and policy | 43 | 40 |
| **C# total per platform** | **2,072** | **190** |
| Python metadata comparator | 23 | Not scheduled |

The platform totals overlap and are not distinct-test totals. The Linux matrix includes
25 opt-in XTEST sequences. Guide additions include 23 portable cases (30,000 seeded
random layouts) and 43 Linux / 40 Windows cases. The latter include two original
geometry replays, real native-window projection and three Linux pointer sequences.
Current local execution passed all 120 core and 1,952 Uno/X11 cases, but uses existing
generated sample XAML/ICU resources; the separate clean SDK/XAML CI build is authoritative
for build acceptance. Inspect the exact revision's workflow results and JSON/JUnit
artifacts rather than treating the matrix alone as proof of execution.

## Visual and provenance evidence

Seven guide PNG/XML scenes include document and tool compasses, highlighted preview,
dark and RTL cases, and equivalents of two original stock scenes. Each realized target
rectangle is checked against the corresponding original public rectangle within one
DIP. Geometry conformance is not pixel identity or an assertion about every layout,
hover state, theme, font or DPI configuration.

Reference: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Initial successful guide probe: `5298acd6024c134925286fc80b83b04608a17304`, run
https://github.com/wieslawsoltes/UnoDock/actions/runs/35825988138.
Artifact SHA256: `828f517484498b3ac29113be7a9804cc9c6427133d1afa926397767ccac5fbe1`.
The final reference workflow also runs the guide observation twice and compares both
PNG and XML hashes. Its result is separate from the product runtime/input tests.

The successful original guide probe injects public Win32 moving-window messages and
records that method in its XML. Earlier real-pointer attempts on the hosted desktop
failed to establish a reference drag session. This is intentionally not described as
original end-to-end pointer acceptance. Implementation tests use actual XTEST input
on the Uno/X11 host for guide drops, native blank-client release and Escape cleanup.

Original templates, Path.Data, resources, algorithm bodies, artwork and font files are
not exported or imported into the independent implementation. Only public observations
and their provenance are retained. See docking-guides.md for migration and limitations.

## API and platform scope

Resolved comparison: **977 / 1,031** (881 declared members, 24 inherited counterparts,
72 type shapes). **54 signature/type** differences and **18 attribute** differences
remain. No scanner, mapping or regression baseline is relaxed. The no-new-diagnostics
gate is not the unsatisfied full-parity strict gate.

Native runtime evidence applies to Uno's Skia X11/Win32 hosts. Native WinUI is package
built, macOS is build-only, browser publishing is not browser runtime/input acceptance,
and mobile/device coverage remains open. The preview uses the existing half-pane split
preview rather than certifying every constraint-resolved final rectangle. Navigator
setter semantics, full virtualization, inherited WPF contracts, arbitrary templates,
accessibility and workload performance equivalence remain outstanding.

CI produces source with revision/checksum, JSON/JUnit and PNG/XML evidence, API inventories
and differences, browser/desktop builds, and UnoDock/UnoDock.Core preview.9 packages with
symbols. Delivered archives are verified by artifact SHA256, Git tree reconstruction
and package repository-commit attribution. Packages are built, not automatically
published to NuGet.org; no full-compatibility attestation is provided.
