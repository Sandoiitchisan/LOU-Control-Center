using HidSharp;
using Lou.Core;
namespace Lou.Devices;

public sealed class HidTransport : IBulkTransport
{
    private readonly HidStream stream;
    private readonly int inputLength;
    private HidTransport(HidStream stream,int inputLength){this.stream=stream;this.inputLength=inputLength;stream.ReadTimeout=1500;stream.WriteTimeout=1500;}
    public static HidTransport Open(UsbDevice target)
    {
        if(target.Family!=DisplayFamily.HidType2)throw new NotSupportedException(Localizer.T("Famille HID non prise en charge."));
        var paths=WindowsInterfaces.ForDevice(WindowsInterfaces.Enumerate(WindowsInterfaces.HidClass),target.InstanceId);
        var matches=DeviceList.Local.GetHidDevices(0x0416,0x5302).Where(d=>paths.Contains(d.DevicePath,StringComparer.OrdinalIgnoreCase)&&d.GetMaxOutputReportLength()==513&&d.GetMaxInputReportLength() is >=21 and <=513).ToArray();
        if(matches.Length!=1)throw new IOException(Localizer.T("Interface HID absente ou ambiguë. Aucun pilote n'a été modifié."));
        var chosen=matches[0];var opened=chosen.Open();
        try{return new(opened,chosen.GetMaxInputReportLength());}catch{opened.Dispose();throw;}
    }
    public static byte[] OutputReport(byte[] payload)
    {
        if(payload.Length!=512)throw new InvalidDataException("HID payload must be exactly 512 bytes.");
        var report=new byte[513];payload.CopyTo(report,1);return report;
    }
    public static byte[] InputPayload(ReadOnlySpan<byte> report)
    {
        if(report.Length<2||report[0]!=0)throw new InvalidDataException("Unexpected HID report ID or empty report.");
        return report[1..].ToArray();
    }
    public void Write(byte endpoint,byte[] bytes){var report=OutputReport(bytes);stream.Write(report,0,report.Length);}
    public byte[] Read(byte endpoint,int length){var report=new byte[inputLength];int count=stream.Read(report,0,report.Length);return InputPayload(report.AsSpan(0,count));}
    public void Dispose()=>stream.Dispose();
}
