# USB protocols and provenance

References consulted for interoperability, without executing their applications or redistributing Thermalright firmware:

| Source | Revision |
|---|---|
| https://github.com/Lexonight1/thermalright-trcc-linux | a73e510d48c8f3ba14421705c0e569410608cf0e |
| https://github.com/Abigor-DIY/Open-Trofeo-LCD | 9ecbf337f6c7e2fa5d0a67cb62022b454bdb115e |
| https://github.com/0iroN/trvision | bc88670ac6b00a569d7228b083ef4badae987567 |
| https://github.com/rejeb/thermalright-lcd-control | 35b0b29001a31ca80b4a9daa7723029620a8ff6e |

LY (0416:5408): 2,048-byte handshake, validated magic, geometry from PM/SUB and timing when present. JPEG under 450,000 bytes, 496-byte fragments in 512-byte blocks, four-block padding, 4096/2048-byte bursts, ACK after each frame.

ChiZhu (87AD:70DB): 64-byte handshake, PM at offset 24, SUB at 36, 64-byte image header with dimensions/format and size at offset 60. 16 KiB transfers and an empty packet when USB-aligned. Some square displays require continuous transmission.

The paragraphs above describe the original stable transports. The current development candidate adds native HID type 2, LY1 and ALi/WinUSB, plus documented RGB565 profiles. See the [current compatibility matrix, protocol details and additional pinned sources](USB_COMPATIBILITY.md). Unknown models and inconsistent geometry remain rejected. No driver replacement, firmware commands, permanent-logo writes or fan controls are sent.

The initial profile is detected automatically; users can check the test pattern and select orientation. Reconnection requires the same PnP identity and native geometry. Both families use independent sessions, with serialized transfers per device.

JPEG profiles in the protocol table can be decoded, but only 1920 × 462 and 480 × 480 have been observed on reference hardware. Other variants require dedicated physical validation. Closing/reopening a handle does not prove recovery after unplugging a cable or resuming from sleep.
