using System.Buffers.Binary;
using Lou.Devices;
using Xunit;
namespace Lou.Tests;
public class UsbTests
{
    [Fact]public void Aio_reply_matches_physical_pm4_and_rejects_corrupt_magic(){var reply=new byte[64];new byte[]{0x12,0x34,0x56,0x78}.CopyTo(reply,0);reply[24]=4;reply[36]=5;var p=new ChiZhuProtocol().Parse(reply);Assert.Equal((480,480,4,5,true),(p.Width,p.Height,p.Pm,p.Sub,p.Jpeg));reply[0]=0;Assert.Throws<InvalidDataException>(()=>new ChiZhuProtocol().Parse(reply));}
    [Fact]public void Aio_reconnect_does_not_accept_the_other_display(){var p=new PanelProfile(480,480,4,5,true,0,"");var binding=new Lou.Core.ConfirmedDisplay("aio",480,480,4,5,0);Assert.True(DeviceValidation.Matches(binding,"aio",p));Assert.False(DeviceValidation.Matches(binding,"large",p));Assert.False(DeviceValidation.Matches(binding,"aio",p with{Sub=1}));}
    private static byte[] Reply(byte mode=1){var r=new byte[512];r[0]=3;r[1]=255;r[8]=1;r[20]=mode;r[22]=2;return r;}
    [Theory][InlineData(0,1920,462)][InlineData(1,1920,462)][InlineData(2,1920,462)][InlineData(4,1280,480)][InlineData(5,1920,440)]public void Geometry_is_from_handshake_model(int mode,int w,int h){var p=new LyProtocol().Parse(Reply((byte)mode));Assert.Equal((w,h),(p.Width,p.Height));}
    [Fact] public void Conflicting_timing_is_rejected(){var r=Reply();BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(24),1920);BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28),480);Assert.Throws<NotSupportedException>(()=>new LyProtocol().Parse(r));}
    [Fact] public void Documented_timing_offset_is_not_raw_height(){var r=Reply();BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(24),1920);BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(28),599);r[44]=137;Assert.Equal(462,new LyProtocol().Parse(r).Height);}
    [Fact] public void Unknown_model_is_blocked(){Assert.Throws<NotSupportedException>(()=>new LyProtocol().Parse(Reply(99)));}
    [Theory][InlineData(0)][InlineData(12)][InlineData(36)]public void Truncated_handshake_is_rejected(int length){Assert.Throws<InvalidDataException>(()=>new LyProtocol().Parse(new byte[length]));}
    [Theory][InlineData(1)][InlineData(496)][InlineData(497)][InlineData(1984)][InlineData(4096)][InlineData(449999)]public void Ly_packets_roundtrip_payload_and_padding(int length){var data=Enumerable.Range(0,length).Select(i=>(byte)(i%251)).ToArray();var packets=new LyProtocol().Frame(data,1920,462,true).ToArray();Assert.All(packets,p=>Assert.True(p.Length is 4096 or 2048));var wire=packets.SelectMany(p=>p).ToArray();var rebuilt=new List<byte>();int count=length/496+1;for(int i=0;i<count;i++){var packet=wire.AsSpan(i*512,512);Assert.Equal(length,BinaryPrimitives.ReadInt32LittleEndian(packet[2..]));Assert.Equal(count,BinaryPrimitives.ReadUInt16LittleEndian(packet[9..]));Assert.Equal(i,BinaryPrimitives.ReadUInt16LittleEndian(packet[11..]));int n=BinaryPrimitives.ReadUInt16LittleEndian(packet[6..]);rebuilt.AddRange(packet.Slice(16,n).ToArray());}Assert.Equal(data,rebuilt);Assert.All(wire.Skip(count*512),b=>Assert.Equal(0,b));}
    [Fact] public void Payload_ceiling_is_enforced(){Assert.Throws<InvalidDataException>(()=>new LyProtocol().Frame(new byte[450000],1920,462,true));}
    [Fact] public void ChiZhu_header_and_zero_length_packet(){var packets=new ChiZhuProtocol().Frame(new byte[448],480,480,true).ToArray();Assert.Equal(2,packets.Length);Assert.Empty(packets[1]);Assert.Equal(new byte[]{0x12,0x34,0x56,0x78},packets[0][..4]);Assert.Equal(448,BinaryPrimitives.ReadInt32LittleEndian(packets[0].AsSpan(60)));Assert.Equal(480,BinaryPrimitives.ReadInt32LittleEndian(packets[0].AsSpan(8)));}
    [Fact] public void Session_fails_on_transfer_error_and_disposes_handle(){var fake=new Fake();using(var session=new LcdSession(fake,new LyProtocol(),9,129)){fake.Throw=true;Assert.Throws<IOException>(()=>session.Send([0xff,0xd8,0xff,0xd9]));Assert.Equal(0,session.Frames);}Assert.True(fake.Disposed);}
    [Fact] public void Session_fails_on_missing_ack(){var fake=new Fake();using var session=new LcdSession(fake,new LyProtocol(),9,129);fake.NoAck=true;Assert.Throws<IOException>(()=>session.Send([1,2,3]));}
    [Fact] public void Failed_handshake_releases_transport(){var fake=new Fake{NoAck=true};Assert.Throws<InvalidDataException>(()=>new LcdSession(fake,new LyProtocol(),9,129));Assert.True(fake.Disposed);}
    [Fact] public void Wrong_vid_pid_never_matches(){Assert.False(DeviceDiscovery.IsSupported(@"USB\VID_87CD&PID_70DB\123"));Assert.False(DeviceDiscovery.IsSupported(@"USB\VID_0416&PID_5409\123"));Assert.True(DeviceDiscovery.IsSupported(@"USB\VID_0416&PID_5408\123"));}
    [Fact] public void Reconnect_requires_same_identity_and_geometry(){var expected=new Lou.Core.ConfirmedDisplay("device-A",1920,462,65,5,180);var profile=new PanelProfile(1920,462,65,5,true,180,"");Assert.True(DeviceValidation.Matches(expected,"device-A",profile));Assert.False(DeviceValidation.Matches(expected,"device-B",profile));Assert.False(DeviceValidation.Matches(expected,"device-A",profile with{Height=480}));Assert.False(DeviceValidation.Matches(expected,"device-A",profile with{Rotation=0}));}
    [Fact] public void Session_serializes_concurrent_frames(){var fake=new Fake();using var session=new LcdSession(fake,new LyProtocol(),9,129);Parallel.For(0,20,_=>session.Send([1,2,3]));Assert.Equal(20,session.Frames);Assert.Equal(1,fake.MaxConcurrent);}
    private sealed class Fake : IBulkTransport
    {
        public bool Throw,NoAck,Disposed;public int MaxConcurrent;private int concurrent;
        public byte[] Read(byte ep,int length)=>NoAck?[]:Reply();
        public void Write(byte ep,byte[] data){if(Throw)throw new IOException("Disconnected");int n=Interlocked.Increment(ref concurrent);MaxConcurrent=Math.Max(MaxConcurrent,n);Thread.SpinWait(100);Interlocked.Decrement(ref concurrent);}
        public void Dispose()=>Disposed=true;
    }
}
