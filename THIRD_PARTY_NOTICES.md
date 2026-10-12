# Dependencies and attributions

LOU Control Center is distributed under GPL-3.0 (see LICENSE), compatible with protocol components informed by the GPL implementations consulted. Research documentation records exact revisions. No TRCC binaries or manufacturer firmware are redistributed.

| Component | Version / source | License / use |
|---|---|---|
| .NET / WPF | 10.0, self-contained x64 runtime | MIT and Microsoft distribution notices |
| SkiaSharp and NativeAssets.Win32 | 3.119.1 | MIT; Skia includes BSD and third-party notices |
| System.Management | 10.0.12 | MIT, .NET Foundation |
| System.Diagnostics.PerformanceCounter | 10.0.12 | MIT, .NET Foundation |
| Microsoft.Windows.SDK.NET.Ref | Windows 19041 build reference | Microsoft package; references and WinRT runtime |
| xUnit | 2.9.3 | Apache-2.0; tests only |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT; tests only |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0; tests only |
| TRCC Linux, Lexonight1 and contributors | Revision in docs/USB.md | GPL-3.0; LY/ChiZhu protocols and profiles consulted |
| Open-Trofeo-LCD, Abigor-DIY | Revision in docs/USB.md | GPL-3.0; capture observations and transport consulted |
| trvision, Osman Demir | Revision in docs/USB.md | MIT; protocol compared, Python code not bundled |
| thermalright-lcd-control, rejeb | Revision in docs/USB.md | Apache-2.0; transport and tests compared |
| LibreHardwareMonitor and contributors | 0.9.6, commit 3d331e3370efb858411f19511373eff65a218701 | MPL-2.0; unmodified library, read-only CPU/motherboard/GPU collection |
| HidSharp | 2.6.4 | Apache-2.0, license included; LibreHardwareMonitor dependency |
| DiskInfoToolkit / RAMSPDToolkit-NDD | 1.1.2 / 1.4.2 | MPL-2.0; unmodified dependencies; storage and SPD monitoring not enabled |
| Mono.Posix.NETStandard | 1.0.0 | MIT; LibreHardwareMonitor dependency, unused for Windows sensors |
| PawnIO | 2.2.0, redistributable included | Official unmodified installer signed by namazso.eu, explicit redistribution permission in binary; bundled in Setup, optional for portable. GPL-2.0-or-later driver with exception |
| AMD ADL | atiadlxx.dll from the installed AMD driver | Public read APIs; no AMD SDK or driver redistributed |
| Intel PresentMon console | 2.6.0 x64 | MIT; official Intel-signed binary, provenance/license in tools/PresentMon; no Intel driver; integrated with installed collector |
| Open-Meteo | Forecast and geocoding / GeoNames | CC BY 4.0 data, on-screen attribution; free non-commercial API |
| Illustrations | Built-in imagegen | Created for this project; see docs/ASSETS.md |
| Bahnschrift / Segoe UI | Installed Windows fonts | Not redistributed |

Official sources: [.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [AMD ADL](https://gpuopen-librariesandsdks.github.io/adl/), [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [PresentMon](https://github.com/GameTechDev/PresentMon), [Open-Meteo](https://open-meteo.com/en/docs), [API pricing and usage](https://open-meteo.com/en/pricing).

License texts are copied into licenses and the package. Redistributing GPL executables requires corresponding source and notices. Private configurations, keys, personal logs and complete research repositories are excluded from the portable package.

Unmodified MPL library sources: [LibreHardwareMonitor v0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6), [DiskInfoToolkit](https://github.com/Blacktempel/DiskInfoToolkit), [RAMSPDToolkit](https://github.com/Blacktempel/RAMSPDToolkit). MPL and third-party notices are included in licenses. [PawnIO 2.2.0 installer](https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0).

[Inno Setup 6](https://jrsoftware.org/isinfo.php) builds the distribution. Its license is in licenses/InnoSetup-LICENSE.txt. The compiler is not needed at runtime and is not included in the repository. Original third-party license texts are preserved without translation.
