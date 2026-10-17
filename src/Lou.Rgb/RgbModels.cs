using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lou.Rgb;

public sealed record RgbMode(string Name, uint Flags, uint SpeedMin, uint SpeedMax,
    uint BrightnessMin, uint BrightnessMax, uint ColorsMin, uint ColorsMax,
    uint Speed, uint Brightness, uint Direction, uint ColorMode, uint[] Colors)
{
    public bool HasSpeed => (Flags & 1) != 0;
    public bool HasBrightness => (Flags & 16) != 0;
    public bool PerLed => (Flags & 32) != 0;
    public bool HasColors => (Flags & 96) != 0;
    public string Signature => RgbIdentity.Hash(Name, Flags, SpeedMin, SpeedMax, BrightnessMin, BrightnessMax, ColorsMin, ColorsMax);
    public override string ToString() => Name;
}
public sealed record RgbZone(string Name, int Type, uint Minimum, uint Maximum, uint Count, uint Flags,
    int ActiveMode, RgbMode[] Modes)
{
    public string ConnectionIdentity => RgbIdentity.Hash(Name, Type, Minimum, Maximum, Flags & 0xfffu);
    public string Signature => RgbIdentity.Hash(Name, Type, Minimum, Maximum, Count, Flags,
        string.Join(";", Modes.Select(m => m.Signature)));
    public override string ToString() => Name;
}
public sealed record RgbDevice(uint SessionId, int Type, string Name, string Vendor, string Description,
    string Version, string Serial, string Location, int ActiveMode, RgbMode[] Modes, RgbZone[] Zones, uint[] Colors)
{
    // Session IDs route packets only. Neither enumeration order nor mutable display names identify hardware.
    public string Identity => RgbIdentity.Hash("OpenRGB", Type, Vendor, Name, Serial, Location);
    public bool HasPhysicalIdentity => !string.IsNullOrWhiteSpace(Serial) || !string.IsNullOrWhiteSpace(Location);
    public string Topology => RgbIdentity.Hash(string.Join(";", Modes.Select(m => m.Signature)),
        string.Join(";", Zones.Select(z => z.Signature)), Colors.Length);
    public override string ToString() => string.IsNullOrEmpty(Vendor) ? Name : $"{Vendor} · {Name}";
}
public static class RgbIdentity
{
    public static string Hash(params object[] parts) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    public static RgbDevice Resolve(IEnumerable<RgbDevice> devices, RgbAssignment assignment)
    {
        var matches = devices.Where(d => d.HasPhysicalIdentity && d.Identity == assignment.Identity).ToArray();
        if (matches.Length != 1 || matches[0].Topology != assignment.Topology)
            throw new InvalidDataException("RGB_IDENTITY_CHANGED");
        return matches[0];
    }
}
public sealed class RgbAssignment
{
    public bool CaseLighting { get; set; }
    public int CaseLedLimit { get; set; }
    public int FramesPerSecond { get; set; } = 30;
    public string Identity { get; set; } = "";
    public string Topology { get; set; } = "";
    public string Label { get; set; } = "";
    public string Zone { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Effect { get; set; } = "";
    public string Color { get; set; } = "#00E5FF";
    public int Speed { get; set; } = 50;
    public int Brightness { get; set; } = 100;
    public RgbAssignment Copy() => (RgbAssignment)MemberwiseClone();
}
public sealed class RgbProfile
{
    public string Name { get; set; } = "";
    public List<RgbAssignment> Devices { get; set; } = [];
    public override string ToString() => Name;
}
public sealed class RgbZoneSize
{
    public string Device { get; set; } = "";
    public string Connection { get; set; } = "";
    public int Count { get; set; }
}
public sealed class RgbDeviceTuning
{
    public string Identity { get; set; } = "";
    public int CaseLedLimit { get; set; }
    public int FramesPerSecond { get; set; } = 30;
}
public sealed class RgbSettings
{
    public int Version { get; set; } = 1;
    public bool StartWithApp { get; set; }
    public bool ApplyOnStart { get; set; }
    public string StartupProfile { get; set; } = "";
    public List<RgbProfile> Profiles { get; set; } = [];
    public List<RgbZoneSize> ZoneSizes { get; set; } = [];
    public List<RgbDeviceTuning> DeviceTuning { get; set; } = [];
    public void Validate()
    {
        if (Version != 1 || Profiles == null || Profiles.Count > 30 || Profiles.Any(p => p == null) || Profiles.Select(p => p.Name).Distinct().Count() != Profiles.Count ||
            ZoneSizes == null || ZoneSizes.Count > 128 || ZoneSizes.Any(z => z == null || z.Device?.Length != 64 || z.Connection?.Length != 64 || z.Count is < 0 or > 16384) ||
            ZoneSizes.Select(z => (z.Device, z.Connection)).Distinct().Count() != ZoneSizes.Count ||
            DeviceTuning == null || DeviceTuning.Count > 128 || DeviceTuning.Any(t => t == null || t.Identity?.Length != 64 || t.CaseLedLimit is < 0 or > 256 || t.FramesPerSecond is < 15 or > 60) ||
            DeviceTuning.Select(t => t.Identity).Distinct().Count() != DeviceTuning.Count)
            throw new InvalidDataException("RGB_SETTINGS_INVALID");
        foreach (var p in Profiles)
        {
            if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 60 || p.Devices == null || p.Devices.Count > 128)
                throw new InvalidDataException("RGB_SETTINGS_INVALID");
            foreach (var d in p.Devices)
            {
                if (d == null || d.Identity?.Length != 64 || d.Topology?.Length != 64 || d.Mode?.Length != 64 ||
                    d.Zone?.Length is not (0 or 64) || d.Label == null || d.Label.Length > 200 || !RgbEffects.IsValid(d.Effect) || d.FramesPerSecond is < 15 or > 60 || d.CaseLedLimit is < 0 or > 256 || d.Speed is < 0 or > 100 || d.Brightness is < 0 or > 100)
                    throw new InvalidDataException("RGB_SETTINGS_INVALID");
                ParseColor(d.Color);
            }
        }
    }
    public static uint ParseColor(string value)
    {
        if (value == null || value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out var color)) throw new InvalidDataException("RGB_COLOR_INVALID");
        return ((color & 0xff0000) >> 16) | (color & 0xff00) | ((color & 0xff) << 16);
    }
    public static RgbSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("RGB_SETTINGS_INVALID");
        var result = JsonSerializer.Deserialize<RgbSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("RGB_SETTINGS_INVALID");
        result.Validate(); return result;
    }
    public void Save(string path)
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        File.Move(path + ".tmp", path, true);
    }
}
