# Build and verification

## Source and portable checks

```sh
dotnet run --project tools/SourceMaintenance -c Release -- --check .
dotnet run --project tools/SourceMaintenance -c Release -- --check-format .
python3 tests/metadata/test_xaml_sources.py
python3 tools/generate-property-adapters.py
git diff --exit-code -- src/
dotnet run --project tests/UnoDock.Core.Tests -c Release -- artifacts/core-tests
```

Formatting uses a pinned Roslyn tool and verifies executable-token preservation across supported parse configurations. Generated adapters and frozen original reference inventories must not be rewritten to make a regression pass.

## Actual desktop hosts

```sh
dotnet build samples/UnoDock.Gallery -c Release -f net10.0-desktop \
  -p:UnoDockTargetFrameworks=net10.0-desktop \
  -p:UnoDockLibraryFrameworks=net10.0 -warnaserror

python3 tools/run-desktop-tests.py \
  --app samples/UnoDock.Gallery/bin/Release/net10.0-desktop/UnoDock.Gallery.dll \
  --output artifacts/runtime --selector fluent-navigator
```

To reproduce a CI desktop job (every group and its evidence gate, then all
remaining suites), run the orchestrator; `--only` limits it to named groups:

```sh
python3 tools/run-ci-desktop.py \
  --app samples/UnoDock.Gallery/bin/Release/net10.0-desktop/UnoDock.Gallery.dll \
  --output artifacts/desktop --only fluent-navigator,docking-sizing
```

Use a fresh output directory. Linux headless runs require Xvfb; physical-input suites must run on a dedicated display with their explicit opt-in. A passing assertion list alone is insufficient when a native process subsequently fails or never completes.

## Browser protocol and runtime

```sh
node --test tests/browser/session.test.mjs
cd tests/browser
npm install --ignore-scripts --no-audit --no-fund
npx playwright install chromium
npx playwright test
```

The default runtime-test URL is `http://127.0.0.1:8765/UnoDock/playground/`. Build/serve the site first using [getting started](getting-started.md). Set `UNODOCK_BASE_URL` to exercise the same suite against a deployed site. Browser tests use fresh browser contexts and no automatic retries.

The suite requires an actual Uno adapter report and model-backed editors; a successful HTML-shell load is not counted as application startup. It exercises real popup windows, the native editor, return-on-close, popup-to-popup movement, stale leases, blocked-popup guidance, reload, and the drag-transfer protocol. Protocol unit tests independently exercise owner/lease invariants without a DOM.

The deployment pipeline retains screenshots, traces on failure, JSON/JUnit results, and a source revision. Browser-specific results do not replace desktop docking, chrome, XAML, or original compatibility acceptance.

## Continuous integration

A single workflow, `.github/workflows/ci.yml`, runs on every pull request and on
`main`:

| Job | Runner | Contents |
|---|---|---|
| Checks | Linux | Source organization and formatting, generated adapters, evidence-gate unit tests, portable core tests, API scan and metadata comparison |
| Desktop | Linux, Windows, macOS | One Gallery build per OS; focused suite groups with their evidence gates, then every remaining registered suite once (Linux: Xvfb, XTEST, Openbox for floating chrome) |
| NuGet packages | Windows | Uno and native WinUI package targets with symbols |
| Browser | Linux | WebAssembly publish, documentation site and Playwright runtime tests |
| Deploy/Verify Pages | Linux | `main` only: deploys the tested site and re-runs the browser suite against it |

Each desktop job uploads one `desktop-<OS>` artifact with per-group JUnit XML,
logs, captures, the runner's execution records and `ci-desktop-summary.json`.
Reference-observation workflows run only when their probes change or on demand.
