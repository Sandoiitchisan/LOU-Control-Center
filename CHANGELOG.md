# Changelog

## 2.2.1 — 2026-10-18

- Responsive portrait compositions for all five dashboard pages, plus the dedicated cooling view.
- Automatic layout selection from each display's oriented dimensions; 90°/270° keeps the physical USB raster intact.
- Taller portrait preview and a bilingual orientation hint in Displays.
- Full-height framing, proportionate gauges and cover art, readable missing-data states and all five existing cooling assignments.
- 256 application tests passed; 320 standalone preview exports across sixteen geometries in French and English. Post-release community feedback reports working portrait pages and requests larger text; model-specific coverage remains limited.
- Published self-contained installer, portable ZIP, exact tagged sources and checksums. See [release details](docs/RELEASE-2.2.1.md).

## 2.2.0 — 2026-10-17

Stable promotion of 2.2 RC1, retaining its application implementation. [Release and downloads](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/tag/v2.2.0).

- RGB controls, expanded Thermalright protocols and rectangular layouts from RC1 are now in the stable channel.
- User feedback confirms that the reported Radeon RX 9060 XT temperature issue is fixed. Positive additional-screen feedback was also received; no blanket model/revision compatibility claim is made.
- Self-contained Windows x64 Setup, portable ZIP and corresponding sources are provided. The in-app stable update check can offer 2.2.0 to older stable installations.
- RC1 remains available as release history. Portrait-specific layouts were reserved for 2.2.1 and were not part of this stable promotion.
- Existing hardware limitations and the unresolved licensing provenance review still apply; stability promotion does not constitute licensing clearance.

## 2.2 RC1 — 2026-10-17

Public prerelease tag: [`v2.2rc1`](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/tag/v2.2rc1). Portable Windows x64 only; the embedded application version is `2.2.0`. At the time, 2.1.1 remained the stable release. [Exact source/build notes](docs/RELEASE-2.2rc1.md).

- Extended Thermalright detection, native HID type-2 support for the Trofeo 6.86-inch family, LY1 framing and an ALi WinUSB path. Known SCSI/HID type-3 variants are listed without sending unsupported commands; physical validation of new families is pending.
- Added documented RGB565 profiles, persistent codec checks, exact per-device routing and wide layouts for 1280 × 480 / 1600 × 720 and related rectangular screens. Existing page timing and orientation preferences remain independent.
- Fixed AMD fallback temperature preservation by matching physical GPU identities and isolating optional VRAM failures. The reported RX 9060 XT case was awaiting hardware confirmation at RC1 publication; the fix was confirmed for 2.2.0.
- Integrated OpenRGB 1.0 with French/English controls for devices, zones, colors, native effects, brightness and speed.
- Optimized Kingston Fury RAM rainbow automatically uses the RAM's built-in effect, eliminating continuous SMBus color updates. Slow controllers no longer block software animation frames for unrelated devices.
- Added 17 built-in RGB choices with software animations, adjustable speed/brightness, profile persistence and independent device groups. Persistent SDK streams target up to 60 FPS without building a frame backlog; identity changes and disconnects stop animation.
- Persistent scene profiles, ARGB LED counts and optional startup application with physical-identity checks.
- Local-only RGB engine, optional isolated elevation, lifecycle cleanup and bounded SDK 6 commands with acknowledgements.
- Prepared source-available licensing for original LOU code; legacy releases and third-party grants remain unchanged.
- 242 application tests passed; the standalone executable generated 220 preview images across eleven geometries in French and English. At RC1 publication, new display families and the RX 9060 XT report still required physical confirmation.
- The source is published with the RC1. Licensing provenance review remains unresolved; publication does not clear the intended licensing transition.


## 2.1.1

- French and English in the same application: choose **Settings → Language**, then **Save and apply**. The interface, tray menu, status messages and all display layouts switch together without restarting.
- New configurations follow Windows (French on French Windows, English otherwise). Existing configurations retain French until changed.
- Optional startup update check and a manual **Check now** button. Newer stable GitHub releases offer the correct Setup or portable ZIP. Downloads open in the browser; nothing is installed automatically.
- English documentation and release notes. The installer now supports English and French.
- Display profiles, independent page intervals, sensor identities and custom names are retained.
- Fixed decimal parsing when switching between French and English settings.

## 2.1.0

- Self-contained Windows installer, icon, shortcuts and optional Windows startup.
- Installed sensor and graphics-presentation collector with privileges separate from the interface.
- Data and installation identities independent of the personal edition; the portable app remains self-contained in Data.
- Background paths compatible with both distributions.
- GitHub source package, licenses, contribution guidelines and Windows build workflow.

## 2.0.0

- Universal portable edition: system, key-free Open-Meteo weather, music, gaming and ambient pages.
- LY/ChiZhu LCD detection, independent settings per display and compact layouts.
- User-selected sensor assignments, AMD/NVIDIA support through LibreHardwareMonitor and read-only monitoring.

---

## Separate utility

### LOU Diagnostics 1.0.0

- Standalone, self-contained Windows x64 executable for internal/USB display diagnostics, with French/English UI.
- Automatic passive collection, user-selected text report location, in-app preview and a GitHub issue link for manual attachment.
- Standard USB/HID metadata, per-interface driver information and explicit unconfirmed candidates beyond the two currently supported USB families.
- No RGB scan, monitor inventory, proprietary LCD commands, automatic upload or network requests by the collector. Serial numbers, personal paths and arbitrary device names are not exported.
- Isolated collection worker with a deadline and sanitized partial reports.
