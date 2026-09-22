# Input, selection and weak-source extension contracts — preview 6 candidate

This increment is independently implemented from the pinned exported API inventory and
public Uno/WinUI input documentation. Original implementation bodies and templates were
not read or translated. The declared framework mappings are explicit; this is not a WPF
binary replacement or an implementation of its complete routed-event engine.

## Protected input hooks are on the actual execution path

`DockInputControl : ContentControl` translates real native pointer events into
control-local stages with the original protected method names. Document/tool tabs,
panes, content controls, anchor titles and auto-hide anchors override the corresponding
methods. The stages include preview left/right press, mouse down, left/right release,
preview right release, enter/leave, move, and preview/completed keyboard focus.

`DockMouseEventArgs` and `DockMouseButtonEventArgs` retain the original native event,
pointer identifier, device kind, changed button, button state and coordinate conversion.
They do not fabricate a WPF MouseDevice or synthetic global mouse state. Setting their
`Handled` stops later docking stages in that control. Native handled state is only set,
never cleared. This local handled flag starts false so a child Button's already-handled
native event does not make a parent docking hook unusable.

Preview is **control-local**, not WPF tunneling. Other ancestors have independent local
stages. WPF route construction, event identity, class handlers, stylus promotion and
all inherited input-event APIs are not claimed to be reproduced. In particular, setting
a docking event's Handled flag does not undo a child control's action that already ran.

```csharp
using Xceed.Wpf.AvalonDock.Compatibility;
using Xceed.Wpf.AvalonDock.Controls;

public sealed class GuardedTab : LayoutDocumentTabItem
{
    public bool LockActivation { get; set; }
    public bool LockDrop { get; set; }

    protected override void OnPreviewMouseLeftButtonDown(DockMouseButtonEventArgs e)
    {
        if (LockActivation) e.Handled = true;
        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(DockMouseButtonEventArgs e)
    {
        if (LockDrop) e.Handled = true;
        base.OnMouseLeftButtonUp(e);
    }
}
```

The default left-down path activates the model and arms the real drag engine. Its
move/release paths advance and commit that engine. Suppressing the default down or up
path therefore suppresses the corresponding operation, rather than merely suppressing
a notification after the operation. A vetoed release terminates capture without a drop.
The ordinary Button.Click path is reserved for keyboard/automation activation, so a
pointer release does not silently bypass a vetoed press. Close buttons are excluded from
header input processing; middle-click close/hide is dispatched once through OnMouseDown.

The control that owns the hooks is separate from the native capture element. Tab input
captures the inner Button already participating in pointer delivery, then routes docking
through the outer tab's hooks. Capturing an unrelated ancestor while the Button owns
capture is not assumed to succeed. Cancellation is pointer-specific; cancellation of
another contact cannot terminate the primary drag. Disable, unload or loss of the chosen
capture element cancels the current gesture. State is cleared before release/cancellation
callbacks can reenter. Explicit secondary-button transitions delivered as PointerMoved
are classified; missing platform chord events are not invented. The X11 chord regression
verifies the primary release survives, not that X11 supplies every intermediate event.

## Focus cancellation and activation timing

`DockKeyboardFocusChangedEventArgs` preserves old/new elements and the native event.
During GettingFocus the preview hook can set Cancel, preventing the native transition.
OnGotKeyboardFocus runs after focus actually changes. Its default tool-content behavior
activates the model only then; Handled can suppress that default without undoing focus.
Cancelling a completed focus event throws rather than pretending it succeeded.

The pending old/new pair and remembered focus use weak references. Post-focus reporting
uses the matching native pre-focus pair when available, and does not treat preview as
proof that the transition completed. Other application focus handlers can still cancel
or redirect focus. General WPF focus scopes, IME/caret state and all host accessibility
behavior remain outside this checkpoint.

## Bindable pane selection with reentrant model synchronization

`LayoutCachePaneControl.SelectedIndex` and `SelectedItem` are real dependency properties.
The constructor binds the supplied pane without invoking virtual selection callbacks.
Native TwoWay binding, model selection and collection movement all update the same
selection state. A movement that changes only the selected item's index does not emit a
false remove/add SelectionChanged event. A null item clears selection; a foreign item is
rejected; an out-of-range index restores the coherent dependency-property values before
propagating the argument error.

A weak pane observer keeps an abandoned control from being retained by a long-lived
model. ReleaseViews detaches it explicitly. SelectionChanged sees already-updated model,
item and index values. Previously realized content presenters switch visibility
synchronously; unvisited content remains unrealized until the normal render path needs it.
The protected LogicalChildren adapter exposes a snapshot of retained content presenters,
not a reconstruction of WPF's logical-parent/inheritance machinery.

The model selection algorithm now drains reentrant requests rather than dropping them.
Each transition snapshots child identities before flag callbacks, rechecks membership,
updates flags, and notifies selection. The last explicit queued request wins, including
reselecting the current target to withdraw an earlier queued request. Internal selected-
flag synchronization does not overwrite a user's queued request. Nonconvergent observers
are bounded to 64 transitions; guards are reset in finally so removing a bad subscriber
permits subsequent operations. Collection edits during callbacks cannot leave a selected
identity that has been removed without being revalidated.

## Weak source notification extension

DockingManager implements the mapped `Compatibility.IWeakEventListener` contract and
routes actual observable-source notifications through protected OnReceiveWeakEvent.
The managerType marker for these events is `typeof(INotifyCollectionChanged)`; this is
an explicit Uno transport convention, not a fake WPF CollectionChangedEventManager.
An override can observe, filter or replace the default source reconciliation. Returning
false without calling base suppresses it; returning true without base can indicate an
application-handled notification. Unknown source/manager combinations return false.

Worker notifications marshal onto the owning dispatcher. Delivery verifies that the
originating subscription is still current; queued events from replaced/disposed sources
are discarded. Source subscriptions hold managers weakly. Exceptions from synchronous
application overrides are not swallowed, and do not permanently lock reconciliation.
This does not make an arbitrary ObservableCollection safe for concurrent mutation or
provide WPF's full weak-event registration infrastructure.

## Consumer construction hooks and gallery

Two additive protected manager factories, CreateDocumentPaneControl and
CreateAnchorablePaneControl, allow custom derived panes to participate in ordinary
rendering. A pane's protected CreateTabItem supplies its custom headers. Factory results
must be non-null, unparented, tied to the requested model and not shared with another
header in the same pane. These additive Uno hooks are not presented as original API.

```csharp
public sealed class CustomPane(LayoutDocumentPane model) : LayoutDocumentPaneControl(model)
{
    protected override LayoutTabItemBase CreateTabItem(LayoutContent model) => new GuardedTab();
}

public sealed class CustomManager : DockingManager
{
    protected override LayoutDocumentPaneControl CreateDocumentPaneControl(LayoutDocumentPane model)
        => new CustomPane(model);
}
```

The gallery's **Input extensions** laboratory uses those actual subclasses without
reflection. It offers activation/drop vetoes, a TwoWay-selected ComboBox, reentrant
selection redirection and observable document-source insertion, with an event log.
Desktop runs the lab in its own workspace window; browser uses a managed laboratory
workspace inside a document. Window/document closure disposes the laboratory manager.

## Validation and remaining boundaries

The local reference-assembly compilation and actual Uno/X11 harness run pass 97 core
cases and 1,839 Uno-host cases: **1,936 C# cases**, including **43 new input-extension
cases** and **20 opt-in XTEST scenarios across all suites**. There are also 23 Python
metadata-comparator cases. The new suite has 35 host cases and eight Linux XTEST cases.
The source-built Windows workflow now includes those 35 host cases beside the existing
47 lifecycle cases; that configuration is not a claim that Windows executed this candidate.

The resolved metadata comparison improves from 946 to **977/1,031** entries: 881 declared,
24 inherited and 72 type-shape matches. Nine missing members, 12 signature differences,
33 type-shape differences and 18 separately reported attribute differences remain. Exactly
31 prior diagnostic IDs are removed and none added; comparison/scanner rules are unchanged.
The reviewed baseline still does not pass the full strict parity gate.

This candidate has not been pushed or run through GitHub Actions in the current session.
Local validation uses the current libraries and test sources against pinned Uno references,
with an existing generated Gallery resource/ICU host. Current sample C# is separately
compiled. A clean SDK/MSBuild XAML solution build, native WinUI packaging, Windows/macOS
execution and browser publishing must still run for this source. Test counts are not a
behavioral-parity certificate. No NuGet.org publication was performed.

Primary platform references:
- https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.control
- https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.input.pointerroutedeventargs
- https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.gettingfocus
- https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.pointercapturelost
- Pinned public/protected contracts in contracts/avalondock-metadata-release.json.
