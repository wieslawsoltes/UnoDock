# Build and release configuration

CI runs on push/PR/manual dispatch. Portable tests emit JSON and JUnit XML; desktop UI
tests run inside the gallery on Linux/Xvfb. Windows/macOS desktop builds and browser
publishing are separate jobs. NuGet artifacts are built from the same source revision.
Inspect all job conclusions before promoting a revision.

The `Publish NuGet` workflow accepts a release tag `v<semver>` or a manual `version`
input. Tags must resolve to the selected commit. It runs tests and packages both
`UnoDock.Core` and `UnoDock`, including symbols. It refuses a stable 1.0+ release unless
`contracts/release-attestation.json` explicitly attests complete compatibility at that
exact source revision. The initial preview contains no such attestation.

Choose one authentication option:

1. Set `NUGET_API_KEY` as a repository or `nuget` environment secret. Scope the key to
   the two package IDs with push permissions and an appropriate expiry.
2. Set repository variable `NUGET_USER`, and configure NuGet trusted publishing for
   owner `wieslawsoltes`, repository `UnoDock`, workflow `publish.yml`, and environment
   `nuget`. The workflow requests OIDC and exchanges it via `NuGet/login@v1`.

Configure required reviewers on the `nuget` environment for release approval. Secrets
and trusted-publishing policies are account administration tasks; merely adding this
workflow does not create them. No package has been represented as published until a
successful publishing run is observed.

Preview packages disclose compatibility limitations. Do not label preview package
metadata or release notes as full AvalonDock parity. Add behavior fixtures and platform
acceptance evidence before removing the compatibility disclaimer.
