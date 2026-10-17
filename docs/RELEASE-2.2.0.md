# LOU Control Center 2.2.0

Stable release, 17 October 2026. Promoted from 2.2 RC1 at the project owner's request following user feedback. The application implementation is unchanged from RC1; release documentation and packaging reflect the stable status.

- [Installer](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2.0/LOU-Control-Center-Setup-2.2.0-win-x64.exe)
- [Portable ZIP](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2.0/LOU-Control-Center-Portable-2.2.0-win-x64.zip)
- [Source ZIP](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2.0/LOU-Control-Center-Sources-2.2.0.zip)
- [Browse versioned source](https://github.com/Sandoiitchisan/LOU-Control-Center/tree/v2.2.0)
- [Checksums](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2.0/SHA256SUMS.txt)

## Included

RGB controls powered by OpenRGB, native RAM effects where supported, per-device profiles, additional Thermalright HID/LY1/ALi paths, documented RGB565 formats, rectangular layouts for the five dashboard pages and improved AMD temperature fallback handling. French and English are included in one application.

The reported Radeon RX 9060 XT temperature issue is **confirmed fixed by user feedback**. Positive feedback was also received for additional screens. Model-specific USB validation limits remain in the [compatibility matrix](USB_COMPATIBILITY.md); GPU RGB and SCSI/HID type-3 image output are not newly claimed as supported.

## Install or update

Close TRCC and other LOU instances using your displays. Run the self-contained Windows x64 installer, or extract the portable ZIP into its own writable folder. Existing installations can use **Settings → Check for updates**. Updates are offered for manual download and are not installed silently.

The portable edition stores configuration in its own `Data` folder. The installed edition uses its established universal-edition data location. Neither edition takes control of BIOS/GPU fan curves. The private edition remains separate.

## Source and validation

Use .NET SDK 10.0.401 on Windows x64 and run `./scripts/build.ps1`; add `-Installer` with Inno Setup 6 for Setup. Dependencies and third-party notices are included; the initial source build restores NuGet packages. Diagnostics has its own build script and tests.

The RC1 source-only rebuild passed 242 application tests and 17 independent Diagnostics tests. The standalone executable generated 220 preview images. GitHub Actions also passed on the published RC1 source. Rebuilt stable packages may differ in Git metadata, documentation and archive compression; no byte-for-byte equivalence to the RC1 ZIP is claimed. Installation on every hardware configuration is not implied by these results.

RC1 remains available as history. **Portrait-specific compositions are planned for 2.2.1 and are not included in this stable promotion.** The [licensing provenance review](LICENSING_REVIEW.md) remains unresolved and is not cleared by publication or stability promotion.
