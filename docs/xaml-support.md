# XAML workspaces and Uno themes

UnoDock keeps the AvalonDock layout vocabulary and docking operations while using
Uno/WinUI dependency properties, compiled XAML, native bindings and resource dictionaries.
The samples are original implementations, not copied AvalonDock theme resources.

## Run the compiled samples

Open **Samples > XAML workspaces** in the gallery. The three workspaces are compiled
UserControls, not XAML strings parsed at runtime:

- **Declarative layout** declares documents, nested tool/document panes, an auto-hide
  rail, native editors and command bars in XAML. Its commands float/dock, auto-hide,
  change density and direction, and save/restore an in-memory XML layout.
- **MVVM bindings** supplies observable application objects through `DocumentsSource`
  and `AnchorablesSource`, two-way `ActiveContent`, an item-container style and a
  `DataTemplateSelector`. New/remove commands edit the source collection.
- **Templates** replaces the manager's `ControlTemplate` without replacing its
  docking surface or editor. `x:Bind` replaces `LayoutDocument.Content` and observes
  changes to the nested title. Its overflow menu switches to a consumer-authored
  `ResourceDictionaryTheme`, including Light, Dark and HighContrast dictionaries.

The shared document view, content/header templates, palette dictionaries, and
command bars are also compiled XAML. Page code initializes bindings and commands;
it does not construct the sample layout or editor trees. The gallery wrapper which
opens a sample as a document is ordinary application integration code.

## Declarative layout

Use WinUI `using:` namespaces, not WPF assembly or AvalonDock XML namespace aliases:

```xml
<dock:DockingManager
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:dock="using:UnoDock"
    xmlns:layout="using:UnoDock.Layout"
    xmlns:themes="using:UnoDock.Themes"
    FloatingWindowMode="InSurface">
    <dock:DockingManager.Theme>
        <themes:FluentTheme RequestedTheme="Default" Density="Comfortable" />
    </dock:DockingManager.Theme>
    <layout:LayoutRoot>
        <layout:LayoutPanel Orientation="Horizontal">
            <layout:LayoutDocumentPane>
                <layout:LayoutDocument ContentId="editor" Title="Workspace.xaml">
                    <TextBox Text="Editable content" AcceptsReturn="True" />
                </layout:LayoutDocument>
            </layout:LayoutDocumentPane>
            <layout:LayoutAnchorablePane DockWidth="240">
                <layout:LayoutAnchorable ContentId="properties" Title="Properties"
                                         CanClose="False">
                    <TextBlock Text="Application properties" />
                </layout:LayoutAnchorable>
            </layout:LayoutAnchorablePane>
        </layout:LayoutPanel>
    </layout:LayoutRoot>
</dock:DockingManager>
```

`DockingManager.Layout`, `LayoutRoot.RootPanel`, group children and content all
support implicit XAML content syntax. Explicit property-element syntax works too.
Each instantiated layout belongs to one manager; do not share a mutable layout or
UIElement through an application-wide resource. Share data and templates instead.

## MVVM and item styles

The manager's source, template, selector, style, active-content and theme properties
are native dependency properties. `LayoutContent.Title`, `ContentId`, `Content`,
`IconSource` and `ToolTip` are native binding targets. The new content binding retains
its expression when the payload changes; views follow the new payload and detach
the previous one. Use an explicit `Source` with ordinary `{Binding}` on nonvisual
layout elements, or the page's `x:Bind` with `Mode=OneWay`/`TwoWay`.

Layout models are not a WinUI visual tree. They do not inherit a page's DataContext
through `Parent`. For application objects, prefer `LayoutItemContainerStyle`: each
`LayoutItem.DataContext` is that item's application payload. The adapter bridges
selection, activation and capabilities through the guarded model APIs rather than
changing ownership or activation fields behind the model's invariant checks.

### Per-item bindings in native styles

WPF's `{Binding}` in a scalar `Setter.Value` is not a portable per-item-binding
contract in Uno/WinUI. Use UnoDock's attached binding definitions in the same
AvalonDock-style `LayoutItemContainerStyle` extension point:

```xml
<Style x:Key="DockItemStyle" TargetType="controls:LayoutItem">
    <Setter Property="controls:LayoutItemBindings.Title">
        <Setter.Value>
            <controls:LayoutBinding Path="Title" Mode="TwoWay" />
        </Setter.Value>
    </Setter>
    <Setter Property="controls:LayoutItemBindings.ContentId">
        <Setter.Value>
            <controls:LayoutBinding Path="ContentId" />
        </Setter.Value>
    </Setter>
    <Setter Property="controls:LayoutItemBindings.CanClose">
        <Setter.Value>
            <controls:LayoutBinding Path="CanClose" Mode="TwoWay" />
        </Setter.Value>
    </Setter>
</Style>
```

Here `controls` is `using:UnoDock.Controls`. The supported attached definitions are
`Title`, `ContentId`, `IconSource`, `CanClose`, `CanFloat`, `IsSelected`, `IsActive`,
`CanHide` (tools) and `Description` (documents). A definition creates an independent
native Binding for each item; shared styles do not share a live binding expression.
Definitions support Path, Mode, Source, converters and parameters, converter language,
fallback/target-null values and UpdateSourceTrigger. Treat a definition as immutable
after applying it; replace its style/attached value to change the configuration.

Ordinary scalar style setters and `BasedOn` still work. Explicit consumer local
values/bindings take precedence. Replacing a style clears only bindings owned by the
adapter, restores model defaults for unstyled properties, and stops updates from the
previous source. Disposal clears per-item binding expressions. Initial scalar and
binding values are synchronized after style resolution, not while transient defaults
are being removed. Application callbacks can revoke a superseded style operation.

The native binding engine still determines platform-specific source conversion,
notification and update-trigger semantics. This is not a second reflection-based
binding engine and does not implement WPF MultiBinding or PriorityBinding.

## Theme and density

`FluentTheme.RequestedTheme` and `.Density` are XAML-settable dependency properties.
Changing either on the existing theme instance invalidates attached managers; callers
do not need to replace the theme or call `Refresh()` for those property changes.
`Default` follows the owner, and explicit Light/Dark controls the retained docking
surface and floating chrome. Native content uses its own ThemeResource references.

| Density | Font | Tool title | Document tab | Tool tab | Rail | Glyph button |
|---|---:|---:|---:|---:|---:|---:|
| Compact | 12 | 18 | 20 | 23 | 26 | 16 |
| Comfortable | 13 | 28 | 32 | 30 | 32 | 24 |
| Touch | 14 | 42 | 44 | 42 | 44 | 36 |

Values are DIPs before explicit metric overrides/text expansion. Comfortable and
Touch add rounded chrome, clearer title emphasis and a selected-tab accent marker.
Compact remains the compatibility default. The gallery's **View > Uno ... chrome**
commands apply the new densities to the existing workspaces without resetting them.
Touch is a chrome sizing preset, not a claim that every sample is a mobile application.

`ResourceDictionaryTheme` accepts a compiled dictionary as its content, a merged
external dictionary, or a replacement `Resources` dictionary. `Refresh()` explicitly
invalidates owners after editing entries within an existing dictionary. Replacing
`Resources` invalidates automatically. Invalid enum/null settings are rejected and
the preceding valid dependency-property value is restored.

Brush aliases are `UnoDock.PaneBrush`, `HeaderBrush`, `InactiveTabBrush`, `BorderBrush`,
`ForegroundBrush`, `HoverBrush`, `PressedBrush`, `AccentBrush`, and `ActiveTitleBrush`.
The pinned Uno XAML compiler emits duplicate insertions for direct entries inside
a nested writable ResourceDictionary property. For inline theme definitions put
entries inside ThemeDictionaries (as the compiled sample does), or use an external
compiled dictionary through Source/MergedDictionaries. This avoids modifying or
silently discarding duplicate consumer keys at runtime.

Size tokens are `UnoDock.FontSize`, `TitleHeight`, `TabHeight`, `ToolTabHeight`,
`RailThickness`, `CornerRadius`, and `ButtonSize`; author size tokens as `x:Double`.
Unspecified semantic brush aliases fall back to the owning Uno/WinUI resource palette.
Local docking overrides preserve the original brush instance; application brushes are
not recolored or cloned.

Lookup checks local entries, reverse merged dictionaries, then the selected theme
(`Default` only when that selected dictionary is absent), walking owner, ancestors
and application scopes. HighContrast uses the platform shell's reported state.
Loaded managers observe shell color/contrast changes; unload/dispose removes that
subscription. Linux/macOS do not acquire a synthetic Windows high-contrast mode.
Changing arbitrary dictionary entries requires the documented refresh, not polling.

## Control templates and built-in chrome

A custom manager template must contain `PART_LayoutHost` as a ContentPresenter and
`PART_AutoHideArea` as the containing auto-hide area. Honor Background, BorderBrush,
BorderThickness, CornerRadius, Padding and content alignments in the template.
Retemplating detaches the old host before reattaching the existing surface; layout,
content identities and editor buffers are preserved.

All built-in button/thumb, menu and navigator templates now reside in the compiled
`Themes/DockChromeResources.xaml` dictionary. Product code no longer parses stock
presentation through `XamlReader.Load`. Controls retain native input, selection and
automation behavior. Header/title/content templates and their selectors remain the
supported customization points; this change does not claim full WPF pane-template
part equivalence.

## Scope and validation

Compiled consumer XAML, not loose runtime XAML parsing, is the supported sample path.
WinUI `ThemeResource`, `StaticResource`, `TemplateBinding`, native Style/ControlTemplate,
ordinary Binding and page `x:Bind` are used within their native contracts. WPF
`DynamicResource`, Style triggers, routed-event setters, pack URIs and WPF binary
compatibility are not emulated. CLR layout properties support declarative values;
only dependency-property targets accept native live bindings. XML layout persistence
is separate from XAML object creation and reconnects application content by ContentId.

The `xaml-workspaces` real-host suite covers compilation, nested ownership, namescopes,
observable sources, per-item style binding isolation/lifetime/precedence, live payload
replacement, template swaps, XML restore, theme changes, metrics and rendered Light/Dark
snapshots. It runs in ordinary Linux and selected Windows acceptance. The dictionary
HighContrast branch is tested independently; this is not physical OS accessibility
switching acceptance. Browser and native WinUI builds are separate compilation checks.
Existing runtime, model-invariant, native-docking and custom-chrome suites remain in CI.
