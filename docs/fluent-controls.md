# Fluent controls and retained docking presentation

## Defaults and scope

The Gallery starts in Fluent Light with the existing classic docking arrangement
and Comfortable density. Light/Dark use native Uno/WinUI controls and optional
WorkbenchResources. Generic remains explicitly selectable and restores the
independently authored compact docking palette, custom legacy button/menu templates
and original pane metrics. The library's null Theme behavior is not changed.

This increment changes presentation, not the ownership tree, drag transactions,
geometry adapters, splitter allocator, serialization or source-binding semantics.
No public API is removed. Native floating-window chrome continues to use its
existing lifetime and input protocol.

## Platform-owned controls

Fluent docking buttons and context-menu rows now derive their styles from the
platform DefaultButtonStyle, DefaultMenuFlyoutItemStyle and
DefaultMenuFlyoutPresenterStyle. Their real ControlTemplates, CommonStates,
keyboard processing, automation peers and two-tone system focus visuals remain
platform-owned. No Uno template source is copied. Generic keeps the original
compiled templates for comparison.

DockChromeButton observes ButtonBase.IsPressed instead of maintaining an unrelated
pointer-only pressed flag. Space/Enter and pointer capture therefore share the
native press lifecycle. The legacy painter also observes that authoritative
state. Right-button and already-handled input cannot invent a second pressed
state. Disabled Fluent glyph/text uses a semantic disabled brush rather than
fading the entire subtree. Inactive tab labels have secondary text; selected
labels and tool captions use semibold text.

Scoped HoverBrush/PressedBrush/ForegroundBrush/DisabledForegroundBrush values
are forwarded by identity to the native Button state-resource keys. MenuHoverBrush,
MenuPressedBrush and menu text/disabled overrides likewise feed native menu states.
The forwarding dictionaries apply only to Light/Dark and do not replace the
platform's HighContrast state dictionaries. Native focus visuals replace the
legacy menu gutter/highlight border in Fluent mode; legacy MenuGutterBrush and
MenuHoverBorderBrush still apply to Generic's original template.

Theme-family changes can replace templates. Light-to-Dark updates within Fluent
retain template and control identities. Application-supplied ContextFlyout objects
are not restyled. Normal resource replacement still requires the documented
manager Refresh; this is not a new general observable dictionary mechanism.

The Gallery toolbar uses native Button/ComboBox templates, 28-DIP minimum control
heights and compiled FontIcon content. It retains command IDs, tooltips, keyboard
focus and handlers. SampleButton remains only where existing diagnostic examples
specifically use it; not every custom diagnostic control is claimed migrated.

The property inspector's styles explicitly use the platform default Button,
ToggleButton, ListViewItem and TextBox styles as their bases. A style without that
base fell back to the pinned host's older control template despite using the right
CLR control type. Category disclosure headers now use neutral checked surfaces
instead of the accent-filled toggle appearance; actual mode buttons keep the
normal native checked accent. Parsing, conflict detection, retained editors,
selection epochs and column resizing are unchanged.

## Surfaces and metrics

WorkbenchResources now supplies neutral light/dark pane surfaces and borders,
primary/secondary/disabled text, restrained active-title shading and distinct
selection accents. Existing density sizes are unchanged. New scoped keys:

| Resource | Fluent default | Bounds |
|---|---:|---:|
| UnoDock.TabCornerRadius | 4 DIP | 0–12 |
| UnoDock.TabHorizontalPadding | 6 DIP | 0–24 |
| UnoDock.PaneCornerRadius | 4 DIP | 0–12 |

The workbench retains its 3-DIP button corners and 2-DIP non-hit-testable selected
tab indicator. Tabs, their header/editor presenters, context menus and pane views
survive theme and metric refreshes. Pane rounding is presentation, not a promise
of rounded clipping for arbitrary application content.

The Gallery changes its surface/status styles only when changing style families.
Clearing their values on every theme refresh would also clear installed resource
bindings, producing a dark document under an incorrectly light toolbar. Regression
coverage includes repeated themes and a Generic-to-Fluent round trip.

The workbench HighContrast dictionary supplies all eleven semantic chrome slots
using system colors. Inspector category state resources also have a HighContrast
branch. Dictionary construction is tested; real operating-system contrast-theme
transitions and hardware rendering remain separate acceptance work.

## Verification

The actual-host fluent-presentation suite contains 24 common cases plus two
opt-in Linux XTEST keyboard cases. It checks native templates/states, semantic
text, retained control/editor identities, scoped brushes reaching native state
parts without cloning, resource binding retention, Generic restoration, shape
metrics and five rendered scenes: light, dark, RTL, large text and narrow.

The state-brush tests explicitly drive native VisualStateManager presentation;
they are not physical input tests. The two XTEST cases inject actual Space press
and release, require zero early Click events, observe ButtonBase.IsPressed and
require one activation on release. On a bare Xvfb server PointerRoot keyboard
focus follows the native window under the mouse: the fixture first places the
real pointer over that host, then assigns XAML keyboard focus. No synthetic
routed event or direct command invocation replaces those assertions.

Existing sample, presentation, inspector, menu, theme, docking, sizing and XAML
suites remain enabled without relaxed assertions. Dedicated desktop CI requires
complete JUnit/native-process agreement and all five captures. Exact final-head
results are recorded in the PR, not predicted in this document.

Windows runtime acceptance uses Uno Skia Win32; native WinUI is compiled/packaged.
Browser runtime, pure Wayland, physical AppKit input, mixed-DPI hardware and full
WPF/binary/pixel equivalence are not established here. No release version change
or package publication is part of this work.
