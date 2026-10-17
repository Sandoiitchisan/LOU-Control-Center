# LOU Control Center 2.2 RC1

Release: [v2.2rc1](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/tag/v2.2rc1), 17 October 2026. Windows x64 portable prerelease, not a stable update. No RC1 installer is published.

## Source corresponding to the portable archive

- [Browse versioned sources](https://github.com/Sandoiitchisan/LOU-Control-Center/tree/v2.2rc1).
- [Download the source ZIP](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2rc1/LOU-Control-Center-Sources-v2.2rc1.zip).
- [Download the portable application](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2rc1/LOU-Control-Center-2.2rc1-20261017-win-x64.zip).
- [Release checksums](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.2rc1/SHA256SUMS.txt).

The uploaded portable ZIP is byte-for-byte the locally tested display candidate, renamed by the owner. Its SHA-256 is:

```text
983db73aed8b85513abf836768050180b056941dd6f126a3fb695d1d24a6b2f3
```

The source publication retains the implementation used to build that archive. Documentation now reflects the public RC1 status rather than the earlier local-candidate status. The release tag initially pointed to 2.1.1 and was corrected to the RC1 source publication; 2.1.1 and Diagnostics tags are unchanged.

The executable reports `2.2.0`, and its original informational metadata includes the older Git base `57f1b7f`, because development changes were built before their publication commit. That metadata alone does not identify the complete RC1 source. The `v2.2rc1` source tag and this archive checksum identify the release. The binary has not been silently rebuilt or replaced. The packaged START-HERE/licensing review documents predate public upload; consult the repository's current licensing status as well.

## Build

Use Windows x64, .NET SDK **10.0.401** and the complete source checkout/archive. Runtime dependencies, visual assets, pinned monitoring tools and the unmodified OpenRGB engine/source archive are included. The first build restores NuGet packages from the network.

```powershell
dotnet test tests/Lou.Tests/Lou.Tests.csproj -c Release
dotnet publish src/Lou.App/Lou.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o artifacts/rc1-rebuild

# Complete portable packaging, including notices and the optional PawnIO installer
./scripts/build.ps1

# Independent diagnostic tool
./scripts/build-diagnostics.ps1
```

The generic package script uses the embedded `2.2.0` version in filenames; this does not turn a rebuild into a stable release. A rebuild can contain different Git informational metadata and is not promised to be byte-identical to the original executable. Rebuilding requires no LCD access. `Lou.ControlCenter.exe --export` creates offline preview images without sending LCD frames. Do not run two normal LOU instances or TRCC and LOU against the same displays.

## Changes and limits

RGB controls through OpenRGB; smoother native RAM rainbow where supported; per-device lighting profiles; new HID type-2, LY1 and ALi/WinUSB paths; documented RGB565 profiles; rectangular layouts for all five pages; improved AMD fallback temperature handling. See [changelog](../CHANGELOG.md), [RGB guide](RGB.md), [display matrix](USB_COMPATIBILITY.md) and [validation record](VALIDATION.md).

242 automated application tests passed. Standalone French/English exports generated 220 images across eleven geometries. Trofeo physical output, the reported RX 9060 XT temperature case and GPU RGB remain unconfirmed on the relevant hardware. SCSI and HID type-3 devices are identified but not driven. Built-in update checks target stable releases and do not offer RC1 automatically.

Before source publication, a fresh source-only copy also built and packaged successfully: zero build warnings/errors, all 242 application tests and all 17 independent Diagnostics tests passed. This rebuild was a source completeness check, not a replacement for the uploaded portable binary.

The owner's intended restrictive license transition remains under [provenance review](LICENSING_REVIEW.md). Publishing sources does not remove previous GPL grants or establish rights to relicense third-party material. See [LICENSING.md](../LICENSING.md).
