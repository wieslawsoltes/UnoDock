# UnoDock

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Reference metadata](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml)

Independent AvalonDock-style docking for **Uno Platform 6.7**, retaining familiar
`Xceed.Wpf.AvalonDock` namespaces while using Uno/WinUI controls.

**Status: 0.1.0-preview.1. This is a functional implementation, not a certified 100%
AvalonDock replacement.** API shape, behavior, platform support and performance are
separate claims. The repository records what is implemented, the original public
contracts used to check it, runnable tests and the remaining boundaries.

## Solution

| Project | Purpose |
| --- | --- |
| `UnoDock.Core` | Framework-independent constrained sizing/resizing, drag state, update batching and bounded XML snapshots |
| `UnoDock` | Layout models, docking manager, visual controls, content adapters, serializer and independent themes |
| `UnoDock.Gallery` | Interactive IDE-style sample with editors, tool panes, persistence, MVVM and capability controls |
| `UnoDock.Core.Tests` | 45 portable tests, including 10,000 randomized allocation/resize cases |
| `UnoDock.Runtime.Tests` | 36 model/control runtime tests plus 44 original-layout/default/interoperability tests, linked into the gallery |
| `tools/ApiScan` | Deterministic public/protected declaration inventory |
| `tools/ApiMetadata` | Resolved PE metadata inventory, without reading IL bodies or executing the assembly |
| `tools/ReferenceProbe` | Independently authored black-box public-API probes against the pinned original |

Stable NuGet pins resolved on 2026-09-21: **Uno.Sdk 6.7.30**, **Uno.WinUI 6.7.135**,
**Uno.Templates 6.7.30**. `global.json` selects .NET 10 with latest-feature roll-forward.
Version upgrades are explicit; product builds consume committed pins.

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
closes an overlay. Editors retain their content presenters when selection changes.
This is content caching, not full tab virtualization.

Desktop floating hosts use native Uno windows; browser floating hosts remain in the
surface. `FloatingWindowMode.InSurface` forces portable hosting. Native cross-window
dragging is incomplete on Uno Skia: use docking commands or in-surface hosting.
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

The CI declaration comparison reports missing/different members rather than treating
name matches as parity. Its optional `--strict` gate is not represented as passed.
Resolved end-to-end counterpart mapping and remaining public/protected APIs still need
work; the larger metadata baseline does not make the implementation fully compatible.

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
