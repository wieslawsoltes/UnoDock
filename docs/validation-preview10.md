# Preview 10 validation scope

Base: `111f477927eaa289121fda04967bb6cf6d9821e6` (preview 9), verified green in
Actions run 35829434436. That base contains the recovered docking-guide work; it was
not recreated or discarded in this continuation.

## Test matrix

| Suite | Linux | Windows |
| --- | ---: | ---: |
| Portable core | 120 | Not scheduled |
| Existing actual Uno runtime suites | 1,952 | 190 selected |
| New splitter quality | 60 | 53 |
| **C# cases per platform** | **2,132** | **243** |
| Python metadata comparator | 23 | Not scheduled |

Linux enables 32 native XTEST scenarios in the complete suite, including seven new
splitter sequences. Windows includes the existing actual HWND-filter cases. Platform
subsets overlap, so the table is not a count of unique tests across platforms.
Test executables return nonzero on failure and emit JSON/JUnit. Consult the exact
revision's Actions run and delivered verification manifest, rather than treating a
configured matrix as evidence of execution.

Local iteration passed 120 core and 2,012 actual Uno/X11 runtime cases. That harness
compiles current libraries and the new tests but reuses existing generated application
resources; it is not a clean SDK/XAML build. Ordinary CI builds the complete current
sample with warnings-as-errors on Linux, Windows and macOS, runs Linux and selected
Windows suites, publishes browser output and builds generic Uno/native WinUI packages.

## Failures exposed and fixed

Actual XTEST input showed that disabled and unloading Thumb capture could complete
before parent lifecycle notifications, reporting Canceled=false and incorrectly
committing sizes. Native completion now waits for matching release evidence and a
still-valid session. Explicit capture-loss, disable, unload and Escape scenarios must
leave the original pair unchanged. Public noncancelled fixture replay also exposed a
subpixel star-ratio jump when rebuilding ratios from rounded grid lengths. Adding the
displacement to the original ratio preserves exact star accounting and matches the
observed original two-pane results.

## Original observation provenance

Original revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Expanded probe revision: `e206e9eece2bb49a61f88d1f71370b001fbc7c5f`.
Probe run: 35835395390; artifact: 10739500300.
Artifact SHA256: `d9a998416bca8e32dc861a2894da0da11cee2e92803f01c6bca2372a0e5fd191`.

The 32-case fixture uses public routed drag events, not native original pointer input.
Sixteen noncancelled cases are replayed against the current port. The original ignores
the cancelled-completion flag in this synthetic protocol; the port intentionally honors
cancellation. Original Escape/native input equivalence is not inferred. The reference
workflow now compares complete splitter XML between its two independent executions.
Only public observations are checked in; no original templates, algorithm bodies,
resources, artwork or fonts are imported into the product.

## API/platform limitations and artifacts

This increment does not relax the scanner, mapping or no-regression baseline. The base
matches 977/1,031 reference entries with 54 unresolved signature/type entries and 18
attribute differences. CI emits the current resolved comparison separately from tests.
A no-new-diagnostics result is not a full strict-parity pass.

Runtime tests target Uno Skia X11/Win32. Native WinUI is package-built; macOS is build-only;
browser publish is not browser runtime/input certification. General WPF inheritance,
mobile/touch, accessibility, nested sizing and workload-level performance remain open.

CI artifacts include exact source/revision/checksum, full metadata reports, platform
JSON/JUnit, guide/navigator/pane/splitter screenshots, desktop/browser builds, and
UnoDock/UnoDock.Core preview.10 packages plus symbols. Download verification checks
artifact digests, reconstructed source Git trees and nuspec commit attribution.
Packages are not automatically published to NuGet.org. No full-parity attestation is
included. See [splitter implementation](splitter-quality.md).
