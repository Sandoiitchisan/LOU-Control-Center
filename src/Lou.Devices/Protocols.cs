using Lou.Core;
using System.Buffers.Binary;
namespace Lou.Devices;
// Protocol interoperability implementation informed by TRCC Linux (GPL-3.0).
// See docs/USB.md and THIRD_PARTY_NOTICES.md for pinned references and credits.
public sealed record PanelProfile(int Width,int Height,int Pm,int Sub,bool Jpeg,int Rotation,string Evidence,bool BigEndian=false);
public static class DeviceValidation
{
    public static bool Matches(Lou.Core.ConfirmedDisplay expected,string id,PanelProfile actual)=>
        string.Equals(expected.InstanceId,id,StringComparison.OrdinalIgnoreCase)&&expected.Width==actual.Width&&expected.Height==actual.Height&&expected.Pm==actual.Pm&&expected.Sub==actual.Sub&&expected.Rotation==actual.Rotation&&expected.Jpeg==actual.Jpeg&&expected.BigEndian==actual.BigEndian;
}
public interface ILcdProtocol
{
    byte[] Handshake {get;}
    int ReplyLength {get;}
    PanelProfile Parse(ReadOnlySpan<byte> reply);
    IEnumerable<byte[]> Frame(byte[] payload,int width,int height,bool jpeg);
    bool NeedsAck {get;}
    int AckLength=>512;
}
public sealed class LyProtocol(bool ly1=false) : ILcdProtocol
{
    public byte[] Handshake {get{var b=new byte[2048];b[0]=2;b[1]=255;b[8]=1;return b;}}
    public int ReplyLength=>512;
    public bool NeedsAck=>true;
    public PanelProfile Parse(ReadOnlySpan<byte> r)
    {
        if(r.Length<45||r[0]!=3||r[1]!=255||r[8]!=1)throw new InvalidDataException(Localizer.T("Réponse LY invalide ou tronquée."));
        int pm=ly1?49+r[20]:64+(r[20]<=3?1:r[20]),sub=ly1?r[22]:r[22]+1;
        var (w,h)=pm switch {50=>(240,320),63 or 64=>(1600,720),65 or 66=>(1920,462),68=>(1280,480),69=>(1920,440),_=>throw new NotSupportedException(Localizer.F($"Modèle LY PM={pm} inconnu : transfert bloqué."))};
        uint timingW=BinaryPrimitives.ReadUInt32LittleEndian(r[24..]),timingH=BinaryPrimitives.ReadUInt32LittleEndian(r[28..]);
        string evidence=Localizer.F($"Profil PM={pm} SUB={sub}; timing brut {timingW}×{timingH}, offset {r[44]}. À confirmer visuellement.");
        if(timingW!=0&&(timingW!=(pm==50?h:w)||timingH-r[44]!=(pm==50?w:h)))throw new NotSupportedException(Localizer.T("Géométrie ambiguë : ")+evidence);
        return new(w,h,pm,sub,pm!=50,pm==50?90:PanelOrientation.Jpeg(w,h,pm,sub),evidence,pm==50);
    }
    public IEnumerable<byte[]> Frame(byte[] payload,int width,int height,bool jpeg)
    {
        if(payload.Length==0||payload.Length>=450000||!jpeg&&(width!=240||height!=320||payload.Length!=width*height*2))throw new InvalidDataException(Localizer.T("Frame invalide."));
        int count=payload.Length/496+1,padded=ly1?count:(count+3)/4*4;
        var buffer=new byte[padded*512];
        for(int i=0;i<count;i++){var chunk=buffer.AsSpan(i*512,512);int size=Math.Min(496,payload.Length-i*496);chunk[0]=1;chunk[1]=255;BinaryPrimitives.WriteInt32LittleEndian(chunk[2..],payload.Length);BinaryPrimitives.WriteUInt16LittleEndian(chunk[6..],(ushort)size);chunk[8]=(byte)(ly1?2:1);BinaryPrimitives.WriteUInt16LittleEndian(chunk[9..],(ushort)count);BinaryPrimitives.WriteUInt16LittleEndian(chunk[11..],(ushort)i);payload.AsSpan(i*496,size).CopyTo(chunk[16..]);}
        var writes=new List<byte[]>();for(int offset=0;offset<buffer.Length;offset+=4096)writes.Add(buffer.AsSpan(offset,Math.Min(4096,buffer.Length-offset)).ToArray());return writes;
    }
}
public sealed class ChiZhuProtocol : ILcdProtocol
{
    public byte[] Handshake{get{var b=new byte[64];Magic.CopyTo(b,0);b[56]=1;return b;}}
    private static byte[] Magic=>[0x12,0x34,0x56,0x78];
    public int ReplyLength=>1024;
    public bool NeedsAck=>false;
    public PanelProfile Parse(ReadOnlySpan<byte> r)
    {
        if(r.Length<57||!r[..4].SequenceEqual(Magic)||r[24]==0)throw new InvalidDataException(Localizer.T("Réponse ChiZhu invalide."));int pm=r[24],sub=r[36];
        var (w,h)=pm switch{1 when sub==48=>(1600,720),1 when sub==49=>(1920,462),1 or 2 or 3 or 4 or 6=>(480,480),7 or 14=>(640,480),9 or 11 or 20=>(854,480),10 or 16=>(960,540),12=>(800,480),13 or 17 or 18=>(960,320),15=>(640,172),32=>(320,320),50=>(240,320),5=>(320,240),63 or 64=>(1600,720),65 or 66=>(1920,462),68=>(1280,480),69=>(1920,440),_=>throw new NotSupportedException(Localizer.F($"ChiZhu PM={pm} non validé : transfert bloqué."))};
        return new(w,h,pm,sub,pm is not (32 or 50),pm==50?90:PanelOrientation.Jpeg(w,h,pm,sub),Localizer.F($"Profil USB PM={pm} SUB={sub}"),pm is 32 or 50);
    }
    public IEnumerable<byte[]> Frame(byte[] payload,int width,int height,bool jpeg)
    {
        if(payload.Length==0||payload.Length>=450000||width is <172 or >3840||height is <172 or >2160||!jpeg&&payload.Length!=checked(width*height*2))throw new InvalidDataException(Localizer.T("Frame invalide."));
        // PM 50 carries a portrait RGB565 raster under its logical 320×240 header.
        int headerWidth=!jpeg&&width==240&&height==320?320:width,headerHeight=!jpeg&&width==240&&height==320?240:height;
        var data=new byte[64+payload.Length];Magic.CopyTo(data,0);BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4),jpeg?2:3);BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8),headerWidth);BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12),headerHeight);data[56]=2;BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(60),payload.Length);payload.CopyTo(data,64);
        var writes=new List<byte[]>();for(int i=0;i<data.Length;i+=16384)writes.Add(data.AsSpan(i,Math.Min(16384,data.Length-i)).ToArray());if(data.Length%512==0)writes.Add([]);return writes;
    }
}
