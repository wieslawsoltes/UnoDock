<div align="center">

# UnoDock

**AvalonDock-style workspaces, built for Uno Platform.**

Composable documents and tool windows. Native Fluent controls. Desktop and browser hosting.

[![Build and test](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/ci.yml)
[![Browser & Pages](https://github.com/wieslawsoltes/UnoDock/actions/workflows/browser-pages.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/browser-pages.yml)
[![Source quality](https://github.com/wieslawsoltes/UnoDock/actions/workflows/source-quality.yml/badge.svg)](https://github.com/wieslawsoltes/UnoDock/actions/workflows/source-quality.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-6875d9.svg)](LICENSE)

[**Documentation**](https://wieslawsoltes.github.io/UnoDock/docs/index.html) · [**Browser workbench**](https://wieslawsoltes.github.io/UnoDock/playground/) · [**Control gallery**](https://wieslawsoltes.github.io/UnoDock/gallery/) · [**Getting started**](docs/getting-started.md)

</div>

---

UnoDock is an independently authored docking library for applications built with **C#, XAML, Uno Platform, and WinUI**. It brings familiar IDE-style documents, tool panes, auto-hide, tab groups, floating windows, and layout persistence to a composable layout model—without making your application's content part of the docking engine.

The browser workbench runs the **real Uno WebAssembly renderer**. Its optional browser host opens separate browser windows and coordinates content ownership, editing, transfer, and recovery. It is not a JavaScript recreation of the docking UI.

## Built for application workspaces

| Capability | What it provides |
|---|---|
| **Docking model** | Document/tool panes, nested splits, tab groups, auto-hide, floating, and guarded activation. |
| **Native desktop windows** | Floating tool/document hosts, custom captions, docking guides, and resize constraints on supported desktop backends. |
| **Fluent presentation** | Platform-templated controls, scoped Light/Dark resources, density profiles, semantic states, and keyboard-focus visuals. |
| **XAML and MVVM** | Declarative layouts, content/header templates, document/tool sources, item styles, and native binding definitions. |
| **Browser workspaces** | User-initiated popup windows, cross-window content transfer, drag-to-dock targets, lease-checked writes, and local recovery. |
| **Explicit contracts** | Independent API/reference checks, adversarial lifecycle tests, cross-platform runtime suites, and browser automation. |

## Start with the samples

```sh
git clone https://github.com/wieslawsoltes/UnoDock.git
cd UnoDock

dotnet run --project samples/UnoDock.Gallery \
  -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0
```

Use a .NET 10 SDK compatible with `global.json`. The repository pins `Uno.Sdk` rather than silently adopting a different UI runtime. Platform prerequisites are described in the [getting-started guide](docs/getting-started.md).

The Gallery includes classic docking, IDE/MVVM workspaces, compiled XAML examples, Fluent navigation, typed property inspection, and focused interaction laboratories. The [browser guide](docs/browser-workspaces.md) covers real popup windows, docking between them, and content recovery.

## Compose a layout in XAML

```xml
<dock:DockingManager
    xmlns:dock="using:UnoDock"
    xmlns:layout="using:UnoDock.Layout"
    xmlns:themes="using:UnoDock.Themes"
    ChromeDensity="Comfortable">
    <dock:DockingManager.Theme>
        <themes:FluentTheme />
    </dock:DockingManager.Theme>
    <layout:LayoutRoot>
        <layout:LayoutPanel Orientation="Horizontal">
            <layout:LayoutAnchorablePane DockWidth="240">
                <layout:LayoutAnchorable
                    Title="Explorer" ContentId="explorer" CanClose="False">
                    <TreeView />
                </layout:LayoutAnchorable>
            </layout:LayoutAnchorablePane>
            <layout:LayoutDocumentPane>
                <layout:LayoutDocument Title="Welcome" ContentId="welcome">
                    <TextBox AcceptsReturn="True" TextWrapping="Wrap" />
                </layout:LayoutDocument>
            </layout:LayoutDocumentPane>
        </layout:LayoutPanel>
    </layout:LayoutRoot>
</dock:DockingManager>
```

This fragment belongs inside a normal Uno Page/UserControl with its presentation namespace. See [complete XAML usage](docs/xaml-workbench.md), [consumer templates and themes](docs/xaml-support.md), and [Fluent control styling](docs/fluent-controls.md).

## Project structure

```text
src/
  UnoDock.Core/          Portable contracts and algorithms
  UnoDock/               Layout model, controls, native hosting, serialization
  UnoDock.Browser/       Optional browser session and content-view adapters
samples/
  UnoDock.Gallery/       Desktop, WebAssembly, and compiled-XAML samples
site/                   Documentation shell and browser workspace host
tests/                  Runtime, browser, source, and compatibility tests
```

Application content remains application-owned. In a browser, separate windows have separate runtimes: use an `IBrowserDockViewFactory` to reconstruct an editor from its portable payload. A CLR object or `UIElement` is never presented as transferable between runtimes. [Architecture →](docs/architecture.md)

## Compatibility and release status

UnoDock follows public AvalonDock-style model and interaction conventions, adapted to Uno/WinUI. It **does not promise WPF binary compatibility, arbitrary WPF XAML support, or complete API/pixel equivalence**. The reference checks report remaining differences rather than hiding them behind a broad compatibility claim.

The repository is a **preview**. The version in `Directory.Build.props` describes source/package metadata; a successful CI package build is not evidence of a published NuGet release. Build from source or use an explicitly published release artifact. [Compatibility and platform boundaries →](docs/platform-support.md)

Browser popup permission, browser-owned title bars, and cross-window drag behavior are documented separately from native desktop windowing. Screenshots and test reports are retained as workflow artifacts; exact tested revisions accompany deployment artifacts.

## Documentation

[Documentation index](docs/index.md) · [Browser workspaces](docs/browser-workspaces.md) · [Docking and sizing](docs/docking-sizing.md) · [Build and verification](docs/testing.md) · [Publishing](docs/deployment.md) · [Contributing](CONTRIBUTING.md)

Historical preview notes have moved to [the archive](docs/history/early-preview-notes.md). Current feature guides, not that archive, define the supported behavior.

## License

[MIT](LICENSE). Independently authored and not affiliated with or endorsed by Xceed or Uno Platform. AvalonDock, Uno Platform, and WinUI are names of their respective projects/owners.
