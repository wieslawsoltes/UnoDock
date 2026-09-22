# Build and release configuration

CI runs on push, pull request and manual dispatch. Portable tests emit JSON/JUnit XML.
The Linux gallery runs model/control and original-layout interoperability tests inside
a real Uno application under Xvfb. Separate jobs build Windows/macOS desktop heads,
publish the browser head and package the generic Uno/native WinUI targets.

Artifacts include test/API reports, a source ZIP with revision/checksum, desktop builds,
the browser build, and UnoDock/UnoDock.Core NuGet packages with symbols. A build-only
platform is not reported as runtime-tested. Inspect every job for the exact revision.

Native WinUI XAML packing uses Visual Studio MSBuild (`microsoft/setup-msbuild`) rather
than dotnet MSBuild, because its current XAML task target requires that host.

## Publication

`Publish NuGet` accepts a published release tag `v<semver>` or a manual version input.
Release tags must identify the checked-out commit. The validation job runs portable
tests, adapter reproduction, the resolved metadata no-regression gate, and the real
Linux gallery runtime/interoperability/drop/lifecycle suites. The metadata gate also
runs 23 Python regression tests. Known diagnostics do not imply full parity.
The gated Windows job packs both libraries and associated .snupkg symbol packages.

Choose one authentication method:

1. Repository or `nuget` environment secret `NUGET_API_KEY`, scoped to package IDs
   UnoDock and UnoDock.Core with appropriate push permissions and expiry.
2. Repository variable `NUGET_USER` plus a NuGet trusted-publishing policy for owner
   wieslawsoltes, repository UnoDock, workflow publish.yml, environment nuget. The
   workflow requests GitHub OIDC and exchanges it using NuGet/login@v1.

Configure reviewers on the `nuget` environment for approval. Workflow files cannot
create NuGet account policies or credentials. Publication is not represented as
successful until an actual publishing run completes successfully.

## Stable-release attestation

A stable 1.0+ release requires contracts/release-attestation.json with
`fullCompatibilityVerified: true` and a matching `sourceTreeSha256`. Compute the latter
with `python3 tools/source-fingerprint.py` after committing the verified source.
The fingerprint covers the tracked tree except the attestation itself, avoiding an
impossible self-referential commit hash. Any other tracked change invalidates it.

The preview intentionally provides no full-compatibility attestation. Its package
metadata and README disclose remaining API, behavior, windowing and platform limits.
An attestation is an explicit reviewed assertion, not a substitute for acceptance tests.
