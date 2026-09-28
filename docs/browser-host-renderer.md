# Browser host renderer

The browser sample explicitly selects Uno's supported `ForceSoftwareRendering()`
host option. It still runs the actual Uno Skia renderer, layout engine, native
controls, semantic accessibility tree and managed editor models. It is not a DOM
mock, a screenshot, or a headless-only replacement for the published application.
Desktop hosts retain their existing rendering configuration.

The current pinned WebGL path crashed before readiness in hosted Chromium and a
local reproduction. The same local artifact rendered its real workspace and
accepted keyboard edits when Uno used its software backend. This isolates a
useful compatibility route but does not establish the precise underlying graphics
failure. GPU acceleration is therefore not claimed for this browser sample.

Software rendering uses CPU rasterization and a 2D canvas upload. It trades GPU
throughput for predictable host behavior, especially when several independent
browser runtimes are open. No browser security flags or simulated accessibility
nodes are required by the published application. All editing, popup transfer,
lease, recovery and idle-projection assertions remain required before deployment.

The regression verifies the actual canvas context after the native editor models
are ready. A successful static page load alone is insufficient. The complete
source-quality and browser workflows must pass at the exact committed revision.
