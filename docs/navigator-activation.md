# Navigator activation ownership (preview 19)

The navigator previews a model adapter, but activation is application code. A
CanExecute callback may remove that model, change its command, replace the workspace
or open another navigator. Previously CommitSelection checked eligibility only before
calling CanExecute and then executed a potentially stale command. Surface closing
could also restore old editor focus after a callback presented another navigator.

## Activation transaction

Direct in-session commits and surface-close commits now use the same captured intent.
The surface captures before detaching, then ends the session and removes the old view
before invoking the one-use action. Its generation fence rejects cancellation or
replacement by another navigator. Calling CommitSelection on an ended instance is a
no-op; retained input from an old view cannot close a new view.

Before and after CanExecute, the intent checks the manager/surface/root, current
selection and session, adapter and command identity, parent membership, visibility
and enabled states. Temporary subscriptions detect root, command and enabled-state
changes that are subsequently reversed during the callback. Recursion cannot execute
the same request twice. Title-only changes do not veto a valid activation. Execute
remains authoritative: a custom command need not activate the previewed item.
Subscriptions and the reentry guard are released even if either command callback
throws. No attempt is made to roll back arbitrary application side effects.

## Presentation and focus

The surface reserves its opening before native window activation. It revalidates
ownership after native focus callbacks, visual attachment, initialization and before
initial focus. Failed initialization removes only its own reservation; cleanup and
original exceptions remain observable. Layout-change handlers are session-scoped so
an obsolete event invocation cannot close a newly initialized session.

Closing ends the old session before application callbacks. Focus restoration checks
the original root, active content and closing generation after refresh, auto-hide
opening and native floating-window activation. It must not override a new navigator
or a callback's replacement workspace. Deferred focus work uses the same fence.
Pointer commit revalidates the clicked row after selection callbacks; it does not
commit whichever other row an application callback selected instead.

The first Windows acceptance run exposed an additional focus-routing defect. Closing
a navigator momentarily focused a tool header, then correctly activated and focused
the requested document editor. A delayed native GotFocus event from that obsolete
tool header subsequently activated the tool again, even though FocusManager already
reported the document editor. An isolated Linux trace reproduced the same sequence.

DockInputControl now verifies actual visual focus ownership, attachment and enabled
state before invoking OnGotKeyboardFocus. It does not synthesize, replay or swallow
native framework events; it declines to present a stale event as a current docking
focus notification. Valid descendant focus still reaches derived hooks. Weak focus
history is cleared on unloading. This addresses the actual asynchronous event path,
not by adding delays or forcing activation again after arbitrary application callbacks.
The focus-ownership suite covers request bursts, LTR/RTL, programmatic/keyboard/pointer
focus states, disabling, root replacement and valid tool focus. Pointer-state Focus
calls are not claimed as native pointer-input tests.

## Sample and acceptance

Diagnostics -> Navigator retains 3/40/200-document scenarios and adds compact themed
sample buttons, a live activation status and an Allow/Block activation policy using
real custom ActivateCommand instances. Previews remain visible while activation is
blocked. Enter/Control release still closes the navigator, but a false CanExecute
leaves the active editor unchanged. Reset detaches the former commands and model
observers. Existing classic/workspace/MVVM samples and UnoDock.* namespaces remain.

The navigator-commit, navigator-sample and focus-ownership suites are registered in
full Linux and selected Windows acceptance. They exercise direct and surface-closing
paths for document and tool selections, callbacks, cancellation, reinitialization,
exceptions and actual focus. Native XTEST Enter/Escape and sample policy cases are
opt-in on the CI display. Actual JSON/JUnit results establish executed totals;
configured tests alone are not a pass claim. Original observations, metadata
inventories, mappings, diagnostic allowlists and earlier tests remain unchanged.

Validation uses fresh SDK/XAML builds and real Uno/X11 execution locally, followed
by the full GitHub Actions platform workflow. The local SDK recovery omits unused
framework-pack downloads and the apphost only; it compiles the current XAML and
runs the resulting desktop DLL, not previously generated gallery code.

## Explicit remaining boundary

This change does not yet resolve the recorded original direct-selection-setter
behavior. UnoDock's SelectedDocument/SelectedAnchorable properties still stage a
preview until explicit commit; the pinned original public observation closes its
host on a non-null direct selection change. Existing preview/list tests are retained
unchanged. Public documentation alone does not specify all null, disabled, repeated,
and callback setter cases, so original event-order equivalence is not asserted.

This is not external screen-reader/UIA transport certification or new original
pixel-equivalence evidence. No original implementation bodies, templates, artwork or
fonts were imported. Public contract guidance:
https://xceed.com/documentation/xceed-toolkit-plus-for-wpf/Xceed.Wpf.AvalonDock~Xceed.Wpf.AvalonDock.Controls.NavigatorWindow.html
https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.dependencyobject.registerpropertychangedcallback

## Bounded native test-host lifetime

Each navigator transaction case creates and disposes its own DockingManager,
models, editors and navigator, but all cases share one real native window. They
do not share a docking tree, selection session, command or event subscription.
Native-window creation, closure and rehosting remain covered by the separate
window-lifecycle and coordinate suites.

This is important for the pinned Uno 6.7.135 X11 host. The initial per-case-window
run was killed before it could write its complete test report. An isolated
diagnostic with forced collection found old docking managers and navigators
collectible, while native Window instances and renderer memory remained retained.
Reversing content-clear/Close order did not eliminate that retention. The test
fixture now bounds native shell allocation instead of retrying on a larger
runner, skipping cases, forcing GC in production or asserting a platform memory
leak has been fixed. The host-level retention remains an explicit limitation.

The Enter/Escape cases establish input focus by an actual native click on the
navigator's empty border before sending keys. Merely calling Control.Focus sets
XAML focus but does not establish where a window-manager-free Xvfb display sends
its hardware input. The click does not hit a row or bypass the keyboard handler.

Sample capture checks now require the activated TextBox to be attached and arranged
before snapshotting. They verify the retained editor/draft across two custom-command
activations and actual visible RTL navigator row containers without changing the
active model. Multiline editing is enabled before assigning CR-projected seed
text, so native single-line coercion cannot truncate the second comment. The
regression checks both logical lines and native text representation. This affects
only sample-owned text, not conversion of a user file. The sample images cover
settled light, dark and open-RTL states.
