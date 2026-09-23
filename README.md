# UnoDock

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Reference metadata](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml)

Independent AvalonDock-style docking for **Uno Platform 6.7**, retaining familiar
`Xceed.Wpf.AvalonDock` namespaces while using Uno/WinUI controls.

**Version: 0.1.0-preview.12. Full API, behavioral and visual parity is not verified.**
The target is the pinned public AvalonDock repository and stock presentation, not
separately licensed commercial themes. Framework mappings require source/XAML migration;
this is not binary compatibility with WPF. The independent implementation is MIT-licensed
and is not affiliated with or endorsed by Xceed or Uno Platform.

## Current continuation: compact menus and safe context lifetimes

Default document/tool menus match ten original public observations of command order,
labels, enabled/collapsed states and 22-DIP rows. They retain real native menu items,
continuous icon gutters, explicit RTL and coherent palettes. Menu and row identities
survive refreshes. Application command subscriptions exist only while open; worker
requeries are coalesced, and command/root identity is revalidated after callbacks.
Bulk-close operations stay within their original workspace and guard reentrancy.

Shared custom menus preserve application styles, local values and bindings. Temporary
contexts now support bounded, nonrecursive replacement/clearing from DataContextChanged,
including removed rows and submenus. Partial failure cleans up still-owned values and
retains original and cleanup exceptions. ContextMenuEx no longer overwrites a nested
MenuDataContext request with a stale outer callback value.

The menu suite has **53 Linux / 50 Windows cases**, including ten original replays,
three native XTEST interactions and six screenshots. **Fourteen additional real-Uno
context-lifetime tests** cover callback reentrancy and cleanup; six initial regressions
failed against the old implementation before the correction. These robustness tests
do not establish original WPF event-ordering equivalence.

See [menu behavior and styling](docs/menu-quality.md), [current validation scope](docs/validation-preview12.md),
and [all compatibility boundaries](docs/compatibility.md). Earlier auto-hide, splitter,
docking-guide, navigator and input-extension increments are included, not pending upload.

## Run the gallery

Committed pins are **Uno.Sdk 6.7.30**, **Uno.WinUI 6.7.135** and **Uno.Templates 6.7.30**.
`global.json` selects .NET 10 with latest-feature roll-forward. Dependency upgrades are
explicit; these are reproducible pins, not an assertion of the latest registry versions.

```bash
dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

Toolbar laboratories include **Window shell**, **Window lifecycle**, **Input extensions**,
**Visual parity**, **Navigator quality**, **Docking guides**, **Splitter quality**,
**Auto-hide quality** and **Menu quality**. They demonstrate native/in-surface windows,
capabilities and cancellation, MRU selection, large tab sets, RTL/density/themes, deferred
resizing, XML persistence, command replacement and model-bound editors.

```bash
dotnet workload install wasm-tools
dotnet publish samples/UnoDock.Gallery -c Release -f net10.0-browserwasm \
  -p:UnoDockTargetFrameworks=net10.0-browserwasm -p:UnoDockLibraryFrameworks=net10.0 \
  -o artifacts/browser
```

The desktop head uses Uno Skia on Windows, macOS and Linux. Android/iOS gallery heads
and device acceptance are not included. Browser publishing is not browser input testing.

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
    // Resolve stable IDs to application-owned models or views:
    // args.Content = myContentRegistry[args.Model.ContentId];
    // args.Cancel = true; // Omit content that cannot be restored.
};
serializer.Serialize("workspace.xml");
serializer.Deserialize("workspace.xml");
```

Use `xmlns:dock="using:Xceed.Wpf.AvalonDock"` and
`xmlns:layout="using:Xceed.Wpf.AvalonDock.Layout"` in Uno XAML. WPF namespace URIs,
framework types, resource dictionaries and templates need explicit migration.
Restoration constructs a detached layout and resolves content before replacing the
active root. Bounded XML parsing rejects unsafe/unknown input; supplied XmlReader
settings remain the caller's responsibility. Arbitrary CLR objects are not deserialized.

## Implemented interaction and extension boundaries

Document/tool headers reorder, combine and split groups. Default `GuidesOnly` docking
requires an explicit glyph or visible tab/caption target; `GuidesAndEdges` and `EdgesOnly`
provide broad edge zones. The same validated plan drives preview, hit testing and drop.
Native floating targets receive projected guides in their own clients. In-surface hosting
is available through `FloatingWindowMode.InSurface`; other native environments can supply
`ICrossWindowCoordinates`.

Compact chrome places document tabs above content and tool tabs below it, hides redundant
single-tool strips, rotates side labels and retains custom templates. Auto-hide moves the
requested tool, preserves restoration links and reveals on hover without activation.
Flyouts retain focus/open menus and reserve a separate resize gutter. Splitters show a
bounded ghost and commit sizing units only on release; cancellation does not write preview
geometry. Model/window lifecycle changes invalidate stale work.

Ctrl+Tab opens the navigator, Control release/Enter commits, Escape cancels, and Ctrl+F4
closes the active document. Navigator collections/rows retain identity with bounded
selected-row reveal. Named ListBox parts remain supported; `NavigatorListBox` provides
reliable realized rows for FrameworkElement model adapters on the pinned Uno host.
Default rows are not virtualized. Editor presenters and weakly recorded focus survive
selection/docking. Protected input/focus hooks execute on the real operation path.

Window commands target explicit hosts. Managed/native chrome, normal-vs-maximized bounds,
close cancellation/reentrancy and Win32 message-filter lifetimes have targeted acceptance.
These are not complete WPF Window, Freezable, HwndHost or routed-event implementations.
See [architecture](docs/architecture.md), [input extensions](docs/input-extensions.md),
[window lifecycle](docs/window-lifecycle.md), [window shell](docs/window-shell.md),
[navigator](docs/navigator-quality.md), [guides](docs/docking-guides.md),
[splitters](docs/splitter-quality.md) and [auto-hide](docs/auto-hide-quality.md).

## Solution and deterministic contracts

| Area | Projects |
| --- | --- |
| Portable geometry, coordinates, drag state and XML | `src/UnoDock.Core` |
| Models, controls, host adapters, persistence and themes | `src/UnoDock` |
| Interactive workbench and laboratories | `samples/UnoDock.Gallery` |
| Portable, real-host and visual acceptance | `tests/UnoDock.Core.Tests`, `tests/UnoDock.Runtime.Tests`, `tests/UnoDock.VisualTests` |
| Syntax and resolved metadata inventories | `tools/ApiScan`, `tools/ApiMetadata` |
| Independently authored original public observations | `tools/ReferenceProbe`, `tools/ReferenceVisualProbe` |

The original is pinned at **`2c71faba5eecc1b6ae6cd3d269408e0df37715d8`**. The syntax
inventory has 996 declarations; resolved Release metadata has **105 type names and
1,031 entries**, including attributes, enum/default values, constraints, constructors
and inheritance. Repeated inventories must be byte-identical; unresolved types fail.
Original implementation bodies, templates, resource definitions, artwork and font files
are not copied into the product. Public protocol observations are distinguished from
actual native input acceptance. See [provenance](docs/clean-room.md).

Current resolved comparison: **978/1,031** (883 declared, 23 inherited, 72 type shapes).
**53 signature/type entries** remain unresolved: 9 missing members, 11 signature differences
and 33 type-shape differences. **18 attribute differences** are separate. The no-regression
gate has not been relaxed; the full strict-parity gate remains unsatisfied. Counts do not
measure whole-product feature completeness.

Measured pane/guide geometry and menu row replays are evidence for their particular
scenes, not pixel identity. Fonts, glyph rasterization, arbitrary templates and native
non-client details differ. Open boundaries include original navigator setter semantics,
inherited framework contracts, full event ordering, custom serialization, accessibility,
mobile/touch/pen, IME, multi-monitor DPI and workload-level performance equivalence.

## Validation and packaging

```bash
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/test-results
# Run in a desktop session, or prefix with xvfb-run -a on Linux:
UNODOCK_SELFTEST=1 dotnet run --project samples/UnoDock.Gallery -c Release \
  -f net10.0-desktop -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

The matrix contains **2,263 core/Linux C# cases**, a **364-case Windows subset** and
**23 Python comparator cases**. Linux enables **42 native XTEST scenarios** on a dedicated
display. Platform totals overlap. Inspect JSON/JUnit reports and all CI job conclusions
for the exact revision consumed. Desktop SDK/XAML builds use warnings-as-errors.

Normal input acceptance has no extra root-event observer. `UNODOCK_INPUT_TRACE=1`
enables bounded diagnostics. After a failing Linux run, CI may collect separate diagnostic
evidence without replacing its original report or clearing failure. The earlier
intermittent release-veto failure and its unresolved cause are documented in the
[validation record](docs/validation-preview12.md).

Artifacts include exact source/revision/checksum, API inventories/differences, platform
reports and PNG/XML captures, desktop/browser output, and packages/symbols. X11/Win32
runtime evidence is distinct from native WinUI package builds, macOS builds and browser
publishing. Native WinUI XAML packing uses Visual Studio MSBuild:

```powershell
msbuild src/UnoDock/UnoDock.csproj -restore -t:Pack -p:Configuration=Release `
  -p:PackageOutputPath=artifacts/packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
dotnet pack src/UnoDock.Core -c Release -o artifacts/packages
```

`Publish NuGet` accepts a release tag or manual version, validates/tests, builds packages
and symbols, then uses `NUGET_API_KEY` or trusted publishing. Credentials, account policies
and environment approval are separate configuration. Built packages do not imply a
NuGet.org publication. Stable 1.0+ requires a reviewed compatibility attestation bound
to the exact source tree; none is supplied. See [publishing](docs/publishing.md).
