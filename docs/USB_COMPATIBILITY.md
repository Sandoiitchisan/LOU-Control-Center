# Display compatibility — 2.2.0, 17 October 2026

This describes [stable 2.2.0](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/tag/v2.2.0), promoted from RC1 without changing the display implementation. Users reported positive results with additional screens, but exact models/revisions were not supplied in the promotion feedback. Implemented protocol support is not proof of physical compatibility with every product sold under a family name; the per-model validation limits below remain applicable.

## Transport coverage

| USB ID | Candidate behavior | Validation |
|---|---|---|
| `0416:5408` | LY / WinUSB, JPEG profiles | 1920 × 462 previously confirmed on reference hardware; framing regression tests retained. |
| `87AD:70DB` | ChiZhu / WinUSB, JPEG and documented RGB565 profiles | 480 × 480 previously confirmed; additional profiles checked offline only. |
| `0416:5302` | Native Windows HID type 2, JPEG / RGB565 | Trofeo 6.86-inch implementation based on the supplied descriptors and published PM 128 / SUB 1 observations; needs one physical confirmation. |
| `0416:5409` | LY1 / WinUSB, separate PM formula, command and packet padding | Offline tests; no physical confirmation. |
| `0416:5406` with WinUSB | ALi F5, RGB565, per-frame acknowledgement | Offline tests; no physical confirmation. A USBSTOR variant is not opened. |
| `0418:5303`, `0418:5304` | Discovered, disabled with an unsupported-transport explanation | HID type 3 transport not implemented. |
| `0402:3922`, `87CD:70DB` | Discovered, disabled with an unsupported-transport explanation | SCSI transport not implemented. |

Unlisted IDs, general monitors and digital segment controllers are not sent LCD commands. No replacement USB driver, firmware upload, boot-logo programming or fan control is performed. Known-but-unimplemented devices are not silently treated as LY/ChiZhu screens.

## Identity and formats

- USB IDs identify a transport family, **not a resolution**. PM/SUB from the device reply select geometry, codec, byte order and initial orientation. Unknown or malformed profiles fail explicitly; there is no guessed 480 × 480 fallback.
- Native HID interfaces are enumerated with SetupAPI, following their PnP ancestors to the exact physical USB instance. Two screens sharing the same VID/PID remain independent. WinUSB registry paths also require the full instance, not a serial prefix.
- HID type 2 requires exactly one matching interface with 513-byte output reports and 21–513-byte input reports. Windows report ID zero is prepended/removed exactly once. The submitted Trofeo's 37-byte input / 513-byte output report sizes fit this path. Short-reply/self-streaming firmware variants are deliberately not inferred from a revision number.
- Type 2 uses a 512-byte identification request and 512-byte frame payload chunks; each Windows output report is 513 bytes including its ID. JPEG header dimensions follow the negotiated raster. RGB565's header uses the documented fixed 240 × 320 fields, while its payload length follows the actual raster. This convention is not a guess about the panel's resolution.
- LY1 uses `49 + reply[20]`, the raw SUB byte, command 2 and unpadded chunk counts. LY retains its existing PM/SUB interpretation, command 1 and four-block padding. PM 50 uses a rotated 240 × 320 RGB565 raster, big-endian. LY timing contradictions remain blocked for review.
- ChiZhu supports documented square, rectangular and panoramic PM variants, including PM 1/SUB 48 and 49. PM 32 and 50 use RGB565. PM 50's raw raster is portrait with a logical 320 × 240 protocol header. Existing confirmed 480 × 480 PM 4 behavior is unchanged.
- ALi F5 accepts only the documented `36`, `65` and `66` hexadecimal identity bytes and an exact RGB565 payload length. It does not probe disk devices.
- JPEG payloads must stay below 450,000 bytes. Raw payloads must match the negotiated raster exactly. I/O calls have timeouts; a frame has a bounded transfer budget. Failed transfers do not increment the sent-frame counter. HID type 2 has no invented per-frame ACK.
- Saved profiles include codec and byte order. Older JSON profiles retain JPEG defaults. An identity, geometry or codec change stops reconnection and requests re-detection; user page intervals and saved orientation are preserved.

## Layouts and verification

All five pages have a landscape layout for aspect ratios from 1.6 up to 3.5 (including 1280 × 480 and 1600 × 720), in addition to compact and ultrawide layouts. Content uses the available width without stretching a square page. The cooling-only view remains available. User rotation is applied to the encoded raster, with shape checks before sending.

Automated coverage includes synthetic anonymized handshake fixtures, packet reconstruction around report boundaries, byte order, rotated raster corners, same-model device isolation, persistence, failed transfers and all five pages in French/English. Standalone `--export` renders 110 sample images per language across eleven geometries without opening any LCD transport. This verifies rendering and packaging, **not** USB timing, sleep/wake recovery or visual orientation on unowned hardware.

The current goal is one consolidated Trofeo candidate, not repeated speculative firmware probes. A successful write only confirms host-side transfer; it must not be reported as a successful physical display test.

## Sources and provenance

The [research record](THERMALRIGHT_COMPATIBILITY_RESEARCH.md) distinguishes official marketed resolutions from wire formats. Sources consulted for this implementation:

- [Thermalright Trofeo Vision 360 specifications](https://www.thermalright.com/product/trofeo-vision-360-argb-black/) — 6.86 inches, 1280 × 480.
- [TRCC Linux, pinned revision 278ab301](https://github.com/Lexonight1/thermalright-trcc-linux/tree/278ab3010c46410d72790f87b08a22e914473966) — `core/protocol.py`, `adapters/device/hid_lcd.py`, `ly_lcd.py`, `bulk_lcd.py` and `_f5.py`; GPL-3.0. The protocol implementation remains subject to the existing provenance review.
- [Trofeo PM 128 / SUB 1 observations](https://github.com/Lexonight1/thermalright-trcc-linux/issues/139) — corroborates the family, not a clean LOU hardware test.
- [InfoPanel model database](https://github.com/habibrehmansg/infopanel/blob/8a3062d7fbcee3381c2fd01e1a3a62cac5756c42/InfoPanel/ThermalrightPanel/ThermalrightPanelModelDatabase.cs) — product/render geometry comparison, GPL-3.0.
- [Microsoft HID report documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/sending-hid-reports) and [HidSharp 2.6.4](https://www.nuget.org/packages/HidSharp/2.6.4) — native Windows HID API access; HidSharp is Apache-2.0 and its license is included.

No Thermalright binary or firmware is bundled. Stable promotion does not clear the pending [licensing transition](LICENSING_REVIEW.md). Per-model physical validation remains necessary beyond the documented evidence.
