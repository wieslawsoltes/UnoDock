# Preview 4 validation checkpoint

The implementation commit `f373ce35ee8c4cb2c6a9fd9058c439f02d155482` was validated by
GitHub Actions run [35756895693](https://github.com/wieslawsoltes/UnoDock/actions/runs/35756895693),
which completed successfully on 2026-09-22. All six jobs passed: portable core/API gate,
Linux desktop/runtime, Windows desktop build, macOS desktop build, browser publish,
and generic Uno/native WinUI package production. The cleanup commit containing this
record changes only documentation and removes the one-use source-integration workflow.

## Executed tests

| Suite | Passed | Failed |
| --- | ---: | ---: |
| Portable core | 97 | 0 |
| Uno runtime/control | 36 | 0 |
| Original-layout interoperability | 44 | 0 |
| Drop/menu/automation | 40 | 0 |
| Lifecycle/source handling | 33 | 0 |
| Interaction | 25 | 0 |
| Converter replay/binding | 1,517 | 0 |
| Window coordinates | 16 | 0 |
| Shell/chrome | 36 | 0 |
| **C# total** | **1,844** | **0** |
| Python metadata comparator | 23 | 0 |

The Linux job enabled `UNODOCK_NATIVE_INPUT_TESTS=1`, executing all eight opt-in XTEST
input scenarios rather than omitting them. The new managed-border cases exercise an
actual pointer resize and rollback when chrome is detached during capture. Core tests
include 75,000 randomized caption-region point probes. Generated property adapters
reproduced without changes. These are regression tests, not exhaustive equivalence
or platform performance certification.

Windows and macOS are build-validated, not runtime-tested in this checkpoint. Browser
publishing passed; browser input/runtime acceptance is separate. Native WinUI library
packaging passed using Visual Studio MSBuild, including the platform-specific shell
code. That does not establish Windows native caption/system-menu/DWM/DPI behavior.

## Resolved API comparison

Pinned reference: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Reference inventory SHA256: `afa13b694cce68ee4f5a9f98e10253ba11ffe48ee05ddd18c0a357ecfd37fe4e`.

| Classification | Count |
| --- | ---: |
| Matched declared members | 833 |
| Matched inherited counterparts | 24 |
| Matched type shapes | 72 |
| **Matched entries** | **929 / 1,031** |
| Missing members | 46 |
| Signature differences | 23 |
| Type-shape differences | 33 |
| **Unresolved signature/type entries** | **102** |
| Attribute differences, reported separately | 19 |

All 105 reference type names have counterparts; that is not the same as matching all
their shapes, members or behaviors. The regression baseline enforces no newly unresolved
entries, while the full-parity strict mode remains unsatisfied. The mappings are explicit
and retain framework differences instead of hiding them with blanket namespace changes.

## Produced artifacts

The run includes `core-and-api-results` (exact source ZIP, revision, checksum, inventories,
difference reports and portable/Python test results), `runtime-test-results` (JSON and
JUnit for all eight Uno-host suites), desktop and browser artifacts, and `nuget-preview`
containing UnoDock and UnoDock.Core version `0.1.0-preview.4` with symbols.

Artifact digests were verified after downloading. NuGet packages were built but were
**not published to NuGet.org**. The release workflow remains separately gated. See
[window shell](window-shell.md) and [compatibility](compatibility.md) for remaining
framework, extension-point, platform and behavior boundaries.
