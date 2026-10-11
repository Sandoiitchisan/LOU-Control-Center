using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Lou.App;

internal static class WindowAppearance
{
    // Native caption, resize and snap behavior remain managed by Windows.
    // https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
    public static void Attach(Window window) => window.SourceInitialized += (_, _) =>
    {
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))return;
        var handle=new WindowInteropHelper(window).Handle;
        int dark=1,caption=0x001D1610,text=0x00F7F5F2;
        _=DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
        _=DwmSetWindowAttribute(handle,35,ref caption,sizeof(int));
        _=DwmSetWindowAttribute(handle,36,ref text,sizeof(int));
    };
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
