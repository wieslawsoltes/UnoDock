# UnoDock

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Reference metadata](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml)

Independent AvalonDock-style docking for **Uno Platform 6.7**, retaining familiar
`Xceed.Wpf.AvalonDock` namespaces while using Uno/WinUI controls.

**Status: 0.1.0-preview.11. Full AvalonDock compatibility is not yet verified.** API shape,
behavior, appearance, platform coverage and performance are separate acceptance areas.
The target is the pinned public repository and its stock theme, not separately licensed
commercial themes. The independent implementation is MIT-licensed and is not affiliated
with or endorsed by Xceed or Uno Platform.

## Preview 11

Auto-hide windows now match the observed default/minimum sizing, reserve a separate
resize gutter and reveal on hover without activating the tool. A bounded ghost previews
resize movement without changing editor geometry or persisted dimensions. Native
cancellation, competing model edits, stale roots/viewports and callback exceptions are
handled through the real shared splitter path. Focus and open menus retain the flyout;
shared menu contexts, protected focus overrides and reentrant host/template callbacks
are validated. The **Auto-hide quality** laboratory exercises all four sides, minimums,
RTL, themes, cancellation and persistence.

The new suite adds 61 Linux / 54 Windows cases, including eight original public
observation replays, seven native XTEST input scenarios and six screenshots. The
resolved API result improves to **978/1,031**, without relaxing mappings or the baseline.
See [auto-hide implementation and limits](docs/auto-hide-quality.md) and
[preview-11 validation scope](docs/validation-preview11.md).

## Preview 10 (included)

Splitters now show a moving translucent preview without resizing the editors or writing
model lengths until commit. Star weights and mixed pixel/star units match the observed
two-pane reference protocol. Escape, capture loss, disabling, unloading, root replacement
and competing edits cancel safely. Orientation-aware keyboard movement follows physical
Left/Right under RTL. The public drag events, IsDragging and CancelDrag are forwarded
through a composed native Thumb with generation-checked completion.

The **Splitter quality** laboratory demonstrates deferred sizing, constraints, RTL,
orientation, star/pixel modes and XML persistence. Sixty Linux / 53 Windows tests cover
these contracts, including sixteen public original-protocol replays, seven actual XTEST
input sequences and four screenshots. The original ignores a synthetic cancelled drag
completion; safe cancellation is an intentional difference, not a full-equivalence claim.
See [splitter implementation](docs/splitter-quality.md) and
[preview-10 validation scope](docs/validation-preview10.md).

## Preview 9 docking guides (included)

The drag overlay now displays independently rendered stock-style docking glyphs:
an 88-DIP pane compass and rectangular workspace-edge guides measured from original
public observations. The same validated drop plans drive the visuals, hit rectangles
and released-pointer operation. Default `GuidesOnly` input requires an explicit glyph
or visible tab/caption insertion target; `GuidesAndEdges` and `EdgesOnly` preserve
preview-8 broad zones. Extra tool-as-tool guides are opt-in rather than presented as
the original stock default.

Native floating targets receive projected guides in their own client. A neutral drop
in another native client no longer accidentally creates a floating window. Glyphs are
non-activating, retain their containers through hover/theme changes, honor capabilities
and cancellable docking, and clean up on capture cancellation. The **Docking guides**
laboratory demonstrates policy, density, RTL, palette, permissions and native/in-surface
workspaces.

The new suites add 23 portable cases (30,000 randomized layouts) and 43 Linux / 40
Windows guide cases, including live original-geometry replays, native client projection
and three real XTEST sequences. The original guide observations use an explicitly
labeled public Win32 message sequence; they are not original end-to-end pointer tests.
No original templates, Path.Data, artwork or font files are copied into the library.

See [docking guides, migration and validation scope](docs/docking-guides.md),
[navigator quality](docs/navigator-quality.md), and [compatibility boundaries](docs/compatibility.md).

## Solution and pinned toolchain

| Project | Purpose |
| --- | --- |
| `src/UnoDock.Core` | Portable constrained sizing, resizing, coordinates, drag state and bounded XML snapshots |
| `src/UnoDock` | Layout models, docking manager, controls, native adapters, persistence and independent themes |
| `samples/UnoDock.Gallery` | Interactive workbench, editors, tool panes, MVVM and feature laboratories |
| `tests/UnoDock.Core.Tests` | 120 portable cases, including randomized geometry and caption-region probes |
| `tests/UnoDock.Runtime.Tests` | Model/control, original-layout, source, converter, input and native-window tests |
| `tests/UnoDock.VisualTests` | Original-geometry replay, live rendering, navigator and PNG/XML capture |
| `tools/ApiScan` / `tools/ApiMetadata` | Deterministic syntax and resolved PE-metadata inventories |
| `tools/ReferenceProbe` / `tools/ReferenceVisualProbe` | Independently authored public-API observations of the pinned original |

Committed pins: **Uno.Sdk 6.7.30**, **Uno.WinUI 6.7.135**, **Uno.Templates 6.7.30**.
`global.json` selects .NET 10 with latest-feature roll-forward. Upgrades are explicit.

## Run the gallery

```bash
dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

The desktop head uses Uno's Skia hosts on Windows, macOS and Linux. Toolbar laboratories
include **Window shell**, **Window lifecycle**, **Input extensions**, **Visual parity**
**Navigator quality**, **Docking guides**, **Splitter quality** and **Auto-hide quality**. Existing editors preserve content and focus across docking.
For the browser head:

```bash
dotnet workload install wasm-tools
dotnet publish samples/UnoDock.Gallery -c Release -f net10.0-browserwasm \
  -p:UnoDockTargetFrameworks=net10.0-browserwasm -p:UnoDockLibraryFrameworks=net10.0 \
  -o artifacts/browser
```

Android/iOS gallery heads and device validation are not included in this preview.
Browser publishing is separate from browser runtime/input acceptance.

## Basic use

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
var explorer = new LayoutAnchorable
{
    Title = "Explorer", ContentId = "explorer", Content = new ListView()
};
var manager = new DockingManager
{
    Layout = new LayoutRoot
    {
        RootPanel = new LayoutPanel(new LayoutDocumentPane(editor))
        {
            Children = { new LayoutAnchorablePane(explorer) }
        }
    }
};
editor.Float();
editor.Dock();
explorer.ToggleAutoHide();

var serializer = new XmlLayoutSerializer(manager);
serializer.LayoutSerializationCallback += (_, args) =>
{
    // Resolve stable IDs to application-owned view models or views:
    // args.Content = myContentRegistry[args.Model.ContentId];
    // args.Cancel = true; // Omit a node that cannot be restored.
};
serializer.Serialize("workspace.xml");
serializer.Deserialize("workspace.xml");
```

Use `xmlns:dock="using:Xceed.Wpf.AvalonDock"` and
`xmlns:layout="using:Xceed.Wpf.AvalonDock.Layout"` in Uno XAML. WPF XML namespace URIs,
framework types, resource dictionaries and templates require explicit migration.
This is not binary compatibility with a WPF assembly.

Restoration builds a detached layout and resolves content before replacing the active
root. File saving uses a same-directory temporary file and replacement. Bounded XML
parsing rejects unexpected nodes and unsafe DTD/entity input; caller-supplied XmlReader
settings remain the caller's responsibility. Arbitrary CLR objects are not deserialized.

## Interaction and customization

Drag document/tool headers to reorder, combine or split groups. Context menus expose
close, close others/all, float, dock, auto-hide and group commands. Dividers support
pointer and keyboard resizing. Ctrl+Tab opens the navigator, Control release/Enter
commits, Ctrl+F4 closes the active document, and Escape cancels a drag or overlay.

Native floating hosts use platform coordinate adapters; browser hosts remain in-surface.
`FloatingWindowMode.InSurface` forces managed hosting. Native X11 and Win32 regression
coverage is not certification of all hosts, monitor arrangements or DPI transitions.
`ICrossWindowCoordinates` is the extension boundary for other hosting environments.

Independent compact chrome keeps document tabs above content and tool tabs below it,
hides redundant single-tool strips, rotates side-rail labels and retains custom header
and title templates. Density/palette keys are documented in [visual parity](docs/visual-parity.md).
Original named navigator ListBox parts are retained. On Uno, use the additive
`NavigatorListBox` for reliably realized FrameworkElement model-adapter rows; plain
ListBox parts retain selection plumbing but may encounter the substrate's container
behavior. Default navigator rows are not virtualized.

Protected input/focus and pane factories are documented in [input extensions](docs/input-extensions.md).
Window commands, chrome and platform capability limits are in [window shell](docs/window-shell.md).
Consult [architecture](docs/architecture.md) before extending the model or host adapters.

## Deterministic contracts and provenance

The original is pinned at **`2c71faba5eecc1b6ae6cd3d269408e0df37715d8`**. The source inventory
has 996 declarations. Resolved Release metadata has **105 exported types and 1,031 API
entries**, including attributes, enum/default values, generic constraints, constructors
and inheritance. Metadata inventories are repeated byte-for-byte; unresolved types fail.
Reference implementation binaries, source bodies, templates, artwork and fonts are not
copied into this implementation. Public behavior/geometry observations and their hashes
are recorded separately. See [clean-room provenance](docs/clean-room.md).

The current resolved comparison is **978/1,031**: 883 declared members, 23 inherited
counterparts and 72 type shapes. **53 signature/type entries** remain unresolved
(9 missing members, 11 signature differences, 33 type-shape differences), plus **18
attribute differences** reported separately. The no-regression baseline passes only
when it introduces no new diagnostics; the full strict parity gate is still unsatisfied.
No mappings or comparison rules were relaxed for the visual increments.

The four original pane-layout scenes have measured geometry assertions. Navigator
captures establish compact-layout evidence, not pixel identity: font-dependent widths,
glyph rasterization and native non-client details differ. The original direct navigator
selection setter closes its window; the port still stages selection until explicit
commit. That observed difference remains open. Arbitrary WPF templates, inherited
framework semantics, mobile/touch, full accessibility and performance parity also need
further acceptance. Do not interpret API counts as whole-product feature percentages.

## Test and package

```bash
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/test-results
# Run inside a desktop session, or prefix with xvfb-run -a on Linux:
UNODOCK_SELFTEST=1 dotnet run --project samples/UnoDock.Gallery -c Release \
  -f net10.0-desktop -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

The configured test matrix has **2,193 core/Linux C# cases**, a **297-case Windows
subset**, and **23 Python comparator cases**. Linux enables 39 native XTEST scenarios
in CI. Platform totals overlap. JSON/JUnit results and workflow conclusions, not the
matrix size alone, establish what passed for a particular revision.

CI builds real SDK/XAML desktop heads, runs Linux and selected Windows acceptance,
publishes the browser head and creates packages. `core-and-api-results` includes the
exact source ZIP, revision/checksum and inventories/differences. Platform-separated
runtime and PNG/XML artifacts make visual regressions inspectable.

The UnoDock package has generic Uno and native WinUI targets. Native WinUI packing
uses Visual Studio MSBuild as configured in CI:

```powershell
msbuild src/UnoDock/UnoDock.csproj -restore -t:Pack -p:Configuration=Release `
  -p:PackageOutputPath=artifacts/packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
dotnet pack src/UnoDock.Core -c Release -o artifacts/packages
```

`Publish NuGet` accepts a release tag or manual semantic version, validates/tests,
builds packages and symbols, then uses `NUGET_API_KEY` or NuGet trusted publishing.
Credentials, trusted-publishing policy and environment approval are separate account
configuration. The existence of packages/workflows does not imply NuGet.org publication.
Stable 1.0+ requires an explicit full-compatibility attestation bound to the exact source
tree; no such attestation is supplied. See [publishing](docs/publishing.md).
