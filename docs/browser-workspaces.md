# Browser workspaces

The browser workbench hosts the real UnoDock controls and Uno WebAssembly runtime. A small browser shell supplies capabilities that must belong to a browser document: user-gesture popup creation, drag-and-drop between documents, window discovery, backup downloads, and recovery messaging.

## Use the sample

Open the [browser workbench](https://wieslawsoltes.github.io/UnoDock/playground/). Select a content chip and choose **Float in browser window**. Wait for the new Uno host to become ready; only then is ownership transferred. The source retains its content if the popup is blocked or never becomes ready.

Each window can contain multiple documents and tools. Use **Move** with a destination and dock position, or drag a content chip into another window and choose one of its five drop regions. **Dock all & close window** returns satellite contents to the main workspace. Closing a satellite through the browser also returns its contents after the primary observes that the window has closed.

Inside each Uno canvas, normal tab dragging, split panes, docking guides, local floating, and tool auto-hide remain the library's responsibility. The native **Float selected** action requests a real browser popup. When the browser refuses a request, the source remains usable and the browser strip explains how to retry.

Browser windows retain browser-owned title/address bars. Drag the **content chip**, not the browser's title bar, to dock between windows. Websites do not receive the operating system's title-bar drag loop. A browser may open a tab rather than a separate window according to user settings.

## Ownership and content transport

There is one authoritative session broker and one Uno runtime per window. Each content record carries stable `Id`, application `Type`, `Kind`, `Title`, serialized `Payload`, current `Owner`, monotonic `Lease`, content `Revision`, and preferred docking `Zone`.

Every edit or transfer must match the current owner and lease. Moving a document increments its lease. A stale editor, old transfer ticket, or callback from a previous ownership generation cannot overwrite the new owner's payload—even if the item subsequently returns to the first window.

A popup first reserves a destination, then becomes eligible when its Uno adapter reports readiness. Changes made in the source while that popup starts are retained. Drag tickets are opaque, session-local, time-limited, and consumed once; a drop from another workspace or an already-used ticket is rejected. The browser controls use accessible buttons/selectors as a keyboard alternative to drag-and-drop.

The initial sample limits a workspace to eight browser windows, 200 content records, and a one-megabyte payload per record. Those are defensive implementation limits, not browser capacity claims.

## Application-owned view factories

Reference `UnoDock.Browser` and implement `IBrowserDockViewFactory`. The factory receives a portable `BrowserDockItem` and a commit callback for title/payload updates. The resulting `IBrowserDockView` exposes its native `FrameworkElement`, current serialized payload, an update method, and disposal.

`BrowserDockingSession` projects that window's records into an actual `DockingManager`. Supply a synchronous JavaScript invocation delegate and call `Poll` on the owning UI thread; the sample uses a 150-ms DispatcherTimer. Background browser timers may be throttled, so this is eventual presentation reconciliation—not a real-time distributed scheduler.

The sample `BrowserTextView` uses native Uno TextBoxes and commits edits synchronously through the ownership check. Another application can use a JSON payload for a diagram, selection, undo history, or an editor model. Register matching factories in every host. CLR objects, delegates, native handles, and UIElements cannot move between separate runtimes. The browser extension does not pretend that layout XML serializes arbitrary application content.

Within one host, ordinary polling and content edits retain existing editor instances. A cross-window transfer reconstructs the target editor from the application payload. Persist editor-specific state in that payload when it must travel too.

## Recovery and backups

Edits are written to a browser-local JSON journal. **Export backup** downloads portable content; the journal is not cloud synchronization. Closing a satellite reassigns its content to the primary. Reloading the primary restores journaled content there rather than silently reopening popups without permission. A satellite whose primary session ended stops accepting edits and exposes explicit recovery.

Storage denial or quota exhaustion is visible. In-memory edits can continue, but durability must not be assumed after such an error; export a backup. Clearing site storage removes the journal. An abrupt browser/OS failure can lose an edit that never reached the broker.

## Browser security and verification boundaries

All windows and the embedded Uno application must be served from the same trusted origin. Window references, random per-window capabilities, session checks, and lease checks reject accidental foreign/stale requests; they are not a sandbox against malicious same-origin JavaScript. Do not place untrusted HTML/scripts on the same origin. Payloads are data and are not evaluated as JavaScript or runtime XAML.

Popup creation follows an explicit user gesture. There is no hidden popup pool or blocker bypass. Cross-origin navigation, a severed opener, or an ended session retires the host. OS window positioning, title-bar decoration, mobile tab selection, and browser throttling remain browser-controlled.

The browser workflow checks real Uno startup, editor changes, popup creation/closing/reload, two-popup transfers, stale leases, popup-blocker fallback, journal recovery, and DOM drag-transfer routing. The DOM drag test is not claimed as physical operating-system pointer automation across native windows. Desktop native-window suites remain separate.

References: [Window.open](https://developer.mozilla.org/en-US/docs/Web/API/Window/open), [same-origin policy](https://developer.mozilla.org/en-US/docs/Web/Security/Same-origin_policy), [Uno WebAssembly publishing](https://platform.uno/docs/articles/uno-publishing-webassembly.html).
