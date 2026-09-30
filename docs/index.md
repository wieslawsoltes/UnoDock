# UnoDock documentation

Build an application workspace from explicit layout models and native Uno/WinUI controls. Start with a running sample, then add only the hosting and presentation capabilities your application needs.

## Start here

| Guide | Purpose |
|---|---|
| [Getting started](getting-started.md) | Build the gallery and compose a first XAML layout. |
| [Browser workspaces](browser-workspaces.md) | Open real windows, transfer documents/tools, and recover content. |
| [Architecture](architecture.md) | Understand model ownership, presentation, and host responsibilities. |
| [Platform support](platform-support.md) | Distinguish implementation, runtime verification, and remaining boundaries. |

## Application integration

[XAML and MVVM](xaml-workbench.md) explains native dependency properties, source-backed items, binding definitions, and layout templates. [Consumer workspaces](xaml-support.md) covers the compiled examples and replaceable resource dictionaries. [Localization](localization.md) lists the bundled languages and how to add your own. [Floating windows](floating-windows.md) covers captions, continuous tear-off and platform behavior. [Themes](themes.md) lists the Generic, Fluent, Aero, Metro and VS2010 themes and every customizable resource key. [Fluent controls](fluent-controls.md) describes platform-template ownership and semantic resource customization; [Fluent navigator](fluent-navigator.md) covers the Ctrl+Tab presentation and command path.

[Docking and sizing](docking-sizing.md) documents multi-pane allocation and stale-resize protection. [Model invariants](model-invariants.md) explains mutation, reentrancy, exception, and activation semantics. [Native property inspector](native-property-inspector.md) records the built-in-control migration and retained editing rules.

## Build, test, and ship

Use [build and verification](testing.md) for local and hosted acceptance, [deployment](deployment.md) for the documentation/browser Pages artifact, and [contributing](../CONTRIBUTING.md) for source organization and review rules. [Troubleshooting](troubleshooting.md) covers popup blockers, storage, and runtime startup.

The public browser workbench is at [the live playground](https://wieslawsoltes.github.io/UnoDock/playground/). The standard [control gallery](https://wieslawsoltes.github.io/UnoDock/gallery/) runs separately from the multi-window host.
