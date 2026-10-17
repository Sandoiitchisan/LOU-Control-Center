using System.Runtime.InteropServices;
using Lou.Core;
namespace Lou.Providers;
// Read-only AMD ADL public API; loads the installed display driver's System32 DLL.
// No driver installation, clock setting, fan control, or tuning API is used.
public sealed class AmdProvider : IDisposable
{
    private IntPtr context;
    private int adapter=-1;
    private string name="";
    private string controller="",preferred="",deviceId="";
    public HardwareReading? FanSensor {get;private set;}
    public object[] AdapterInventory {get;private set;}=[];
    private bool attempted;
    private readonly Allocate allocate=Marshal.AllocHGlobal;
    public PcData Enrich(PcData pc,string preferredController="")
    {
        FanSensor=null;
        try{
            if(preferred!=preferredController){Dispose();attempted=false;adapter=-1;preferred=preferredController;}
            if(!attempted){attempted=true;Initialize();}if(context==IntPtr.Zero||adapter<0)return pc;
            var log=new LogData{Size=Marshal.SizeOf<LogData>(),Sensors=new Sensor[256]};bool ok=ADL2_New_QueryPMLogData_Get(context,adapter,ref log)==0;
            double? Value(int id,double max=double.MaxValue)=>ok&&log.Sensors[id].Supported!=0&&log.Sensors[id].Value>=0&&log.Sensors[id].Value<=max?log.Sensors[id].Value:null;
            double? temp=Value(8,150)??Value(28,150),hotspot=Value(27,150),load=Value(19,100),fan=Value(14,15000),power=Value(73,1000)??Value(23,1000);
            // Optional VRAM APIs vary by driver. Their failure must not discard valid PMLog readings.
            double? memory=null;
            try{if(ADL2_Adapter_DedicatedVRAMUsage_Get(context,adapter,out int mb)==0&&mb>=0)memory=mb/1024d;}
            catch(EntryPointNotFoundException){}catch(SEHException){}
            FanSensor=new(controller+"/pmlog/fan-rpm",name,"GpuAmd","GPU Fan RPM","Fan",fan,controller,controller,name,"AMD ADL",deviceId);
            return pc with{Gpu=load??pc.Gpu,GpuTemp=temp,GpuHotspot=hotspot,GpuFan=fan,GpuPower=power,Vram=memory,GpuName=name,GpuDeviceId=deviceId};
        }catch(DllNotFoundException){return pc;}catch(EntryPointNotFoundException){return pc;}catch(SEHException){return pc;}
    }
    private void Initialize()
    {
        if(ADL2_Main_Control_Create(allocate,1,ref context)!=0)return;int count=0;if(ADL2_Adapter_NumberOfAdapters_Get(context,ref count)!=0||count is <1 or >64)return;
        int size=Marshal.SizeOf<Adapter>();IntPtr buffer=Marshal.AllocHGlobal(size*count);
        try{
            for(int i=0;i<count;i++)Marshal.WriteInt32(buffer,i*size,size);
            if(ADL2_Adapter_AdapterInfo_Get(context,buffer,size*count)!=0)return;
            var adapters=Enumerable.Range(0,count).Select(i=>Marshal.PtrToStructure<Adapter>(buffer+i*size)).Where(a=>a.Present!=0).ToArray();
            AdapterInventory=adapters.Select(a=>(object)new{a.Name,a.Udid,a.Pnp,a.Bus,a.Device,a.Function,a.Vendor,ControllerId=Identity(a)}).ToArray();
            var candidates=adapters.Where(a=>preferred!=""?Identity(a)==preferred:a.Name.Contains("Radeon RX",StringComparison.OrdinalIgnoreCase)).GroupBy(Identity).Select(g=>g.OrderBy(a=>a.Pnp.Length).First()).ToArray();
            if(candidates.Length!=1||string.IsNullOrWhiteSpace(candidates[0].Udid)&&string.IsNullOrWhiteSpace(candidates[0].Pnp))return;
            var chosen=candidates[0];adapter=chosen.Index;name=chosen.Name.Trim();controller=Identity(chosen);deviceId=chosen.Pnp.Trim();
        }finally{Marshal.FreeHGlobal(buffer);}
    }
    // ADL exposes several outputs for one physical PCI function. Bind the PCI device, not its output index.
    public static string PhysicalControllerIdentity(string pnp,int bus,int device,int function)=>"/amd-adl/"+pnp.Trim().ToUpperInvariant().Split('\\').ElementAtOrDefault(1)+$"/pci/{bus}:{device}:{function}";
    public static string PreferredController(IEnumerable<FanBinding>? bindings)=>bindings?.FirstOrDefault(b=>b.Role==FanRole.Gpu&&b.Provider=="AMD ADL")?.ControllerId??"";
    private static string Identity(Adapter a)=>PhysicalControllerIdentity(a.Pnp,a.Bus,a.Device,a.Function);
    public void Dispose(){if(context!=IntPtr.Zero){ADL2_Main_Control_Destroy(context);context=IntPtr.Zero;}}
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Allocate(int size);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi)]private struct Adapter
    {
        public int Size,Index;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Udid;
        public int Bus,Device,Function,Vendor;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Display;public int Present,Exist;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Driver;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string DriverExt;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Pnp;public int OsDisplay;
    }
    [StructLayout(LayoutKind.Sequential)]private struct Sensor{public int Supported,Value;}
    [StructLayout(LayoutKind.Sequential)]private struct LogData{public int Size;[MarshalAs(UnmanagedType.ByValArray,SizeConst=256)]public Sensor[] Sensors;}
    [DllImport("atiadlxx.dll",CallingConvention=CallingConvention.Cdecl)][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int ADL2_Main_Control_Create(Allocate callback,int connected,ref IntPtr context);
    [DllImport("atiadlxx.dll",CallingConvention=CallingConvention.Cdecl)][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int ADL2_Main_Control_Destroy(IntPtr context);
    [DllImport("atiadlxx.dll",CallingConvention=CallingConvention.Cdecl)][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context,ref int count);
    [DllImport("atiadlxx.dll",CallingConvention=CallingConvention.Cdecl)][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context,IntPtr info,int size);
    [DllImport("atiadlxx.dll",CallingConvention=CallingConvention.Cdecl)][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context,int adapter,ref LogData data);
    [DllImport("atiadlxx.dll",CallingConvention=CallingConvention.Cdecl)][DefaultDllImportSearchPaths(DllImportSearchPath.System32)]private static extern int ADL2_Adapter_DedicatedVRAMUsage_Get(IntPtr context,int adapter,out int megabytes);
}
