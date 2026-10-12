# LOU Control Center 2.1.0 — Your information, on your Thermalright displays

Turn your Thermalright case and AiO screens into dashboards for system monitoring, weather, music, gaming and ambient visuals, in one Windows app.

## Download

**Recommended: [self-contained Windows x64 Setup](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Setup-2.1.0-win-x64.exe).**

Setup includes the .NET runtime, monitoring tools, official sensor component and artwork. It installs the app, shortcuts and sensor collector; Windows startup is optional. Internet is needed for city search and weather.

Prefer a folder-based app? [Download the portable ZIP](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Portable-2.1.0-win-x64.zip), extract everything and run Lou.ControlCenter.exe.

## Features

- System: CPU/GPU/RAM load, temperatures, network throughput in MB/s and available fan speeds.
- Weather: current conditions and forecasts for your city, without an API key.
- Music: now-playing information from compatible Windows media players.
- Gaming: graphics-activity detection and presentation rate through PresentMon.
- Ambient: clock and subtle decorative backgrounds.

All five pages support ultrawide and compact displays. Each display has independent pages, timing, transitions and orientation. A dedicated cooling view is also available.

## First launch

1. Exit TRCC or other software using the same displays.
2. Install and open LOU Control Center.
3. Check devices and orientation in Displays (Écrans).
4. Assign fan and pump sensors in Sensors (Capteurs).
5. Select a city in Weather (Météo), then save.

Version 2.1.0’s interface is French. Monitoring is read-only: BIOS and existing software retain control of fan curves.

## Compatibility and validation

Windows 10 version 2004 or later / Windows 11, x64. Recognized JPEG profiles in the Thermalright LY and ChiZhu families are supported. Physical tests used simultaneous 1920 × 462 and 480 × 480 displays. Other models require validation; brand alone does not establish compatibility.

109 automated tests passed. Installation, repair, non-administrator launch and uninstallation were verified on the reference PC. Clean Windows installation remains unverified. NVIDIA support exists through LibreHardwareMonitor but has not been tested on a physical NVIDIA card.

LOU Setup is not publisher-signed: Windows may show a reputation warning. Third-party redistributables are official signed versions. Download hashes are in SHA256SUMS.txt. Key-free Open-Meteo access is for non-commercial use within service quotas.

## Files

| File | Purpose |
|---|---|
| LOU-Control-Center-Setup-2.1.0-win-x64.exe | Recommended Windows installation |
| LOU-Control-Center-Portable-2.1.0-win-x64.zip | Extract-and-run application |
| LOU-Control-Center-Sources-2.1.0.zip | Corresponding sources and licenses |
| SHA256SUMS.txt | Integrity hashes |

Independent GPL-3.0 project, unaffiliated with Thermalright. Feedback with the exact display model is welcome in Issues.
