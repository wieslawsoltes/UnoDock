# Floating windows

Tools and documents float into real desktop windows on Windows, macOS and Linux
(X11). The browser and mobile heads use in-surface floating hosts instead.

## Caption

Every floating window draws its own caption, in the active theme's colors:

| Element | Tools | Documents |
|---|---|---|
| Title | Selected tool of a single-pane window | Floating document |
| ▾ Window position | Tool menu (Float, Dock, Dock as Tabbed Document, Auto Hide, Hide) | Document menu (Close, Close All But This, Close All, Float, Dock as Tabbed Document, New Horizontal/Vertical Tab Group) |
| Minimize | Custom native captions only | Custom native captions only |
| □ / ❐ Maximize / Restore | Yes | Yes |
| × Close | Hides or closes the window's tools | Closes the document |

A floating tool window with a single pane shows its tool title only once: the
pane's own title row is merged into the window caption. Windows with several
panes keep a title row per pane. Double-clicking the caption maximizes or
restores the window; right-clicking it opens the system menu.

Caption and frame colors follow window activation through the theme keys
`ToolTitleBrush`/`ActiveToolTitleBrush`, their foregrounds, and
`FloatingBorderBrush`/`ActiveFloatingBorderBrush` with
`FloatingBorderThickness` (see [Themes](themes.md)).

## Tearing content off

With native floating windows, dragging behaves like a desktop IDE:

* **A tab** reorders inside its tab strip. Once the pointer leaves the strip,
  the content floats immediately into a new window under the pointer and the
  same gesture keeps moving that window, showing docking guides over every pane
  and workspace edge it passes. Releasing on a guide docks it; releasing
  elsewhere leaves it floating.
* **A tool pane's title** floats the whole pane (all of its tools) and continues
  as a window move in the same way.
* **The only tab of a floating window** moves that window instead of creating a
  new one.
* **Escape** ends the move and keeps the content floating; **Ctrl** suppresses
  docking while held.

![A document torn off its tab strip on Windows, following the pointer over the target pane's docking guides](images/floating/tear-off-windows.png)

Set `DockingManager.ContinuousTearOff = false` to keep drags inside the
workspace; content then floats only when released outside the window. In-surface
floating hosts always use that release behavior.

The window opens with the size of the pane it came from, placed so the pointer
keeps its grip on the caption. `FloatingLeft`, `FloatingTop`, `FloatingWidth`
and `FloatingHeight` are persisted in top-left, Y-down device-independent
pixels on every platform, so saved layouts reopen in the same place on
Windows, macOS and Linux.

## Platform notes

* **Windows**: the custom caption replaces the OS title bar; eight-edge resize,
  maximize to the work area and the system menu are supported. Physical input is
  covered by the `windows-floating-input` and `tear-off` suites.
* **macOS**: windows are AppKit child windows of the workspace; frames are
  converted between AppKit's bottom-left space and persisted top-left bounds.
  High-density displays are handled by sizing new windows with the owner's
  scale before the new window has its own.
* **Linux (X11)**: decorations are removed through Motif hints and tool windows
  are marked as utility windows owned by the workspace. The `tear-off` suite
  runs with XTEST on a dedicated display. Pure Wayland sessions need XWayland.

Virtual machines without a working GPU driver can run the Gallery with
`UNODOCK_SOFTWARE_RENDERING=1` to select Skia's software rasterizer.
