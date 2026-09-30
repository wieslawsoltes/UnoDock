# Header, title, menu and icon templates

`DockingManager` exposes data templates that change how a content is presented
in the docking chrome without replacing any control template. Each template has
a matching `...TemplateSelector`; a selector result that is not `null` wins, and a
`null` result falls back to the template property.

| Property (and selector) | Applies to | Data context |
|---|---|---|
| `DocumentHeaderTemplate` | Document tabs; document rows of the Ctrl+Tab navigator | the `LayoutDocument` |
| `AnchorableHeaderTemplate` | Tool tabs; auto-hide rail tabs; tool rows of the Ctrl+Tab navigator | the `LayoutAnchorable` |
| `DocumentTitleTemplate` | Floating document window caption | the `LayoutDocument` |
| `AnchorableTitleTemplate` | Tool pane title row; auto-hide flyout title; floating tool window caption | the `LayoutAnchorable` |
| `DocumentPaneMenuItemHeaderTemplate` | Rows of a document pane's open-documents list (the list button at the end of the tab strip) | the listed `LayoutContent` |
| `IconContentTemplate` | The icon of the default presentation in all of the above | the `IconSource` value |

## Default presentation

Without a header, title or menu template, each place shows the content's icon
followed by its title. `LayoutContent.IconSource` is an `ImageSource`; the default
icon is a 16x16 `Image`. When `IconContentTemplate` (or its selector) yields a
template, that template presents the icon instead and receives the `IconSource`
value as its data context, for example to tint, badge or resize it. A content
without an icon shows its title only.

A header, title or menu template owns the whole presentation of its place,
including any icon: bind `IconSource` inside the template to keep one.

## Behavior

- Open-documents rows remain standard `ToggleMenuFlyoutItem` controls, so the
  menu's keyboard focus, navigation and invocation apply unchanged. The selected
  content is checked, disabled contents are disabled, and a row activates its
  content only if it still belongs to the pane when invoked.
- Navigator rows keep the list's selection and commit behavior; their content is
  still the `LayoutItem` that is selected. Replacing the navigator list's
  `ItemTemplate` or setting its `ItemTemplateSelector` restores full control of the
  row to that template.
- Floating captions keep the caption text for the native window title and
  automation even while a title template is displayed. The caption remains a drag
  handle; templates in it are not hit-test targets.
- Changing any of these properties re-renders the chrome on the next layout
  pass. `DockingManager.Refresh()` applies the change synchronously. The navigator
  and the open-documents list resolve templates each time they open.

## Example

```xml
<dock:DockingManager
    DocumentPaneMenuItemHeaderTemplate="{StaticResource DocumentMenuRow}"
    IconContentTemplate="{StaticResource TabIcon}"
    AnchorableHeaderTemplate="{StaticResource ToolHeader}">
    <dock:DockingManager.Resources>
        <DataTemplate x:Key="DocumentMenuRow">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Image Source="{Binding IconSource}" Width="16" Height="16"/>
                <TextBlock Text="{Binding Title}"/>
                <TextBlock Text="{Binding Description}" Opacity="0.6"/>
            </StackPanel>
        </DataTemplate>
        <DataTemplate x:Key="TabIcon">
            <Image Source="{Binding}" Width="14" Height="14" Opacity="0.85"/>
        </DataTemplate>
        <DataTemplate x:Key="ToolHeader">
            <TextBlock Text="{Binding Title}" FontStyle="Italic"/>
        </DataTemplate>
    </dock:DockingManager.Resources>
    <layout:LayoutRoot>
        <layout:LayoutPanel>
            <layout:LayoutDocumentPane>
                <layout:LayoutDocument Title="Program.cs" ContentId="program"
                                       IconSource="ms-appx:///Assets/Icons/code.png"/>
            </layout:LayoutDocumentPane>
        </layout:LayoutPanel>
    </layout:LayoutRoot>
</dock:DockingManager>
```

## Gallery

- **Docking** and **IDE workspace** samples set `IconSource` on every document
  and tool (original 32x32 assets in `samples/UnoDock.Gallery/Assets/Icons`).
- **IDE workspace** also sets `DocumentPaneMenuItemHeaderTemplate`, a custom
  `AnchorableContextMenu` whose rows bind the `LayoutAnchorableItem` commands,
  and `NamedPaneLayoutStrategy`, an `ILayoutUpdateStrategy` that places tools
  added with Layout > New tool window into the pane named `WorkspaceTools`.
- **XAML workbench** declares `DocumentPaneMenuItemHeaderTemplate` and tool
  icons in compiled XAML.

The `templates-icons` desktop suite (`tests/UnoDock.VisualTests/TemplateIconTests.cs`)
covers every row of the table above, the default presentation, selectors and
runtime changes.
