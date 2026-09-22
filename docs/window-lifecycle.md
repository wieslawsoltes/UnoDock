# Floating-window lifecycle and navigator contracts (preview 5)

This implementation uses only the pinned public/protected PE contract and public platform
documentation. It is an independent Uno/WinUI implementation, not copied WPF window code.
The reference is wpftoolkit revision 2c71faba5eecc1b6ae6cd3d269408e0df37715d8.

## Composed lifecycle

`DockWindowControl : ContentControl` supplies protected virtual `OnInitialized`,
`OnClosing`, `OnClosed` and `OnStateChanged`, plus corresponding public events. Floating,
navigator and overlay types derive from this base. Initialization waits until the derived
model has been assigned and occurs once when loaded or explicitly prepared for hosting;
no overridable callback runs from the base constructor. Unloading or hiding/rehosting is
not permanent closure. The close-completed flag is set before callbacks for reentrancy.

```csharp
using System;
using System.ComponentModel;
using Xceed.Wpf.AvalonDock.Controls;
using Xceed.Wpf.AvalonDock.Layout;

public sealed class GuardedDocumentWindow : LayoutDocumentFloatingWindowControl
{
    public GuardedDocumentWindow(LayoutDocumentFloatingWindow model) : base(model) { }
    public bool HasUnsavedChanges { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel |= HasUnsavedChanges;
        base.OnClosing(e); // Dispatches the public Closing event.
    }
    protected override void OnClosed(EventArgs e)
    {
        // Release application-owned subscriptions here; this is permanent closure.
        base.OnClosed(e);
    }
}
```

A user close first evaluates host cancellation, then model close/hide cancellation.
Reentrant close/hide cannot recursively perform another transition. Root replacement
inside a callback stops traversal, so the old operation cannot remove replacement
workspace content. Exceptions in Closing restore the reentrancy and user-close flags.
Closed is raised once after host cleanup and adapter release. State notifications occur
after model flags have been updated. Override implementations must call base to preserve
public event delivery. These are window-lifetime hooks, not all WPF event-routing semantics.

The public concrete document/tool controls override their reference lifecycle and filter
hooks. Tool hide snapshots members and verifies root ownership between callbacks. A
closed tool window clears its single-content adapter and disables close/hide commands.

## Geometry: presentation is not persistence

`FloatingLeft/Top/Width/Height` store the normal rectangle. `IsMaximized` is persisted
separately. Managed maximize fits the live docking surface without overwriting those
normal values; surface resizing changes only display bounds. Minimize remains collapsed
through manager refresh. Restoring a serialized maximized layout recovers the original
normal rectangle.

Native AppWindow notifications are coalesced into a live presenter/geometry snapshot.
Maximized or minimized geometry never becomes normal geometry. Pending snapshots and
native-close callbacks verify the original Window identity, so a detached/recreated native
host cannot receive stale updates. Non-finite/non-positive rectangles are ignored without
mutating the model. Cross-monitor DPI behavior still needs device acceptance; synthetic
presenter snapshots test the persistence invariant, not OS maximize transitions on Xvfb.

## Windows message filters

`LayoutFloatingWindowControl.FilterMessage(nint hwnd, int msg, nint wParam, nint lParam,
ref bool handled)` participates in the actual native window chain on Windows. The adapter
uses SetWindowSubclass with a unique callback/ID pair, checks UI-thread ownership, roots
the delegate for its entire native lifetime, and removes the subscription on permanent
close or native rehosting. A failed removal retains only a forwarding callback until
WM_NCDESTROY rather than leaving a dangling function pointer.

An unhandled message goes to DefSubclassProc. A handled message returns the override's
result. WM_NCDESTROY is always forwarded and releases the registry entry. Managed filter
exceptions cannot cross the unmanaged boundary; `MessageFilterFailed` is dispatched later
on the UI thread. Applications should keep filters short and nonblocking; arbitrary
window messages are not sandboxed. This is not a general HwndHost replacement.

Two Windows-only tests send a private WM_APP message to the real HWND: one verifies
handled results and hide/reopen lifetime; one verifies exception containment and deferred
error reporting. The Windows CI step runs these in the actual Uno Win32 host. The native
WinUI target is also package-built, but that is not full native WinUI runtime acceptance.
No HWND filter is claimed for non-Windows hosts.

## Two-list navigator and focus

The default view has separate document and tool lists, and custom ControlTemplates can
supply `PART_DocumentListBox` and `PART_AnchorableListBox` as Microsoft.UI.Xaml.Controls.ListBox.
Template application detaches old handlers and binds current item/selection state to new
parts. LayoutDocumentsLabel/LayoutAnchorablesLabel update live through bindings.

The initial MRU selection is relative to actual active content, not an assumption that
it is the first sorted item. During a navigation session the order is stable even when
activation timestamps change. Hidden/disabled items are filtered, auto-hidden tools remain
eligible, and new members append deterministically. SelectedDocument and SelectedAnchorable
are mutually exclusive. Foreign/stale selections cannot execute commands. Layout replacement
ends the session and invalidates queued work; detached/disabled items are revalidated at
commit, and ActivateCommand.CanExecute is honored.

Ctrl+Tab/Shift+Ctrl+Tab advance MRU order. Up/down move within a category; left/right choose
tools/documents with FlowDirection accounted for. Enter or Control release commits; Escape
cancels. Both generic and left/right Control key values are recognized. Preview handlers
and bubbling fallbacks share handled-once dispatch because Uno backend routing differs.
These fallbacks do not claim to reproduce WPF tunneling for arbitrary user handlers.

The surface removes the overlay before activating the selected content. Each retained
editor presenter remembers its last focused Control through a weak reference. Commit and
cancel restore that editor when still valid, otherwise the first eligible Control in the
retained presenter. Auto-hidden tools are revealed; native floating targets are activated.
Deferred focus retries are guarded by root, active content and navigation generation.
Hyperlink/text-selection/IME-caret restoration and every custom focus scope are not certified.

A shortcut originating in a native float routes to its manager and activates the main
window for the navigator. On native WinUI, register the main Window for explicit lookup:

```csharp
using Microsoft.Windows.Shell;
var lease = SystemCommands.RegisterWindow(mainWindow);
mainWindow.Closed += (_, _) => lease.Dispose();
```

The gallery does this automatically. Portable Uno hosts also expose application-window
enumeration. Closing an overlay does not by itself dispose application-owned editor content.

## Gallery and validation

The **Window lifecycle** laboratory creates managed/native document specimens, displays
persisted bounds, logs initialized/closing/closed/state events, toggles close cancellation,
serializes XML and exposes navigator activation. Two editors in each specimen demonstrate
returning to the last focused child rather than the first child.

Local Linux checkpoint: 1,893 passing C# cases (97 portable + 1,796 in the real Uno host),
including 49 new lifecycle/navigation cases, with 23 Python comparator cases. Four new
XTEST keyboard scenarios bring real Linux input coverage to 12 cases across suites:
Control-release commit, Escape cancellation, tool-category selection with Enter, and
native-floating-to-main navigation. XTEST requires explicit opt-in on a dedicated display.
Pressed keys/buttons are released during failure cleanup.

Windows executes 47 lifecycle/navigation cases: 45 host cases plus two Windows-only
native filter cases; the four Linux XTEST cases are not counted as Windows execution.
Inspect CI logs for the exact commit's result. No test-count total is a behavioral parity
certificate. Browser/mobile runtime, accessibility traversal, IME, multiple-DPI native
windows and exhaustive original event ordering remain acceptance work.

## Deterministic metadata evidence

No scanner/comparison rules were relaxed. Mapping System.Windows.Window to the actual
composed DockWindowControl base makes override relationships explicit. Existing native
Window and ContentControl shell overloads remain available; corresponding DockWindowControl
overloads preserve the refined mapping. TemplatePartAttribute and ListBox mappings are
explicitly enumerated, not namespace-wide substitutions.

The local resolved comparison is 946/1,031 matched entries (850 declared, 24 inherited,
72 type shapes), with 40 missing members, 12 signature differences and 33 type-shape
differences. Eighteen attribute differences are separately reported. Eighteen prior
diagnostic IDs were removed with none added (17 signature entries plus one attribute).
The full --strict parity gate remains unsatisfied; the reviewed baseline detects regressions.

Primary references:
- https://learn.microsoft.com/windows/win32/api/commctrl/nf-commctrl-setwindowsubclass
- https://learn.microsoft.com/windows/win32/api/commctrl/nf-commctrl-removewindowsubclass
- https://learn.microsoft.com/dotnet/api/system.windows.window.onclosing
- https://platform.uno/docs/articles/features/windows-ui-xaml-window.html
- Pinned exported contracts in contracts/avalondock-metadata-release.json.
