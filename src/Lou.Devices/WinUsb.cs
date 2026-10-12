using Lou.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
namespace Lou.Devices;

public sealed record UsbDevice(string InstanceId,string Name,string Service,string Status,string[] Paths)
{
    public bool IsLy=>InstanceId.Contains("VID_0416&PID_5408",StringComparison.OrdinalIgnoreCase);
    public string Serial=>InstanceId.Split('\\').Last();
    public override string ToString()=>$"{(IsLy?"0416:5408 / LY":"87AD:70DB / ChiZhu")} • {Serial} • {Service}";
}
public sealed record UsbEndpoint(byte Address,int Type,ushort PacketSize);
public static class DeviceDiscovery
{
    public static UsbDevice[] Scan()
    {
        using var search=new ManagementObjectSearcher("SELECT PNPDeviceID,Name,Service,Status FROM Win32_PnPEntity WHERE Present = TRUE");
        var result=new List<UsbDevice>();using var collection=search.Get();
        foreach(ManagementObject d in collection){using(d){string id=d["PNPDeviceID"]?.ToString()??"";if(!IsSupported(id))continue;result.Add(new(id,d["Name"]?.ToString()??"LCD",d["Service"]?.ToString()??"Inconnu",d["Status"]?.ToString()??"Inconnu",FindPaths(id)));}}
        return result.ToArray();
    }
    public static bool IsSupported(string id)=>id.StartsWith("USB\\VID_0416&PID_5408\\",StringComparison.OrdinalIgnoreCase)||id.StartsWith("USB\\VID_87AD&PID_70DB\\",StringComparison.OrdinalIgnoreCase);
    private static string[] FindPaths(string id)
    {
        string needle=id.Replace('\\','#');var paths=new List<string>();using var classes=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses");if(classes==null)return [];
        foreach(var guid in classes.GetSubKeyNames()){using var group=classes.OpenSubKey(guid);if(group==null)continue;foreach(var name in group.GetSubKeyNames()){if(!name.Contains(needle,StringComparison.OrdinalIgnoreCase))continue;paths.Add("\\\\?\\"+name[4..]);}}
        return paths.OrderBy(p=>p.Contains("a5dcbf10",StringComparison.OrdinalIgnoreCase)?1:0).ToArray();
    }
    public static bool VendorRunning()=>Process.GetProcesses().Any(p=>{using(p){try{return new[]{"TRCC","USBLCDNEW","USBLCD","TRCCCAP"}.Contains(p.ProcessName,StringComparer.OrdinalIgnoreCase);}catch{return false;}}});
}
public interface IBulkTransport : IDisposable
{
    void Write(byte endpoint,byte[] bytes);
    byte[] Read(byte endpoint,int length);
}
public sealed class WinUsbTransport : IBulkTransport
{
    private SafeFileHandle? file;
    private IntPtr usb;
    public UsbEndpoint[] Endpoints {get;private set;}=[];
    public string Path {get;private set;}="";
    private WinUsbTransport(){}
    public static WinUsbTransport Open(UsbDevice device)
    {
        if(!device.Service.Equals("WINUSB",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(Localizer.T("Pilote WinUSB absent. Aucun pilote n'a été modifié."));
        Exception? last=null;
        foreach(var path in device.Paths){var t=new WinUsbTransport();try{t.file=CreateFile(path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);if(t.file.IsInvalid)throw new Win32Exception();if(!WinUsb_Initialize(t.file,out t.usb))throw new Win32Exception();if(!WinUsb_QueryInterfaceSettings(t.usb,0,out var descriptor))throw new Win32Exception();var pipes=new List<UsbEndpoint>();for(byte i=0;i<descriptor.Endpoints;i++){if(!WinUsb_QueryPipe(t.usb,0,i,out var p))throw new Win32Exception();pipes.Add(new(p.Id,p.Type,p.MaxPacket));uint timeout=1500;if(!WinUsb_SetPipePolicy(t.usb,p.Id,3,4,ref timeout))throw new Win32Exception();}t.Endpoints=pipes.ToArray();t.Path=path;return t;}catch(Exception ex){last=ex;t.Dispose();}}
        throw new IOException(Localizer.T("Accès WinUSB impossible (TRCC actif, appareil déconnecté ou chemin non accessible)."),last);
    }
    public void Write(byte endpoint,byte[] bytes){if(!WinUsb_WritePipe(usb,endpoint,bytes,(uint)bytes.Length,out var count,IntPtr.Zero))throw new Win32Exception();if(count!=bytes.Length)throw new IOException(Localizer.T("Transfert USB incomplet."));}
    public byte[] Read(byte endpoint,int length){var data=new byte[length];if(!WinUsb_ReadPipe(usb,endpoint,data,(uint)length,out var count,IntPtr.Zero))throw new Win32Exception();return data.AsSpan(0,(int)count).ToArray();}
    public void Dispose(){if(usb!=IntPtr.Zero){WinUsb_Free(usb);usb=IntPtr.Zero;}file?.Dispose();file=null;}
    [StructLayout(LayoutKind.Sequential,Pack=1)] private struct Descriptor {public byte Length,Type,Number,Alternate,Endpoints,Class,SubClass,Protocol,Index;}
    [StructLayout(LayoutKind.Sequential)] private struct Pipe {public int Type;public byte Id;public ushort MaxPacket;public byte Interval;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_Initialize(SafeFileHandle file,out IntPtr handle);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_Free(IntPtr handle);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_QueryInterfaceSettings(IntPtr handle,byte alternate,out Descriptor descriptor);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_QueryPipe(IntPtr handle,byte alternate,byte index,out Pipe pipe);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_SetPipePolicy(IntPtr handle,byte pipe,uint policy,uint length,ref uint value);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_WritePipe(IntPtr handle,byte pipe,byte[] data,uint size,out uint written,IntPtr overlapped);
    [DllImport("winusb.dll",SetLastError=true)]private static extern bool WinUsb_ReadPipe(IntPtr handle,byte pipe,byte[] data,uint size,out uint read,IntPtr overlapped);
}
public sealed class LcdSession : IDisposable
{
    private readonly IBulkTransport transport;
    private readonly ILcdProtocol protocol;
    private readonly byte output,input;
    private readonly object gate=new();
    private bool disposed;
    public PanelProfile Profile{get;}
    public byte[] RawReply{get;}
    public double LastTransferMs{get;private set;}
    public double LastWriteMs{get;private set;}
    public double LastAckMs{get;private set;}
    public long Frames{get;private set;}
    public LcdSession(IBulkTransport transport,ILcdProtocol protocol,byte output,byte input)
    {
        this.transport=transport;this.protocol=protocol;this.output=output;this.input=input;
        try{transport.Write(output,protocol.Handshake);RawReply=transport.Read(input,protocol.ReplyLength);Profile=protocol.Parse(RawReply);}catch{transport.Dispose();throw;}
    }
    public static LcdSession Connect(UsbDevice device)
    {
        if(DeviceDiscovery.VendorRunning())throw new InvalidOperationException(Localizer.T("Ferme TRCC et ses auxiliaires avant le handshake. Le second LCD conservera sa dernière image, mais ses valeurs ne seront plus actualisées."));
        var transport=WinUsbTransport.Open(device);
        try{
            var outs=transport.Endpoints.Where(p=>p.Type==2&&(p.Address&128)==0).ToArray();var ins=transport.Endpoints.Where(p=>p.Type==2&&(p.Address&128)!=0).ToArray();
            if(outs.Length!=1||ins.Length!=1)throw new NotSupportedException(Localizer.T("Endpoints bulk ambigus : aucun handshake envoyé."));
            if(device.IsLy&&outs[0].Address is not (0x09 or 0x01))throw new NotSupportedException(Localizer.T("Endpoint LY non documenté."));
            if(!device.IsLy&&outs[0].Address!=0x01)throw new NotSupportedException(Localizer.T("Endpoint ChiZhu non documenté."));
            return new(transport,device.IsLy?new LyProtocol():new ChiZhuProtocol(),outs[0].Address,ins[0].Address);
        }catch{transport.Dispose();throw;}
    }
    public void Send(byte[] payload)
    {
        lock(gate){ObjectDisposedException.ThrowIf(disposed,this);var watch=Stopwatch.StartNew();foreach(var chunk in protocol.Frame(payload,Profile.Width,Profile.Height,Profile.Jpeg))transport.Write(output,chunk);LastWriteMs=watch.Elapsed.TotalMilliseconds;if(protocol.NeedsAck&&transport.Read(input,512).Length==0)throw new IOException(Localizer.T("ACK LCD absent."));Frames++;LastTransferMs=watch.Elapsed.TotalMilliseconds;LastAckMs=LastTransferMs-LastWriteMs;}
    }
    public void Dispose(){lock(gate){if(disposed)return;disposed=true;transport.Dispose();}}
}
