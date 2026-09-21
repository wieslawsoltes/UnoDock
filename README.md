# UnoDock

Independent AvalonDock-style docking for **Uno Platform 6.7**, using the familiar
`Xceed.Wpf.AvalonDock` namespaces and Uno/WinUI controls.

**Status: 0.1.0-preview.1 — functional implementation under validation, not a certified
100% AvalonDock replacement.** The repository contains an independently implemented
layout engine, controls, sample, test executables, reference inventory, and release
workflows. API shape, behavior, platform support, and performance are separate claims;
a build or API name match does not establish complete compatibility.

## Packages and projects

| Project | Purpose |
| --- | --- |
| `UnoDock.Core` | Framework-independent sizing, constrained resizing, drag state, update batching, bounded XML snapshots |
| `UnoDock` | Uno/WinUI layout models, docking manager, visual controls, content adapters, XML layout serializer, themes |
| `UnoDock.Gallery` | Interactive IDE-style sample, editor/tool panes, persistence, MVVM, capability flags, runtime tests |
| `UnoDock.Core.Tests` | Dependency-free test executable with JSON and JUnit results |
| `UnoDock.Runtime.Tests` | Tests linked into the gallery and executed on a real Uno UI thread |
| `tools/ApiScan` | Pinned declaration-only Roslyn inventory, with repeatability verification |

Version pins resolved from stable NuGet packages on 2026-09-21:
`Uno.Sdk 6.7.30`, `Uno.WinUI 6.7.135`, `Uno.Templates 6.7.30`.
`global.json` requests .NET 10 with latest-feature roll-forward. Updates are explicit,
not floating dependencies on every build.

## Run the gallery

```sh
dotnet build samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

The desktop host supports Windows, macOS, and Linux through Uno's Skia hosting APIs.
The browser head uses `net10.0-browserwasm` and requires the `wasm-tools` workload.
The generic library can also be referenced by other Uno heads; Android and iOS sample
heads and device validation are not included in this preview.

## Basic usage

```csharp
using Microsoft.UI.Xaml.Controls;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using LayoutPanel = Xceed.Wpf.AvalonDock.Layout.LayoutPanel;

var editor = new LayoutDocument
{
    Title = "Program.cs",
    ContentId = "editor:Program.cs",
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

For XAML, use `xmlns:dock="using:Xceed.Wpf.AvalonDock"` and
`xmlns:layout="using:Xceed.Wpf.AvalonDock.Layout"`. WPF XML namespace URIs and WPF
resource dictionaries must be migrated to Uno's XAML dialect. The gallery contains
WinUI `DataTemplate` and `Style` examples for MVVM content.

## Interaction and persistence

Drag document/tool headers to reorder or dock; edge previews indicate a split.
Context menus expose close, close others/all, float, dock, auto-hide, and tab-group
commands. Dividers support pointer dragging and arrow-key resizing. Ctrl+Tab opens
an MRU navigator; Ctrl+F4 closes the active document; Escape cancels dragging or
closes an overlay. Content presenters are retained rather than recreated when tabs
switch. This is content caching, not full tab virtualization.

Floating windows use native Uno windows on desktop, and in-surface windows in the
browser. `FloatingWindowMode.InSurface` forces portable hosting. Native cross-window
dragging is not complete on Uno Skia; use dock commands or in-surface hosting. See
[compatibility boundaries](docs/compatibility.md).

```csharp
var serializer = new XmlLayoutSerializer(manager);
serializer.LayoutSerializationCallback += (_, args) =>
{
    // Resolve stable ContentId values to application-owned view models/views.
    // args.Content = myContentRegistry[args.Model.ContentId];
    // args.Cancel = true; // Omit a node that should not be restored.
};
serializer.Serialize("workspace.xml");
serializer.Deserialize("workspace.xml");
```

The serializer does not deserialize arbitrary CLR objects. Restore builds a detached
layout, resolves content, and only then replaces the active root. File saves use a
same-directory temporary file and replacement. XML readers prohibit DTDs, external
resolvers, unexpected nodes, excessive depth, and oversized documents. Caller-supplied
`XmlReader` instances remain the caller's responsibility for reader settings.

## Validate

```sh
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/test-results
# Inside a graphical desktop session (use xvfb-run on Linux):
UNODOCK_SELFTEST=1 dotnet run --project samples/UnoDock.Gallery -c Release \
  -f net10.0-desktop -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

The runners return nonzero on any failed assertion and write JSON/JUnit reports.
CI also builds the library/sample and packages NuGet artifacts. The current API
inventory is in `contracts/avalondock.txt`; `tools/audit-api.py` reports exact mapped
**declaration** differences and does not mistake missing members for compatibility.
Use `--strict` to require no differences; that gate is intentionally not represented
as passed while compatibility work remains.

## NuGet releases

The publishing workflow builds and validates the source at the selected release tag,
creates `.nupkg` and `.snupkg` artifacts, validates the requested semantic version,
and publishes via NuGet trusted publishing or a repository `NUGET_API_KEY` secret.
No credentials are checked in. See [release configuration](docs/publishing.md).
A workflow file is not proof that a package has been published.

## Clean-room provenance

No original AvalonDock implementation, templates, resources, or artwork are copied.
A separate Actions job obtains a pinned reference revision, emits **declarations and
file hashes only**, verifies identical output twice, and deletes its temporary source.
Public documentation describes behavior; all implementation code is independent.
See [provenance](docs/clean-room.md) and [architecture](docs/architecture.md).

This project is not affiliated with or endorsed by Xceed or Uno Platform. Product
names identify compatibility targets. The independent implementation is MIT-licensed.
