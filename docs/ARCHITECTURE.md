# Architecture

The project is independent of the personal edition. No server connection, city, SSH address, hardware assignment or personal USB serial is included.

- Core: JSON configuration, five pages, per-display profiles and independent schedulers.
- Providers: one shared DataHub collects PC, media, game and weather information. LibreHardwareMonitor 0.9.6 provides AMD/NVIDIA/Intel temperatures and fan readings, supplemented by AMD ADL. Missing readings never become zero.
- Devices: PnP discovery limited to recognized USB families, LY/ChiZhu handshake and serialized sessions. Persistent profiles are checked at reconnection; changes require a detection reset.
- Rendering: panoramic and five compact compositions. 90/270-degree orientation changes logical dimensions before physical rotation. An optional cooling view is available.
- App: a render/transport thread pair per display, cancellable high-resolution pacing and latest-frame-only buffering. WPF samples previews at 5 Hz without blocking rendering. Requested frame limits are not measured FPS.

## Reading identity

Assignments include provider, motherboard, controller ID, sensor ID, hardware name and topology fingerprint. The fingerprint comes from sorted controller inventory and physical GPU IDs, never RPM or enumeration order. Inventory changes block assignments instead of silently reassigning them. Motherboard/Super I/O names do not identify headers: users must confirm wiring in the BIOS or reliable documentation.

Identical GPU models are distinguished by DeviceId when exposed. Without a stable physical identity, the app cannot recognize an indistinguishable replacement; assignments remain configurable and should be checked after hardware changes.

ReadOnlyHardwareSettings supplies LibreHardwareMonitor settings: no external settings file is loaded, controls remain Undefined, and no hardware SetDefault / SetSoftware method is called.

## Isolation

Portable Data is resolved beside the executable, never in the personal edition’s LocalAppData. Advanced collection writes only there and stops with its parent, whose PID and start time are checked. The display mutex is shared with the personal edition to prevent two LCD writers. Preview uses its own mutex and opens no transport.

Closing the window may minimize to the notification area. Exit stops loops, releases transports and stops PresentMon. The portable edition creates no Windows startup entry or permanent service.

## Weather

Explicit HTTPS city-name search through geocoding-api.open-meteo.com. Users choose a city with region/country; only that selection’s coordinates are used internally. Forecast requests run every 20 minutes with caching, retry delay and Retry-After handling. Open-Meteo and GeoNames attribution is retained. No account or API key.

## Windows installation

The installed.mode marker, added only by Inno Setup, selects %LocalAppData%/LouControlCenterUniversal. Relative Data/Backgrounds paths resolve against that root; Program Files stays immutable. Portable storage and collection remain temporary and local.

A SYSTEM task launches the protected Program Files executable with --sensor-collector --machine. Its fixed command loads no user configuration. It writes bounded snapshots to ProgramData/LouControlCenterUniversal/Sensors, read-only for standard users. PresentMon runs in a Windows job that kills the child when the collector exits, using an edition-specific ETW session. User security-group membership is unchanged. Presentation readings contain no window names or displayed content and expire after five seconds.

Setup and uninstallation stop only this task and session. The interface shutdown event is universal-edition-specific; the USB mutex remains shared with the personal edition. The latter’s settings, data and tasks stay independent.

## Localization

French and English share one executable and configuration schema. Old settings default to French; new configurations use the Windows language with English fallback. Embedded Core catalogs supply WPF dynamic resources and renderer labels. A scoped language context isolates concurrent display workers. Numeric input uses the selected culture explicitly rather than the dispatcher’s inherited culture.

Provider status text is translated at the presentation boundary. User labels, media/game metadata and physical identities are preserved. Default fan labels remain translatable rather than being saved as custom names. Language changes update existing workers without resetting their USB identity or page interval.

## Release updates

The interface checks the public GitHub latest-release endpoint asynchronously, once per launch automatically if enabled, plus manual requests. A 12-second timeout and 1 MiB response limit bound requests. Only valid newer stable three-part versions trigger an offer. Exact repository URLs and package names are checked; missing assets fall back to the validated release page. Installed apps select Setup, portable apps select ZIP. There is no automatic download, installation, elevation or collector-side check. Preview skips automatic checks; UI validation may explicitly exercise a manual check.
