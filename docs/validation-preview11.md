# Preview 11 validation scope

Base: `dfaed4a6bf0c4075cd115691df20df858442d924` (preview 10). All preceding implemented
sources were already on `main`; this increment preserves them and continues the port.

## New implementation

Auto-hide sizing now uses the publicly observed minimum/default rule and a separate
splitter gutter. Hover no longer activates the tool. Deferred, bounded resizing reuses
the native-release/cancellation protocol and rejects stale snapshots. Focus retention,
shared menus, throwing/reentrant close callbacks and template-selector redirection are
integrated into the real flyout path. See [implementation and limits](auto-hide-quality.md).

## Test matrix and execution evidence

| Suite | Linux cases | Windows cases |
| --- | ---: | ---: |
| Portable core | 120 | Not scheduled |
| Existing Uno runtime suites | 2,012 | 243 selected |
| New auto-hide quality | 64 | 57 |
| **C# cases per platform** | **2,196** | **300** |
| Python metadata comparator | 23 | Not scheduled |

Linux includes 39 opt-in XTEST scenarios across all suites, seven new in this increment.
Windows includes the existing actual HWND-filter cases. Platform suites overlap; their
sum is not a count of unique tests. Tests return nonzero on failure and emit JSON/JUnit.
The matrix describes configured coverage. The exact revision's Actions conclusions,
downloaded reports and delivery manifest establish execution, rather than these counts
being treated as proof by themselves.

Local iteration compiled current core/library/test C# against existing generated
sample XAML/ICU resources. The complete old suites and the first 59 new cases passed;
the 64-case suite also passed after template-redirection and rendered-chrome regressions. This is
not a clean SDK/XAML build. CI independently builds the current complete desktop sample
with warnings-as-errors on Linux, Windows and macOS; executes Linux and selected Windows
acceptance; publishes the browser sample; and builds generic Uno/native WinUI packages.

## Reference provenance

Original revision: `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
Probe revision: `769210a607f304d11a840a461d6ec7888693489a`.
Probe run: 35849397532; artifact: 10744772008.
Artifact SHA256: `dfddb6eb5fa42e2d18824e9d4fb68b60ceb491b6c9a36f753768cf9786ddd36e`.

Eight scenarios record public flyout dimensions, model sizes/minimums and activation,
using public constructors, models and a MouseEnter routed event. The probe pumps the
dispatcher to obtain the revealed flyout. It does not establish original end-to-end
pointer input, animation timing or native HWND internals. Only original public
observations are checked in, not original algorithm bodies, templates or resources.
The reference workflow compares complete auto-hide XML from two independent processes.

Additional public screen probe revision: `3b158f75ee5ab0c07d0c29ab319f295c13c28053`.
Reference run: 35852914812; artifact: 10746760882.
Artifact SHA256: `2f0d5bb0d665ec00271057b8bc74afa09f3f5c34440ecc9b50aa0b08731c91cc`.
The original auto-hide XML reproduced byte-for-byte in separate probe processes.
Four screen-copy images include the HwndHost content that ordinary subtree rendering
omitted. The capture method and public physical/logical bounds are explicitly recorded
in `auto-hide-screen-provenance.json` and `auto-hide-screen-observations.xml`.

The first full platform run (35852914782) passed at that probe revision, including
all six CI jobs. Actual Windows screenshots nevertheless exposed unpainted rail
background and a single-line sample initialization defect. The final correction adds
three acceptance cases (dropdown invocation/context retention, caption metrics/palette,
and rendered rail opacity/six text bands), plus client-only captures. Its new CI run
must pass before delivery; the manifest identifies that final exact revision.

## API accounting and platform limits

The local resolved comparison matches **978 / 1,031** entries: 883 declared members,
23 inherited counterparts and 72 type shapes. Remaining: 9 missing members, 11 signature
differences, 33 type-shape differences and 18 separately reported attribute differences.
The new protected bounded MeasureOverride removes one prior signature diagnostic;
ArrangeOverride moves from inherited to declared classification. No comparison rules,
reference mappings or baseline acceptance entries were relaxed. The regression gate
and the unsatisfied full strict gate remain distinct.

Runtime evidence is for Uno's Skia X11/Win32 hosts. Native WinUI package compilation,
macOS build and browser publish do not establish runtime/input parity on those hosts.
Original timing, HwndHost inheritance, all popup/IME/touch/accessibility combinations,
multiple monitor DPI and workload performance remain open. No full-compatibility
attestation or NuGet.org publication is implied.

## Deliverables

CI produces the exact source ZIP with revision/checksum, complete metadata inventories
and differences, platform-separated JSON/JUnit and real PNG captures, desktop/browser
builds, and UnoDock/UnoDock.Core preview.11 packages with symbols. Delivery verification
checks artifact SHA256, reconstructs Git blob/tree hashes from the source, and validates
package repository commit attribution. The final manifest identifies the exact tested
revision and workflow run, including any unsuccessful earlier iteration separately.
