# Changelog

All notable changes to UnoDock are recorded here. Versions follow [Semantic Versioning](https://semver.org/); 0.x previews may still change public API.

## 0.1.0-preview.2

Native WinUI 3 is now verified end to end: every Gallery sample and self-test suite runs on the Windows App SDK in CI, alongside the Uno Platform hosts.

### Native WinUI

- Layouts declared in XAML compile with the WinUI XAML compiler. Layout groups expose their positioning members and an object-typed content property in the WinUI build, so markup is the same on both platforms.
- Styles and templates apply to UnoDock controls and to `DockingManager` subclasses created only in code.
- Floating windows can be docked, floated and closed repeatedly. A native window's control is hosted in a disposable root, because WinUI makes the elements of a closed window's tree unusable.
- Views, tabs and flyouts move reliably between panes, floating windows, auto-hide flyouts and `TabView` strips.
- Theme and palette changes reach open menus, hot visual states and controls with application theme dictionaries. Lookups ignore framework theme resources and theme values that `XamlControlsResources` cannot supply.
- Stale `Unloaded` events, a `Loaded` raised after layout, and focus restored after the navigator closes are handled.
- A manager stops rendering when its host window is destroyed.
- See [Uno and WinUI differences](docs/native-winui.md) for everything that differs between the platforms and what UnoDock does about it.

### Fixes on all platforms

- A layout item disposed by the application is replaced the next time its content is shown.
- Relative image URIs resolve to the application package on WinUI.

## 0.1.0-preview.1

First public preview of IDE-style docking for Uno Platform applications on Windows, macOS, Linux and WebAssembly.

### Packages

- `UnoDock`: docking manager, layout model, controls, Generic and Fluent themes, serialization. Builds for Uno Platform (`net10.0`) and native WinUI 3 (`net10.0-windows10.0.26100`).
- `UnoDock.Core`: platform-independent geometry, splitting, placement and layout snapshots.
- `UnoDock.Themes.Aero`, `UnoDock.Themes.Metro`, `UnoDock.Themes.VS2010`: classic themes.

### Features

- **Layout model**: document and tool panes, nested horizontal and vertical splits, tab groups, auto-hide rails with flyouts, hidden tools, and restore to the previous container.
- **Docking**: docking guides and previews across the main window and floating windows, continuous tab and pane tear-off, and keyboard and context-menu commands.
- **Floating windows**: native windows with a themed caption on Windows, macOS and Linux (X11), eight-edge resize, and monitor-aware placement, including per-monitor scaling on Windows. In-surface floating windows in the browser and on mobile.
- **Native WinUI 3**: works without registering the host window. Floating windows are owned by the host and close with it.
- **Themes**: Generic, Fluent (Light, Dark and system), Aero, Metro and VS2010, optional native `TabView` document tabs, and resource keys for every chrome state.
- **XAML and MVVM**: declarative layouts, `DocumentsSource` and `AnchorablesSource` with templates, selectors, container styles and a layout update strategy.
- **Persistence**: `XmlLayoutSerializer` with a content callback, reading and writing the established layout file format.
- **Keyboard and accessibility**: Ctrl+Tab navigator, Ctrl+F4, keyboard navigation of tabs and splitters, and UI Automation peers.
- **Localization**: chrome strings in 14 languages.

### Known limitations

- Linux supports X11; Wayland sessions run through XWayland.
- Mixed-DPI placement on Windows is tested with a two-monitor layout in which the second monitor is simulated (see the [floating windows guide](docs/floating-windows.md)), not yet on physical mixed-DPI hardware.
- Multi-window browser workspaces (`UnoDock.Browser`) are available from source and in the browser workbench, but are not packaged in this preview.
- As a 0.x preview, the public API may still change between previews.
