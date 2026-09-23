# UnoDock

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Reference metadata](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml)

Independent AvalonDock-style docking for **Uno Platform 6.7**, using the **`UnoDock`** namespace family and Uno/WinUI controls.

**Version: 0.1.0-preview.15. Full API, behavioral and visual parity is not verified.**
The target is the pinned public AvalonDock repository and stock presentation, not
separately licensed commercial themes. Framework mappings require source/XAML migration;
this is not binary compatibility with WPF. The independently authored implementation is
MIT-licensed and is not affiliated with or endorsed by Xceed or Uno Platform.

## Preview 15: typed property inspection and safe editor ownership

The classic sample now has native boolean and enum editors, color swatches, collapsible
category bands, field descriptions and a resizable property-name column. Filtering,
sorting and column resizing retain editors. Alignment, margin, padding, multiline input
and wrapping are editable through validated, explicitly registered properties.

Selection epochs prevent stale controls from editing a previous document. Deferred
native TextChanged events cannot erase drafts; competing application edits cause a
conflict instead of being overwritten. Unchanged numeric and brush values do not run
setters, preserving round-trip precision and brush identity. Float/dock transitions
retain the current inspector rows after the tree transaction settles. Theme selectors
now reflect programmatic theme changes and compact combo values have more usable space.

See [inspector implementation, tests and limits](docs/inspector-quality.md). The original
API comparison is unchanged; this is sample/workflow progress, not an API percentage gain.

## Preview 14: UnoDock namespaces and classic samples

**Breaking change:** product types now live in `UnoDock`, `UnoDock.Layout`,
`UnoDock.Controls`, `UnoDock.Themes`, `UnoDock.Converters` and their corresponding
subnamespaces. There are no deprecated Xceed aliases. Update C# imports and Uno XAML
`using:` declarations; the assembly and package names stay `UnoDock` / `UnoDock.Core`.
See [namespace migration](docs/namespace-migration.md).

The default sample is now a compact, independently authored **classic docking** scene,
inspired by the documented public AvalonDock example: Properties, two documents,
Alarms/Journal, and Agenda/Contacts auto-hide rails. A native menu bar and compact sample
and theme selectors replace the oversized banner and crowded laboratory toolbar.
**IDE workspace**, **MVVM documents**, and all previous diagnostic laboratories remain
available from the Samples and Diagnostics menus. The sample property inspector edits a
whitelisted subset of real model/editor properties and validates input; it is not a
complete port of Xceed PropertyGrid.

Large text now expands tool captions, document/tool tabs and auto-hide rails instead of
keeping 12-point geometry. Default-density geometry is unchanged. Manager/grid
initialization and document-tab-panel pointer-leave extension points run on the actual
lifecycle/input path. Manager-owned-view enumeration takes a stable snapshot.

The original reference inventories and diagnostic allowlist remain unchanged. Explicit
per-type namespace mappings document the deliberate source-breaking rename; they do not
normalize virtual overrides, framework type shape or behavior. Original probes remain
in the original namespace and are isolated from product packaging.

See [sample fidelity and acceptance](docs/sample-quality.md),
[dropdown lifetime](docs/dropdown-quality.md), and [prior validation](docs/validation-preview13.md).

## Included menus, visuals and interaction

Compact document/tool menus match ten original public observations of command order,
labels, enabled/collapsed states and 22-DIP rows. Menu and row identities survive refreshes.
Command subscriptions are scoped to an opening, worker requeries are coalesced, and
command/root identity is revalidated after callbacks. Bulk-close operations stay in their
original workspace and guard reentrancy. Shared custom menus preserve styles, bindings
and local values, including callback-driven replacement and removal during context cleanup.

Compact pane chrome places document tabs above content and tool tabs below it, hides
redundant single-tool strips, rotates side labels and retains custom templates. Auto-hide
moves the requested tool and reveals on hover without activation. Flyouts reserve a
separate resize gutter. Splitters show a bounded ghost and commit their preserved sizing
units only on release. Cancellation and model/window changes invalidate stale work.

Document/tool headers reorder, combine and split groups. Default GuidesOnly docking
requires an explicit glyph or visible tab/caption target; GuidesAndEdges and EdgesOnly
provide broad zones. The same validated plan drives preview, hit testing and drop.
Native floating targets receive guides projected into their own clients. In-surface
hosting is available through FloatingWindowMode.InSurface; other host environments can
supply ICrossWindowCoordinates.

Ctrl+Tab opens the navigator, Control release/Enter commits, Escape cancels, and Ctrl+F4
closes the active document. Navigator collections and row containers retain identity with
bounded selected-row reveal. Named ListBox parts remain supported; NavigatorListBox
provides realized rows for FrameworkElement model adapters on the pinned Uno host.
Default navigator rows are not virtualized. Retained editor presenters and weak focus
records survive selection and docking. Protected input/focus hooks execute on operation
paths, rather than merely receiving notifications after them.

Window commands target explicit hosts. Managed/native chrome, normal/maximized bounds,
close cancellation/reentrancy and Win32 message-filter lifetimes have targeted tests.
These are not complete WPF Window, Freezable, HwndHost or routed-event implementations.

## Run the gallery

Committed pins: **Uno.Sdk 6.7.30**, **Uno.WinUI 6.7.135**, **Uno.Templates 6.7.30**.
`global.json` selects .NET 10 with latest-feature roll-forward. These are reproducible
pins, not an assertion of the latest registry versions. Dependency upgrades are explicit.

```bash
dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

The Diagnostics menu includes Window shell, Window lifecycle, Input extensions, Visual
parity, Navigator quality, Docking guides, Splitter quality, Auto-hide quality and Menu
quality. Open Dropdown contracts from Menu quality. The workbench demonstrates native
and in-surface windows, capabilities/cancellation, source-bound editors, MRU navigation,
large tab sets, RTL/density/themes, deferred resizing, XML persistence and command changes.

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
using UnoDock;
using UnoDock.Layout;
using UnoDock.Layout.Serialization;
using LayoutPanel = UnoDock.Layout.LayoutPanel;

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

Use `xmlns:dock="using:UnoDock"` and
`xmlns:layout="using:UnoDock.Layout"` in Uno XAML. WPF namespace URIs,
framework types, resource dictionaries and templates need explicit migration.
Restoration constructs a detached layout and resolves content before replacing the
active root. Bounded XML parsing rejects unsafe/unknown input; supplied XmlReader
settings remain the caller's responsibility. Arbitrary CLR objects are not deserialized.

## Architecture, contracts and provenance

| Area | Projects |
| --- | --- |
| Portable geometry, coordinates, drag state and XML | `src/UnoDock.Core` |
| Models, controls, native adapters, persistence and themes | `src/UnoDock` |
| Interactive workbench and laboratories | `samples/UnoDock.Gallery` |
| Portable, real-host and visual acceptance | `tests/UnoDock.Core.Tests`, `tests/UnoDock.Runtime.Tests`, `tests/UnoDock.VisualTests` |
| Syntax and resolved metadata inventories | `tools/ApiScan`, `tools/ApiMetadata` |
| Independent original public observations | `tools/ReferenceProbe`, `tools/ReferenceVisualProbe`, `tools/ReferenceSampleProbe` |

The original is pinned at **2c71faba5eecc1b6ae6cd3d269408e0df37715d8**. The syntax inventory
has 996 declarations; resolved Release metadata has 105 type names and 1,031 entries,
including attributes, enum/default values, constraints, constructors and inheritance.
Repeated inventories must be byte-identical; unresolved types fail. Original implementation
bodies, templates, resource definitions, artwork and font files are not copied into the
product. Public protocol observations are distinguished from native input acceptance.

The preview-13 resolved regression comparison was **978/1,031**, with **53 signature/type entries**
and **18 separately reported attribute differences** unresolved. Adding callable protected
virtual input methods does not make them WPF overrides: that distinction remains visible.
The comparator and diagnostic allowlist remain strict. Only explicit namespace mappings
are added for the intentional rename; the baseline mapping digest is rebound accordingly. The full strict-parity gate
remains unsatisfied; API counts do not measure whole-product feature completeness.

Measured pane/guide geometry and menu row replays establish their specific scenes, not
pixel identity. Fonts, glyph rasterization, arbitrary templates and non-client details
differ. Original navigator setter semantics, framework inheritance, complete event
ordering, custom serialization, accessibility, mobile/touch/pen, IME, multi-monitor DPI
and workload-level performance equivalence remain open.

See [architecture](docs/architecture.md), [provenance](docs/clean-room.md),
[compatibility boundaries](docs/compatibility.md), [input extensions](docs/input-extensions.md),
[window lifecycle](docs/window-lifecycle.md), [window shell](docs/window-shell.md),
[navigator](docs/navigator-quality.md), [guides](docs/docking-guides.md),
[splitters](docs/splitter-quality.md), [auto-hide](docs/auto-hide-quality.md),
[menus](docs/menu-quality.md), and [dropdowns](docs/dropdown-quality.md).

## Validation and packaging

```bash
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/test-results
# Run in a desktop session, or prefix with xvfb-run -a on Linux:
UNODOCK_SELFTEST=1 dotnet run --project samples/UnoDock.Gallery -c Release \
  -f net10.0-desktop -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

The registered matrix contains **2,335 core/Linux C# cases**, a **430-case Windows
subset** and **23 Python comparator cases**. Linux opts into **48 XTEST scenarios** on
a dedicated display. Platform cases overlap. Inspect JSON/JUnit reports and every CI
job conclusion for the consumed revision. SDK/XAML desktop builds use warnings-as-errors.

Normal input acceptance has no extra root-event observer. UNODOCK_INPUT_TRACE=1 enables
bounded diagnostics. After a failing Linux run, CI can collect separate diagnostic evidence
without replacing its original report or clearing the failure. Historical intermittent
input behavior is recorded in [preview-12 validation](docs/validation-preview12.md).

Artifacts include exact source/revision/checksum, API inventories/differences, platform
reports and PNG/XML captures, desktop/browser output and packages/symbols. X11/Win32
runtime evidence is distinct from native WinUI package builds, macOS builds and browser
publishing. Native WinUI XAML packing uses Visual Studio MSBuild:

```powershell
msbuild src/UnoDock/UnoDock.csproj -restore -t:Pack -p:Configuration=Release `
  -p:PackageOutputPath=artifacts/packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
dotnet pack src/UnoDock.Core -c Release -o artifacts/packages
```

Publish NuGet accepts a release tag or manual version, validates/tests, builds packages
and symbols, then uses NUGET_API_KEY or trusted publishing. Credentials, account policies
and approvals are separate configuration. Built packages do not imply NuGet.org publication.
Stable 1.0+ requires a reviewed full-compatibility attestation bound to the source tree;
none is supplied. See [publishing](docs/publishing.md).
