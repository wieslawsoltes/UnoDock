# XAML workbenches on UnoDock

UnoDock uses Uno/WinUI XAML rather than a second WPF parser. A `DockingManager`
contains a `LayoutRoot`, panels/groups contain panes, and document/tool models
contain content. The model remains separate from the native visual tree.

## Declarative layout and content

```xml
<dock:DockingManager xmlns:dock="using:UnoDock"
    xmlns:layout="using:UnoDock.Layout" xmlns:themes="using:UnoDock.Themes"
    FloatingWindowMode="Native">
  <dock:DockingManager.Theme>
    <themes:FluentTheme RequestedTheme="Default" />
  </dock:DockingManager.Theme>
  <layout:LayoutRoot>
    <layout:LayoutPanel Orientation="Horizontal">
      <layout:LayoutAnchorablePane DockWidth="220" DockMinWidth="140">
        <layout:LayoutAnchorable Title="Explorer" ContentId="explorer" CanClose="False">
          <ListView />
        </layout:LayoutAnchorable>
      </layout:LayoutAnchorablePane>
      <layout:LayoutDocumentPane>
        <layout:LayoutDocument Title="Document.xaml" ContentId="document">
          <TextBox AcceptsReturn="True" TextWrapping="Wrap" />
        </layout:LayoutDocument>
      </layout:LayoutDocumentPane>
    </layout:LayoutPanel>
  </layout:LayoutRoot>
</dock:DockingManager>
```

The snippets assume the normal presentation and `x:` namespaces on their containing
Page/UserControl. Compiled sample markup is in `XamlWorkbenchView.xaml` and
`XamlMvvmView.xaml`. Both are available from the Gallery's **Samples** menu.
The first declares panels, tools, a source-bound document, content/header templates,
style selectors and native float/auto-hide/layout-serialization commands. The
second declares observable DocumentsSource and AnchorablesSource collections, per-item
bindings, document/tool templates, and live movement/hide/auto-hide/tabbed-document policies. Code-behind handles application commands; it does not construct the UI.

## Model dependency properties

Native DP endpoints were added for content, tooltip, image, enabled/close/float
policy, activation, selection, floating coordinates, document movement/description,
tool hide/auto-hide/document-docking policy and auto-hide dimensions. Concrete
panels, pane groups and panes also expose their dimensions, minimum dimensions,
floating geometry, maximized/reposition/duplicate policy, orientation and selection
where applicable. Root panels, all four root sides, root ActiveContent, floating-window
root slots and anchorable visibility also have guarded native endpoints. There are 92 new endpoints across these model types; Title and
ContentId keep their existing native properties. FloatingWindowMode and
FluentTheme.RequestedTheme are now native DPs as well.

These endpoints invoke the existing public model setters, not parallel implementations
of ownership, activation, pane selection or validation. CLR/model-originated changes
publish to the DP, so TwoWay bindings receive them. Rejected/coerced writes are
reconciled with the actual model even when a setter throws. The model's transaction
and callback contracts in `model-invariants.md` still apply. Native DP notifications
are sequential, not an atomic multi-property notification transport.

Use `x:Bind` for compiled Page/UserControl sources, or a normal `Binding` with an
explicit Source for a nonvisual layout node. Do not assume WPF logical-tree
DataContext inheritance on nonvisual objects. Read-only placement/state properties
remain observable binding sources, not arbitrary writable DP targets. Structural slots invoke their existing ownership setters through their DP endpoints.
Collections retain their get-only ownership collection; declare children inline
or use DocumentsSource/AnchorablesSource rather than replacing the child list. WPF markup extensions, triggers and arbitrary WPF resources are not WinUI
XAML and are not emulated.

## Source-backed items and AvalonDock-style container conventions

The normal `DocumentsSource`, `AnchorablesSource`, `LayoutItemTemplate`, template
selectors, header/title templates and `LayoutItemContainerStyle` entry points
remain. LayoutItem.DataContext is its Model (the content payload); header templates
receive the LayoutContent model. A content template therefore binds `Title`, while
a header template can bind `Title` or `Content.Title` according to its intention.

Uno 6.7 compiled binding-valued `Setter.Value` expressions do not create independent
bindings on layout items. Use the portable attached binding definitions instead:

```xml
<Style x:Key="DocumentStyle" TargetType="controls:LayoutItem">
  <Setter Property="controls:LayoutItemBindings.Bindings">
    <Setter.Value>
      <controls:LayoutItemBindingCollection>
        <controls:LayoutItemBinding Property="Title" Path="Title" Mode="TwoWay" />
        <controls:LayoutItemBinding Property="ContentId" Path="ContentId" />
        <controls:LayoutItemBinding Property="CanClose" Path="CanClose" />
      </controls:LayoutItemBindingCollection>
    </Setter.Value>
  </Setter>
</Style>
```

`controls` maps to `using:UnoDock.Controls`. Definitions support Path, Mode,
UpdateSourceTrigger, Source, Converter, ConverterParameter, ConverterLanguage,
FallbackValue and TargetNullValue. ElementName/RelativeSource are passed to the
native binding engine; they do not create a namescope or visual ancestors for the
nonvisual item. Prefer explicit sources for those cases. Definition collections
are configuration snapshots: replace the attached collection/style to reconfigure
an existing item, rather than mutating a used collection in place.

Every item receives a new native Binding. Local application bindings/values win.
Old definition-owned bindings are released without clearing the model's state;
source/content replacement follows the item DataContext. Invalid/duplicate targets
are rejected before retiring the old bindings. Reentrant collection replacement is
serialized with a bounded convergence check and cannot finish an obsolete plan.
Literal container-style metadata and capability setters are synchronized to the
model and default commands, rather than only changing the adapter's appearance.
Container-style replacement is serialized too: an application callback which
selects a newer style prevents stale literal policy values from being published.

## Binding retirement and adapter lifetime

Retiring an owned binding or command can invoke application dependency-property
callbacks. Cleanup snapshots and retires its ownership records before executing
these callbacks, attempts every independent removal, and removes only expressions
or commands whose current identity still matches that snapshot. A binding installed
by application code during removal of another binding is not cleared by the old
operation. The normal native binding engine continues to own that replacement.

Disposal becomes terminal before callbacks run. It independently attempts model
unsubscription, menu disposal, definition/default-binding removal, command removal,
view detachment and clearing of presenter content/template/data context. Retained
presenter references are retired before callbacks can attempt to recreate a view.
A single observer failure retains its identity and stack; multiple cleanup failures
are reported in occurrence order. Repeated disposal is inert, even when the first
call reported an error. Disposing an adapter does not dispose or clear its layout
model's application-owned content payload.

A callback failure while replacing definitions does not roll back arbitrary
application side effects or install a partially retired request as a successful
replacement. All old owned bindings are attempted before the error is propagated;
a subsequent explicit assignment of a new configuration can establish fresh
bindings. Invalid preflight still leaves the previous bindings untouched. These
are distinct failure boundaries. Fourteen public-API regressions cover document
and tool adapters, failure aggregation, recovery and reentrant binding ownership. Two additional
cases deliver a model event captured before disposal: a subscriber registered
before the adapter disposes it first, and the remaining captured delivery must
not repopulate its cleared Model, DataContext or presenter. The model itself
retains its new application payload.

## Theme and lightweight styling

Merge `themes:WorkbenchResources` in an owner scope, then use
`Style="{StaticResource UnoDock.WorkbenchManagerStyle}"` with `FluentTheme`.
The optional dictionary supplies independently authored Light/Dark brushes,
26-DIP tool captions, 28-DIP document tabs, 30-DIP rails, and 3-DIP chrome-button
corners. The legacy default/Generic palette and compact metrics do not change.

FluentTheme.RequestedTheme can be set in XAML or changed/bound at runtime. Default
follows the owning manager; explicit Light/Dark remains independent of the host.
Managers subscribe only to their active theme and detach when replaced/disposed.
Numeric `UnoDock.*` overrides now use the same owner/ancestor/application lookup as
Fluent brushes. Manager resource overrides remain most specific. A Refresh applies
ordinary ResourceDictionary replacements; dictionary edits are not a general
observable-collection notification mechanism.

The default manager template now respects Background, BorderBrush, BorderThickness,
CornerRadius and Padding. Custom templates retain `PART_LayoutHost` (ContentPresenter)
and `PART_AutoHideArea`. Retemplating releases the previous host's surface before
reattaching it, preserving owned content presenters and layout models. Border
rounding is presentation; arbitrary child clipping is not implicitly added.

## Density and selected-tab presentation

`DockingManager.ChromeDensity` is a bindable, validated native property. It alters
retained chrome geometry, not layout ownership or editor identity. The optional
WorkbenchManagerStyle selects Comfortable; unstyled managers retain Default.

| Density | Tool caption | Document tab | Tool tab | Rail | Icon button |
|---|---:|---:|---:|---:|---:|
| Compact | 18 | 20 | 23 | 26 | 16 |
| Comfortable | 26 | 28 | 26 | 30 | 20 |
| Spacious | 32 | 36 | 32 | 38 | 28 |

Sizes are DIPs, before explicit resource overrides and font-legibility floors.
Default uses the original palette metrics and unqualified resources; the optional
workbench dictionary's unqualified metrics remain backward compatible. Explicit
profiles can be selected in markup or changed while tool/document windows are open:

```xml
<dock:DockingManager ChromeDensity="Spacious"
    Style="{StaticResource UnoDock.WorkbenchManagerStyle}">
  <dock:DockingManager.Theme>
    <themes:FluentTheme />
  </dock:DockingManager.Theme>
</dock:DockingManager>
```

For Fluent managers, a profile first considers `UnoDock.Spacious.TitleHeight` (for
example), then `UnoDock.TitleHeight` **in the same resource scope**. A nearer
unqualified application override therefore beats a profile default in an ancestor
or merged workbench dictionary. Font scaling retains readable minimum geometry,
and icon button sizes are bounded by their containing rows. Change ordinary
resource values and call Refresh; dictionary replacement is not an automatic
resource-notification system.

`UnoDock.ActiveTabIndicatorThickness` opts into a retained, non-hit-testable top
indicator: the active selected tab uses the accent brush and an inactive selection
uses the border brush. WorkbenchResources chooses 2 DIPs; legacy chrome defaults
to zero and its geometry/palette stays unchanged. Rounded button geometry now
also follows the workbench palette in native floating captions. It does not
replace native windows, editors or their caption command handlers.

The compiled density picker drives the manager through an ElementName Binding.
Both sample command strips scroll horizontally when constrained. The workbench's
star-sized side panes and document policy labels remain usable at the exercised
600-DIP width; this is not a mobile/adaptive docking-layout contract.

## Source-backed tool policy and reverse TwoWay binding

LayoutDocumentItem exposes `CanMove`. LayoutAnchorableItem exposes `CanAutoHide`
and `CanDockAsTabbedDocument` as native binding/style targets, alongside CanHide.
The MVVM sample declares a fixed initial tool pane and supplies its tools from
AnchorablesSource. Its title editor and policy toggles are XAML templates, not
code-created controls:

```xml
<Style x:Key="ToolStyle" TargetType="controls:LayoutAnchorableItem">
  <Setter Property="controls:LayoutItemBindings.Bindings">
    <Setter.Value>
      <controls:LayoutItemBindingCollection>
        <controls:LayoutItemBinding Property="Title" Path="Title" Mode="TwoWay" />
        <controls:LayoutItemBinding Property="CanHide" Path="CanHide" Mode="TwoWay" />
        <controls:LayoutItemBinding Property="CanAutoHide" Path="CanAutoHide" Mode="TwoWay" />
        <controls:LayoutItemBinding Property="CanDockAsTabbedDocument"
            Path="CanDockAsTabbedDocument" Mode="TwoWay" />
      </controls:LayoutItemBindingCollection>
    </Setter.Value>
  </Setter>
</Style>
```

These values update the real model and its command eligibility. Model-side edits
now propagate back through explicitly TwoWay definition-owned bindings without
replacing the Binding expression. OneWay definitions do not write the source,
and a binding installed later by application code is not reclaimed. Synchronizing
an owned binding also tolerates a callback replacing the definitions with another
source. Command/menu invalidation is still attempted if binding synchronization
raises an error; binding-engine conversion and exception behavior remain native.

Source selection must be unambiguous: specify at most one of Source, ElementName
or RelativeSource. Empty target names and invalid binding-mode/update-trigger enums
are rejected while preflighting the new definitions. Rejection leaves the previous
owned bindings installed. Source removal releases adapter-owned expressions on
normal manager reconciliation, not synchronously inside collection removal.

The workbench identifies its document by retained content identity and initializes
its source ContentId consistently. This fixes the previous Float/Dock handler's
search for an ID replaced by its own item style. Tests exercise the real button
handler, save/restore and a subsequent float rather than only invoking model APIs.

## Validation and scope

The registered `xaml-workbench` actual-host suite exercises compiled x:Bind,
explicit native Binding, TwoWay model writes, invalid dimensions, reentrant
activation, runtime XamlReader construction, model defaults, native/in-surface
float/dock retention, light/dark resources, live metrics, retemplating, per-item
binding ownership, literal capability setters and superseded binding definitions.
The suite has 86 cases, including 16 binding/disposal cleanup regressions.
Light/dark workbench and source-backed policy captures, plus a narrow workbench
capture, are emitted under the suite's visuals. The sample action regressions
invoke actual buttons through their native automation Invoke provider; these
cases do not claim physical pointer coverage.

XamlReader is a trusted-markup feature, not an untrusted document sandbox. Runtime
markup must use types present in the application's metadata; compiled samples also
retain the relevant types for trimmed hosts. Browser publish and native WinUI
package builds are not browser/native-WinUI input acceptance. High-contrast hardware,
pure Wayland, mixed-DPI displays and physical AppKit pointer automation remain
separate acceptance work. No full WPF XAML/binary/pixel equivalence is asserted.
