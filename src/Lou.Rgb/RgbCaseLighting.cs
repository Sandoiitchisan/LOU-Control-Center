using System.IO;

namespace Lou.Rgb;

// Group scope is the whole physical motherboard controller, never RAM/GPU or list neighbours.
public static class RgbCaseLighting
{
    public static string Fingerprint(RgbDevice d) => RgbIdentity.Hash("case-v1", d.Type,
        string.Join(";", d.Modes.Select(m => m.Signature)),
        string.Join(";", d.Zones.Select(z => RgbIdentity.Hash(z.ConnectionIdentity,
            string.Join(";", z.Modes.Select(m => m.Signature))))));

    public static RgbDevice Resolve(IEnumerable<RgbDevice> devices, RgbAssignment setting)
    {
        var matches = devices.Where(d => d.Type == 0 && d.HasPhysicalIdentity && d.Identity == setting.Identity).ToArray();
        if (matches.Length != 1 || Fingerprint(matches[0]) != setting.Topology)
            throw new InvalidDataException("RGB_IDENTITY_CHANGED");
        return matches[0];
    }

    public static RgbMode[] Modes(RgbDevice d) => d.Type != 0 ? [] : d.Modes.Where(m =>
        (m.Flags & 1024) == 0 && d.Zones.All(z => z.Modes.Length == 0 || z.Modes.Count(n => n.Signature == m.Signature) == 1)).ToArray();

    // Uniform IT5702 colors/effects cover the header capacity advertised by OpenRGB.
    // This is an output coverage limit, NOT a detected physical LED count.
    // Spatial software effects repeat within that capacity; it is not a physical strip length.
    public static bool CanCoverNativeHeader(RgbDevice d, RgbMode mode, RgbZone zone) =>
        d.Type == 0 && d.Vendor == "Gigabyte" && d.Location.Contains("VID_048D&PID_5702", StringComparison.OrdinalIgnoreCase) &&
        d.Description.StartsWith("IT5701-GIGABYTE", StringComparison.Ordinal) &&
        (mode.Flags & 1024) == 0 && zone.Type == 1 && (zone.Flags & 3) != 0 &&
        zone.Minimum <= 1 && zone.Maximum is >= 1 and <= 256 && zone.Modes.Any(m => m.Signature == mode.Signature);

    public static RgbMode Validate(RgbDevice d, RgbAssignment setting)
    {
        if (d.Zones.Select(z => z.ConnectionIdentity).Distinct().Count() != d.Zones.Length)
            throw new InvalidDataException("RGB_IDENTITY_CHANGED");
        var choices = Modes(d).Where(m => m.Signature == setting.Mode).ToArray();
        if (choices.Length != 1) throw new InvalidDataException("RGB_DEVICE_MODE_REQUIRED");
        if (d.Zones.Any(z => z.Count == 0 && !CanCoverNativeHeader(d, choices[0], z)))
            throw new InvalidDataException("RGB_ZONE_EMPTY");
        return choices[0];
    }

    public static RgbAssignment[] Plan(RgbDevice d, RgbAssignment setting)
    {
        Validate(d, setting);
        RgbAssignment Entry(string zone) => new() { Identity = d.Identity, Topology = d.Topology,
            Label = setting.Label, Zone = zone, Mode = setting.Mode, Color = setting.Color,
            Speed = setting.Speed, Brightness = setting.Brightness };
        // Also overwrite native per-zone overrides, otherwise old zone effects can beat the global mode.
        return new[] { Entry("") }.Concat(d.Zones.Where(z => z.Modes.Length > 0).Select(z => Entry(z.Signature))).ToArray();
    }
}
