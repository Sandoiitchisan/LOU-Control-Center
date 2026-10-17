# Contributing

PolyForm Strict is the owner's intended license for original 2.2 RC1 material, subject to the unresolved provenance review described in [LICENSING.md](LICENSING.md). Bug reports and review feedback are welcome. For material covered by those intended restrictions, request permission before modifying, redistributing or submitting patches, and agree contribution licensing with the maintainer. Existing GPL releases and third-party licenses are unaffected.

Install the .NET 10 SDK and clone the repository on Windows x64. Official redistributables and their licenses in tools are kept unmodified; their hashes are checked during the build.

```powershell
./scripts/build.ps1
# With Inno Setup 6, also build the installer:
./scripts/build.ps1 -Installer
```

Read [AGENTS.md](AGENTS.md) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Fan monitoring is strictly read-only. Every new display family needs an identified transport, confirmed geometry and documented physical tests. Brand matching alone does not establish compatibility.

Test first with `--preview`: no USB transfers or PresentMon capture. Then test the standalone package on the relevant devices. Never run multiple USB writers at once. `--review-ui --english` captures the interface and exercises language changes in preview mode; `--export --english` exports simulated and missing-data display layouts.

For issue reports, include Windows version, LOU version, device models and expected behavior. Remove account names, serial numbers, private paths and personal configurations from logs before sharing them.

Compiled packages belong in Releases, not Git history. Provide sources matching the binaries exactly and retain all license notices. The workflow builds and archives packages; release publication is manual.

Translations are embedded in `src/Lou.Core/Localization`. French source strings are stable presentation keys. Add English entries and keep format placeholders intact. Never translate sensor IDs, connector names, user labels, media metadata or executable identities. Test both languages, empty data and compact layouts.
