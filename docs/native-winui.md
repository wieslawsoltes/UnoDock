# Native WinUI and Uno differences

UnoDock ships two builds in each package: `net10.0` for Uno Platform (Skia desktop,
WebAssembly, mobile) and `net10.0-windows10.0.26100` for native WinUI 3 on the Windows App
SDK. The public API, layout model and behavior are the same. This page lists where the two
platforms differ, what UnoDock does about each difference, and what an application has to
know when it targets native WinUI.

## Building

| | Uno Platform | Native WinUI |
|---|---|---|
| Build tool | `dotnet build` | Visual Studio MSBuild (`msbuild`) |
| Packaging in the samples | framework-dependent `dotnet <app>.dll` | unpackaged (`WindowsPackageType=None`), self-contained Windows App SDK |

Uno.Sdk refuses to build a WinUI class library that contains XAML with `dotnet build`
(error `UNOB0008`), because the WinUI XAML compiler needs Visual Studio MSBuild. UnoDock's
packages are therefore packed on Windows with MSBuild, and the repository's native WinUI
heads build the same way:

```sh
msbuild samples/UnoDock.Gallery/UnoDock.Gallery.csproj -restore -p:Configuration=Release -p:Platform=x64 -p:UnoDockTargetFrameworks=net10.0-windows10.0.26100.0 -p:UnoDockLibraryFrameworks=net10.0-windows10.0.26100.0
```

Applications that only *consume* the UnoDock package are not affected: an application
builds the way any WinUI application does.

On Windows, the package's dependency on `Uno.WinUI` contributes only the small
`Uno.UI.Toolkit` assembly; Uno's build tasks are excluded.

## XAML markup for layouts

The WinUI XAML compiler writes markup as XAML Binary Format (XBF). Two of its limits affect
layouts declared in markup, and UnoDock works around both in the WinUI build:

* **Members of generic base classes.** XBF cannot encode a property declared on a generic
  type. Layout groups inherit `Children`, `DockWidth`, `DockMinWidth`, `FloatingLeft`,
  `IsMaximized` and the other positioning members from `LayoutGroup<T>` and
  `LayoutPositionableGroup<T>`. The WinUI build declares these members again on each
  concrete group (`LayoutPanel`, `LayoutAnchorablePaneGroup`, `LayoutDocumentPaneGroup`,
  `LayoutAnchorablePane`, `LayoutDocumentPane`, `LayoutAnchorGroup`, `LayoutAnchorSide`),
  forwarding to the base. Without this, any layout in markup fails to compile with
  `WMC0610: The XAML Binary Format (XBF) generator reported syntax error '0x07da'`.
* **Interface-typed collections.** The compiler checks markup items against a collection's
  item type through base classes only, not interfaces. `LayoutPanel` holds
  `ILayoutPanelElement` items, so a pane could not be added in markup. In the WinUI build,
  the content property of each group is `XamlChildren`, an object-typed view of `Children`
  that checks each item's type when it is added.

Markup is therefore the same on both platforms, including the content syntax. Code always
uses `Children`. Property-element syntax for the collection (`<LayoutPanel.Children>`) does
not compile on native WinUI; omit it and place the children directly in the group.

## Styles and code-only subclasses

Native WinUI checks a `Style`'s `TargetType` through the application's XAML type metadata,
which only describes types that appear in markup. A class that derives from `DockingManager`
and is only created in code is unknown to that metadata, so assigning a style with
`TargetType="dock:DockingManager"` to it throws `COMException 0x800F1000` ("No installed
components were detected"). Uno compares the .NET types and accepts it.

Name such a subclass once in markup, for example in a resource dictionary:

```xml
<Style x:Key="MyManagerTypeMetadata" TargetType="local:MyDockingManager" />
```

## Windows and the host

| | Uno Platform | Native WinUI |
|---|---|---|
| Enumerating application windows | `ApplicationHelper.Windows` | not available |
| Finding the window that hosts an element | from the enumeration | from the `XamlRoot`'s `ContentIslandEnvironment.AppWindowId` |
| Window registration | optional | optional (`SystemCommands.RegisterWindow` still works) |

UnoDock resolves the host window without registration on both platforms. On native WinUI
it owns floating windows through the host's window handle, activates it for the navigator
and maps drop targets through it.

**Shutdown.** Windows destroys owned windows before the owner's `AppWindow` raises
`Destroying`, and a WinUI window destroyed that way ends the process. UnoDock therefore
hides and releases its floating windows as soon as the host starts to hide, owns and shows
them again if the host reappears, and closes them once the host is gone. AppWindow APIs are
not called on a window that is being destroyed.

## Platform services

* **High contrast.** `AccessibilitySettings.HighContrastChanged` throws `0x80070490` in an
  unpackaged WinUI app. UnoDock reads high contrast with `SPI_GETHIGHCONTRAST` on every
  Windows host instead.
* **`Dispose` on elements.** Uno's `FrameworkElement` has a `Dispose()` method and native
  WinUI's does not. Code that declares its own `Dispose()` on an element with `new` builds on
  both; on WinUI the compiler reports that `new` is not needed (`CS0109`).
* **Browser workspaces.** `UnoDock.Browser` runs only on Uno's WebAssembly host.

## Testing

The Gallery has a native WinUI head that runs every sample and the self-test suites under
the Windows App SDK. Test code finds windows through `TestWindows`, which uses Uno's window
enumeration on Uno and the windows registered with UnoDock on native WinUI. Suites that
drive another host's native input (X11 XTEST, AppKit events) do not run on native WinUI.
