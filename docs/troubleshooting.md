# Troubleshooting

## Float does not open a window

Check the browser's popup indicator. Popup creation must follow an explicit user action, and the browser may deny it. The workbench reports the denial and keeps source ownership. Allow popups for this site, select the content, and press Float again. Do not repeatedly retry automatically or open hidden reserve windows.

## A popup is empty while starting

Each popup starts its own Uno WebAssembly runtime. Content transfers only after the new host is ready. If startup fails or the popup is closed, the source retains/reclaims the content. Verify the browser's network/console output and that `/gallery/` assets load from the configured base path.

## The primary window was reloaded or closed

A primary reload reconstructs journaled content in its main workspace. Existing satellites from the old session must not continue writing. Use their recovery action or reopen the primary workbench. Popups are not reopened automatically without a user gesture.

## Changes are not being saved

Private browsing, disabled storage, or an exhausted quota can prevent journal writes. The status strip reports storage failures. Export a backup while the in-memory content is available. The journal is local to the browser/profile; it is not a cloud backup.

## Docking by the browser title bar does nothing

Browser-owned title bars are outside the application's pointer event stream. Use the draggable content chips in the browser strip, or the destination/position selectors. Use ordinary UnoDock tab dragging for layouts inside an individual canvas.

## XAML behaves differently from WPF

UnoDock uses native Uno/WinUI XAML. Nonvisual layout models do not gain a WPF logical tree or automatic inherited DataContext. Use compiled `x:Bind`, explicit sources, or the documented item binding definitions. See [XAML workbenches](xaml-workbench.md).
