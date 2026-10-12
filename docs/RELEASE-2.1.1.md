# LOU Control Center 2.1.1 — French & English

The same LOU Control Center, now in **French and English**, with built-in GitHub update checks.

## What’s new

- Choose **Settings → Language → English / Français**, then **Save and apply**. The interface, notification-area menu, status messages and all five pages on ultrawide and compact displays change together, including the dedicated cooling view.
- New configurations follow Windows: French on French Windows, English otherwise. Existing configurations stay in French until you choose to change them.
- **Settings → Updates** checks for newer stable releases, automatically at startup if enabled or whenever you press **Check now**. Downloads open in your browser: Setup for an installed app, ZIP for a portable app. Nothing is installed automatically.
- English documentation, a bilingual installer and a fix for decimal input when switching languages.
- Your sensor assignments, custom labels, display profiles and page intervals are preserved. Monitoring remains read-only.

## Download

- [Windows x64 Setup — recommended](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.1/LOU-Control-Center-Setup-2.1.1-win-x64.exe)
- [Portable ZIP](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.1/LOU-Control-Center-Portable-2.1.1-win-x64.zip)
- [Corresponding sources](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.1/LOU-Control-Center-Sources-2.1.1.zip)
- [SHA-256 checksums](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.1/SHA256SUMS.txt)

Setup and portable packages include the .NET runtime and required components. Internet is needed for weather and update checks. Windows 10 version 2004 or later / Windows 11, x64.

## Upgrade

Exit LOU before updating. Installed users can run Setup over the existing universal installation. Portable users should extract into a new folder and copy their Data folder after exiting the old app. Version 2.1.0 has no built-in updater, so this first upgrade is a manual download.

145 automated tests pass, covering localization, settings compatibility, independent display layouts and update-feed validation. The standalone preview has been verified in both languages. See [validation details](https://github.com/Sandoiitchisan/LOU-Control-Center/blob/v2.1.1/docs/VALIDATION.md) for hardware test scope and limitations.

LOU is not yet publisher-signed. Third-party components retain their official signatures. Thermalright variants outside the tested profiles and physical NVIDIA hardware still need validation. Open-Meteo’s key-free service is for non-commercial use within its quotas.

An independent GPL-3.0 project, built with substantial AI assistance and refined through hands-on hardware testing. Feedback and contributions are welcome.
