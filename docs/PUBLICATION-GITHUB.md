# Publishing the project and downloads

Target repository: https://github.com/Sandoiitchisan/LOU-Control-Center

## Repository presentation

The root README is the project landing page. Publish docs/images alongside it. Keep its “What’s new” section and CHANGELOG in sync with each release.

Suggested About description:

> A Windows dashboard for Thermalright displays: system, weather, music, gaming and ambient pages. Independent display settings, French/English UI and read-only sensors.

Suggested topics: thermalright, lcd, hardware-monitoring, windows, aio, system-monitor, csharp, dotnet.

## Publish a release

1. Publish the matching source commit, licenses and docs/images.
2. Open **Releases → Draft a new release**.
3. Create **v2.1.1** on the matching source commit.
4. Use **LOU Control Center 2.1.1 — French & English** and docs/RELEASE-2.1.1.md.
5. Attach Setup EXE, portable ZIP, source ZIP and SHA256SUMS.txt.
6. Verify attachment sizes and hashes, then publish. Check the README download links afterward.

The README uses the explicit release tag and exact asset names. Packages belong in Release attachments, never Git history. Automatic “Source code” archives are not ready-to-run applications. Public downloads require a public repository.

The update checker recognizes stable three-part version tags, with an optional v prefix. Keep these names: LOU-Control-Center-Setup-X.Y.Z-win-x64.exe and LOU-Control-Center-Portable-X.Y.Z-win-x64.zip. Publish only after both packages are uploaded. Drafts and prereleases are not offered.

Local publication bundles are kept under ignored artifacts/publication-vX.Y.Z folders.

GitHub documentation: [managing releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository) and [direct asset links](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases).
