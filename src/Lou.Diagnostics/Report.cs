using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lou.Diagnostics;

public sealed record InterfaceInfo(int Number, int Alternate, int Class, int Subclass, int Protocol);
public sealed record EndpointInfo(int Interface, int Address, int TransferType, int PacketSize, int Interval);
public sealed record InterfaceDriverInfo(string Interface, string Service, string Version, uint WindowsProblemCode);
public sealed class DisplayInfo
{
    public string Alias { get; set; } = "";
    public string Parent { get; set; } = "";
    public string UsbId { get; set; } = "";
    public string Revision { get; set; } = "";
    public string Product { get; set; } = "";
    public string Classification { get; set; } = "";
    public string LouSupport { get; set; } = "";
    public string DriverService { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public uint WindowsProblemCode { get; set; }
    public string UsbSpeed { get; set; } = "Not measured";
    public List<InterfaceInfo> Interfaces { get; set; } = [];
    public List<EndpointInfo> Endpoints { get; set; } = [];
    public List<InterfaceDriverInfo> InterfaceDrivers { get; set; } = [];
    public List<string> HidCapabilities { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}
public sealed class DisplayReport
{
    public int Schema { get; set; } = 1;
    public string ToolVersion { get; set; } = "1.0.0";
    public string RecognitionRules { get; set; } = "LOU USB families LY/ChiZhu; diagnostics catalog 1";
    public string WindowsVersion { get; set; } = Environment.OSVersion.Version.ToString();
    public string Architecture { get; set; } = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
    public bool Complete { get; set; }
    public List<DisplayInfo> Devices { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public string Text()
    {
        var b = new StringBuilder("LOU Diagnostics — Display report / Rapport écrans\n\n");
        b.AppendLine("LOCAL ONLY / LOCAL UNIQUEMENT — no upload, no network request by this tool.");
        b.AppendLine("No monitor inventory, RGB scan, sensor readings, firmware commands or LCD image transfers.");
        b.AppendLine("Serial numbers, device paths, usernames and machine names are not exported. Aliases are local to this report.");
        b.AppendLine("Aucun numéro de série, chemin personnel, nom de compte ou nom du PC n’est exporté. Les alias ne valent que pour ce rapport.");
        b.AppendLine("A generic candidate is NOT proof of a display. A known USB family is NOT proof of model compatibility.");
        b.AppendLine("Native resolution, orientation and firmware version: not queried (no proprietary handshake).");
        b.AppendLine("USB revision fields are device-declared revisions, not verified firmware versions.");
        b.AppendLine($"\nCollection / Collecte : {(Complete ? "complete / terminée" : "PARTIAL / PARTIELLE")} — {Devices.Count} screen(s) or candidate(s) / écran(s) ou candidat(s).");
        b.AppendLine("Un candidat générique n’est pas forcément un écran. Une famille USB connue ne garantit pas la compatibilité du modèle.");
        foreach (var d in Devices) b.AppendLine($"  {d.Alias} · {d.UsbId} · {d.Product} · {d.Classification}");
        if (Devices.Count == 0) b.AppendLine("No identifiable candidate found. This does not prove that no screen is connected.");
        b.AppendLine("\nCheck this file before sharing. / Vérifiez ce fichier avant de le partager.");
        b.AppendLine("GitHub: https://github.com/Sandoiitchisan/LOU-Control-Center/issues/new");
        b.AppendLine("Describe the screen model, LOU version, symptom, and whether the manufacturer's app works. Attach this .txt file manually.");
        b.AppendLine("\n--- STRUCTURED TECHNICAL DATA / DONNÉES TECHNIQUES ---");
        b.AppendLine(JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return b.ToString();
    }
}

public static class DisplayRules
{
    // usbioctl structures are packed: ConnectionStatus is at byte 31, not an aligned offset 32.
    public static bool ConnectionPresent(ReadOnlySpan<byte> data) => data.Length >= 35 && data[4] == 18 && data[5] == 1 && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data[31..]) == 1;
    public static string UsbId(string id)
    {
        var m = Regex.Match(id, @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
        return m.Success ? $"{m.Groups[1].Value}:{m.Groups[2].Value}".ToUpperInvariant() : "";
    }
    public static bool Known(string id) => id is "0416:5408" or "87AD:70DB";
    public static bool Excluded(string name, string deviceClass) =>
        Regex.IsMatch(deviceClass, @"^(Monitor|Display|Keyboard|Mouse|DiskDrive|Net|Bluetooth|MEDIA|Camera|Image|Printer|PrintQueue)$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(name, @"\b(RGB|ARGB|lighting|keyboard|mouse|clavier|souris|webcam|audio|microphone|storage|ethernet|bluetooth|hub)\b|IT570[123]|Aura|Mystic Light|Polychrome", RegexOptions.IgnoreCase);
    public static bool NamedScreen(string name) => Regex.IsMatch(name, @"\b(LCD|AMOLED|OLED|Trofeo|ChiZhu|USBLCD|USBDISPLAY|screen|écran)\b|TR-VISION|TRVISION|KRAKEN.*ELITE", RegexOptions.IgnoreCase);
    // Do not export arbitrary strings from Windows or USB string descriptors: those may be personalized.
    // Keep only recognized public product/family tokens; the user supplies the commercial model in the issue.
    public static string ProductLabel(string name) => string.Join(" ", Regex.Matches(name,
        @"\b(Thermalright|Trofeo|Peerless|Vision|Elite|Kraken|NZXT|ASUS|ROG|Ryujin|Lian|Li|Galahad|Corsair|iCUE|LCD|AMOLED|OLED|ChiZhu|USBLCD|USBDISPLAY|[236]60|[124]40|6[.,]8[568])\b", RegexOptions.IgnoreCase)
        .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(16));
    public static string ServiceLabel(string value) => Regex.IsMatch(value, @"^(winusb|hidusb|hidclass|usbccgp|libusbK|libusb0|WUDFRd)$", RegexOptions.IgnoreCase) ? value.ToLowerInvariant() : "Other or unavailable";
    public static string VersionLabel(string value) => Regex.IsMatch(value, @"^\d{1,5}(\.\d{1,5}){1,3}$") ? value : "Unavailable";
    public static void ParseConfiguration(ReadOnlySpan<byte> data, DisplayInfo output)
    {
        int current = -1;
        for (int i = 0; i + 2 <= data.Length;)
        {
            int length = data[i];
            if (length < 2 || i + length > data.Length) { output.Notes.Add("Truncated or malformed configuration descriptor."); break; }
            var d = data.Slice(i, length);
            if (d[1] == 4 && length >= 9) { current = d[2]; output.Interfaces.Add(new(d[2], d[3], d[5], d[6], d[7])); }
            if (d[1] == 5 && length >= 7) output.Endpoints.Add(new(current, d[2], d[3] & 3, d[4] | d[5] << 8, d[6]));
            i += length;
        }
    }
    public static string IssueUrl => "https://github.com/Sandoiitchisan/LOU-Control-Center/issues/new?title=" + Uri.EscapeDataString("[Display compatibility] Screen model") + "&body=" + Uri.EscapeDataString(
        "### Screen model / Modèle de l’écran\n\n### LOU version / Version de LOU\n\n### Problem / Problème\n\n### Does the manufacturer's app work? / Le logiciel constructeur fonctionne-t-il ?\n\n### Diagnostics\nDrag the LOU Diagnostics .txt report here, or use GitHub’s attachment button. Review its contents before posting.\nGlissez le rapport .txt ici, ou utilisez le bouton de pièce jointe GitHub. Vérifiez son contenu avant publication.\n");
}
