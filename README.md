# UnoDock

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Reference metadata](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml)

Independent AvalonDock-style docking for **Uno Platform 6.7**, retaining familiar
`Xceed.Wpf.AvalonDock` namespaces while using Uno/WinUI controls.

**Status: 0.1.0-preview.8. This is a functional implementation, not a certified 100%
AvalonDock replacement.** API shape, behavior, platform support and performance are
separate claims. The repository records what is implemented, the original public
contracts used to check it, runnable tests and the remaining boundaries.

## Solution

| Project | Purpose |
| --- | --- |
| `UnoDock.Core` | Framework-independent constrained sizing/resizing, drag state, update batching and bounded XML snapshots |
| `UnoDock` | Layout models, docking manager, visual controls, content adapters, serializer and independent themes |
| `UnoDock.Gallery` | Interactive IDE-style sample with editors, tool panes, persistence, MVVM and capability controls |
| `UnoDock.Core.Tests` | 97 portable tests, including sizing/coordinate/scroll cases and 75,000 caption-region point probes |
| `UnoDock.Runtime.Tests` | 36 runtime + 44 interoperability + 40 drop/menu/automation + 33 lifecycle + 25 interaction tests, 1,517 converter replay/binding tests and 16 native coordinate tests, 36 shell/chrome cases and 49 Linux window/navigation cases and 43 input-extension cases, linked into the gallery |
| `UnoDock.VisualTests` | Original-geometry replay, compact control behavior, live PNG/XML captures |
| `tools/ApiScan` | Deterministic public/protected declaration inventory |
| `tools/ApiMetadata` | Resolved PE metadata inventory, without reading IL bodies or executing the assembly |
| `tools/ReferenceProbe` | Independently authored black-box public-API probes against the pinned original |

Stable NuGet pins resolved on 2026-09-21: **Uno.Sdk 6.7.30**, **Uno.WinUI 6.7.135**,
**Uno.Templates 6.7.30**. `global.json` selects .NET 10 with latest-feature roll-forward.
Version upgrades are explicit; product builds consume committed pins.

## Preview 7: compact chrome and reference visual conformance

Default docking controls now follow the pinned original stock layout: compact captions,
document tabs above content, tool tabs below content, hidden single-tool strips,
selected-document close controls, vector caption buttons, document overflow, thin
separators and vertical auto-hide labels. Headers reveal the selected overflow item;
custom title/header templates, icons and retained editor identity remain supported.
The **Visual parity** lab demonstrates classic/dark palettes, RTL and density overrides.

Four original public-geometry scenes and four original per-item auto-hide observations
are replayed in the actual Uno runtime. There are **27 new regression cases**, including
live palettes, caption capabilities, cancellation, overflow, icons/templates and native
RTL coordinates. Screenshots and measured XML are CI artifacts, not original graphics
embedded in the package. The initial Linux captures match pane/editor/rail bounds within
0.08 DIP; the checked tolerance is 1 DIP. This is scene-specific layout evidence, not
pixel-identical or whole-product visual parity.

The combined core/Linux suites contain **1,963 C# cases** and 23 Python comparator cases.
The resolved gate remains **977/1,031** matched entries, with 54 signature/type and 18
attribute differences unresolved. API matching rules and regression baselines are
unchanged. See [visual implementation, evidence and limits](docs/visual-parity.md).

## Preview 6 additions (previous checkpoint)

Protected mouse/focus hooks now participate in real tab activation, drag and auto-hide
paths. Subclasses can veto activation/drop and cancel native preview focus. Captured
pointer ownership is separate from the composed header that owns its hooks. Panes expose
bindable SelectedIndex/SelectedItem dependency properties with coherent selection events,
weak model observation and bounded reentrant selection draining. Observable collection
notifications reach the protected weak-event override and reject stale queued sources.
Additive pane/header factories and the **Input extensions** gallery lab make these
extensions usable without reflection.

Local validation passes **1,936 C# cases** (97 core + 1,839 actual Uno/X11-host cases),
including **43 new tests** and **20 XTEST input scenarios** across the suites; 23 Python
comparator tests also pass. Resolved API matches are **977/1,031**, up from 946, with
54 signature/type entries and 18 attribute differences remaining. The scanner and
comparison rules were not relaxed; this is not full parity.

Preview 6 is now in repository history. Its original local validation note is retained
in [input/selection contracts](docs/input-extensions.md); current validation is the
preview-7 workflow for the consumed commit. The previously failing Linux release-veto
input test is corrected without removing its assertion.

## Preview 5 additions (previous checkpoint)

Floating windows now have a shared composed lifecycle, protected initialization/closing/
closed/state hooks, cancellation-safe close dispatch and Windows HWND `FilterMessage`
subscriptions. Managed and native presentation state is separate from serialized normal
bounds; maximize/minimize no longer overwrite the restore rectangle.

The navigator exposes the original two named ListBox template parts, document/tool
selection, stable session MRU ordering, eligibility checks at commit and retained-editor
focus restoration. Actual keyboard tests cover Control release, Escape, category keys
and switching from a native floating editor back to the main window. Uno hosts that
route only bubbling key events have a handled-once fallback; left/right Control keys
are recognized. The **Window lifecycle** gallery lab demonstrates the new contracts.

The preview-5 Linux checkpoint passed **1,893 C# cases** (97 core + 1,796 Uno-host cases), including
**49 window/navigation cases** and **12 opt-in XTEST input cases** across all suites.
There are **23 Python comparator tests**. A dedicated Windows runtime CI step executes
the window-lifecycle suite including two HWND filter cases; inspect the workflow result
for the exact consumed revision instead of interpreting test presence as execution.

The preview-5 resolved API comparison matched **946/1,031 entries**: **85 signature/type differences
and 18 attribute differences remain**. The no-regression baseline was tightened by 18
diagnostic IDs without relaxing comparison rules. The full-parity gate is not passed.
See [window lifecycle and navigator](docs/window-lifecycle.md).

## Preview 4 additions

Independent `Microsoft.Windows.Shell.SystemCommands`, `SystemParameters2` and
`WindowChrome` adapters now provide explicitly targeted window commands, observable OS
metrics, attached caption configuration, interactive chrome exclusions, Windows native
caption/resize/system-menu/DWM integration and portable managed-frame resizing. Commands
respect cancellation and closed-host lifetimes. Border drags anchor the opposite edge,
clamp minimum/maximum sizes and roll back on cancellation, capture loss or detach.
The **Window shell** gallery laboratory exposes commands, sizing, chrome settings,
close protection and applied platform capabilities.

The preview-4 checkpoint passed **1,844 C# cases** (97 core + 1,747
actual Uno-host cases) and **23 Python metadata-comparator tests**. This includes eight
opt-in Linux/XTEST scenarios, with two new managed-border input cases. Its resolved
comparison matched **929/1,031** reference entries, up from 884, with **102 unresolved
signature/type entries and 19 attribute differences**. All 105 reference type names
have counterparts, but 33 type shapes still differ. This is not full behavioral parity.
Native Windows paths require Windows runtime acceptance; compiling them is not that
acceptance. See [shell architecture, usage and limits](docs/window-shell.md).

## Preview 3 additions

Native Linux/X11 client coordinates, server-ordered occlusion checks, captured-pointer
cross-window tool docking, floating-document caption docking, destination-window
previews and stationary-pointer tab-edge scrolling are implemented. Header insertion
now wins over top-edge splitting and retains correct model indices around hidden tabs.
In-surface activation updates visual stacking without detaching captured controls.
The gallery parity lab exposes native-host selection and a 40-tab scroll scenario.

The preview-3 checkpoint had **1,772 passing C# cases** (61 core, 36 runtime, 44 original XML,
40 drop/menu/automation, 33 lifecycle, 25 interaction, 1,517 converter and 16 coordinate),
including six opt-in XTEST
input scenarios in the Linux/Xvfb CI job. These are regression cases, not a full
behavioral equivalence certificate. The preview-3 resolved API gate matched **884/1,031** entries
with **147 unresolved signatures/type shapes and 18 attribute differences**. The gate
is still conservative and full parity is not asserted. See
[interaction implementation and validation](docs/interaction.md).

## Preview 2 additions

Validated plans for all 19 drop-target kinds, shared overlay/drop policy, duplicate-content
and immutable-host guards, reentrant close/hide/dock/float and source reconciliation,
protected dependency-property hooks, lazy retained editors, constrained tab sizing,
selection/invoke automation, shared-context menu controls, model diagnostics,
XML/XAML metadata and a **Parity lab** in the gallery.

The preview-2 checkpoint had **198 C# tests and 23 metadata-gate tests**. The current
preview-5 counts and gate result are reported above. CI enforces no newly unresolved
entries; this is not a passed full-parity gate. See [implementation and evidence](docs/parity-progress.md).

## Run the sample

```sh
dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

The desktop head targets Windows, macOS and Linux using Uno's Skia hosting APIs.
For the browser build:

```sh
dotnet workload install wasm-tools
dotnet publish samples/UnoDock.Gallery -c Release -f net10.0-browserwasm \
  -p:UnoDockTargetFrameworks=net10.0-browserwasm -p:UnoDockLibraryFrameworks=net10.0 \
  -o artifacts/browser
```

The generic library can be consumed from other Uno heads. Android/iOS sample heads
and hardware/device validation are not included in this preview.

## Basic usage

```csharp
using Microsoft.UI.Xaml.Controls;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using LayoutPanel = Xceed.Wpf.AvalonDock.Layout.LayoutPanel;

var editor = new LayoutDocument
{
    Title = "Program.cs", ContentId = "editor:Program.cs",
    Content = new TextBox { AcceptsReturn = true, Text = "// Edit here" }
};
var tools = new LayoutAnchorable
{
    Title = "Explorer", ContentId = "explorer", Content = new ListView()
};
var manager = new DockingManager
{
    Layout = new LayoutRoot
    {
        RootPanel = new LayoutPanel(new LayoutDocumentPane(editor))
        {
            Children = { new LayoutAnchorablePane(tools) }
        }
    }
};
editor.Float();
editor.Dock();
tools.ToggleAutoHide();
```

XAML namespace mappings are `xmlns:dock="using:Xceed.Wpf.AvalonDock"` and
`xmlns:layout="using:Xceed.Wpf.AvalonDock.Layout"`. WPF XML namespace URIs, framework
types, resource dictionaries and templates require migration to Uno/WinUI XAML.
This is not binary compatibility with a WPF assembly.

## Interaction

Drag document/tool headers to reorder or dock. Edge previews indicate splits;
context menus expose close, close others/all, float, dock, auto-hide and tab-group
commands. Dividers support pointer dragging and arrow-key resizing. Ctrl+Tab opens
an MRU navigator, Ctrl+F4 closes the active document, and Escape cancels a drag or
closes an overlay. Editors create presenters on first use and retain them when selection changes.
This is lazy content caching, not full header/tab virtualization.

Desktop floating hosts use native Uno windows; browser floating hosts remain in the
surface. `FloatingWindowMode.InSurface` forces portable hosting. Linux/X11 cross-window
dragging is input-tested. Other Skia desktop hosts still need a coordinate adapter;
use docking commands or in-surface hosting there.
Host coordinate integration is exposed through `ICrossWindowCoordinates`.

The gallery includes nested groups, document and tool editors, source-bound items,
capability controls, independent light/dark palettes and a large-tab stress scenario.
See [compatibility boundaries](docs/compatibility.md) before production migration.

## Persistence and original-format fixtures

```csharp
var serializer = new XmlLayoutSerializer(manager);
serializer.LayoutSerializationCallback += (_, args) =>
{
    // Resolve stable IDs to application-owned view models or views:
    // args.Content = myContentRegistry[args.Model.ContentId];
    // args.Cancel = true; // Omit a node that should not be restored.
};
serializer.Serialize("workspace.xml");
serializer.Deserialize("workspace.xml");
```

Restoration constructs a detached layout and resolves content before replacing the
active root. File saving uses a same-directory temporary file and replacement.
DTD/external entity resolution, unexpected node types, excessive depth and oversized
input are rejected. Caller-supplied XmlReader settings remain the caller's responsibility.
Arbitrary CLR objects are not deserialized.

`contracts/reference-fixtures` contains **13 layouts produced through the original
public serializer** and public defaults for 13 original types. These cover ordinary,
hidden, auto-hidden and floating layouts, plus directional `AddToLayout` with and
without `Most`. Tests check topology, IDs, selection, capability flags, dimensions,
previous-container restoration and roundtrips. Legacy invariant timestamps and earlier
UnoDock ISO timestamps are accepted. This corpus is evidence for those cases, not a
claim of lossless compatibility with every legacy or custom layout.

## Deterministic API inventory

The pinned original revision is `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
The source inventory has 996 declarations. The resolved Release metadata has **105
exported types and 1,031 declared API entries**; the Debug profile has 1,020 entries.
Release and Debug scans are independently repeated with byte-identical results.

Metadata includes resolved signatures, attributes, enum values, default arguments,
implicit public constructors, generic constraints and base/interface relationships.
Original implementation bodies, XAML templates, resources and artwork are not emitted
or copied into this implementation. Generated container GUIDs in behavioral fixtures
are normalized with their reference links; content IDs and behavior data are retained.
See [provenance](docs/clean-room.md).

CI keeps the conservative syntax comparison and now also builds and scans the actual
Uno PE metadata with resolved inherited counterparts. Explicit type mappings, reference
hashes, known diagnostics and 23 comparator regression tests back the no-regression gate.
Type-shape and attribute differences are separate diagnostics. The full `--strict` gate
is **not passed**. See [the measured breakdown](docs/parity-progress.md).

## Validate and package

```sh
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/test-results
# Run inside a desktop session; prefix with xvfb-run -a on Linux:
UNODOCK_SELFTEST=1 dotnet run --project samples/UnoDock.Gallery -c Release \
  -f net10.0-desktop -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

Test executables return nonzero on failure and produce JSON/JUnit results. CI builds
the desktop gallery on three operating systems, executes runtime/interoperability tests
in a real Linux Uno application, publishes the browser build and creates NuGet packages.
`core-and-api-results` also contains the exact source ZIP, commit ID and checksum.
Inspect the Actions conclusions for the revision being consumed.

The `UnoDock` package includes generic Uno and native WinUI targets. Packing the native
WinUI XAML target uses Visual Studio MSBuild, as configured in CI:

```powershell
msbuild src/UnoDock/UnoDock.csproj -restore -t:Pack -p:Configuration=Release `
  -p:PackageOutputPath=artifacts/packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
dotnet pack src/UnoDock.Core -c Release -o artifacts/packages
```

The release workflow accepts a published release tag or manual version, validates/tests,
creates packages and symbols, then uses NuGet trusted publishing or `NUGET_API_KEY`.
Authentication and environment approval must be configured separately. No NuGet.org
publication is implied by the presence of the workflow. Stable 1.0+ requires an explicit
compatibility attestation for the exact source tree; no such attestation is supplied.
See [publishing](docs/publishing.md) and [architecture](docs/architecture.md).

This project is not affiliated with or endorsed by Xceed or Uno Platform. Product names
identify compatibility targets. The independent implementation is MIT-licensed.

## Preview 8: navigator quality

The compact navigator now creates real selectable rows for FrameworkElement-based
layout adapters, preserves category collections and containers during navigation,
reveals off-screen selections through the actual ScrollViewer, handles Home/End and
live model/theme/label updates, and revalidates queued work after cancellation or
retemplating. It includes an additive NavigatorListBox for custom named template parts,
an independent stock-style two-column presentation, five screenshot scenarios, and
39 new Linux / 37 Windows regression cases. The gallery adds a Navigator quality lab.
See [implementation, provenance and remaining differences](docs/navigator-quality.md).

The three inherited Dispose warnings are resolved through explicit, target-conditional
member hiding; unsupported ListBox selection-mode/scroll APIs are no longer called.
API comparison remains 977/1,031 with 54 signature/type and 18 attribute differences.
This increment does not assert full strict API, event-ordering, template, pixel or
platform parity. NuGet packages are not automatically published by a source commit.
