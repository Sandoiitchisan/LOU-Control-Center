# LOU Diagnostics

The new standalone diagnostic source in `src/Lou.Diagnostics` and its dedicated tests use the PolyForm Strict license supplied in those directories, as selected by the project owner. This does not relicense existing LOU Control Center releases or third-party software. The release's notices file retains the bundled .NET runtime licenses.

A standalone Windows x64 tool for investigating internal/USB screen compatibility. It does not require LOU Control Center or a separately installed .NET runtime.

[Download LOU Diagnostics 1.0.0](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/diagnostics-v1.0.0/LOU-Diagnostics.exe) · [Release and checksums](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/tag/diagnostics-v1.0.0) · [Versioned source](https://github.com/Sandoiitchisan/LOU-Control-Center/tree/diagnostics-v1.0.0/src/Lou.Diagnostics)

1. Run **LOU-Diagnostics.exe**. Collection starts automatically, without administrator permission.
2. Choose where to save the `.txt` report. Cancelling the save dialog keeps the report visible and lets you save later.
3. Read the exact report in the application.
4. Click **Create a GitHub issue**, describe the screen model and problem, then drag the report into GitHub's issue editor or use its attachment button. Posting requires a GitHub account; nothing is uploaded automatically.

The collection makes no network requests. Only an explicit click on the GitHub button opens a browser. The issue URL contains a generic template, never the report or its saved path.

## Scope and limitations

Windows PnP properties and standard USB hub descriptor queries supply device VID/PID, declared revisions, driver versions, problem codes, interface/endpoint descriptions and USB speed where accessible. HID queries retrieve capabilities only, never input events or report contents. No WinUSB session is initialized and no proprietary handshake, LCD image, firmware command, reset or configuration change is sent.

Desktop monitors and recognized RGB, input, storage, network and audio devices are excluded. Generic proprietary USB/HID devices may be screens but cannot always be classified passively; these are explicitly labeled **unconfirmed candidates**. Unknown screens are not filtered solely by LOU's two existing USB families. Conversely, not every screen can be discovered: devices hidden behind GPU, network or proprietary controllers may be absent. No automatic compatibility claim is made from a product name or USB ID.

Native resolution, orientation and firmware version are not inferred. USB `bcdDevice` is a device-declared revision, not a verified firmware version. LOU's model-specific handshake and physical test patterns remain separate developer-guided tests.

No RGB inventory, sensor readings, personal LOU settings, application/process inventory, serial numbers or raw instance paths are exported. Product text is reduced to allowlisted public product/family tokens; arbitrary device names are not exported. Physical relationships use report-local aliases, not persistent fingerprints. The commercial model can be supplied manually in the GitHub issue.

## Implementation and checks

The diagnostic is independent of LOU's LCD/RGB runtime and has no NuGet runtime dependencies. An unprivileged child process performs bounded collection. It checkpoints only sanitized text in a random temporary file; a 30-second worker deadline and 35-second UI timeout preserve partial output if a driver stalls. Temporary files are removed during normal shutdown. A crash can leave only the already-sanitized checkpoint, not a raw inventory.

No installation, service, startup task or elevation is performed. Single-file .NET deployment may extract its bundled runtime libraries into the standard per-user .NET temporary cache.

Build and run the dedicated tests with:

```powershell
./scripts/build-diagnostics.ps1 -DotNetPath dotnet
```

Output: `artifacts/LOU-Diagnostics.exe` and its SHA-256 checksum. The project also provides `--collect <report.txt>` for headless developer validation, and `--review-ui <preview.png>` for an application-owned UI capture without a save dialog or browser action.

Windows reference documentation: [USBView](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/usbview), [USB descriptor requests](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_descriptor_request), [HID capabilities](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidpi/ns-hidpi-_hidp_caps). GitHub supports [text attachments in issues](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files).
