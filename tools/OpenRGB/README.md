# OpenRGB engine bundled with LOU

Unmodified OpenRGB 1.0, Windows x64, revision `81bbe18`, downloaded from the official release on 13 October 2026.

- Binary: https://codeberg.org/OpenRGB/OpenRGB/releases/download/release_1.0/OpenRGB_1.0_Windows_64_81bbe18.zip
- Corresponding OpenRGB source archive is included as `OpenRGB-1.0-81bbe18-source.zip`.
- Upstream source: https://codeberg.org/OpenRGB/OpenRGB/src/commit/81bbe18
- License: GPL-2.0-or-later. OpenRGB and its components retain their own licenses, independently of LOU.
- Protocol: OpenRGB SDK 6. LOU communicates through TCP on loopback only, port 16742. An existing external server may be used on localhost:6742.
- Qt 6.8.3 is supplied as replaceable shared DLLs with the official OpenRGB build; Qt source: https://download.qt.io/archive/qt/6.8/6.8.3/submodules/ (qtbase, qt5compat, qtsvg as applicable). Qt licensing: https://doc.qt.io/qt-6/licensing.html . LOU does not link to Qt.
- libusb source and notices: https://github.com/libusb/libusb ; hidapi-hotplug: https://codeberg.org/OpenRGB/hidapi . Microsoft DirectX shader libraries: https://github.com/microsoft/DirectXShaderCompiler . PawnIO user library and modules: https://github.com/namazso/PawnIO . These are upstream dependencies of the unmodified engine.

`SHA256.json` pins all engine executable/runtime files. LOU embeds that manifest and checks the engine before launch. No plugin, user configuration, automatic profile or hardware identifiers are bundled. This binary is not a firmware flasher and LOU exposes no fan-control API.

Redistributors must preserve the upstream licenses and provide corresponding sources under the relevant terms. The official project is not affiliated with LOU.
