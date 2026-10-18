# Installation and first setup

Windows 10 version 2004 (19041) or Windows 11, x64. Physical output requires a supported Thermalright transport and a recognized JPEG or RGB565 profile; see the [compatibility matrix](USB_COMPATIBILITY.md). The interface can run without a display.

## Install

Run Setup, accept the administrator prompt, choose shortcuts and optionally enable Windows startup. It installs LOU in Program Files/LOU Control Center Universal, configures read-only collection and opens the app with your account’s ordinary privileges. A restart may be required after the hardware component is first installed.

Setup is self-contained. Internet is required for city search, weather and update checks. LOU does not yet have a publisher signature; SHA-256 hashes verify file integrity, not publisher trust.

1. Exit TRCC and any other LOU edition from the notification area.
2. Check devices, orientation and pages in **Displays**. Each display has an independent page interval. For a landscape panel mounted vertically, select 90° or 270° and save; the pages adapt automatically.
3. In **Sensors**, assign the radiator, case fans and pump using confirmed wiring. RPM alone cannot identify a connector. Temperatures and a single GPU are detected automatically when available.
4. Find and select your city in **Weather**, then save.
5. Choose **Settings → Language → English / Français**, then **Save and apply**. The app and all display layouts change together. Existing configurations keep French; new configurations follow Windows (French or English fallback).

## Updates

**Settings → Updates** can check GitHub at startup (optional) or on demand. A newer stable release appears in Settings and Overview. The download opens in your browser: Setup for an installed app, ZIP for a portable app. Exit LOU before updating. No installer is run automatically. If no matching package is available, the button opens the release page.

For portable updates, extract the entire ZIP to a new folder, exit the old app, and copy its Data folder into the new one. Keep the old folder until you have verified the update. Do not add `installed.mode` to a portable folder.

## Uninstall or repair

Run Setup again to repair files and recreate the collector task. Settings are preserved. Uninstalling through Windows removes LOU, shortcuts, its startup entry and collector task. User settings and PawnIO are retained. Uninstall PawnIO separately if no other application uses it.

## Troubleshooting

- No display: check the internal USB cable, close TRCC and wait for detection. Devices declaring `USB/MS_COMP_WINUSB` use Windows’ built-in WinUSB automatically. Other variants may require their manufacturer’s driver. LOU does not change firmware or driver security settings.
- Missing temperatures or RPM: check Sensors, then `%ProgramData%/LouControlCenterUniversal/Sensors/installation.log`. Restart Windows if requested after PawnIO installation. Some components expose no reading; LOU shows an absence, never an invented value.
- Missing FPS: for the installed edition, check that `LOU Control Center Universal - Capteurs` is running in Task Scheduler. This persistent task name is unchanged between languages. The interface does not need administrator privileges. Protected games or unobserved APIs may remain unavailable.
- Settings: `%LocalAppData%/LouControlCenterUniversal`, with `settings.json.bak` as a backup. Portable: Data beside the executable.
- Another edition is running: exit it completely before launching this one. Two versions must not write to the same displays at once.
- Update check unavailable: check your connection and try later. GitHub may rate-limit anonymous requests. This status does not mean you are up to date.

## Portable

Extract the entire ZIP to a writable folder. The .NET runtime and components are included. Temporary advanced collection requires Windows permission; no permanent LOU service or startup entry is installed. Custom images stay in Data/Backgrounds: copy this folder together with settings when migrating manually.
