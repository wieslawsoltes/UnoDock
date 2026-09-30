# Themes

UnoDock ships six ready-to-use docking themes. Each one is a `Theme` subclass
assigned to `DockingManager.Theme`; application content is never re-created when
the theme changes.

| Theme | Package | Character |
|---|---|---|
| `GenericTheme` | `UnoDock` | Compact grey classic chrome. |
| `FluentTheme` | `UnoDock` | Uno/WinUI semantic colors; follows Light/Dark or a fixed variant. |
| `AeroTheme` | `UnoDock.Themes.Aero` | Glassy blue captions, slanted document tabs, bold selected title. |
| `MetroTheme` | `UnoDock.Themes.Metro` | Flat white surfaces, hairline borders, accent bar above the active tab. |
| `VS2010Theme` | `UnoDock.Themes.VS2010` | Navy workspace, gold active captions, tabs and document frame. |
| `ResourceDictionaryTheme` / `DictionaryTheme` | `UnoDock` | Your own dictionary of `UnoDock.*` resources. |

```xml
<dock:DockingManager xmlns:dock="using:UnoDock" xmlns:themes="using:UnoDock.Themes">
    <dock:DockingManager.Theme>
        <themes:VS2010Theme />
    </dock:DockingManager.Theme>
</dock:DockingManager>
```

Aero, Metro and VS2010 are fixed light designs: their chrome keeps its colors
when the application or window switches to Dark. The Gallery's **Theme** picker
switches between all of them at runtime.

| Generic | Aero | Metro |
|---|---|---|
| ![Generic](images/themes/generic.png) | ![Aero](images/themes/aero.png) | ![Metro](images/themes/metro.png) |
| **VS2010** | **Fluent Light** | **Fluent Dark** |
| ![VS2010](images/themes/vs2010.png) | ![Fluent Light](images/themes/light.png) | ![Fluent Dark](images/themes/dark.png) |

## Resource keys

Every theme is a resource dictionary of `UnoDock.*` keys. The same keys can be
placed in `DockingManager.Resources` (followed by `Refresh()`) to override
individual values of any theme.

### Base palette

`PaneBrush`, `HeaderBrush`, `InactiveTabBrush`, `BorderBrush`, `ForegroundBrush`,
`SecondaryForegroundBrush`, `DisabledForegroundBrush`, `HoverBrush`,
`PressedBrush`, `AccentBrush`, `ActiveTitleBrush`, plus the metrics `FontSize`,
`TitleHeight`, `TabHeight`, `ToolTabHeight`, `RailThickness`,
`ChromeButtonSize`, `ButtonCornerRadius`, `ActiveTabIndicatorThickness`,
`TabCornerRadius`, `TabHorizontalPadding` and `PaneCornerRadius`.

### State-specific chrome

These keys are optional. Each falls back to the base slot that historically
painted the same surface, so existing dictionaries render unchanged.

| Key | Surface | Fallback |
|---|---|---|
| `WorkspaceBrush` | Area behind panes | `HeaderBrush` |
| `SplitterBrush` | Splitters | `WorkspaceBrush` |
| `DocumentTabStripBrush`, `ToolTabStripBrush` | Tab strip backgrounds | `HeaderBrush` |
| `DocumentTabBrush`, `ToolTabBrush` | Unselected tabs | `InactiveTabBrush` |
| `SelectedDocumentTabBrush`, `SelectedToolTabBrush` | Selected tabs | `PaneBrush` |
| `ActiveDocumentTabBrush` | Selected tab of the active document | `SelectedDocumentTabBrush` |
| `DocumentTabForegroundBrush`, `SelectedDocumentTabForegroundBrush`, `ActiveDocumentTabForegroundBrush` | Document tab text and glyphs | palette text rule |
| `ToolTabForegroundBrush`, `SelectedToolTabForegroundBrush` | Tool tab text | palette text rule |
| `TabHoverBrush` | Hovered tab label | `HoverBrush` |
| `TabBorderBrush` | Tab separators and outlines | `BorderBrush` |
| `ToolTitleBrush`, `ActiveToolTitleBrush` | Tool captions | `HeaderBrush`, `ActiveTitleBrush` |
| `ToolTitleForegroundBrush`, `ActiveToolTitleForegroundBrush` | Caption text and buttons | palette text rule |
| `PaneBorderBrush`, `ActiveDocumentPaneBorderBrush` | Pane frames | `BorderBrush` |
| `PaneBorderThickness`, `ActiveDocumentPaneBorderThickness` | Pane frame widths | `1` |
| `SelectedTabIndicatorBrush`, `ActiveTabIndicatorBrush` | Selected-tab indicator | `BorderBrush`, `AccentBrush` |
| `RailBrush`, `AnchorTabBrush`, `AnchorTabForegroundBrush` | Auto-hide rails and their tabs | `WorkspaceBrush`, transparent, text rule |
| `FloatingBorderBrush`, `ActiveFloatingBorderBrush` | Floating window frames | `BorderBrush` |

Shape options: `DocumentTabShape` (`Rectangle` or `Slanted`),
`TabIndicatorPlacement` (`Top` or `Bottom`), `BoldSelectedTab` (`x:Boolean`) and
`DocumentTabSpacing` (`x:Double`).

Brushes may be solid or gradient. A minimal custom theme:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="UnoDock.WorkspaceBrush" Color="#1F2937" />
    <SolidColorBrush x:Key="UnoDock.ActiveDocumentTabBrush" Color="#F59E0B" />
    <x:String x:Key="UnoDock.TabIndicatorPlacement">Bottom</x:String>
</ResourceDictionary>
```

## Authoring a packaged theme

Derive from `Theme`, return the dictionary's `ms-appx:///<Assembly>/<path>` URI
from `GetResourceUri()`, and override `ChromeTheme` to `ElementTheme.Light` or
`ElementTheme.Dark` when the design targets one color scheme. Give each theme
assembly its own root namespace; the public theme class can still live in
`UnoDock.Themes`.

## Verification

The `classic-themes` desktop suite checks that each packaged theme resolves its
dictionary under a dark host, that VS2010 moves the gold tab and frame with
document activation, that Metro's indicator takes the accent only while active,
that Aero's slanted outline follows the arranged tab without widening it, and
that switching back to Generic restores the default chrome while keeping
application content. It also writes one screenshot per theme.

The palettes were authored independently from rendered observations of the
reference themes; no reference XAML, images or color tables are used.
