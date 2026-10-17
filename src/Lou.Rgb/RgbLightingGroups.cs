using System.IO;

namespace Lou.Rgb;

public enum RgbLightingGroup { Case, Ram, Gpu, All }

public static class RgbLightingGroups
{
    // GPU integration is deliberately deferred; All never writes unsupported GPU/peripheral controllers.
    public static RgbDevice[] Targets(IEnumerable<RgbDevice> devices, RgbLightingGroup group) => devices.Where(d =>
        group switch { RgbLightingGroup.Case => d.Type == 0, RgbLightingGroup.Ram => d.Type == 1,
            RgbLightingGroup.All => d.Type is 0 or 1, _ => false }).ToArray();
    public static RgbMode? Direct(RgbDevice device)
    {
        var modes = device.Type == 0 ? RgbCaseLighting.Modes(device) : device.Modes;
        return modes.FirstOrDefault(m => m.Name == "Direct" && RgbEffects.IsDirect(m)) ?? modes.FirstOrDefault(RgbEffects.IsDirect);
    }
    public static RgbAssignment[] Plan(IReadOnlyList<RgbDevice> devices, RgbLightingGroup group, string effect,
        string color, int speed, int brightness, IReadOnlyList<RgbDeviceTuning> tuning)
    {
        if (!RgbEffects.All.Any(e => e.Id == effect) || speed is < 0 or > 100 || brightness is < 0 or > 100)
            throw new InvalidDataException("RGB_SETTINGS_INVALID");
        RgbSettings.ParseColor(color);
        var targets = Targets(devices, group);
        if (targets.Length == 0) throw new InvalidDataException("RGB_GROUP_EMPTY");
        var result = new List<RgbAssignment>();
        foreach (var device in targets)
        {
            if (!device.HasPhysicalIdentity || devices.Count(d => d.Identity == device.Identity) != 1)
                throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            var mode = Direct(device) ?? throw new InvalidDataException("RGB_GROUP_UNSUPPORTED");
            if (device.Modes.Count(m => m.Signature == mode.Signature) != 1) throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            var saved = tuning.SingleOrDefault(t => t.Identity == device.Identity);
            var entry = new RgbAssignment { Identity = device.Identity, Topology = device.Type == 0 ? RgbCaseLighting.Fingerprint(device) : device.Topology,
                CaseLighting = device.Type == 0, Label = device.Name, Mode = mode.Signature, Effect = effect,
                Color = color, Speed = speed, Brightness = brightness, CaseLedLimit = saved?.CaseLedLimit ?? 0, FramesPerSecond = saved?.FramesPerSecond ?? 30 };
            if (entry.CaseLighting) RgbCaseLighting.Validate(device, entry);
            else if (device.Colors.Length == 0 || device.Zones.Any(z => z.Modes.Length > 0 && z.Modes.Count(m => m.Signature == mode.Signature) != 1))
                throw new InvalidDataException("RGB_GROUP_UNSUPPORTED");
            result.Add(entry);
        }
        return result.ToArray();
    }
}
