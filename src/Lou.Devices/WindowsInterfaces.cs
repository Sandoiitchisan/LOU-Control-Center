using System.Runtime.InteropServices;
using System.Text;
namespace Lou.Devices;

public sealed record DisplayInterface(string Path,string RootInstance);
public static class WindowsInterfaces
{
    public static readonly Guid HidClass=new("4D1E55B2-F16F-11CF-88CB-001111000030");
    public static DisplayInterface[] Enumerate(Guid guid)
    {
        var paths=new List<DisplayInterface>();
        var set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,18);
        if(set==new IntPtr(-1))return [];
        try{
            for(uint i=0;i<1024;i++){
                var face=new InterfaceData{Size=Marshal.SizeOf<InterfaceData>()};
                if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref face))break;
                var node=new DevInfo{Size=Marshal.SizeOf<DevInfo>()};
                SetupDiGetDeviceInterfaceDetail(set,ref face,IntPtr.Zero,0,out int required,ref node);
                if(required is <8 or >16384)continue;
                var buffer=Marshal.AllocHGlobal(required);
                try{
                    Marshal.WriteInt32(buffer,IntPtr.Size==8?8:6);
                    if(!SetupDiGetDeviceInterfaceDetail(set,ref face,buffer,required,out _,ref node))continue;
                    string path=Marshal.PtrToStringUni(buffer+4)??"";
                    uint current=node.DevInst;
                    for(int depth=0;depth<16;depth++){
                        var id=new StringBuilder(1024);
                        if(CM_Get_Device_ID(current,id,id.Capacity,0)!=0)break;
                        if(DisplayFamilies.Identify(id.ToString())!=DisplayFamily.Unknown){paths.Add(new(path,id.ToString()));break;}
                        if(CM_Get_Parent(out uint parent,current,0)!=0||parent==current)break;
                        current=parent;
                    }
                }finally{Marshal.FreeHGlobal(buffer);}
            }
        }finally{SetupDiDestroyDeviceInfoList(set);}
        return paths.ToArray();
    }
    // Exact ancestor identity prevents selecting the first of two identical USB devices.
    public static string[] ForDevice(IEnumerable<DisplayInterface> paths,string root)=>paths.Where(p=>p.RootInstance.Equals(root,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    [StructLayout(LayoutKind.Sequential)]private struct DevInfo{public int Size;public Guid Class;public uint DevInst;public IntPtr Reserved;}
    [StructLayout(LayoutKind.Sequential)]private struct InterfaceData{public int Size;public Guid Class;public uint Flags;public IntPtr Reserved;}
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string? enumerator,IntPtr parent,uint flags);
    [DllImport("setupapi.dll")]private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref InterfaceData data);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode)]private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,int size,out int required,ref DevInfo node);
    [DllImport("setupapi.dll")]private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]private static extern uint CM_Get_Device_ID(uint node,StringBuilder id,int length,uint flags);
    [DllImport("cfgmgr32.dll")]private static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
}
