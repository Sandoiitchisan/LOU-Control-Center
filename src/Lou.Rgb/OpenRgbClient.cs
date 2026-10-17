using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Lou.Rgb;

public sealed class OpenRgbClient(int port = OpenRgbEngine.Port)
{
    private readonly SemaphoreSlim serial = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> animationLocks = new();
    private long animationFrames;
    public long AnimationFrames => Interlocked.Read(ref animationFrames);
    private long animationCommandTicks, animationCheckTicks;
    public double AnimationCommandMilliseconds => Interlocked.Read(ref animationCommandTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    public double AnimationCheckMilliseconds => Interlocked.Read(ref animationCheckTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    /// <summary>One persistent SDK connection, bounded writes, no queued animation frames.</summary>
    public async Task AnimateAsync(RgbAssignment setting, Action ready, CancellationToken stop)
    {
        if (string.IsNullOrEmpty(setting.Effect) || !RgbEffects.IsValid(setting.Effect) || setting.FramesPerSecond is < 15 or > 60) throw new InvalidDataException("RGB_SETTINGS_INVALID");
        RgbEffects.Render(setting, 0, 0); // Validate before any hardware command.
        var initial = await DiscoverAsync(stop).ConfigureAwait(false);
        var target = setting.CaseLighting ? RgbCaseLighting.Resolve(initial, setting) : RgbIdentity.Resolve(initial, setting);
        var available = setting.CaseLighting ? RgbCaseLighting.Modes(target) : string.IsNullOrEmpty(setting.Zone) ? target.Modes :
            target.Zones.SingleOrDefault(z => z.Signature == setting.Zone)?.Modes ?? [];
        if (available.Length == 0 && !string.IsNullOrEmpty(setting.Zone)) available = target.Modes;
        if (!available.Any(m => m.Signature == setting.Mode && RgbEffects.IsDirect(m))) throw new InvalidDataException("RGB_DEVICE_MODE_REQUIRED");
        if (string.IsNullOrEmpty(setting.Zone) && target.Zones.Any(z => z.Modes.Length > 0 && z.Modes.Count(m => m.Signature == setting.Mode) != 1))
            throw new InvalidDataException("RGB_DEVICE_MODE_REQUIRED");
        var direct = setting.Copy(); direct.Effect = ""; direct.Brightness = 100; direct.Color = "#000000";
        await ApplyAsync(direct, stop).ConfigureAwait(false);
        if (!setting.CaseLighting && string.IsNullOrEmpty(setting.Zone))
            foreach (var z in target.Zones.Where(z => z.Modes.Length > 0))
            { var entry = direct.Copy(); entry.Zone = z.Signature; await ApplyAsync(entry, stop).ConfigureAwait(false); }
        using var c = await Connection.Open(port, stop).ConfigureAwait(false);
        var inventory = await c.Inventory(stop).ConfigureAwait(false);
        target = setting.CaseLighting ? RgbCaseLighting.Resolve(inventory, setting) : RgbIdentity.Resolve(inventory, setting);
        var binding = new RgbAssignment { Identity = target.Identity, Topology = target.Topology };
        int zoneIndex = string.IsNullOrEmpty(setting.Zone) ? -1 : Array.FindIndex(target.Zones, z => z.Signature == setting.Zone);
        if (!string.IsNullOrEmpty(setting.Zone) && zoneIndex < 0) throw new InvalidDataException("RGB_IDENTITY_CHANGED");
        int count = zoneIndex < 0 ? target.Colors.Length : checked((int)target.Zones[zoneIndex].Count);
        if (count is < 1 or > 16384) throw new InvalidDataException("RGB_ZONE_EMPTY");
        var timer = System.Diagnostics.Stopwatch.StartNew(); double nextCheck = 0;
        using var pacer = new Lou.Core.FramePacer(stop);
        bool announced = false;
        var frameSerial = animationLocks.GetOrAdd(setting.Identity, _ => new SemaphoreSlim(1, 1));
        while (true)
        {
            stop.ThrowIfCancellationRequested();
            double frameStart = timer.Elapsed.TotalSeconds;
            await frameSerial.WaitAsync(stop).ConfigureAwait(false);
            try
            {
                if (frameStart >= nextCheck)
                {
                    long checkStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    // Routing IDs are unique within this SDK connection. Re-read only this controller;
                    // a topology notification invalidates the stream rather than triggering rediscovery mid-frame.
                    var current = RgbIdentity.Resolve([OpenRgbProtocol.ParseDevice(target.SessionId,
                        await c.Request(target.SessionId, 1, Word(6), stop).ConfigureAwait(false))], binding);
                    c.AssertInventoryUnchanged();
                    Interlocked.Add(ref animationCheckTicks, System.Diagnostics.Stopwatch.GetTimestamp() - checkStart);
                    if (current.SessionId != target.SessionId || current.ActiveMode != target.ActiveMode ||
                        (zoneIndex < 0 ? !current.Zones.Select(z => z.ActiveMode).SequenceEqual(target.Zones.Select(z => z.ActiveMode)) :
                            current.Zones[zoneIndex].ActiveMode != target.Zones[zoneIndex].ActiveMode))
                        throw new InvalidDataException("RGB_IDENTITY_CHANGED");
                    nextCheck = frameStart + 1;
                }
                var colors = RgbEffects.Render(setting, count, frameStart);
                // Each physical output begins at the same phase, even when an ARGB header exposes spare capacity.
                if (zoneIndex < 0 && target.Zones.Sum(z => (long)z.Count) == count)
                {
                    int offset = 0;
                    foreach (var z in target.Zones)
                    { RgbEffects.Render(setting, checked((int)z.Count), frameStart).CopyTo(colors, offset); offset += (int)z.Count; }
                }
                var payload = OpenRgbProtocol.Sized(w =>
                { if (zoneIndex >= 0) w.Write(zoneIndex); w.Write((ushort)count); foreach (uint color in colors) w.Write(color); });
                long commandStart = System.Diagnostics.Stopwatch.GetTimestamp();
                await c.Command(target.SessionId, zoneIndex < 0 ? 1050u : 1051u, payload, stop).ConfigureAwait(false);
                Interlocked.Add(ref animationCommandTicks, System.Diagnostics.Stopwatch.GetTimestamp() - commandStart);
                c.AssertInventoryUnchanged();
                Interlocked.Increment(ref animationFrames);
                if (!announced) { announced = true; ready(); }
            }
            finally { frameSerial.Release(); }
            double delay = 1.0 / setting.FramesPerSecond - (timer.Elapsed.TotalSeconds - frameStart);
            if (!pacer.Wait(delay)) stop.ThrowIfCancellationRequested();
        }
    }
    public async Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken stop = default)
    {
        await serial.WaitAsync(stop);
        try { using var connection = await Connection.Open(port, stop); return await connection.Inventory(stop); }
        finally { serial.Release(); }
    }
    public async Task ApplyAsync(RgbAssignment setting, CancellationToken stop = default)
    {
        if (setting.Speed is < 0 or > 100 || setting.Brightness is < 0 or > 100 || setting.CaseLedLimit is < 0 or > 256) throw new InvalidDataException("RGB_SETTINGS_INVALID");
        RgbSettings.ParseColor(setting.Color);
        if (setting.CaseLighting) { await ApplyCaseAsync(setting, stop); return; }
        await serial.WaitAsync(stop);
        try
        {
            using var c = await Connection.Open(port, stop);
            var d = RgbIdentity.Resolve(await c.Inventory(stop), setting);
            int zoneIndex = string.IsNullOrEmpty(setting.Zone) ? -1 : Array.FindIndex(d.Zones, z => z.Signature == setting.Zone);
            if (!string.IsNullOrEmpty(setting.Zone) && (zoneIndex < 0 || d.Zones.Count(z => z.Signature == setting.Zone) != 1))
                throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            var zone = zoneIndex < 0 ? null : d.Zones[zoneIndex];
            bool zoneMode = zone?.Modes.Length > 0;
            var modes = zoneMode ? zone!.Modes : d.Modes;
            int modeIndex = Array.FindIndex(modes, m => m.Signature == setting.Mode);
            if (modeIndex < 0 || modes.Count(m => m.Signature == setting.Mode) != 1) throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            var mode = modes[modeIndex];
            // Device-wide effects cannot pretend to be independently scoped to one ARGB header.
            if (zone != null && !zoneMode && (!mode.PerLed || (mode.Flags & 1024) != 0 || d.ActiveMode != modeIndex))
                throw new InvalidDataException("RGB_DEVICE_MODE_REQUIRED");
            if (zone == null || zoneMode)
                await c.Command(d.SessionId, zoneMode ? 1103u : 1101u,
                    OpenRgbProtocol.ModePayload(modeIndex, zoneMode ? zoneIndex : null, mode, setting), stop);
            if (mode.PerLed && (mode.Flags & 64) == 0)
            {
                int count = zone == null ? d.Colors.Length : checked((int)zone.Count);
                if (count == 0) throw new InvalidDataException("RGB_ZONE_EMPTY");
                uint color = RgbSettings.ParseColor(setting.Color);
                // A software brightness scale is used only for direct colors without a hardware brightness control.
                if (!mode.HasBrightness) color = Scale(color, setting.Brightness);
                var payload = OpenRgbProtocol.Sized(w =>
                {
                    if (zone != null) w.Write(zoneIndex);
                    w.Write(checked((ushort)count)); for (int i = 0; i < count; i++) w.Write(color);
                });
                await c.Command(d.SessionId, zone == null ? 1050u : 1051u, payload, stop);
            }
            // ACK confirms SDK command handling. Some drivers enqueue hardware work separately;
            // neither ACK nor cached state confirms the physical LED output.
            await c.Request(d.SessionId, 1, Word(6), stop);
        }
        finally { serial.Release(); }
    }
    private async Task ApplyCaseAsync(RgbAssignment setting, CancellationToken stop)
    {
        var d = RgbCaseLighting.Resolve(await DiscoverAsync(stop), setting);
        var mode = RgbCaseLighting.Validate(d, setting); // Validate the entire group before writing any zone.
        uint Coverage(RgbZone z) => setting.CaseLedLimit == 0 ? z.Maximum : Math.Min(z.Maximum, (uint)setting.CaseLedLimit);
        var emptyHeaders = d.Zones.Where(z => RgbCaseLighting.CanCoverNativeHeader(d, mode, z) && z.Count != Coverage(z)).Select(z => z.ConnectionIdentity).ToArray();
        foreach (var header in emptyHeaders)
        {
            d = RgbCaseLighting.Resolve(await DiscoverAsync(stop), setting);
            var matches = d.Zones.Where(z => z.ConnectionIdentity == header).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            await ResizeAsync(new() { Identity = d.Identity, Topology = d.Topology, Zone = matches[0].Signature }, checked((int)Coverage(matches[0])), stop);
        }
        d = RgbCaseLighting.Resolve(await DiscoverAsync(stop), setting);
        foreach (var entry in RgbCaseLighting.Plan(d, setting)) await ApplyAsync(entry, stop);
    }
    public async Task ResizeAsync(RgbAssignment identity, int count, CancellationToken stop = default)
    {
        await serial.WaitAsync(stop);
        try
        {
            using var c = await Connection.Open(port, stop);
            var d = RgbIdentity.Resolve(await c.Inventory(stop), identity);
            int index = Array.FindIndex(d.Zones, z => z.Signature == identity.Zone);
            if (index < 0 || d.Zones.Count(z => z.Signature == identity.Zone) != 1) throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            var z = d.Zones[index];
            if ((z.Flags & 3) == 0 || count < z.Minimum || count > Math.Min(16384, z.Maximum)) throw new InvalidDataException("RGB_ZONE_SIZE_INVALID");
            byte[] payload = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(payload, index);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), count);
            await c.Command(d.SessionId, 1000, payload, stop);
            await c.Request(d.SessionId, 1, Word(6), stop);
        }
        finally { serial.Release(); }
    }
    private static uint Scale(uint c, int percent) => (uint)(((c & 255) * percent / 100) |
        (((c >> 8 & 255) * percent / 100) << 8) | (((c >> 16 & 255) * percent / 100) << 16));
    private static byte[] Word(uint value) { var bytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); return bytes; }

    private sealed class Connection : IDisposable
    {
        private readonly TcpClient tcp = new();
        private NetworkStream stream = null!;
        private bool inventoryChanged;
        public void AssertInventoryUnchanged() { if (inventoryChanged) throw new InvalidDataException("RGB_IDENTITY_CHANGED"); }
        public static async Task<Connection> Open(int port, CancellationToken stop)
        {
            var c = new Connection();
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop); deadline.CancelAfter(TimeSpan.FromSeconds(8));
                await c.tcp.ConnectAsync(IPAddress.Loopback, port, deadline.Token); c.tcp.NoDelay = true; c.stream = c.tcp.GetStream();
                var protocol = await c.Request(0, 40, Word(6), deadline.Token);
                if (protocol.Length != 4 || BinaryPrimitives.ReadUInt32LittleEndian(protocol) < 6) throw new InvalidDataException("RGB_VERSION_OLD");
                await c.Send(0, 50, Encoding.UTF8.GetBytes("LOU Control Center\0"), deadline.Token);
                return c;
            }
            catch { c.Dispose(); throw; }
        }
        public async Task<IReadOnlyList<RgbDevice>> Inventory(CancellationToken stop)
        {
            inventoryChanged = false;
            byte[] list = await Request(0, 0, [], stop);
            if (list.Length < 4) throw new InvalidDataException("RGB_PACKET_INVALID");
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(list);
            if (count > 128 || list.Length != 4 + count * 4) throw new InvalidDataException("RGB_PACKET_INVALID");
            var result = new List<RgbDevice>(); var ids = new HashSet<uint>();
            for (int i = 0; i < count; i++)
            {
                uint id = BinaryPrimitives.ReadUInt32LittleEndian(list.AsSpan(4 + i * 4));
                if (!ids.Add(id)) throw new InvalidDataException("RGB_PACKET_INVALID");
                result.Add(OpenRgbProtocol.ParseDevice(id, await Request(id, 1, Word(6), stop)));
            }
            if (inventoryChanged) throw new InvalidDataException("RGB_IDENTITY_CHANGED");
            return result;
        }
        public async Task Send(uint device, uint packet, byte[] payload, CancellationToken stop)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop); deadline.CancelAfter(TimeSpan.FromSeconds(8));
            byte[] header = new byte[16]; "ORGB"u8.CopyTo(header);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), device);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), packet);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), checked((uint)payload.Length));
            byte[] frame = new byte[header.Length + payload.Length]; header.CopyTo(frame, 0); payload.CopyTo(frame, header.Length);
            await stream.WriteAsync(frame, deadline.Token);
        }
        public Task<byte[]> Command(uint device, uint packet, byte[] payload, CancellationToken stop) => Request(device, packet, payload, stop, true);
        public async Task<byte[]> Request(uint device, uint packet, byte[] payload, CancellationToken stop, bool acknowledgement = false)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop); deadline.CancelAfter(TimeSpan.FromSeconds(acknowledgement ? 30 : 8));
            await Send(device, packet, payload, deadline.Token);
            for (int i = 0; i < 256; i++)
            {
                byte[] header = new byte[16]; await stream.ReadExactlyAsync(header, deadline.Token);
                if (!header.AsSpan(0, 4).SequenceEqual("ORGB"u8)) throw new InvalidDataException("RGB_PACKET_INVALID");
                uint incomingDevice = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                uint incomingPacket = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
                if (size > OpenRgbProtocol.MaximumPacket) throw new InvalidDataException("RGB_PACKET_INVALID");
                byte[] data = new byte[size]; await stream.ReadExactlyAsync(data, deadline.Token);
                if (incomingPacket == 100) inventoryChanged = true;
                if (incomingPacket == 10 && incomingDevice == device && data.Length == 8 && BinaryPrimitives.ReadUInt32LittleEndian(data) == packet)
                {
                    uint result = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
                    if (result != 0) throw new InvalidDataException("RGB_COMMAND_REJECTED:" + result);
                    if (acknowledgement) return [];
                }
                if (!acknowledgement && incomingPacket == packet && incomingDevice == device) return data;
                // Other messages are asynchronous server notifications, not replies to this request.
            }
            throw new InvalidDataException("RGB_PACKET_INVALID");
        }
        public void Dispose() => tcp.Dispose();
    }
}
