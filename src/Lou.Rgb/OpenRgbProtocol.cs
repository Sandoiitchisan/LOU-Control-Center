using System.IO;
using System.Text;

namespace Lou.Rgb;

// Independently implemented from the documented OpenRGB SDK v6 wire format.
// Bounded parsing is also used for data from an existing localhost SDK server.
public static class OpenRgbProtocol
{
    public const uint Version = 6;
    public const int MaximumPacket = 4 * 1024 * 1024;
    public static RgbDevice ParseDevice(uint id, byte[] data)
    {
        using var input = new MemoryStream(data, false);
        using var r = new BinaryReader(input, Encoding.UTF8);
        if (r.ReadUInt32() != data.Length) throw new InvalidDataException("RGB_PACKET_INVALID");
        int type = r.ReadInt32();
        string name = Text(r), vendor = Text(r), description = Text(r), version = Text(r), serial = Text(r), location = Text(r);
        int count = Count(r, 256), active = r.ReadInt32();
        var modes = Enumerable.Range(0, count).Select(_ => Mode(r)).ToArray();
        count = Count(r, 256);
        var zones = Enumerable.Range(0, count).Select(_ => Zone(r)).ToArray();
        int leds = Count(r, 16384);
        for (int i = 0; i < leds; i++) Text(r);
        var colors = Colors(r);
        count = Count(r, 16384);
        for (int i = 0; i < count; i++) Text(r);
        r.ReadUInt32(); Text(r); Skip(r, checked((int)r.ReadUInt32()));
        if (input.Position != input.Length || colors.Length != leds) throw new InvalidDataException("RGB_PACKET_INVALID");
        return new(id, type, name, vendor, description, version, serial, location, active, modes, zones, colors);
    }
    private static RgbMode Mode(BinaryReader r) => new(Text(r), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(),
        r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(),
        r.ReadUInt32(), r.ReadUInt32(), Colors(r));
    private static RgbZone Zone(BinaryReader r)
    {
        string name = Text(r); int type = r.ReadInt32();
        uint min = r.ReadUInt32(), max = r.ReadUInt32(), count = r.ReadUInt32();
        if (count > 16384 || min > max) throw new InvalidDataException("RGB_PACKET_INVALID");
        Skip(r, r.ReadUInt16()); int segments = Count(r, 16384);
        for (int i = 0; i < segments; i++) { Text(r); Skip(r, 12); Skip(r, r.ReadUInt16()); r.ReadUInt32(); }
        uint flags = r.ReadUInt32(); int active = r.ReadInt32(); int modes = Count(r, 256);
        var choices = Enumerable.Range(0, modes).Select(_ => Mode(r)).ToArray();
        Text(r); return new(name, type, min, max, count, flags, active, choices);
    }
    private static int Count(BinaryReader r, int maximum)
    {
        int count = r.ReadUInt16(); if (count > maximum) throw new InvalidDataException("RGB_PACKET_INVALID"); return count;
    }
    private static uint[] Colors(BinaryReader r)
    {
        int n = Count(r, 16384); return Enumerable.Range(0, n).Select(_ => r.ReadUInt32()).ToArray();
    }
    private static string Text(BinaryReader r)
    {
        int size = Count(r, 16384);
        if (size == 0) return "";
        byte[] bytes = r.ReadBytes(size);
        if (bytes.Length != size || bytes[^1] != 0) throw new InvalidDataException("RGB_PACKET_INVALID");
        return new UTF8Encoding(false, true).GetString(bytes.AsSpan(0, size - 1));
    }
    private static void Skip(BinaryReader r, int size)
    {
        if (size < 0 || size > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("RGB_PACKET_INVALID");
        r.BaseStream.Position += size;
    }
    public static byte[] Sized(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.UTF8);
        w.Write(0u); write(w); stream.Position = 0; w.Write(checked((uint)stream.Length)); return stream.ToArray();
    }
    public static byte[] ModePayload(int modeIndex, int? zoneIndex, RgbMode mode, RgbAssignment setting)
    {
        uint color = RgbSettings.ParseColor(setting.Color);
        return Sized(w =>
        {
            if (zoneIndex.HasValue) w.Write(zoneIndex.Value);
            w.Write(modeIndex);
            byte[] name = Encoding.UTF8.GetBytes(mode.Name + "\0"); w.Write(checked((ushort)name.Length)); w.Write(name);
            w.Write(mode.Flags); w.Write(mode.SpeedMin); w.Write(mode.SpeedMax);
            w.Write(mode.BrightnessMin); w.Write(mode.BrightnessMax); w.Write(mode.ColorsMin); w.Write(mode.ColorsMax);
            w.Write(mode.HasSpeed ? Percent(mode.SpeedMin, mode.SpeedMax, setting.Speed) : mode.Speed);
            w.Write(mode.HasBrightness ? Percent(mode.BrightnessMin, mode.BrightnessMax, setting.Brightness) : mode.Brightness);
            w.Write(mode.Direction);
            bool specific = (mode.Flags & 64) != 0 && mode.ColorsMax > 0;
            w.Write(specific ? 2u : mode.PerLed ? 1u : mode.ColorMode);
            int n = specific ? checked((int)Math.Max(1, mode.ColorsMin)) : mode.Colors.Length;
            if (n > 16384 || specific && n > mode.ColorsMax) throw new InvalidDataException("RGB_PACKET_INVALID");
            w.Write((ushort)n); for (int i = 0; i < n; i++) w.Write(specific ? color : mode.Colors[i]);
        });
    }
    private static uint Percent(uint min, uint max, int percent)
    {
        if (percent is < 0 or > 100) throw new InvalidDataException("RGB_SETTINGS_INVALID");
        return checked((uint)Math.Round(min + ((double)max - min) * percent / 100));
    }
}
