# Stock-style context menus (preview 12)

## Public observation and independent implementation

The pinned original is `2c71faba5eecc1b6ae6cd3d269408e0df37715d8`.
`MenuObservations` uses public layout models, the manager's public context-menu
properties, DataContext/IsOpen, public command getters and realized row geometry.
It records ten document/tool capability scenarios and two rendering captures.
It does not inspect source bodies, IL, template definitions, resources, artwork,
icon geometry or font files. The input is protocol opening, not original native
pointer acceptance. Raw XML and provenance are in `contracts/visual-fixtures`.
The reference workflow compares the entire menu XML between two independent runs.

The observed default menu is compact, with 22-DIP rows, 12-DIP text, two-DIP padding,
a one-DIP frame, a pale background and continuous icon gutter. The document menu
orders Close, Close All But This, Close All, Float, Dock as Tabbed Document, new
groups, then adjacent groups. Tool menus instead offer Float, Dock, Dock as Tabbed
Document, Auto Hide/Restore, Close and Hide. There are no default separators.
Unavailable group commands collapse; unavailable Float/Dock/Hide commands remain
visible and disabled. Close collapses when CanClose is false. Auto-hidden tools
use Restore rather than Auto Hide, while their Dock command is disabled.

The port draws this presentation independently, keeping **real MenuFlyoutItem**
controls with native keyboard and Invoke automation behavior. It uses an independent
MenuFlyoutPresenter template and bounded ScrollViewer. A nominal 235-DIP minimum
width approximates the observed 234.75-DIP width; text-driven width and rasterization
are platform-dependent. Large text expands the rows rather than being clipped.
RTL is set explicitly on the popup presenter and row layout instead of assuming
that the popup inherits the owner tree's flow direction. The default light gutter
is continuous; hover/focus overlays do not split it into disconnected cells.

## Retained state and command safety

Each LayoutItem owns one retained default flyout and stable command rows. Ordinary
rendering, activation and title changes do not rebuild them. Command replacements
are resolved from the current adapter. Application CanExecuteChanged subscriptions
exist only while the menu is open; replacement, close, custom-menu takeover and
layout disposal remove them. Worker notifications use one coalesced UI continuation
and an opening generation; stale callbacks cannot update a later session.

Execution checks ownership, root identity, model enablement and current command
identity **after** calling application CanExecute. A callback that changes the
command or replaces the layout cannot cause a stale command to execute. Disposing
the menu releases command closures and model/manager references; externally retained
wrappers become inert. Menu command wrappers deliberately do not require IsOpen:
automation may close a native menu before executing its selected action.

Changes while open update capability visibility, command enablement and palette.
When the focused row disappears or becomes disabled, focus is handed to a remaining
enabled row. Menu-local default behavior never replaces an application-supplied menu's
rows, templates, commands or styles. Shared custom menus receive the current target's
adapter context for one opening, including auto-hide rail targets. Explicit item
DataContext values/bindings and application edits survive cleanup.

Bulk close snapshots its original root, suppresses nested bulk-close requests on that
root, honors per-document cancellation/CanClose, and revalidates each remaining
item. Root replacement stops it; moving a document to another workspace excludes
it. An exception releases the guard but does not undo documents already closed.
An initiating adapter may close before its remaining documents; its disposal is
not confused with replacing the whole workspace. Adjacent-group commands follow
siblings in a LayoutDocumentPaneGroup, not unrelated root-level panes.

## Styling and sample

The default menu consumes these additive resources from the docking manager:
`UnoDock.MenuBrush`, `MenuGutterBrush`, `MenuBorderBrush`, `MenuForegroundBrush`,
`MenuDisabledBrush`, `MenuHoverBrush`, `MenuHoverBorderBrush`, `MenuRowHeight`, and
`MenuMinWidth` (all names prefixed with `UnoDock.`), plus `UnoDock.FontSize`.
Explicit theme dictionaries and requested/actual themes resolve foreground and
background coherently. English labels use the observed wording; the existing
Resources.Culture/Translations mechanism remains available and updates retained
rows on refresh/reopening.

**Menu quality** in the gallery demonstrates document/tool menus, capability changes,
close cancellation, application command replacement/requery, themes, RTL and larger
text. The shared main-editor initializer now sets AcceptsReturn before assigning
multiline text, avoiding the earlier sample-only single-line normalization problem.

## Evidence and limits

The suite contains 53 Linux / 50 Windows cases: ten original state/geometry replays,
identity/subscription assertions, worker command changes, reentrant execution/bulk
close, custom contexts, theme/focus/automation cases and six PNG/XML captures. Three
Linux-only XTEST cases use actual pointer click, Enter and Escape. The original
probe does not establish end-to-end original input behavior. Test counts are not
whole-product compatibility percentages; inspect the exact revision's CI reports.

Default per-item flyouts are obtained from realized control ContextFlyout properties;
manager DocumentContextMenu/AnchorableContextMenu still act as optional shared custom
menu slots. This is not the original resource-backed default property identity.
The wrapper command is not reference-equal to the adapter command. WPF routed commands,
access-key underlining, arbitrary WPF menu styles/submenus, full screen-reader behavior,
high-contrast system changes and every original event order remain separate work.
Dark styling and larger-density controls are independent extensions. No new API
mapping or comparator relaxation is used to count the visual improvements as parity.
