# Dépendances et attributions

Le code de LOU Control Center est livré sous GPL-3.0 (voir LICENSE), choix compatible avec les composants de protocole informés par les implémentations GPL consultées. La documentation de recherche référence les révisions exactes. Aucun binaire TRCC ou firmware constructeur n'est redistribué.

| Élément | Version / source | Licence / usage |
|---|---|---|
| .NET / WPF |10.0, runtime autonome x64 | MIT et notices incluses par Microsoft dans la distribution |
| SkiaSharp et NativeAssets.Win32 |3.119.1 | MIT ; Skia inclut ses propres notices BSD et tierces |
| System.Management |10.0.12 | MIT, .NET Foundation |
| System.Diagnostics.PerformanceCounter |10.0.12 | MIT, .NET Foundation |
| Microsoft.Windows.SDK.NET.Ref | référence de build Windows19041 | package Microsoft ; références et runtime WinRT |
| xUnit |2.9.3 | Apache-2.0 ; test seulement |
| Microsoft.NET.Test.Sdk |18.10.1 | MIT ; test seulement |
| xunit.runner.visualstudio |3.1.5 | Apache-2.0 ; test seulement |
| TRCC Linux, Lexonight1 et contributeurs | révision dans docs/USB.md | GPL-3.0 ; protocoles LY/ChiZhu et profils consultés |
| Open-Trofeo-LCD, Abigor-DIY | révision dans docs/USB.md | GPL-3.0 ; observations des captures et transport consultés |
| trvision, Osman Demir | révision dans docs/USB.md | MIT ; protocole comparé, code Python non embarqué |
| thermalright-lcd-control, rejeb | révision dans docs/USB.md | Apache-2.0 ; transport et tests comparés |
| LibreHardwareMonitor et contributeurs | bibliothèque0.9.6, commit3d331e3370efb858411f19511373eff65a218701 | MPL-2.0 ; bibliothèque non modifiée incluse, CPU/carte mère/GPU en lecture seule dans le collecteur optionnel |
| HidSharp |2.6.4 | Apache-2.0, licence incluse ; dépendance de LibreHardwareMonitor |
| DiskInfoToolkit / RAMSPDToolkit-NDD |1.1.2 /1.4.2 | MPL-2.0 ; dépendances non modifiées de LibreHardwareMonitor, stockage et mémoire SPD non activés |
| Mono.Posix.NETStandard |1.0.0 | MIT ; dépendance de LibreHardwareMonitor, non utilisée pour les capteurs Windows |
| PawnIO |2.2.0, redistribuable inclus | installateur officiel signé namazso.eu, original non modifié ; permission explicite de redistribution dans le binaire ; inclus dans le Setup ; facultatif dans le portable. Pilote GPL-2.0-or-later avec exception |
| AMD ADL | DLL atiadlxx.dll déjà installée avec le pilote AMD | API publique, appels de lecture ; aucun SDK ou driver AMD redistribué |
| PresentMon console Intel |2.6.0 x64 | MIT, binaire officiel signé Intel, licence et provenance dans tools/PresentMon ; aucun driver Intel ; intégré au collecteur système en mode installé |
| Open-Meteo | API forecast et geocoding / GeoNames | données CC BY4.0, attribution à l'écran ; API gratuite non commerciale |
| Illustrations | imagegen intégré | nouvelles créations pour ce projet ; détails dans docs/ASSETS.md |
| Bahnschrift / Segoe UI | polices Windows installées | non redistribuées |

Sources officielles : [support .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [AMD ADL](https://gpuopen-librariesandsdks.github.io/adl/), [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [PresentMon](https://github.com/GameTechDev/PresentMon), [Open-Meteo](https://open-meteo.com/en/docs), [tarification et usage](https://open-meteo.com/en/pricing).

Les textes de licence utiles sont copiés dans `licenses` et dans le package. Pour redistribuer les exécutables GPL, fournir le code source correspondant et ces notices. Les configurations privées, clés, journaux personnels et dépôts de recherche complets sont exclus du package portable.

Sources des bibliothèques MPL non modifiées : https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6 ; https://github.com/Blacktempel/DiskInfoToolkit ; https://github.com/Blacktempel/RAMSPDToolkit. Licence MPL et notices tierces incluses dans licenses. Installation PawnIO facultative : https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0 .

Inno Setup 6 (compilateur de distribution) : https://jrsoftware.org/isinfo.php ; licence Inno Setup dans `licenses/InnoSetup-LICENSE.txt`. Le compilateur n’est pas requis à l’exécution et n’est pas inclus dans le dépôt.
