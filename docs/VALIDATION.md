# Validation

## 2.2.1 — 18 October 2026

256 application tests pass. The standalone executable exports 320 images across sixteen geometries in French and English. Full-height portrait compositions, empty states and dedicated cooling/long-label captures were visually reviewed. Raw RGB565 and JPEG rotation checks cover 90°/270° without USB writes. Offline render plus JPEG encoding has 8–12 ms medians on the development PC; this is not a physical frame-rate claim. See [release details](RELEASE-2.2.1.md). No installed application, saved settings or sensor assignments were changed.


Post-release community feedback in [issue #3](https://github.com/Sandoiitchisan/LOU-Control-Center/issues/3) reports working portrait pages and requests larger fonts. This does not extend model-specific transport validation or constitute an installer test.

## Stable 2.2.0 promotion — October 17, 2026

- The user confirmed that the reported RX 9060 XT temperature problem is fixed. This is a user-reported hardware result, not a new measurement made by the development agent.
- The user also reported positive additional-screen results without supplying exact model/revision details. The compatibility matrix therefore retains its specific hardware-validation limits.
- The application implementation is unchanged from RC1. Its 242 application tests, 17 Diagnostics tests and source-only rebuild passed; all four GitHub Actions jobs triggered by the RC1 source publication succeeded.
- Portrait-specific layouts are reserved for 2.2.1. No installed private application, saved device assignments or live LCD configuration was changed during promotion.

## Thermalright compatibility candidate — October 17, 2026

Publication update: the owner subsequently uploaded this exact portable ZIP as `v2.2rc1`. Its implementation is now available under that tag. The test results below describe the candidate before upload; no new hardware validation is implied. See RELEASE-2.2rc1.md for the archive checksum and source relationship.

- 242 automated application tests pass (zero failures/skips), including 51 compatibility cases for native HID framing, documented profiles, RGB565 byte order, rotated raster corners, exact physical routing, persistent codec validation and new layouts. Existing GPU fallback regression tests are included.
- Release self-contained Windows x64 publish succeeds without warnings or errors. The published executable exits 0 for both French and English exports: 110 images per language across eleven geometries, with demo and missing data on all five pages.
- Visually inspected rectangular system/music/weather/gaming pages, French and English text, centered missing-cover artwork, a compact portrait view and the 640 × 172 panoramic layout.
- No LCD transport was opened for these tests. No changes to the private installation, saved page interval, sensor assignments, drivers or firmware. The local candidate is a portable build; no new Setup or public release was published.
- The Trofeo HID reply is synthetic and based on public PM/SUB observations, not a captured private handshake. The user's anonymized report independently corroborates the USB family and Windows HID report sizes. Physical Trofeo output and RX 9060 XT temperature remain unconfirmed.
- The compatibility matrix and licensing provenance review remain authoritative limits. Test success does not turn unowned devices into physically validated models.

## RGB 2.2.0 development — October 13, 2026

- 170 automated tests pass, including fragmented SDK packets, v6 unique routing IDs, physical-identity resolution, hot-swap refusal, malformed/truncated data, profiles, cancelled requests, rejected commands, delayed SDK acknowledgements and whole-case group isolation/capability checks.
- Native standalone English universal preview exited 0 (PID 29120), exercised both languages and reported zero USB frames.
- The shared module was ported to personal edition 1.5.0. Its 95 existing tests pass. Native standalone private preview exited 0 (PID 25868); five cooling assignments, server settings and native settings-file hash were preserved. Both RGB surfaces were visually inspected.
- Native elevated detection found Kingston Fury DDR5, Gigabyte B650E AORUS ELITE X AX ICE and SteelSeries Apex Pro TKL Gen 3. GPU RGB was not detected. Engine and elevated helper exited after the diagnostic.
- A RAM Static/cyan command initially changed OpenRGB cached state without the requested visual result. Matching ACK waits and rejection handling were added and covered by tests: software readback can precede hardware writes. After the correction, a native Windows test of 2.2.0 completed successfully and the user confirmed both Kingston modules displayed cyan on 13 October 2026. Other devices and effects remain unverified physically.
- Whole-case/fan Direct cyan was physically confirmed on 13 October 2026, including motherboard-connected and hub-connected lights. The successful diagnostic used ordinary permissions. Static-effect attempts with ordinary/elevated access and a 30-second engine hold had no visible effect; no native-effect compatibility is claimed for this board. SDK ACK confirms software handling, not physical hardware completion.
- Local portable and installer candidates are built, but have not been installed over the personal working edition or published. Restrictive-license provenance review is also a release gate.


## Distribution 2.1.1 — October 12, 2026

- Release build without warnings; 145 automated tests pass, including language isolation, legacy settings, custom metadata, both display formats and update-feed validation.
- Standalone preview runs in English, switches to French and back through the real Settings controls, saves and reloads language, and verifies propagation to both rendering workers. Independent page intervals are retained; zero USB frames are sent.
- A decimal-culture mismatch found during the first preview test was corrected. The subsequent complete preview run passed.
- Manual update check against the real GitHub endpoint succeeds. The published 2.1.0 is correctly treated as older than the running 2.1.1, with no downgrade offer.
- Native Windows test of the extracted 2.1.1 package: preview and physical run both exited successfully. With clearly labeled demo data, the large LCD delivered 23.97 transmissions/s and 23.89 distinct frames/s; the AiO delivered 10.00 of both. All five English pages appeared on each display during a 36-second test with three 250 ms UI stalls. Saved intervals remained 15 seconds and the personal app was relaunched afterward.
- Native English Setup test: installation and uninstallation both exited 0. The installed 2.1.1 preview ran under a standard user, verified fresh diagnostics against its PID, sent zero USB frames and received 38 fresh hardware readings. Existing settings and the personal-edition binary hash were unchanged. The temporary universal installation was removed after testing.
- Original third-party licenses and binaries remain unchanged. No private settings are included in distribution packages.

## Distribution 2.1.0 — October 11, 2026

- 109 automated tests passed; warning-free Release build and successful rebuild from an isolated source copy.
- Self-contained ZIP: 517 entries, no settings, personal traces or captures. Executable tested after extraction and from another working directory; preview sent zero USB frames.
- Native Setup installation: exit 0, shortcuts, active SYSTEM collector, 38 real readings and active PresentMon capture. Interface launched as a non-elevated user.
- Simultaneous display test: large LCD 23.99 transmissions/s and 23.22 distinct frames/s; AiO 10.01 transmissions and distinct frames/s. All five pages appeared on both displays. Three 250 ms UI stalls were injected.
- Repair preserved configuration, restored sensors/FPS and removed startup when unchecked.
- --tray launch: hidden window, non-administrator process, --shutdown exit 0.
- Uninstallation: exit 0, executable and task removed, no remaining universal process or ETW session; settings and shared PawnIO retained. Personal-edition executable unchanged.
- Reference PC already had PawnIO 2.2.0 and WinUSB. First-time PawnIO installation and a clean Windows installation remain unverified. Official installers and verified signatures are included.
- Source and the 2.1.0 release were subsequently published to GitHub.

## Portable 2.0.0 — October 11, 2026

- Warning-free Release build and 106 passing automated tests.
- All five pages rendered with simulated and unavailable data at 1920 × 462, 480 × 480, 320 × 240 and portrait sizes; square pages and interface visually inspected.
- Six-section interface at normal/minimum size. Changing the first display’s interval did not change the second. Preview saved separately and sent zero USB frames.
- Live key-free Open-Meteo city search for Lyon, France/United States disambiguation and weather request.
- Approximately 36-second simultaneous physical test: all five pages on both auto-detected displays. Large display: 23.98 transmissions/s and 23.79 distinct frames/s. AiO: 10.01 transmissions/s and 9.93 distinct frames/s. Three 250 ms WPF stalls were injected; transports remained independent.
- Temporary 3-second intervals were not saved; both profiles stayed at 15 seconds. No personal sensor assignments were imported.
- Hardware traces and serial numbers remained in local QA folders, excluded from the ZIP.

## Limits

These are short tests, not proof of multi-day endurance, physical unplugging or sleep/resume recovery. NVIDIA hardware and other motherboard/display models still need physical testing. Automated tests check identities and the absence of fabricated values; they do not replace hardware tests. Users must map RPM readings to their own wiring.
