# Classic sample fidelity and quality

## Public target and provenance

The public AvalonDock documentation describes two documents, a left Properties pane,
right Alarms/Journal tools, and left Agenda/Contacts auto-hide tools:

- https://github.com/xceedsoftware/wpftoolkit/wiki/AvalonDock
- https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.DockingManager.html

Preview 14 independently recreates that public arrangement using UnoDock models and
native Uno controls. It does not copy LiveExplorer implementation, XAML templates,
resource definitions, logos, artwork, fonts or separately licensed commercial themes.
The gallery's toolbar glyph paths and templates are independently authored. Generic
stock presentation is the fidelity target. The Light and Dark options are UnoDock
palettes, not assertions of Aero, Metro or VS2010 theme equivalence.

## Sample structure and real commands

The default classic scene has a 200-DIP Properties pane, a central document group,
a 125-DIP Alarms/Journal pane group, and two left auto-hide items. Document 1 is an
interactive Button; Document 2 is an editable retained TextBox. Alarms contains three
rows; Journal, Agenda and Contacts retain editable buffers. The Properties tool cannot
be closed or hidden through normal capabilities.

A compact native menu bar, sample/theme selectors, vector toolbar and status bar occupy
87 DIPs, leaving the rest to docking. File and Layout commands invoke actual document,
docking and serialization operations. Samples selects the classic scene, the retained
IDE workspace, or MVVM source-bound documents. Diagnostics exposes every prior lab.
Horizontal toolbar scrolling keeps commands accessible at narrow widths.

The independent property inspector follows the last-focused document. It exposes only
explicitly registered public properties: title/capabilities and selected editor layout,
font, brush and text-editability settings. ContentId/type are read-only. Enter commits,
Escape cancels, category/alphabetical views and search retain editors, and invalid or
nonfinite numbers are rejected. Subscriptions detach on unload, selection replacement
and disposal. It is a useful sample inspector, **not full Xceed PropertyGrid parity**.

Theme and density changes retain the layout and editors. Large fonts expand the chrome
rather than clipping text into the default row heights. The original 12-point geometry
and existing original-scene acceptance remain intact.

## Acceptance and evidence

`SampleQualityTests` runs inside the real Uno gallery, both as `sample-quality` and in
the full Linux/Windows acceptance suites. It exercises the namespace and XAML surface,
model structure, property edits/validation, selected-document tracking, serialization
identity, theme/density retention, narrow layouts, initialization and stable enumeration.
Linux additionally opts into XTEST for the tab panel's real protected pointer-leave hook.
Screenshots include classic, workspace, RTL, dark, large-text and narrow views.

`ReferenceSampleProbe` is an independently authored Windows observer. It constructs the
public documented arrangement against the pinned reference through public APIs only,
including the public original PropertyGrid for comparison. It records 1000x640 manager
captures and arranged control geometry for classic, selected-editor and RTL scenes.
It neither examines private members nor reads original sample implementation. The
original is built in temporary CI storage and removed before evidence upload. Reference
screenshots are validation artifacts, never product resources.

A successful compilation is not runtime/visual acceptance. Check the exact CI revision
and native result files. The local continuation environment can perform C# semantic
compilation against pinned Uno reference assemblies but cannot run the complete Uno SDK
or native Windows host; complete builds and native acceptance run in GitHub Actions.
Whole-product pixel identity, full PropertyGrid, original sample-shell details, mobile
input and screen-reader acceptance remain separate work.
