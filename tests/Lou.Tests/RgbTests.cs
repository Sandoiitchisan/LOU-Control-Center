using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Lou.Rgb;
using Xunit;

namespace Lou.Tests;

public class RgbTests
{
    // Synthetic devices, no personal hardware identity in fixtures.
    private static byte[] Device(string serial = "fixture-A", uint zoneCount = 2, bool kingston = false)
    {
        using var output = new MemoryStream(); using var w = new BinaryWriter(output, Encoding.UTF8);
        void S(string value) { var bytes = Encoding.UTF8.GetBytes(value + "\0"); w.Write((ushort)bytes.Length); w.Write(bytes); }
        w.Write(0u); w.Write(1); S("Fixture controller"); S(kingston ? "Kingston" : "Test vendor"); S(kingston ? "Kingston Fury Beast/Renegade DDR4/5 DRAM Device" : "Test only"); S("1"); S(serial); S("USB: fixture");
        w.Write((ushort)(kingston ? 2 : 1)); w.Write(0); S("Direct");
        foreach (uint v in new uint[] { 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }) w.Write(v);
        w.Write((ushort)0);
        if (kingston) { S("Rainbow"); foreach (uint v in new uint[] { 21, 60, 0, 0, 100, 0, 0, 30, 80, 0, 0 }) w.Write(v); w.Write((ushort)0); }
        w.Write((ushort)1); S("ARGB header"); w.Write(1); w.Write(0u); w.Write(120u); w.Write(zoneCount);
        w.Write((ushort)0); w.Write((ushort)0); w.Write(2u); w.Write(-1); w.Write((ushort)0); S("");
        w.Write((ushort)2); S("LED 1"); S("LED 2"); w.Write((ushort)2); w.Write(0x112233u); w.Write(0x445566u);
        w.Write((ushort)0); w.Write(0u); S(""); w.Write(1u); w.Write((byte)0);
        output.Position = 0; w.Write((uint)output.Length); return output.ToArray();
    }
    [Fact] public void ParsesV6WithoutLegacyModeOrLedValues()
    {
        var d = OpenRgbProtocol.ParseDevice(731, Device());
        Assert.Equal(731u, d.SessionId); Assert.Equal("Test vendor", d.Vendor);
        Assert.Single(d.Modes); Assert.True(d.Modes[0].PerLed); Assert.Equal(2u, d.Zones[0].Count);
        Assert.Equal(new uint[] { 0x112233, 0x445566 }, d.Colors);
    }
    [Fact] public void SessionRoutingIdsDoNotChangePhysicalIdentity()
    {
        var a = OpenRgbProtocol.ParseDevice(1, Device()); var b = OpenRgbProtocol.ParseDevice(83, Device());
        Assert.Equal(a.Identity, b.Identity); Assert.Equal(a.Topology, b.Topology);
        Assert.NotEqual(a.Identity, OpenRgbProtocol.ParseDevice(1, Device("fixture-B")).Identity);
    }
    [Fact] public void AmbiguousOrChangedHardwareBlocksAssignments()
    {
        var a = OpenRgbProtocol.ParseDevice(1, Device()); var binding = new RgbAssignment { Identity = a.Identity, Topology = a.Topology };
        Assert.Same(a, RgbIdentity.Resolve([a], binding));
        Assert.Throws<InvalidDataException>(() => RgbIdentity.Resolve([a, a with { SessionId = 7 }], binding));
        Assert.Throws<InvalidDataException>(() => RgbIdentity.Resolve([OpenRgbProtocol.ParseDevice(1, Device(zoneCount: 12))], binding));
        Assert.Throws<InvalidDataException>(() => RgbIdentity.Resolve([], binding));
    }
    [Fact] public void EveryTruncatedDevicePacketIsRejected()
    {
        byte[] data = Device();
        for (int length = 0; length < data.Length; length++)
        {
            byte[] truncated = data[..length]; if (length >= 4) BinaryPrimitives.WriteInt32LittleEndian(truncated, length);
            Assert.ThrowsAny<Exception>(() => OpenRgbProtocol.ParseDevice(1, truncated));
        }
    }
    [Theory] [InlineData("#FF0000", 255u)] [InlineData("#0000FF", 16711680u)] [InlineData("#12AB34", 3451666u)]
    public void ColorEncodingUsesSdkByteOrder(string text, uint expected) => Assert.Equal(expected, RgbSettings.ParseColor(text));
    [Theory] [InlineData("red")] [InlineData("#123")] [InlineData("#GG0000")] [InlineData("FFFF00")]
    public void RejectsMalformedColors(string color) => Assert.Throws<InvalidDataException>(() => RgbSettings.ParseColor(color));
    [Fact] public void SettingsRoundTripPreservesProfilesWithoutSessionIds()
    {
        var d = OpenRgbProtocol.ParseDevice(7, Device()); string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "rgb.json");
        try
        {
            var settings = new RgbSettings { Profiles = [new() { Name = "Evening", Devices = [new() { Identity = d.Identity, Topology = d.Topology, Mode = d.Modes[0].Signature }] }] };
            settings.Save(path); settings.ApplyOnStart = true; settings.Save(path);
            Assert.True(RgbSettings.Load(path).ApplyOnStart); Assert.False(RgbSettings.Load(path + ".bak").ApplyOnStart);
            Assert.DoesNotContain("SessionId", File.ReadAllText(path));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    [Fact] public async Task FragmentedRepliesAndNonContiguousIdsAreSupported()
    {
        await using var server = new FakeServer();
        var data = await new OpenRgbClient(server.Port).DiscoverAsync();
        Assert.Single(data); Assert.Equal(731u, data[0].SessionId);
    }
    [Fact] public async Task ApplyResolvesIdentityAndSendsModeThenColorsToUniqueId()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0];
        await client.ApplyAsync(new() { Identity = d.Identity, Topology = d.Topology, Mode = d.Modes[0].Signature, Color = "#FF0000", Brightness = 50 });
        var writes = server.Commands.Where(x => x.Packet >= 1000).ToArray();
        Assert.Equal(new uint[] { 1101, 1050 }, writes.Select(x => x.Packet));
        Assert.All(writes, x => Assert.Equal(731u, x.Device));
        Assert.Equal(127u, BinaryPrimitives.ReadUInt32LittleEndian(writes[1].Data.AsSpan(6)));
    }
    [Fact] public async Task HotSwapWithSameListPositionCannotReceiveSavedColors()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0]; server.Serial = "replacement";
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ApplyAsync(new() { Identity = d.Identity, Topology = d.Topology, Mode = d.Modes[0].Signature }));
        Assert.DoesNotContain(server.Commands, x => x.Packet >= 1000);
    }
    [Fact] public async Task OlderProtocolIsExplicitlyRejected()
    {
        await using var server = new FakeServer { Protocol = 5 };
        var e = await Assert.ThrowsAsync<InvalidDataException>(() => new OpenRgbClient(server.Port).DiscoverAsync());
        Assert.Equal("RGB_VERSION_OLD", e.Message);
    }
    [Fact] public async Task HardwareRejectionIsNotReportedAsSuccess()
    {
        await using var server = new FakeServer { RejectWrites = true }; var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0];
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => client.ApplyAsync(new() { Identity = d.Identity, Topology = d.Topology, Mode = d.Modes[0].Signature }));
        Assert.Equal("RGB_COMMAND_REJECTED:3", ex.Message);
        Assert.DoesNotContain(server.Commands, x => x.Packet == 1050);
    }
    [Fact] public async Task CancellationDoesNotLeaveTheClientLocked()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DiscoverAsync(cancel.Token));
        Assert.Single(await client.DiscoverAsync());
    }
    [Fact] public async Task CommandDoesNotCompleteBeforeHardwareAcknowledgement()
    {
        await using var server = new FakeServer { DelayAcknowledgement = true }; var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0];
        var applying = client.ApplyAsync(new() { Identity = d.Identity, Topology = d.Topology, Mode = d.Modes[0].Signature });
        await server.WriteReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(applying.IsCompleted);
        server.ReleaseWrite.TrySetResult();
        await applying.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact] public async Task AnimationStreamsWithoutRepeatingModesAndCancellationStopsAllWrites()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0];
        using var animations = new RgbAnimations();
        await animations.ApplyAsync(client, new() { Identity = d.Identity, Topology = d.Topology,
            Mode = d.Modes[0].Signature, Effect = "rainbow" }, default);
        await Task.Delay(250); await animations.StopAsync();
        int count = server.Commands.Count;
        Assert.Single(server.Commands, c => c.Packet == 1101);
        Assert.True(server.Commands.Count(c => c.Packet == 1050) >= 3);
        await Task.Delay(100); Assert.Equal(count, server.Commands.Count);
        Assert.Single(await client.DiscoverAsync());
    }
    [Fact] public async Task AnimationStopsOnHotSwapInsteadOfWritingToReplacement()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0];
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = client.AnimateAsync(new() { Identity = d.Identity, Topology = d.Topology,
            Mode = d.Modes[0].Signature, Effect = "breathing" }, () => ready.TrySetResult(), cancel.Token);
        await ready.Task; server.Serial = "replacement";
        await Assert.ThrowsAsync<InvalidDataException>(() => work);
        var commands = server.Commands.ToArray();
        Assert.Equal(1u, commands[^1].Packet);
    }
    [Fact] public async Task StaticReplacementCancelsThePriorAnimationBeforeItsWrite()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0];
        using var animations = new RgbAnimations();
        var setting = new RgbAssignment { Identity = d.Identity, Topology = d.Topology,
            Mode = d.Modes[0].Signature, Effect = "rainbow", FramesPerSecond = 60 };
        await animations.ApplyAsync(client, setting, default);
        setting.Effect = "solid"; setting.Color = "#0000FF";
        await animations.ApplyAsync(client, setting, default);
        var final = server.Commands.Last(c => c.Packet == 1050);
        Assert.Equal(16711680u, BinaryPrimitives.ReadUInt32LittleEndian(final.Data.AsSpan(6)));
        int count = server.Commands.Count; await Task.Delay(120); Assert.Equal(count, server.Commands.Count);
    }
    [Fact] public async Task StreamCommandFailureStopsAndIsReported()
    {
        await using var server = new FakeServer(); var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0]; using var animations = new RgbAnimations();
        var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        animations.Failed += e => failed.TrySetResult(e);
        await animations.ApplyAsync(client, new() { Identity = d.Identity, Topology = d.Topology,
            Mode = d.Modes[0].Signature, Effect = "wave" }, default);
        server.RejectWrites = true;
        Assert.Equal("RGB_COMMAND_REJECTED:3", (await failed.Task.WaitAsync(TimeSpan.FromSeconds(3))).Message);
        int count = server.Commands.Count; await Task.Delay(100); Assert.Equal(count, server.Commands.Count);
    }
    [Fact] public async Task KingstonRainbowUsesOneNativeCommandAndStopReturnsToFixedColor()
    {
        await using var server = new FakeServer { Kingston = true }; var client = new OpenRgbClient(server.Port);
        var d = (await client.DiscoverAsync())[0]; using var animations = new RgbAnimations();
        await animations.ApplyAsync(client, new() { Identity = d.Identity, Topology = d.Topology,
            Mode = d.Modes[0].Signature, Effect = "rainbow", Speed = 75, Brightness = 42 }, default);
        var write = Assert.Single(server.Commands, c => c.Packet >= 1000);
        Assert.Equal(1101u, write.Packet); Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(write.Data.AsSpan(4)));
        int count = server.Commands.Count; await Task.Delay(150); Assert.Equal(count, server.Commands.Count);
        await animations.StopEffectsAsync(client, default);
        Assert.Contains(server.Commands, c => c.Packet == 1050);
    }
    [Fact] public void NativeOptimizationRequiresValidatedControllerIdentityAndWholeDeviceScope()
    {
        var d = OpenRgbProtocol.ParseDevice(731, Device(kingston: true));
        var s = new RgbAssignment { Identity = d.Identity, Topology = d.Topology, Mode = d.Modes[0].Signature, Effect = "rainbow" };
        Assert.Equal(d.Modes[1].Signature, RgbNativeEffects.Select([d], s)!.Mode);
        Assert.Null(RgbNativeEffects.Rainbow(d with { Vendor = "Other" }));
        Assert.Null(RgbNativeEffects.Rainbow(d with { Type = 0 }));
        var zone = s.Copy(); zone.Zone = d.Zones[0].Signature; Assert.Null(RgbNativeEffects.Select([d], zone));
        Assert.Throws<InvalidDataException>(() => RgbNativeEffects.Select([d with { Serial = "replacement" }], s));
        Assert.Null(RgbNativeEffects.Rainbow(d with { Modes = [d.Modes[0], d.Modes[1], d.Modes[1]] }));
    }
    [Fact] public async Task SlowControllerCannotStallAnotherDevicesAnimation()
    {
        await using var server = new FakeServer { SecondDevice = true, SlowSecondDevice = true }; var client = new OpenRgbClient(server.Port);
        var devices = await client.DiscoverAsync(); using var animations = new RgbAnimations();
        foreach (var d in devices)
            await animations.ApplyAsync(client, new() { Identity = d.Identity, Topology = d.Topology,
                Mode = d.Modes[0].Signature, Effect = "wave", FramesPerSecond = 60 }, default);
        int before = server.Commands.Count(c => c.Device == 731 && c.Packet == 1050);
        await Task.Delay(650);
        int frames = server.Commands.Count(c => c.Device == 731 && c.Packet == 1050) - before;
        await animations.StopAsync(); Assert.True(frames >= 15, $"Fast controller received only {frames} frames while another controller was slow.");
    }
    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new(); private readonly Task work;
        public uint Protocol = 6; public string Serial = "fixture-A"; public bool RejectWrites;
        public bool DelayAcknowledgement;
        public bool Kingston, SecondDevice, SlowSecondDevice;
        private readonly List<Task> clients = [];
        public TaskCompletionSource WriteReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<(uint Device, uint Packet, byte[] Data)> Commands = new();
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public FakeServer() { listener.Start(); work = Serve(); }
        private async Task Serve()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var tcp = await listener.AcceptTcpClientAsync(stop.Token);
                    clients.Add(Handle(tcp));
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
            finally { await Task.WhenAll(clients); }
        }
        private async Task Handle(TcpClient tcp)
        {
            using (tcp)
            {
                tcp.NoDelay = true; using var stream = tcp.GetStream();
                    try
                    {
                        while (!stop.IsCancellationRequested)
                        {
                            byte[] header = new byte[16]; await stream.ReadExactlyAsync(header, stop.Token);
                            uint device = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)), packet = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
                            byte[] data = new byte[BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12))]; await stream.ReadExactlyAsync(data, stop.Token);
                            Commands.Enqueue((device, packet, data)); byte[]? reply = packet switch { 40 => BitConverter.GetBytes(Protocol),
                                0 => SecondDevice ? new byte[] { 2, 0, 0, 0, 219, 2, 0, 0, 231, 3, 0, 0 } : new byte[] { 1, 0, 0, 0, 219, 2, 0, 0 },
                                1 => Device(device == 999 ? "fixture-B" : Serial, kingston: Kingston), _ => null };
                            if (packet >= 1000)
                            {
                                WriteReceived.TrySetResult();
                                if (DelayAcknowledgement) await ReleaseWrite.Task.WaitAsync(stop.Token);
                                if (SlowSecondDevice && device == 999) await Task.Delay(150, stop.Token);
                                reply = new byte[8]; BinaryPrimitives.WriteUInt32LittleEndian(reply, packet);
                                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), RejectWrites ? 3u : 0u);
                                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 10);
                            }
                            if (reply == null) continue;
                            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), reply.Length);
                            var bytes = header.Concat(reply).ToArray();
                            for (int offset = 0; offset < bytes.Length; offset += 3) await stream.WriteAsync(bytes.AsMemory(offset, Math.Min(3, bytes.Length - offset)), stop.Token);
                        }
                    }
                    catch (EndOfStreamException) { }
                    catch (IOException) { }
                    catch (OperationCanceledException) { }
                    catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
            }
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await work; stop.Dispose(); }
    }
}
