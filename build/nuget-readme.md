# UnoDock

IDE-style docking for Uno Platform applications: documents, tool windows, auto-hide, floating windows and themes for Windows, macOS, Linux and WebAssembly, with a familiar `DockingManager` / `LayoutRoot` API.

![UnoDock with the VS2010 theme and floating tool and document windows on Windows 11](https://raw.githubusercontent.com/wieslawsoltes/UnoDock/main/docs/images/readme/windows-vs2010.png)

## Features

- **Docking model**: document and tool panes, nested splits, tab groups, auto-hide rails with flyouts, hidden tools, and restore to the previous container.
- **Floating windows**: native desktop windows with a themed caption, eight-edge resize, docking guides across windows, and monitor-aware placement. In-surface windows in the browser and on mobile.
- **Continuous tear-off**: drag a tab or a tool pane out and it floats into a window that follows the pointer.
- **Themes**: Generic, Fluent (Light, Dark, system), Aero, Metro and VS2010, plus optional native WinUI `TabView` document tabs.
- **XAML and MVVM**: declare layouts in XAML, or bind `DocumentsSource` and `AnchorablesSource` with templates, selectors and container styles.
- **Layout persistence**: `XmlLayoutSerializer` saves and restores layouts, with a callback to reconnect content.
- **Keyboard and accessibility**: Ctrl+Tab navigator, keyboard navigation and UI Automation peers.

## Packages

| Package | Contents |
|---|---|
| `UnoDock` | Docking manager, layout model, controls, Generic and Fluent themes, serialization |
| `UnoDock.Core` | Platform-independent geometry, splitting and placement algorithms |
| `UnoDock.Themes.Aero` | Aero theme |
| `UnoDock.Themes.Metro` | Metro theme |
| `UnoDock.Themes.VS2010` | VS2010 theme |

UnoDock targets .NET 10 with Uno Platform 6.7 (Skia desktop and WebAssembly) and native WinUI 3 on Windows.

## Quick start

```xml
<dock:DockingManager xmlns:dock="using:UnoDock"
                     xmlns:layout="using:UnoDock.Layout"
                     xmlns:themes="using:UnoDock.Themes">
    <dock:DockingManager.Theme>
        <themes:FluentTheme />
    </dock:DockingManager.Theme>
    <layout:LayoutRoot>
        <layout:LayoutPanel Orientation="Horizontal">
            <layout:LayoutAnchorablePane DockWidth="240">
                <layout:LayoutAnchorable Title="Explorer" ContentId="explorer">
                    <TreeView />
                </layout:LayoutAnchorable>
            </layout:LayoutAnchorablePane>
            <layout:LayoutDocumentPane>
                <layout:LayoutDocument Title="Program.cs" ContentId="program">
                    <TextBox AcceptsReturn="True" />
                </layout:LayoutDocument>
            </layout:LayoutDocumentPane>
        </layout:LayoutPanel>
    </layout:LayoutRoot>
</dock:DockingManager>
```

## Links

- [Documentation](https://github.com/wieslawsoltes/UnoDock/blob/main/docs/index.md)
- [Getting started](https://github.com/wieslawsoltes/UnoDock/blob/main/docs/getting-started.md)
- [Browser workbench](https://wieslawsoltes.github.io/UnoDock/playground/) and [control gallery](https://wieslawsoltes.github.io/UnoDock/gallery/)
- [Release notes](https://github.com/wieslawsoltes/UnoDock/blob/main/CHANGELOG.md)
- [Source and issues](https://github.com/wieslawsoltes/UnoDock)

UnoDock is MIT-licensed.
