using System.Buffers.Binary;
using Lou.Core;
namespace Lou.Devices;

// Interoperability facts and versioned research references: docs/USB_COMPATIBILITY.md.
public sealed class HidLcdProtocol : ILcdProtocol
{
    private static ReadOnlySpan<byte> Magic=>[0xda,0xdb,0xdc,0xdd];
    public byte[] Handshake{get{var b=new byte[512];Magic.CopyTo(b);b[12]=1;return b;}}
    public int ReplyLength=>512;
    public bool NeedsAck=>false;
    public PanelProfile Parse(ReadOnlySpan<byte> r)
    {
        // Report ID was removed by the HID transport. Do not shift PM/SUB again.
        if(r.Length<20||!r[..4].SequenceEqual(Magic)||r[12]!=1)throw new InvalidDataException(Localizer.T("Réponse HID invalide ou tronquée."));
        int pm=r[5],sub=r[4];
        var (w,h,jpeg,be,rotation)=pm switch {
            36 or 37=>(240,240,false,false,0),
            49=>(240,320,false,true,90),
            50 or 51 or 52 or 53 or 58=>(240,320,false,false,90),
            54 when sub!=4=>(360,360,true,false,0),
            54=>(360,360,false,false,0),
            100 or 101 or 102=>(320,320,false,true,0),
            32=>(320,320,false,true,0),
            9 or 11 or 20 or 224=>(854,480,true,false,0),
            10 or 16=>(960,540,true,false,sub is 5 or 7?180:0),
            12=>(800,480,true,false,0),
            13 or 17 or 18=>(960,320,true,false,sub<5?0:180),
            63 or 64 or 114=>(1600,720,true,false,sub==3?0:180),
            192=>(1920,462,true,false,sub is 2 or 3 or 4?0:180),
            128=>(1280,480,true,false,sub==2?90:0),
            65 or 66=>(1920,462,true,false,sub is 2 or 3 or 4?0:180),
            68=>(1280,480,true,false,sub==2?90:0),
            69=>(1920,440,true,false,sub is 2 or 3 or 4?0:180),
            _=>throw new NotSupportedException(Localizer.F($"HID PM={pm} SUB={sub} non documenté : transfert bloqué."))
        };
        return new(w,h,pm,sub,jpeg,rotation,Localizer.F($"Profil USB PM={pm} SUB={sub}"),be);
    }
    public IEnumerable<byte[]> Frame(byte[] payload,int width,int height,bool jpeg)
    {
        if(width is <172 or >3840||height is <172 or >2160||payload.Length==0||payload.Length>=450000||!jpeg&&payload.Length!=checked(width*height*2))throw new InvalidDataException(Localizer.T("Frame invalide."));
        int length=20+payload.Length;var wire=new byte[(length+511)/512*512];Magic.CopyTo(wire);
        wire[4]=2;wire[6]=(byte)(jpeg?0:1);wire[12]=2;
        // Type-2 RGB565 uses fixed protocol fields even for square panels.
        // The actual raster size is negotiated separately and checked above.
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(8),(ushort)(jpeg?width:240));BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(10),(ushort)(jpeg?height:320));
        BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(16),payload.Length);payload.CopyTo(wire,20);
        return Enumerable.Range(0,wire.Length/512).Select(i=>wire.AsSpan(i*512,512).ToArray());
    }
}

public sealed class AliProtocol : ILcdProtocol
{
    private static byte[] Header(int length,bool frame){var b=new byte[16];new byte[]{0xf5,(byte)(frame?1:0),1,0,0xbc,0xff,0xb6,0xc8}.CopyTo(b,0);BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(12),length);return b;}
    public byte[] Handshake{get{var b=new byte[1040];Header(1024,false).CopyTo(b,0);return b;}}
    public int ReplyLength=>1024;
    public bool NeedsAck=>true;
    public int AckLength=>16;
    public PanelProfile Parse(ReadOnlySpan<byte> r)
    {
        if(r.Length<14||r[0] is not (0x36 or 0x65 or 0x66))throw new InvalidDataException(Localizer.T("Réponse ALi invalide ou tronquée."));
        bool rectangular=r[0]==0x36;return new(rectangular?240:320,320,r[0]-1,0,false,rectangular?90:0,"ALi F5",!rectangular);
    }
    public IEnumerable<byte[]> Frame(byte[] payload,int width,int height,bool jpeg)
    {
        if(jpeg||width is not (240 or 320)||height!=320||payload.Length!=width*height*2)throw new InvalidDataException(Localizer.T("Frame invalide."));
        var wire=new byte[16+payload.Length];Header(payload.Length,true).CopyTo(wire,0);payload.CopyTo(wire,16);
        return Enumerable.Range(0,(wire.Length+16383)/16384).Select(i=>wire.AsSpan(i*16384,Math.Min(16384,wire.Length-i*16384)).ToArray());
    }
}
