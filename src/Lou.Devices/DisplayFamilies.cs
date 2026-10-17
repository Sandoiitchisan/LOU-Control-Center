using Lou.Core;
namespace Lou.Devices;

public enum DisplayFamily { Unknown, Ly, Ly1, ChiZhu, HidType2, HidType3, Ali, Scsi }

public static class DisplayFamilies
{
    // USB identity is a transport candidate, never a screen resolution.
    public static DisplayFamily Identify(string instance)
    {
        var parts=instance.Split('\\');
        if(parts.Length!=3||!parts[0].Equals("USB",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(parts[2]))return DisplayFamily.Unknown;
        return parts[1].ToUpperInvariant() switch {
            "VID_0416&PID_5408"=>DisplayFamily.Ly,
            "VID_0416&PID_5409"=>DisplayFamily.Ly1,
            "VID_87AD&PID_70DB"=>DisplayFamily.ChiZhu,
            "VID_0416&PID_5302"=>DisplayFamily.HidType2,
            "VID_0418&PID_5303" or "VID_0418&PID_5304"=>DisplayFamily.HidType3,
            "VID_0416&PID_5406"=>DisplayFamily.Ali,
            "VID_0402&PID_3922" or "VID_87CD&PID_70DB"=>DisplayFamily.Scsi,
            _=>DisplayFamily.Unknown
        };
    }
    public static string Label(DisplayFamily family)=>family switch {
        DisplayFamily.Ly=>"Thermalright LY",DisplayFamily.Ly1=>"Thermalright LY1",
        DisplayFamily.ChiZhu=>"Thermalright ChiZhu",DisplayFamily.HidType2=>"Thermalright HID",
        DisplayFamily.HidType3=>"Thermalright HID F5",DisplayFamily.Ali=>"Thermalright ALi",
        DisplayFamily.Scsi=>"Thermalright SCSI",_=>"USB"
    };
    public static bool CanStream(DisplayFamily family,string service)=>family is DisplayFamily.Ly or DisplayFamily.Ly1 or DisplayFamily.ChiZhu or DisplayFamily.HidType2 || family==DisplayFamily.Ali&&service.Equals("WinUSB",StringComparison.OrdinalIgnoreCase);
}
