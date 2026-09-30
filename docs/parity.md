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

## Platform validation

Desktop suites (more than 3,000 tests) run in real Uno hosts on Windows,
macOS and Linux in CI; physical-input suites (`tear-off`,
`windows-floating-input`, XTEST caption drags) run on dedicated desktops.
Known environment-dependent cases (window stacking relative to other
applications on an interactive desktop) are called out in
[Platform support](platform-support.md).
