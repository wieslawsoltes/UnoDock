# UnoDock

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Reference metadata](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/reference-metadata.yml)

Independent AvalonDock-style docking for **Uno Platform 6.7**, using **`UnoDock.*`**
namespaces and Uno/WinUI controls.

**Version: 0.1.0-preview.18. Full API, behavioral and visual parity is not verified.**
The target is the pinned public AvalonDock repository and stock presentation, not
separately licensed commercial themes. This is not WPF binary compatibility. The
independently authored implementation is MIT-licensed and is not affiliated with or
endorsed by Xceed or Uno Platform.

## Preview 18

Docking splitters now expose a guarded RangeValue automation provider in DIPs,
Home/End and PageUp/PageDown keys, and a visible keyboard-focus cue that does not
change layout dimensions. The Splitter quality laboratory has live numeric range
inspection and real provider-driven resize controls. Tab providers respect current
activation commands, single-selection rules, current ownership, labels and focus.
See [automation and keyboard contracts](docs/accessibility-quality.md). The earlier
source-ownership and compact MVVM improvements remain included.

## Preview 17: source ownership and compact MVVM chrome

Source reconciliation now validates the complete source/root/strategy generation across
user enumeration, descriptors and insertion callbacks. Replaced sources cannot publish
stale models or invoke stale AfterInsert hooks. Source removal cannot detach a model
moved to another manager or repurposed by application Content replacement. Tracking is
reserved before callbacks so a throwing insertion does not leave an untracked orphan;
aborted unattached candidates can be retried. Foreign direct model ownership is rejected
before either source is mutated. Both sources are still snapshotted before removals.

The MVVM editor now uses compact themed command buttons in a 31-DIP command bar and a
23-DIP status bar. Commands, bindings, editors and drafts survive theme changes. Native
Save/Revert input acceptance and new generic/light/dark/RTL geometry captures accompany
source callback regressions. The classic sample and original observations are unchanged.
See [source ownership and sample acceptance](docs/source-ownership.md). Full original
callback ordering, concurrent collection safety and pixel equivalence are not asserted.

## Preview 16: MVVM workspace and guarded layout restoration

The **MVVM binding** sample now has a source-backed file catalogue, observable open
documents and tool windows, compact editors, dirty tab titles, save/revert/close
commands, close protection, and layout capture/restore by stable identity. Save writes
application-owned text separately from layout XML. Edits made during an asynchronous
save remain dirty. Closing a tab keeps its buffer available for reopening in Workspace.
The existing classic Docking sample and its reference geometry checks remain unchanged.

The library serializer now serializes restore ownership per manager, rejects reentry
from user readers and callbacks, detects replaced/disposed workspaces, and preserves
both primary and cleanup errors. It does not overwrite a callback's replacement root.
Public/protected API mappings and original reference inventories are not relaxed.

See [MVVM workspace and restore boundaries](docs/mvvm-workspace.md). This is not a
pixel-identical recreation of a commercial sample, an arbitrary file editor, or a fix
for every platform's binding-valued style setter behavior. The new sample uses explicit
public adapter bindings and ordinary compiled Uno content templates.

## Preview 15: typed property inspection and safe editing

The classic sample now has native boolean checkboxes and named-enum selectors, color
swatches, collapsible category bands, field descriptions and a resizable property-name
column. Sorting, filtering and column resizing retain the field controls. Search reveals
matching fields in collapsed categories; clearing it restores their expansion state.
Alignment, margin, padding, multiline input and wrapping are editable through validated,
explicitly registered properties.

Selection epochs prevent stale controls from editing a previous document. Draft detection
reads current text rather than relying on deferred native TextChanged events. A competing
application edit produces a conflict instead of being overwritten. Unchanged values do
not run setters, preserving round-trip numeric precision and brush identity. Float/dock
transactions retain the inspector rows after reparenting settles. Programmatic theme
changes synchronize the visible selector; redundant padding no longer clips combo values.

The inspector is sample-owned, not a full Xceed PropertyGrid implementation. No original
PropertyGrid templates, bodies, artwork or fonts were imported. This increment changes
sample presentation and editing behavior, **not the normalized AvalonDock API match count**.
See [inspector implementation, tests and limits](docs/inspector-quality.md).

## UnoDock namespaces and classic samples

**Breaking change since preview 14:** product types live in `UnoDock`, `UnoDock.Layout`,
`UnoDock.Controls`, `UnoDock.Themes`, `UnoDock.Converters` and their subnamespaces. There
are no old-namespace aliases or forwarding assemblies. Update imports and XAML `using:`
declarations and recompile. Assembly/package names remain `UnoDock` and `UnoDock.Core`.
Stable layout XML element names and application ContentId values do not change. See
[namespace migration](docs/namespace-migration.md).

The default **Docking** sample independently recreates the documented public arrangement:
Properties on the left, two documents in the center, Alarms/Journal on the right, and
Agenda/Contacts auto-hide rails. A compact native menu bar, sample/theme selectors,
vector toolbar and status bar occupy 87 DIPs. **IDE workspace**, **MVVM binding**, and
all earlier feature laboratories remain available from Samples and Diagnostics.

Commands invoke actual document, docking, source-collection and serialization operations.
The Properties inspector follows the last-focused document, supports Enter/blur commit
and Escape cancellation, and validates values; it does not execute arbitrary reflected
properties. Large-text density expands docking captions, tabs and rails. Theme changes
retain the model and editor content. See [sample fidelity and acceptance](docs/sample-quality.md).

## Run the gallery

Committed pins: **Uno.Sdk 6.7.30**, **Uno.WinUI 6.7.135**, **Uno.Templates 6.7.30**.
`global.json` selects .NET 10 with latest-feature roll-forward. These are reproducible
pins, not an assertion of the latest registry versions. Upgrades are explicit.

```bash
dotnet run --project samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop -p:UnoDockLibraryFrameworks=net10.0
```

The desktop head uses Uno Skia on Windows, macOS and Linux. Browser publishing:

```bash
dotnet workload install wasm-tools
dotnet publish samples/UnoDock.Gallery -c Release -f net10.0-browserwasm \
  -p:UnoDockTargetFrameworks=net10.0-browserwasm -p:UnoDockLibraryFrameworks=net10.0 \
  -o artifacts/browser
```

Android/iOS gallery heads and device acceptance are not included. Browser publishing is
not browser runtime/input acceptance.

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

Use `xmlns:dock="using:UnoDock"` and `xmlns:layout="using:UnoDock.Layout"` in Uno XAML.
WPF namespace URIs, framework types, resource dictionaries and templates need migration.
Restoration constructs a detached layout and resolves content before replacing the active
root. Bounded XML parsing rejects unsafe/unknown input; supplied XmlReader settings remain
the caller's responsibility. Arbitrary CLR objects are not deserialized.

## Docking, presentation and extensibility

Nested panels and document/tool groups support header reordering, combining and splitting.
GuidesOnly requires an explicit glyph or visible tab/caption target; GuidesAndEdges and
EdgesOnly provide broad zones. The same validated plan drives preview, hit testing and
drop. Native floating targets receive guides projected into their own clients. Use
FloatingWindowMode.InSurface for managed hosting or supply ICrossWindowCoordinates for
another host. Transformed detection regions conservatively enclose all four corners.

Document tabs sit above content; tool tabs below. Redundant single-tool strips are hidden,
side labels rotate, and custom title/header templates remain usable. Auto-hide moves the
requested tool and reveals on hover without activation. Flyouts reserve a separate resize
gutter. Splitters preview bounded geometry and commit preserved sizing units on release;
cancellation and conflicting model/window changes invalidate stale work.

Compact document/tool menus replay recorded original command order, labels, enabled and
collapsed states, and row geometry. Menu rows retain identity through refreshes. Command
subscriptions are scoped to openings, worker requeries coalesce, and callback-driven
command/root replacement is revalidated. Bulk-close operations guard reentrancy and stay
in their originating workspace. Shared custom menus preserve templates, bindings and
application-owned context while cleaning temporary assignments.

Ctrl+Tab opens the navigator, Control release/Enter commits, Escape cancels, and Ctrl+F4
closes the active document. Navigator collections/containers retain identity with bounded
selected-row reveal. Named ListBox parts remain supported; NavigatorListBox realizes rows
for FrameworkElement model adapters on the pinned Uno host. Default rows are not virtualized.
Retained editor presenters and weak focus records survive selection and docking.

Window commands use explicit hosts. Managed/native chrome, normal/maximized bounds,
close cancellation/reentrancy and Win32 message-filter lifetimes have targeted tests.
Protected focus/input and initialization adapters run on the real operation path. These
are not complete WPF Window, Freezable, HwndHost, logical-tree or routed-event implementations.

## Architecture and deterministic contracts

| Area | Projects |
| --- | --- |
| Portable geometry, coordinates, drag state and bounded XML | `src/UnoDock.Core` |
| Models, controls, native adapters, persistence and themes | `src/UnoDock` |
| Interactive samples and laboratories | `samples/UnoDock.Gallery` |
| Portable, real-host and visual acceptance | `tests/UnoDock.Core.Tests`, `tests/UnoDock.Runtime.Tests`, `tests/UnoDock.VisualTests` |
| Syntax and resolved metadata inventories | `tools/ApiScan`, `tools/ApiMetadata` |
| Independent original public observations | `tools/ReferenceProbe`, `tools/ReferenceVisualProbe`, `tools/ReferenceSampleProbe` |

The original is pinned at **2c71faba5eecc1b6ae6cd3d269408e0df37715d8**. The syntax inventory
has 996 declarations; resolved Release metadata has 105 exported type names and 1,031
entries, including attributes, enum/default values, constraints and inheritance. Repeated
inventories must be byte-identical; unresolved types fail.

The resolved comparison is **978/1,031**, with **53 signature/type entries** and
**18 separately reported attribute differences** unresolved. Callable virtual adapters
are not misreported as WPF overrides. Original inventories and diagnostic allowlist
remain unchanged. Explicit per-type namespace mappings describe the rename, not a parity
gain. The no-regression gate differs from the still-unsatisfied full strict-parity gate.

Original bodies, templates, resource definitions, artwork and fonts are not copied into
the product. Public protocol observations are distinguished from native input acceptance.
Measured pane/guide geometry and menu replays establish their specific scenes, not pixel
identity. Fonts, rasterization, arbitrary templates and non-client details differ. Original
navigator setter semantics, framework inheritance, complete event ordering, custom
serialization, accessibility, mobile/touch/pen, IME, multi-monitor DPI and workload-level
performance equivalence remain open.

See [architecture](docs/architecture.md), [provenance](docs/clean-room.md),
[compatibility](docs/compatibility.md), [input extensions](docs/input-extensions.md),
[window lifecycle](docs/window-lifecycle.md), [window shell](docs/window-shell.md),
[navigator](docs/navigator-quality.md), [guides](docs/docking-guides.md),
[splitters](docs/splitter-quality.md), [auto-hide](docs/auto-hide-quality.md),
[menus](docs/menu-quality.md), and [dropdowns](docs/dropdown-quality.md).

## Validation and packaging

```bash
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/test-results
# In a desktop session, or prefixed by xvfb-run -a on Linux:
UNODOCK_SELFTEST=1 dotnet run --project samples/UnoDock.Gallery -c Release \
  -f net10.0-desktop -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

Actual JSON/JUnit reports, execution summaries and every CI job conclusion establish the
counts and results for a consumed revision. Platform suites overlap. The inspector
suite registers 35 Linux / 33 Windows cases, including two opt-in XTEST scenarios; these
are part of the platform totals, not additional independent cases. Eight namespace
invariants and 23 metadata-comparator cases remain enabled. Standalone selectors include
`UNODOCK_TEST_SUITE=inspector-quality`, `source-ownership` and `mvvm-chrome`. Native input
requires a dedicated test display and explicit `UNODOCK_NATIVE_INPUT_TESTS=1`.

Normal input acceptance has no extra root-event observer. UNODOCK_INPUT_TRACE=1 enables
bounded diagnostics. Failed Linux acceptance may generate separate diagnostics without
replacing its original report or clearing the failure. Desktop SDK/XAML builds use
warnings-as-errors; package/browser warnings have separate accounting.

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
