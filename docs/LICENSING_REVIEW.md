# Licensing transition review — release gate

**17 October 2026 status:** the owner has published the 2.2 RC1 portable archive. Its corresponding source and this review are being made available together. This records the publication; it does not resolve the provenance questions below or certify the restrictive licensing of the whole application. Earlier releases and third-party rights remain unchanged. Complete this review before treating the intended transition as cleared for subsequent releases.

The owner requests free permitted use of original LOU code, with commercial exploitation, derivatives and redistribution subject to a separate agreement. The unmodified PolyForm Strict text has been prepared in LICENSE. It is not an attempt to impose these restrictions on previous GPL grants or on third-party software.

## Evidence

- Published releases through 2.1.1 used GPL-3.0. Those releases and their commercial-use permissions remain intact.
- Original C# application projects reference MPL/permissive libraries. No TRCC binary or manufacturer firmware is packaged.
- `src/Lou.Devices/Protocols.cs` explicitly records that its interoperability implementation was informed by TRCC Linux (GPL-3.0). `docs/USB.md` also records Open-Trofeo-LCD (GPL-3.0), trvision (MIT), and thermalright-lcd-control (Apache-2.0). Earlier third-party notices deliberately selected GPL compatibility for this history.
- Reviewing dependency declarations and source headers does not by itself establish whether an implementation is an independent expression of protocol facts or a derivative of the consulted GPL implementation. A syntax rewrite or moving code across files would not resolve a derivative-work obligation.
- OpenRGB remains an unmodified, separate GPL-2.0-or-later program using its public TCP SDK. Its license and corresponding source are retained. LOU claims no exclusivity over it.

## Required before public restrictive-license distribution

Establish and document rights to relicense the original application, especially the USB implementation. If GPL-derived material is present, obtain the relevant permission or replace it through an independently sourced implementation with a defensible provenance record. A process boundary alone is not a blanket GPL exception. Review any third-party contributions before relicensing them.

The technical review is not a legal clearance. The prepared license must not be described as a guarantee that all existing code can be commercially restricted. A qualified licensing review is appropriate before treating the transition as complete or negotiating commercial terms. Physical compatibility claims remain limited to documented tests.

Primary references: https://www.gnu.org/licenses/gpl-faq.en.html ; https://polyformproject.org/licenses/strict/1.0.0 ; https://eur-lex.europa.eu/legal-content/EN/SUM/?uri=celex%3A62010CJ0406 . The protocol-functionality distinction does not permit copying protected expression.
