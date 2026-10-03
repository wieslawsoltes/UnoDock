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

## Styles, templates and code-only subclasses

Native WinUI checks a `Style`'s or `ControlTemplate`'s `TargetType` through the
application's XAML type metadata, which only describes types that appear in markup. Uno
compares the .NET types instead. On WinUI, a class that derives from `DockingManager` and is
only created in code is therefore known only as `Control`. Applying a style or template with
`TargetType="dock:DockingManager"` to it throws `COMException 0x800F1000` ("Cannot apply a
Style with TargetType 'UnoDock.DockingManager' to an object of type 'Control'"). On the UI
thread, that ends the process.

UnoDock handles the common case itself:

* The WinUI build ships a resource dictionary that names every public UnoDock control, so the
  library's own types are always described.
* A `DockingManager` subclass that the metadata does not describe gets the default template
  through a style that targets `Control`, so it renders without any markup.

Two things still need the application's help:

* **Custom styles or templates for a subclass.** Name the subclass once in markup, for
  example in a resource dictionary, before giving it a style or template that targets
  `DockingManager`:

  ```xml
  <Style x:Key="MyManagerTypeMetadata" TargetType="local:MyDockingManager" />
  ```

* **Nested or private subclasses.** These cannot be named in markup. Do not give them a
  template that targets `DockingManager`; they keep the default template.

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

## Framework behavior UnoDock relies on

| | Uno Platform | Native WinUI |
|---|---|---|
| Exception thrown by an event handler or property-changed callback that the framework raises (`DataContextChanged`, `RegisterPropertyChangedCallback`, ...) | returns to the code that changed the value | not returned to the caller |
| Exception thrown by a `DispatcherQueue` callback | does not end the process | ends the process without raising `UnhandledException` |
| `Loaded` | raised when the element joins the live tree | raised on a later dispatcher turn, after the layout pass; `Unloaded` likewise arrives after the change that removed the element |
| A `ContentPresenter`'s or `ContentControl`'s element content, a `TabViewItem`'s header | becomes a visual child when assigned | becomes a visual child during the next layout pass |
| Changed `ContentTemplate` | expanded when set | expanded during the next layout pass |
| `ResourceDictionary.ContainsKey` and `Keys.Contains` | the dictionary's own entries | also merged and theme dictionaries, and the framework's theme resources (for example `ButtonBackgroundPointerOver` in any dictionary) |
| `XamlControlsResources` theme dictionaries | visible | resolved internally, for the application's theme only |
| `MenuFlyoutPresenterStyle` | applied whenever the menu opens | applied when the presenter is created; the presenter is reused |
| One `ResourceDictionary` instance under several theme keys | allowed | rejected: a dictionary has one parent |
| `Focus` on an element that has not been laid out | succeeds | fails |
| `ControlTemplate` without `TargetType` that uses `{TemplateBinding}` on a member `Control` does not declare (such as `Content`) | binds | layout fails with `0x80004005` |
| Elements of a window's content tree after the window closes | reusable | unusable anywhere (`0x800F1000`), even when the window's `Content` was cleared first; only elements removed from that tree before `Close()` survive |
| `CornerRadius` with negative or non-finite values | accepted | the constructor throws `ArgumentException` |
| Relative URIs | accepted | WinRT URIs are absolute |
| Automation pattern of `ToggleMenuFlyoutItem` | Invoke | Toggle |

UnoDock keeps its behavior the same on both platforms where it can:

* **Callback exceptions.** UnoDock still releases partial state when a callback fails, but on
  WinUI such failures do not reach it. Application callbacks that must report errors should
  not depend on exceptions passing through the framework.
* **Deferred rendering.** UnoDock renders layout changes on a `DispatcherQueue` callback.
  If an application handler throws during that render (a template selector, a style selector,
  a content factory), it reaches `DockingManager.RenderingFailed` first. On native WinUI, the
  process then ends; handle errors in those callbacks.
* **Closing floating windows.** A native floating window hosts its control in a disposable
  root element, and the control leaves that root before the window closes, so its panes and
  your content can be docked or floated again. Do the same in your own windows: remove
  content you will show again from the window's tree before calling `Close()`.
* **Re-hosting views.** UnoDock moves content between panes, auto-hide flyouts, floating
  windows and tab strips. It records each content host, so it can release content that WinUI
  has not connected yet. Do not give the same `UIElement` content to another host while
  UnoDock shows it.
* **`Refresh()`** applies template changes; on native WinUI their visuals appear after the
  next layout pass (call `UpdateLayout()` to force it).
* **Coordinates.** `DesktopWindowCoordinates` accepts an element that is already connected
  under its window content but whose `Loaded` event has not been raised yet.
* **Focus.** Restoring a document's last focused editor, and `SetFocus` on a splitter's
  automation peer, lay out newly shown content first.
* **Theme resources.** UnoDock looks up density and palette overrides in each dictionary's own
  entries first, then in its merged dictionaries, on both platforms.
* **Menus.** A docking menu whose palette changes while it exists repaints its existing
  presenter.
* **Images.** `UriSourceToBitmapImageConverter` resolves a relative URI as `ms-appx:///` (a
  file in the application package) on native WinUI.

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
enumeration on Uno and the windows registered with UnoDock on native WinUI.

Some tests are platform-specific:

* Suites that drive another host's native input (X11 XTEST, AppKit events) do not run on
  native WinUI.
* Tests that need a framework callback's exception to reach UnoDock, or an `Unloaded`
  handler to run during a change, are compiled only for Uno.
* Tests that give a test-only `DockingManager` subclass another manager's template use
  `UsingTemplateOf`, which keeps the subclass template on native WinUI.
