# Parity status

UnoDock follows the public API and behavior of a widely used WPF docking
library (pinned in `contracts/reference.json`) so that layouts, code and
concepts carry over. This page summarizes where it stands. It is measured, not
assumed: the numbers come from the gates described in
[Build and verification](testing.md).

## Public API

`tools/check-metadata.sh` compares UnoDock's compiled public surface with the
reference's resolved metadata (1,031 entries):

| Result | Entries |
|---|---|
| Declared members matching name, signature, modifiers and defaults | 883 |
| Inherited members matching | 23 |
| Types matching shape | 72 |
| **Total matched** | **978 / 1,031** |

The remaining 53 entries are WPF framework plumbing with no WinUI equivalent:

* **HWND hosting** — `FilterMessage(hwnd, msg, …)` overrides and
  `HwndHost.BuildWindowCore/DestroyWindowCore` on the auto-hide window. UnoDock
  handles the corresponding native messages internally on Windows and raises
  `LayoutFloatingWindowControl.MessageFilterFailed` for diagnostics.
* **WPF element lifecycle overrides** — `OnInitialized`, `LogicalChildren`,
  `OnApplyTemplate` signatures, `Freezable.CreateInstanceCore` and WPF mouse
  overrides (`OnMouseLeave`, `OnMouseRightButtonDown`, …). UnoDock provides the
  same hooks through `DockInputControl` with WinUI pointer semantics.
* **Base-type shape** — 33 types derive from WinUI bases (`Control`,
  `ContentControl`, `Grid`) instead of their WPF counterparts; members and
  behavior are otherwise matched.
* **18 attribute differences** — WPF-only attributes (for example
  `ContentProperty` and `TemplatePart` forms, `XmlIgnore` placement).

## Features

| Area | Status |
|---|---|
| Layout model: panels, pane groups, document/tool panes, anchor sides, hidden tools, floating windows, previous-container restore | Complete |
| `DockingManager` sources, templates, selectors, container styles, layout update strategy | Complete |
| Commands: close, close all, close all but this, float, dock, dock as document, auto-hide, hide, new/move tab groups, activate | Complete |
| Docking guides and previews, including across floating windows | Complete |
| Auto-hide rails and flyouts with resize | Complete |
| Floating tool and document windows with caption menu, maximize, close and eight-edge resize | Complete on Windows, macOS, Linux (X11); in-surface on WebAssembly/mobile |
| Continuous tab and pane tear-off | Complete on native desktop hosts |
| Ctrl+Tab navigator, keyboard navigation, context-menu keys | Complete |
| Header, title, icon and document-menu templates | Complete |
| `XmlLayoutSerializer` with content callback | Complete, verified against reference fixtures |
| Themes: Generic, Aero, Metro, VS2010 (+ Fluent and native TabView tabs) | Complete, palettes authored independently |
| Localized chrome (13 languages) | Complete, translations authored independently |

## Behavior verified against the reference

Layout operations are checked against layout trees recorded from the reference
driven through its public API with the same layouts and calls. The
`reference-behavior` suite compares UnoDock's tree dumps with those recordings
character for character, covering:

* auto-hide (per tool; restore appends) and closing or hiding a floating tool
  window (the window stays in the layout and `Show()` floats the tools again);
* New Horizontal/Vertical Tab Group (wraps a panel-level document pane in a
  document pane group; reorients a single-pane group) and Move To Next/Previous
  Tab Group (adjacent sibling pane only; inserts at the front);
* garbage collection of emptied panes, dock as document and back, floating tool
  commands, `CanRepositionItems` (reordering only), deselection;
* layout restore without a callback (unmatched tools hidden, documents dropped)
  and with one (items kept).

For visual comparison the Gallery renders review scenes selected with
`UNODOCK_SCENARIO` (docked, active-doc, active-tool, float-both, autohide,
context-doc, dropdown, tool-menu, navigator) and themed with
`UNODOCK_GALLERY_THEME`. Each uses the same layout, window size and actions as
the reference review harness, so captures from Windows, macOS and Linux compare
one to one with reference screenshots.

## Validation by feature

Each area is compared with the reference in the way the last column names, and
covered by the listed suites. Every registered desktop suite runs in a real Uno
host in CI on Windows (54 suites), macOS (53) and Linux (53); the per-platform
rows name the suites that exist on one platform only.

| Area | Suites | Platforms | Compared with the reference by |
|---|---|---|---|
| Public API | `tools/check-metadata.sh` | build | Compiled surface against the reference's metadata (above) |
| Layout model and operations | `reference-behavior`, `layout-mutation-invariants`, `parity`, `restore-ownership`, `lifecycle` | all | Tree dumps recorded from the reference, compared character for character |
| Serialization | `interop`, `converters`, `runtime` | all | Layout files written by the reference (`contracts/reference-fixtures`) |
| Sources, templates, MVVM | `mvvm-workspace`, `mvvm-chrome`, `source-ownership`, `source-identity`, `xaml-workbench`, `xaml-workspaces`, `templates-icons` | all | Public API contracts and observed reference behavior |
| Commands and menus | `menu-quality`, `menu-context-lifetime`, `dropdown-quality` | all | Captures of the context, tool and document menus |
| Docking guides and previews | `docking-guides`, `interaction`, `docking-sizing` | all | Captures of guides and resulting layouts |
| Auto-hide | `auto-hide-quality`, `visual-parity` | all | Arranged geometry and per-item sequences from the reference |
| Floating windows | `desktop-floating`, `floating-chrome-*`, `floating-resize-policy-*`, `floating-drag-cleanup`, `window-placement`, `window-lifecycle`, `window-coordinates` | all | Captures of floating tool and document windows |
| Platform window integration | `mac-native` (macOS), `windows-floating-input` (Windows), native WinUI smoke test (Windows) | per platform | Native ownership, activation and shutdown |
| Tab and pane tear-off | `tear-off` | all | Observed drag behavior |
| Navigator and keyboard | `navigator-quality`, `navigator-commit`, `navigator-revocation`, `navigator-sample`, `fluent-navigator`, `focus-ownership`, `input-extensions`, `accessibility-quality` | all | Labels, ordering and focused selection captured from the reference |
| Splitters | `splitter-quality` | all | Observed resize limits |
| Themes | `classic-themes`, `visual-parity`, `presentation-quality`, `fluent-presentation`, `fluent-state-resources`, `uno-theme`, `tabview-strip` | all | Side-by-side captures of Generic, VS2010, Aero and Metro on Windows |
| Localization | `localization` | all | Resource keys of the reference |

## Platform validation

Desktop suites (more than 3,000 tests) run in real Uno hosts on Windows,
macOS and Linux in CI; physical-input suites (`tear-off`,
`windows-floating-input`, XTEST caption drags) run on dedicated desktops.
The native stacking suites also pass on interactive desktops at 200% scaling.
On macOS they need a desktop Space to be current: another application's
full-screen Space hides the test windows, and the suite reports that cause.
