# ASRock GPU lighting and LCD — 2.2.3 investigation

Status: research started on 18 October 2026. No GPU lighting or LCD write has been performed by this work. This is not a compatibility claim or a release announcement.

## Confirmed observations

- The reference GPU exposes AMD PCI device `1002:7550`, with ASRock subsystem `1849:5415`. Retain its complete physical identity locally when routing commands; the model name alone must not select an adapter.
- ASRock documents an LCD Information Center and Polychrome lighting for the **RX 9070 XT Taichi White**. Its manual gives a maximum media size of **320 × 170**, static-image limits of 450 KB and GIF limits of 1.4 MB. These are documented media limits, not yet a measured raw-frame transport or refresh rate.
- The installed ASRock package contains an x64 `vgalcd.dll` with `Initialize`, `QueryInterface` and `Uninitialize` exports, plus `vgalcdsvc.exe`. Export names alone do not establish function signatures, interface IDs, ownership rules or transport capabilities.
- A separate x86 `LedCtrl.dll` exposes lighting-related entry points. It cannot be loaded directly into the x64 LOU process. No vendor DLL has been loaded or invoked by this investigation.
- Polychrome was running during the read-only inspection. Any eventual hardware-control test must ensure exclusive ownership of the corresponding lighting/LCD resource.
- LOU's bundled engine remains the pinned, unmodified OpenRGB 1.0 revision `81bbe18`. Previous physical inventory did not expose this GPU as an RGB device.

## RGB route

Upstream issue reports describe an ASRock GPU controller at I2C address `0x36`. A separate Taichi controller experiment exists, but its author labels it a hardware test and its installer targets Linux. Steel Legend success is not evidence of Taichi White support; channel maps and subsystem IDs differ. Reports also mention flashing during rapid writes.

Prefer extending or updating the **separate OpenRGB engine** once a Windows AMD ADL path and this exact board are validated. LOU can retain its existing SDK connection, physical assignments and GPU group. Do not import GPL controller code into LOU's differently licensed assemblies. Any modified engine needs its own corresponding source and notices. No experimental engine or vendor DLL is currently bundled.

First hardware milestone: exact-adapter discovery and controller identification, then a reversible static-color test. Only after physical confirmation should native effects and animation rate be evaluated. Do not infer success from cached RGB state or blindly send commands to every GPU bus.

## LCD route

Investigate the documented ASRock workflow and the installed component's actual ABI/IPC before selecting a transport. Do not guess a function prototype from an export name or reuse Thermalright packets for this device. The exposed interface may support media upload rather than continuous framebuffer streaming; that remains unknown.

Once identified, add a separate GPU-display provider with its own capability profile and physical-adapter identity. A 320 × 170 rendering target must be supported deliberately: current general settings reject heights below 172. Test the geometry and content budget before enabling output. The existing Thermalright schedulers, transport locks and profiles must remain intact.

The first display milestone is a single reversible test image, followed by GPU temperature/load/fan data if the transport supports refreshing it. No boot-logo programming, firmware update, persistent flash loop, voltage changes or cooling control belongs in this integration.

## Sources

- [ASRock Taichi White product specifications](https://www.asrock.com/Graphics-Card/AMD/Radeon%20RX%209070%20XT%20Taichi%20White%2016GB%20OC/)
- [ASRock LCD Information Center manual](https://download.asrock.com/Manual/LCD%20Information%20Center_User%20Manual.pdf), media limits on pages 14–15.
- [OpenRGB ASRock GPU proposal #5225](https://gitlab.com/CalcProgrammer1/OpenRGB/-/work_items/5225) and [flashing report #5226](https://gitlab.com/CalcProgrammer1/OpenRGB/-/work_items/5226).
- [Experimental Taichi OpenRGB controller](https://github.com/VirulentArc/openrgb-asrock-rx9070xt-taichi-controller), Linux test package; not installed or executed.
- Local read-only PE export inspection and Windows PnP inventory. Raw local paths, user configuration and vendor binaries are excluded from this repository.
