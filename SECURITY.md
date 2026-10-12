# Security

The installed collector runs with system privileges to access sensors and ETW. It uses only fixed commands and protected locations in Program Files and ProgramData. It must never load a command, script, library or address supplied by user settings.

No fan curve, GPU configuration or LCD firmware writes. No unsigned PawnIO mode and no disabling Windows driver protections.

Enable private vulnerability reporting in the GitHub repository settings before publication. When available, use the repository’s Security tab to report sensitive issues privately. Never publish keys, personal configuration files or machine identifiers.

Third-party redistributable signatures and hashes are checked before distribution. LOU does not yet have a code-signing certificate; Windows may display a reputation warning.

The update checker makes an anonymous HTTPS request to this repository’s latest stable release endpoint. It compares numeric versions and accepts only exact release/download URLs for this project. It sends no settings, hardware inventory or credentials. Downloading requires a user action; the browser handles the download. No update is executed automatically. A failed check is never reported as proof that the app is up to date.
