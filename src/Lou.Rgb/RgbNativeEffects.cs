namespace Lou.Rgb;

/// <summary>Reviewed native alternatives to expensive software streaming. Routing still uses physical identity.</summary>
public static class RgbNativeEffects
{
    public static RgbMode? Rainbow(RgbDevice device)
    {
        // Validated Kingston Fury driver family, not a blanket promise for arbitrary RAM firmware.
        if (device.Type != 1 || device.Vendor != "Kingston" ||
            device.Description != "Kingston Fury Beast/Renegade DDR4/5 DRAM Device") return null;
        var modes = device.Modes.Where(m => m.Name == "Rainbow" && !m.PerLed && m.HasSpeed && m.HasBrightness &&
            (m.Flags & 64) == 0 && device.Zones.All(z => z.Modes.Length == 0)).ToArray();
        return modes.Length == 1 ? modes[0] : null;
    }
    public static RgbAssignment? Select(IReadOnlyList<RgbDevice> inventory, RgbAssignment requested)
    {
        if (requested.Effect != "rainbow" || requested.CaseLighting || requested.Zone.Length != 0) return null;
        var device = RgbIdentity.Resolve(inventory, requested);
        if (device.Modes.Count(m => m.Signature == requested.Mode) != 1) throw new System.IO.InvalidDataException("RGB_IDENTITY_CHANGED");
        var native = Rainbow(device); if (native == null) return null;
        var result = requested.Copy(); result.Mode = native.Signature; result.Effect = "";
        return result;
    }
}
