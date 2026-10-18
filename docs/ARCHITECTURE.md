# Architecture

The project is independent of the personal edition. No server connection, city, SSH address, hardware assignment or personal USB serial is included.

- Core: JSON configuration, five pages, per-display profiles and independent schedulers.
- Providers: one shared DataHub collects PC, media, game and weather information. LibreHardwareMonitor 0.9.6 provides AMD/NVIDIA/Intel temperatures and fan readings, supplemented by AMD ADL. Missing readings never become zero.
- Devices: PnP discovery limited to recognized USB families, native HID type 2 and LY/LY1/ChiZhu/ALi WinUSB handshakes with serialized sessions. Exact physical instance routing prevents cross-device writes. Persistent geometry, codec and byte order are checked at reconnection; changes require a detection reset. SCSI/HID type 3 are identified but disabled. See USB_COMPATIBILITY.md for validation limits.
- Rendering: panoramic, rectangular landscape, compact and full-height portrait compositions. Portrait layout uses a fixed logical width with a variable height; circles, fonts and artwork are never stretched. 90/270-degree orientation changes logical dimensions before physical rotation. JPEG and RGB565 encoding share explicit raster checks. An optional cooling view is available in square and portrait layouts.
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

## RGB

`Lou.Rgb` is an edition-independent WPF surface, settings store, bounded SDK 6 client and process host. It has no reference to cooling providers or LCD transports. Each command reconnects, negotiates SDK 6, fetches the current physical inventory and resolves a persistent assignment before routing via the server's unique ID. Writes await the matching ACK and reject error statuses. Requests have payload/count limits and deadlines; operations are serialized off the render paths.

The bundled OpenRGB executable is hash-checked against an embedded manifest and runs in a separate process on loopback. The optional elevated host checks its parent's executable and start time, owns a job object and stops on parent exit or a named stop event. It receives no arbitrary executable or configuration path. It creates its own empty temporary OpenRGB configuration, preserving upstream user settings and avoiding automatic third-party profiles/plugins. The standard-user UI sends only RGB SDK commands; the SYSTEM sensor collector is unchanged.

The native server starts listening before detection completes. LOU waits for the pinned engine's completion signal before reading the inventory. The process can report cached RGB state before hardware writes finish, so a readback request is not used as a substitute for ACK.

Software effects initialize a compatible Direct mode, then maintain a persistent SDK connection with sequential acknowledged color frames. A local high-resolution waitable timer (shared `FramePacer` utility only) schedules up to 60 FPS without a global timer-resolution change or spin loop. Device identity/topology and active modes are re-read once per second; changed-inventory notifications, rejected writes and connection loss stop the worker. Overlapping assignments cancel and await the prior worker before changing modes; different devices and disjoint zones may animate independently. Workers run outside WPF and display rendering. RGB profiles persist stable effect IDs and an optional explicit whole-case ARGB coverage limit.
