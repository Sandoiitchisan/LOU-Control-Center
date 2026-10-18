# Release checklist

## One version, one source snapshot

1. Commit the intended application version and changes, run the appropriate tests and verify the standalone package. Record physical tests separately from previews and offline benchmarks.
2. Build Setup and portable from that clean commit. Inspect the ZIP for missing dependencies and exclude `Data`, `Review`, logs and personal identifiers.
3. Tag that exact commit. Published version tags are immutable; do not move a tag to a later README correction or overwrite a published binary with a different build.
4. Merge the released implementation into `main`. Documentation may subsequently be corrected on `main`; explain that tagged sources and packaged documentation retain their original snapshot.

## Release assets and metadata

For an application release `vX.Y.Z`, attach:

- `LOU-Control-Center-Setup-X.Y.Z-win-x64.exe`
- `LOU-Control-Center-Portable-X.Y.Z-win-x64.zip`
- `LOU-Control-Center-Sources-X.Y.Z.zip`, produced from the exact tag with `git archive`
- `SOURCE-MANIFEST.json`, recording the source commit and package hashes
- `SHA256SUMS.txt`, containing a separate SHA-256 line for every downloadable package and the manifest

Keep a release in draft while uploading and verifying its files. Use prerelease status for candidates. Mark the intended stable application release as Latest: the in-app updater reads GitHub's latest stable endpoint and expects the exact Setup/Portable names above. Check the endpoint after publication.

LOU Diagnostics is a separate utility, with `diagnostics-vX.Y.Z` tags. Keep its releases and downloads separate and do not make it the latest LOU Control Center update.

## Documentation and verification

- Update the README badge URL (not only its alt text), main download button, portable/source links and checksum link together.
- Mark the changelog entry as published with its date. Move completed work out of the upcoming roadmap.
- Check release notes and installation/compatibility guides for outdated candidate claims and limits. Keep historical validation evidence tied to the version actually tested.
- Use ordinary Markdown links; check that pasting into GitHub has not nested links twice.
- Confirm every linked release asset exists and its GitHub digest matches the local file. Inspect Actions for the released commit.
- Preserve earlier releases, their assets and tags as history. Add a successor notice when useful instead of deleting them.
- Review the existing licensing provenance status; release publication does not resolve an outstanding review.

User reports remain open until their actual request is resolved. Do not treat successful rendering or a source-code merge as confirmation of every hardware model.
