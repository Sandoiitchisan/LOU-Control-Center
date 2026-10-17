# RGB control

RGB is optional and disabled by default. Open the RGB section and close other utilities that control the same lighting. LOU never takes over fan curves or changes cooling settings.

## Engine

OpenRGB 1.0 is included. **Enable RGB** starts the bundled engine with ordinary permissions. **Full hardware access** requests Windows elevation for RAM and some motherboards. PawnIO is supplied with Setup and by the existing hardware-monitoring activation in portable mode. The interface and LCD workers remain ordinary user processes. On startup, ordinary permissions are used; elevation is an explicit action, not a hidden scheduled task.

**Existing OpenRGB server** connects only to localhost:6742 and requires SDK 6 (OpenRGB 1.0+). LOU does not stop external OpenRGB processes. Its own engine uses localhost:16742, an isolated temporary configuration, a shared single-engine mutex and a kill-on-close process job. It stops when LOU exits. No remote host setting, firmware tool or plugin loader is exposed.

## Devices, zones and effects

The main selector identifies **Case and fans**, **RAM**, and **Graphics card** from the controller's reported type. Other devices remain available separately. Case and fans applies one setting to all lighting zones on the selected motherboard, including attached hubs and directly connected lights. It never includes RAM, the GPU, or unrelated controllers. Zone controls remain under the advanced expander.

Whole-case profiles preserve the motherboard identity and connector/mode capabilities. They intentionally cover every zone even when its configured length changes; a replaced controller or changed connector layout is rejected. The group also replaces existing per-zone effects, so an old zone override cannot silently defeat the group setting.

For the reviewed Gigabyte IT5702 controller, uniform whole-case colors cover each ARGB header's advertised capacity (up to 256 addresses). This is an output coverage limit, **not a detected physical LED count**. The default group method is Direct. For individual LED layouts, use the actual connected count in advanced settings. Other controllers do not receive this hardware-specific coverage adjustment.

The advanced **Case group ARGB range** setting limits the addressed length per compatible Gigabyte output. Zero preserves full capacity; a smaller value reduces USB traffic but must cover all connected LEDs. Confirm that every light animates before keeping it. This explicit value is saved in the profile, never inferred from fan RPM, list position or a guessed fan model.

LOU offers 17 built-in choices on controllers with a compatible direct mode: **Static color, Off, Breathing, Rainbow, Color cycle, Wave, Comet, Pulse, Sparkle, Rainbow wave, Chase, Scanner, Blink, Alternating, Fire, Aurora, and Ripple**. Hardware-native effects reported by OpenRGB remain available separately. Brightness, animation speed and the selected color (for single-color effects) are saved in profiles. Audio synchronization and third-party effect plugins are not included.

LOU animations run while the application and RGB connection remain open, including when minimized to the tray. A persistent SDK connection sends up to 60 color frames per second; slow hardware lowers the effective rate instead of queuing old frames. Elapsed time drives animation, independently of LCD rendering. **Stop animations** freezes the current colors. Selecting a fixed color, turning the lights off, changing an overlapping selection or disconnecting stops its animation first. RAM, GPU and case groups can run different effects simultaneously when their controllers support direct colors.

Spatial patterns repeat every 24 addresses and restart at each reported output. This avoids treating unused header capacity as an actual physical strip length. Signal-duplicating hubs mirror the pattern; individual fans on such a hub cannot be addressed separately. On a one-color device, spatial effects reduce to color or brightness changes.

**Kingston Fury RAM rainbow:** when the reviewed Kingston Fury controller exposes its native Rainbow mode, selecting LOU Rainbow for the whole RAM device automatically uses that mode. Brightness and speed remain adjustable; FPS is disabled because the RAM animates itself. No continuous color stream is sent. Existing LOU Rainbow profiles get the same optimization after their saved identity/topology is validated. Advanced individual-zone assignments and other controller families keep their existing path. The native effect may continue after LOU closes; the Stop animations button returns this optimized RAM effect to the selected fixed color. The user confirmed the smoother result on both reference DDR5 modules on 13 October 2026.

Software animation writes are serialized per physical controller, so a slow RAM response cannot hold up an unrelated motherboard's frames. Connections and physical identity checks remain separate for each stream.

The streaming worker rechecks device identity, topology and active modes once per second and stops on device-change notifications, rejected commands or a lost connection. It does not reconnect and blindly resume a saved routing ID. RGB animation workers never control cooling or write LCD data.

Select **Whole device** to change a device-wide effect. A zone without independent modes can change its colors only while a compatible per-LED mode is already active on the whole device. LOU does not silently change the rest of a device when you select one zone.

ARGB headers may initially report zero LEDs. Expand **ARGB configuration** and enter the real LED count, within the device's supported range. This setting is remembered by physical device and header identity and restored when LOU starts its own engine. A signal-duplicating hub cannot address each attached fan independently. Neither RPM nor a fan-header label reveals an ARGB LED count.

## Profiles and identity

Save selections under the same profile name to combine devices and zones. Profiles store the provider/device identity, serial/location, capabilities and zone topology, not the transient SDK routing ID or list position. Missing, duplicated or changed identities block writes. Reassign a device after replacing it or changing its LED layout. Some controllers do not expose enough identity and cannot safely receive a saved profile.

Startup activation and startup profile application are separate opt-in choices. They are saved immediately in `rgb.json` beside this edition's settings, with an atomic replacement and `.bak` backup. No personal profiles or hardware IDs are included in distributions. A corrupted file is preserved and must be restored before editing profiles.

LOU waits for the OpenRGB v6 command acknowledgement. Some controller implementations then schedule hardware work separately; neither an acknowledgement nor cached color readback proves that the LEDs changed. Some devices automatically save effects internally; LOU does not send explicit SaveMode or firmware commands. A bounded engine diagnostic is kept in LocalAppData/LouControlCenterRgb/engine-last.log; it contains hardware identification and should be reviewed before sharing.

## Validation status

On the reference PC, detection found Kingston Fury DDR5, the Gigabyte B650E AORUS ELITE X AX ICE RGB controller, and a SteelSeries Apex Pro TKL Gen 3 keyboard. The ASRock RX 9070 XT Taichi White did not appear; GPU RGB support is therefore **not claimed**. After correcting command completion handling, the user physically confirmed that both Kingston modules displayed the requested static cyan on 13 October 2026. This validates that RAM command path, not every device, effect or ARGB header.

Other RAM, motherboard, GPU, fan-hub and peripheral models require actual testing. The OpenRGB supported-device list is a useful starting point, not a LOU compatibility guarantee. No additional LCD model was enabled by this RGB change.

On 13 October 2026 the user confirmed that the whole case/fan group changed to cyan using Direct mode, including the hub and directly connected lights. Native Static effects produced no visible change on this reference Gigabyte firmware, even with administrator access; these effects are not validated. Direct is therefore the default for the group. The installed personal app was not upgraded during these tests.


Software animation validation on 13 October 2026: full 256-address output coverage reached about 18 acknowledged frames/s and the user reported stutter. An explicit 64-address range per output covered all LEDs on the reference PC and reached 29.4 frames/s with a 30 FPS target. The 60 FPS target reached about 50.5 acknowledged frames/s; the user confirmed the improvement and chose it. These are SDK transmission measurements with physical visual confirmation, not a guarantee of the hardware's internal refresh rate. Default distributions retain full coverage and 30 FPS; this PC's tuning stays in its private settings. GPU lighting remains unvalidated.
