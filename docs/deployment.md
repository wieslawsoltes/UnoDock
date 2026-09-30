# Documentation and browser deployment

The `Browser` job of the CI workflow (`ci.yml`) builds one static site containing documentation, the standard Uno gallery, and the multi-window browser workbench. It publishes the real `net10.0-browserwasm` application—not a screenshot or JavaScript-only substitute.

## Published routes

| Route | Content |
|---|---|
| `/UnoDock/` | Project landing page. |
| `/UnoDock/docs/` | Generated documentation pages. |
| `/UnoDock/gallery/` | Standard UnoDock control gallery. |
| `/UnoDock/playground/` | Browser multi-window workspace host. |
| `/UnoDock/revision.txt` | Exact deployed source revision. |

The Uno publish base path must match `/UnoDock/gallery/`. Deploying a fork under a different project name requires changing that base path and the runtime-test URL together. Serve over HTTP locally or HTTPS remotely; `file://` is not supported.

## GitHub Pages

Use GitHub Actions as the repository's Pages publishing source. Building/testing needs repository read access. Deployment uses narrowly scoped `pages: write` and `id-token: write`; the build does not need repository secrets. Enabling a new Pages site is a separate repository-administration operation and can require maintainer action when the installation cannot administer Pages.

The intended order is publish, assemble the site, execute browser acceptance locally, upload the Pages artifact, deploy, and verify the deployed revision/runtime. A failed deployment is not reported as a live site. Browser window tests require that the workspace and gallery use the same origin and compatible opener policy.

## Other static hosts

`tools/build-browser-site.py --publish artifacts/publish --output artifacts/site` produces the complete site. A host must serve WebAssembly with the correct content type and retain the project paths. This browser implementation does not require SharedArrayBuffer, a custom COOP/COEP service worker, a server process, or a websocket service.

Do not put untrusted scripts on the same origin as the workbench. Read the [browser security and recovery contract](browser-workspaces.md).
