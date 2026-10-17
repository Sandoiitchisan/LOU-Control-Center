# Thermalright displays and AMD temperature investigation

Research completed 16 October 2026. This is an engineering evidence record, **not a claim that all listed products work in LOU**. The public release has not been replaced by this work.

Publication update, 17 October: the subsequent implementation is now available in [2.2 RC1](RELEASE-2.2rc1.md). Stable 2.1.1 is unchanged. Statements below about local/unpublished fixes describe the research checkpoint, not the current RC1 publication status.

## Findings that determine the implementation

Stable LOU 2.1.1 discovers only `0416:5408` and `87AD:70DB`, then opens WinUSB bulk pipes. The submitted Trofeo report identifies `0416:5302`, with its usable endpoints on a HID interface. Adding its ID to the existing WinUSB filter would not implement support. The subsequent local development candidate is described in [USB_COMPATIBILITY.md](USB_COMPATIBILITY.md); the research below records the evidence, not a physical validation claim.

Thermalright's [official software page](https://www.thermalright.com/support/download/) distinguishes current TRCC (listed as 2.1.8 when checked) from TR-VISION HOME for legacy Frozen Vision V1 / Frozen Guardian. A shared brand or product name does not imply one wire protocol. No public manufacturer SDK or complete official wire specification was found in the consulted material.

## Product and resolution map

Product specifications establish panel size. Protocol observations establish the size of the images the application should send. These are separate facts.

| Product / family | Published panel specification | Engineering implication / evidence |
|---|---|---|
| Trofeo Vision 360, 6.86-inch | 1280 × 480 | First priority: HID type 2, `0416:5302`; public Trofeo handshake observations identify PM 128, SUB 1. |
| Trofeo Vision LCD, 6.86-inch accessory | 1280 × 480 | Same marketed geometry; choose transport from the actual device, not its label. |
| Trofeo Vision 9.16-inch LCD / ARGB LCD | 1920 × 480 | LY `0416:5408` exists; `0416:5409` is a separate variant. LOU's physically validated LY unit accepts 1920 × 462. Do not force the catalogue height. |
| Trofeo Vision 11.3-inch LCD | 1920 × 400 | Official product exists; its exact protocol/profile is not established by the submitted report. |
| Peerless Vision 360 UB | 3.95-inch, 480 × 480 | LOU has a physically validated 480 × 480 ChiZhu device. This is not validation of every Peerless revision. |
| Grand / Hydro / Hyper Vision | Common 480 × 480 software profiles | Community driver mappings group these with Peerless; confirm the device's negotiated profile. |
| Wonder Vision 360 UB | 6.67-inch, 2400 × 1080 | InfoPanel distinguishes panel geometry from a 1600 × 720 render size. Requires a profile with separate render dimensions. |
| Frozen Warframe / SE / Pro, Elite, AS120 / BA120 Vision | Multiple small rectangular and square formats | Public implementations include 240 × 240, 240 × 320, 320 × 240 and 320 × 320. Names alone cannot choose orientation, codec or transport. |
| Fan Frame Vision-1 | 3.69-inch, 640 × 172 | Official accessory; exact USB profile remains to be established for LOU. |
| GPU Bracket LCD 4.58 | 320 × 960 | Official accessory; do not infer its transport from the resolution. |

Official sources: [Trofeo AIO](https://www.thermalright.com/product/trofeo-vision-360-argb-black/), [LCD accessory catalogue](https://www.thermalright.com/tw/product-category/products/accessories/lcd-screen/), [Peerless Vision](https://www.thermalright.com/product/peerless-vision-360-ub-argb-black/), [Wonder Vision](https://www.thermalright.com/product/wonder-vision-360-ub-argb-black/). Software mappings: [InfoPanel model database, pinned revision](https://github.com/habibrehmansg/infopanel/blob/8a3062d7fbcee3381c2fd01e1a3a62cac5756c42/InfoPanel/ThermalrightPanel/ThermalrightPanelModelDatabase.cs). A database entry is weaker evidence than a successful physical test.

## Connection and communication families

These screens receive application-generated images over USB. For the families below, they are not ordinary HDMI/DisplayPort Windows desktop monitors. A Type-C connector describes the physical connector, not the protocol. The supplied Trofeo enumerates at USB high speed; its PWM and ARGB connections are separate from this display channel.

| USB identity | Communication family | Windows integration to implement / retain |
|---|---|---|
| `0416:5408` | LY bulk | Existing WinUSB implementation; negotiate profile and timings. |
| `0416:5409` | LY1 variant | Separate initialization/framing review; not an alias blindly routed to LY. |
| `87AD:70DB` | ChiZhu bulk | Existing WinUSB implementation, with additional verified profiles as needed. |
| `0416:5302` | Nuvoton/Winbond HID type 2 | Native HID enumeration and report I/O on the correct interface; first implementation priority. |
| `0418:5303`, `0418:5304` | HID type 3 | Separate framing/codec path. |
| `0402:3922`, `0416:5406`, `87CD:70DB` | SCSI-family variants | Explicit SCSI/BOT transport work; not interchangeable with the current bulk frame protocol. |
| `0416:8001` | Digital segment/LED controllers | Not a general framebuffer LCD. Separate future capability, outside this LCD effort. |

The [TRCC Linux device inventory](https://github.com/Lexonight1/thermalright-trcc-linux/blob/278ab3010c46410d72790f87b08a22e914473966/README.md) and [Rejeb's implementation](https://github.com/rejeb/thermalright-lcd-control/tree/35b0b29001a31ca80b4a9daa7723029620a8ff6e) document these families. Some labels differ between projects, especially bulk versus SCSI-over-USB; actual descriptors and initialization responses must decide the transport.

TRCC's documented architecture renders content on the PC, passes frames to a transport helper, and sends encoded images with device-specific headers. JPEG and RGB565 variants exist. Video/GIF playback does not imply that the panel accepts the original video file. Rotation, dimensions, maximum payload, chunking, acknowledgements and pacing belong to the protocol profile. USB speed and advertised panel refresh rate do not establish achievable application FPS. See the [transport investigation](https://github.com/Lexonight1/thermalright-trcc-linux/blob/278ab3010c46410d72790f87b08a22e914473966/doc/PROTOCOL_USBLCDNEW.md).

## What the submitted Trofeo report already tells us

The display candidate has `0416:5302`, product token `USBDISPLAY`, bcdDevice `0407`, and no reported Windows device error. Revision `0407` is not proof of a particular resolution or firmware feature set.

- Composite parent: `usbccgp`.
- Interface 0: `hidusb`; interrupt OUT `0x02`, 512-byte maximum packet; interrupt IN `0x83`, 8-byte maximum packet.
- HID capabilities: usage page `0xFF06`, usage `1`, input report length 37, output report length 513, feature report length 9.
- Interface 1: `winusb`, but no endpoints listed for that interface in this report. Its presence does not make the HID frame stream a WinUSB bulk stream.

Windows HID report lengths include the report-ID slot. The 513-byte report and 512-byte USB packet are not contradictory, nor are a 37-byte logical input report and 8-byte endpoint packet. Streaming must use the actual HID capabilities and supported report IDs. Microsoft documents [HID report writes](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/sending-hid-reports); use native HID access rather than replacing the user's driver.

A [public Trofeo 6.86-inch report](https://github.com/Lexonight1/thermalright-trcc-linux/issues/139) records the same USB identity and endpoint pair, PM 128 / SUB 1, and a 1280 × 480 JPEG profile. This is strong evidence for the intended implementation, not a handshake captured from our reporter's unit. That issue also contains unresolved visual instability; it does not establish universal reliability.

A [separate two-device report](https://github.com/Lexonight1/thermalright-trcc-linux/issues/128) demonstrates that a Trofeo and a Frozen Warframe can share `0416:5302`. Enumerate and open the exact device instance, not the first matching VID/PID.

The older transport notes and current profile code disagree on some handshake byte labels. Account for the report-ID prefix explicitly and compare the framing against actual response fixtures before implementing a parser. Do not interpret a byte-offset disagreement by trying arbitrary commands on the customer's device.

The other candidates in the supplied log (`381C:0001`, `10F5:2211`) remain unidentified as displays. The latter includes audio interfaces. Neither is authorized as an LCD target merely because it was included in an inventory.

## Implementation and validation plan with minimal tester burden

1. Add a capability-based discovery record: physical instance, interfaces, driver, report sizes, transport family, negotiated model/profile, render size and orientation. Keep unknown devices visible as unsupported rather than opening them.
2. Implement native HID type 2 for the Trofeo first. Preserve working LY and ChiZhu paths and the single-writer mutex. Do not require a replacement driver on this reported configuration.
3. Build deterministic tests for report-ID handling, partial input, malformed/short replies, bounded sizes, timeouts, disconnects, duplicate VID/PIDs, JPEG framing and unsupported profiles. Use synthetic or anonymized protocol fixtures; do not publish serial bytes from third-party logs.
4. Render every page at 1280 × 480 locally. Keep the USB worker independent of the UI and use bounded latest-frame buffering. Start with conservative pacing; measure completed transfers rather than promising 30/60 FPS from USB bandwidth alone.
5. Prepare one consolidated candidate that identifies the device, renders its normal pages and records bounded transport outcomes. A single normal-use check should answer whether it displays correctly and stably. Further contact should be driven by a specific remaining failure, not a succession of guessed profiles.

HID support is **not implemented by this research change**. SCSI, HID type 3 and unknown accessory mappings remain later work. Existing render tests and public source evidence cannot certify unseen hardware.

Research source provenance is retained separately from original implementation. No third-party protocol implementation was copied into the application in this change. Existing licensing review requirements remain applicable; see `LICENSING_REVIEW.md`.

## RX 9060 XT: verified LOU defects versus unconfirmed device cause

The available customer report concerns displays, not GPU telemetry. There is no RX 9060 XT sensor dump or driver result here, so the exact cause on that PC is not established. LOU uses LibreHardwareMonitor 0.9.6 and AMD's read-only ADL APIs; there is no per-model 9070-only temperature table.

The code review nevertheless found and corrected three concrete defects:

| Previous behavior | Local correction |
|---|---|
| A GPU fan assignment from LibreHardwareMonitor was passed as an AMD ADL adapter identifier, preventing ADL selection. | Only an AMD ADL assignment selects an ADL controller. |
| A missing optional VRAM API could throw after successful temperature collection and discard the whole result. | Isolate that optional query; retain successfully collected readings. |
| A missing LibreHardwareMonitor temperature overwrote a valid current ADL value. | Preserve the current value only when the full physical PnP identity matches every row of the selected GPU. |

Do not merge cards by model name, vendor/device pair, sensor order or measured value. Multiple GPUs still require selection; an explicit invalid temperature assignment still returns unavailable. These rules prevent an integrated GPU's temperature appearing as the discrete GPU's temperature.

AMD documents [PMLog](https://gpuopen-librariesandsdks.github.io/adl/overdrive8_8h.html) and [OverdriveN temperature queries](https://gpuopen-librariesandsdks.github.io/adl/group__OVERDRIVENAPI.html). [LibreHardwareMonitor's AMD reader](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/c49fd12e4cb4d9e739a32c2d91ad60850b4cac8e/LibreHardwareMonitorLib/Hardware/Gpu/AmdGpu.cs) uses multiple available API paths and exposes the PnP identity. An additional API fallback is a possible later improvement, not something proven necessary specifically for this 9060 XT.

Validation: all 191 application tests passed, including 9 new telemetry regression cases; self-contained Windows x64 publication succeeded. The published executable exited successfully in offline export mode and generated 40 preview images (demo/empty data across the existing four export geometries). This is a packaging/rendering smoke test, not a GPU measurement or a 1280 × 480 physical test. No fan-control API, LCD command, driver installation or personal setting was changed by these fixes. The application fixes remain local and are not yet a published release or a verified RX 9060 XT hardware result.
