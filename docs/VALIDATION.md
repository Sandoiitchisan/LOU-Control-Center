# Validation

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
