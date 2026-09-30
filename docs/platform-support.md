# Platform and compatibility scope

Implementation support, successful compilation, and exercised runtime behavior are different claims. Workflow artifacts record the source revision and cases actually executed.

| Target | Hosting | Verification model |
|---|---|---|
| Windows, Uno Skia Win32 | Native main/floating windows and custom chrome | Hosted runtime and selected physical-input suites. |
| Linux, X11 | Native main/floating windows; Openbox chrome tests | Hosted runtime, XTEST input, and window-manager acceptance. |
| macOS, AppKit | Native main/floating windows | Hosted runtime/geometry/presentation; physical pointer automation is separate. |
| Native WinUI | Native package target | Compilation/package validation; not equivalent to the Skia Win32 runtime suite. |
| WebAssembly | Real Uno browser gallery and optional popup workspace host | Browser workflow with real startup, editing, ownership, and recovery cases. |

The browser workbench runs each window in its own runtime and transfers registered payloads. It cannot move arbitrary live .NET objects between windows or suppress browser-owned chrome. Pure Wayland, mixed-DPI hardware, OS high-contrast transitions, mobile-specific workspace design, and arbitrary third-party native content are not implied by the hosted matrix.

## Reference-library conventions

The project follows the public layout models of the reference WPF docking library and interaction conventions through an independent implementation. Frozen public reference inventories and normalized API comparisons remain review artifacts. Remaining signature/attribute/behavior differences are reported rather than hidden.

This is not WPF binary compatibility, a WPF parser, arbitrary WPF resource/trigger support, or a reproduction of separately licensed commercial themes. Native Uno/WinUI XAML is the supported markup model. The implementation is MIT and is not affiliated with Xceed or Uno Platform.
