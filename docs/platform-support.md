# Platform support

Implementation support, successful compilation and exercised runtime behavior
are different claims. CI artifacts record the source revision and the cases
actually executed.

| Platform | Hosting | Validation |
|---|---|---|
| Windows 10/11 (Uno Skia, Win32) | Native main and floating windows, custom caption, Snap-aware drop targeting, per-monitor DPI for floating bounds | Hosted desktop suites; physical-input suites (`windows-floating-input`, `tear-off`) on a 200% DPI Windows 11 desktop |
| Windows (native WinUI 3) | Native floating windows owned by the host island, closing with it; no window registration needed | Package validation, a native WinUI smoke test, and every Gallery sample and self-test suite on the Windows App SDK in CI; see [Uno and WinUI differences](native-winui.md) |
| macOS (Uno Skia, AppKit) | Native child floating windows, Retina-aware sizing, top-left persisted bounds | Hosted desktop suites on Retina displays |
| Linux (Uno Skia, X11/XWayland) | Native floating windows with Motif, EWMH and ICCCM hints | Hosted desktop suites; XTEST physical input under Xvfb and Openbox |
| WebAssembly | Real Uno browser gallery; optional multi-window browser workspace | Playwright: startup, editing, ownership transfer and recovery |

## Floating windows

Native floating windows are used on desktop hosts; the browser and mobile heads
use in-surface floating windows. Continuous tear-off requires native floating
windows. Floating bounds are persisted in top-left, device-independent pixels
and fitted to a visible monitor when shown. See
[Floating windows](floating-windows.md).

## Known boundaries

* **Wayland** sessions are supported through XWayland; a native Wayland
  adapter is not provided.
* The **browser workspace** runs each window in its own runtime and transfers
  registered payloads; it cannot move live .NET objects between windows or
  hide browser-owned chrome.

## Reference conventions

UnoDock follows the public layout model, API and interaction conventions of a
well-known WPF docking library through an independent implementation (see
[Parity status](parity.md) and the [clean-room process](clean-room.md)). It is
not WPF binary compatibility, a WPF XAML parser or a reproduction of any
commercial theme; native Uno/WinUI XAML is the supported markup model. UnoDock
is MIT-licensed and not affiliated with the reference library's vendor or with Uno Platform.
