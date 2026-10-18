# Licensing of LOU Control Center

**2.2-series publication status, 18 October 2026:** RC1, stable 2.2.0 and stable 2.2.1 have been published with corresponding sources. The protocol-provenance review in `docs/LICENSING_REVIEW.md` is **not resolved**. Publication or stable promotion is not evidence that all preexisting material can be relicensed. The terms below describe the owner's intended grant only to the extent of rights actually held; they do not override third-party or earlier GPL grants. The public 2.1.1 release has not been relicensed.

## Original LOU material, starting with 2.2.0

Licensor: Sandoiitchisan, the project owner at https://github.com/Sandoiitchisan/LOU-Control-Center.

The original LOU code is offered under the unmodified **PolyForm Strict License 1.0.0** in `LICENSE`, to the extent the licensor holds the relevant rights. This is **source-available software**, not an OSI open-source license. Reading the source and using the application for permitted noncommercial purposes is allowed. The license does not grant permission to distribute the software, modify it, create derivatives, or exploit it commercially. The exact license controls, including its permitted organizational uses and statutory-rights provisions.

For commercial use, redistribution, derivative versions or other uses outside that license, obtain a **separate written agreement** from the project owner before proceeding. Commercial terms, including any fee or revenue share, must be agreed explicitly; this license does not create an automatic royalty rate. Contact the owner through the GitHub profile linked above. No trademark license or endorsement of third-party products is implied.

## Previously published releases

LOU 2.1.1 and earlier releases were published under GPL-3.0. Their existing grants cannot be revoked by this change. Their source, tags and release artifacts remain under their original terms, including the GPL's permission for commercial use when its conditions are met. This change does not claim exclusive rights over copies of those releases or erase third-party rights. The historical GPL text is retained in `licenses/LOU-legacy-GPL-3.0.txt`.

## Third-party components

This change **does not relicense third-party software**. `tools`, `licenses`, dependency assemblies and their corresponding sources retain their respective upstream licenses and notices. In particular:

- OpenRGB runs as a separate, unmodified executable and communicates through its standard localhost SDK. It remains GPL-2.0-or-later; its source archive and license are included. LOU's restrictions do not apply to OpenRGB itself.
- LibreHardwareMonitor and its MPL components remain MPL-2.0; their files are unmodified.
- PawnIO, PresentMon, Qt, .NET, SkiaSharp and other dependencies retain their own terms.
- Protocol documentation and upstream research references do not become the property of LOU. See `docs/USB.md` and `THIRD_PARTY_NOTICES.md`.

## Contributions

Bug reports and compatibility observations are welcome. Before modifying or submitting a patch, request permission and agree the contribution's licensing with the maintainer. No assignment of a contributor's copyright is implied. Do not add GPL-derived code to a restrictively licensed component without first resolving compatibility. Do not remove existing copyright notices.
