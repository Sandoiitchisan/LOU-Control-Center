using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;
using Lou.Core;
namespace Lou.Providers;

public sealed record HardwareReading(string Id,string Hardware,string Kind,string Name,string Type,double? Value,string ControllerId="",string ParentId="",string ParentName="",string Provider="LibreHardwareMonitor",string DeviceId="");
public sealed record HardwareSnapshot(DateTimeOffset Time,string Status,HardwareReading[] Readings,string? Board=null,string LibraryVersion="",string Topology="");
public static class HardwareSensors
{
    public static string SnapshotPath=>Path.Combine(ConfigStore.Root,"hardware-sensors.json");
    public static string MachineSnapshotPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"LouControlCenterUniversal","Sensors","hardware-sensors.json");
    public static bool IsAdministrator=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public static async Task CollectAsync(int parentId,int seconds=0,bool machine=false)
    {
        string output=machine?MachineSnapshotPath:SnapshotPath;
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var unique=new Mutex(true,machine?"Global\\Lou.ControlCenter.UniversalMachineSensors":"Local\\Lou.ControlCenter.UniversalSensors",out bool first);if(!first)return;
        string? board=null;
        void Write(string status,List<HardwareReading> values){string data=JsonSerializer.Serialize(new HardwareSnapshot(DateTimeOffset.UtcNow,status,values.ToArray(),board,typeof(Computer).Assembly.GetName().Version?.ToString()??"",SensorCatalog.Topology(values)),ConfigStore.Json);
            // A concurrent reader or antivirus must not terminate the sensor service.
            for(int attempt=0;attempt<3;attempt++)try{File.WriteAllText(output+".tmp",data);File.Move(output+".tmp",output,true);return;}catch(IOException){Thread.Sleep(25);}catch(UnauthorizedAccessException){Thread.Sleep(25);}
        }
        if(!IsAdministrator){Write("Autorisation Windows nécessaire",[]);return;}
        if(!LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled){Write("Composant PawnIO absent",[]);return;}
        var watch=Stopwatch.StartNew();DateTime parentStart=default;
        if(parentId>0){try{using var parent=Process.GetProcessById(parentId);parentStart=parent.StartTime;}catch{return;}}
        while(seconds==0||watch.Elapsed.TotalSeconds<seconds){
        var computer=new Computer(new ReadOnlyHardwareSettings()){IsCpuEnabled=true,IsMotherboardEnabled=true,IsGpuEnabled=true};
        try{
            computer.Open();
            board=computer.Hardware.FirstOrDefault(h=>h.HardwareType==HardwareType.Motherboard)?.Name;
            while(seconds==0||watch.Elapsed.TotalSeconds<seconds){
                if(parentId>0){try{using var parent=Process.GetProcessById(parentId);if(parent.HasExited||parent.StartTime!=parentStart)break;}catch{break;}}
                var readings=new List<HardwareReading>();
                void Read(IHardware h,IHardware? parent=null){h.Update();foreach(var sensor in h.Sensors.Where(s=>s.SensorType is SensorType.Temperature or SensorType.Fan || h.HardwareType.ToString().StartsWith("Gpu") && s.SensorType is SensorType.Load or SensorType.SmallData or SensorType.Power)){
                    if(readings.Count<256)readings.Add(new(sensor.Identifier.ToString(),h.Name,h.HardwareType.ToString(),sensor.Name,sensor.SensorType.ToString(),sensor.Value is {} value&&float.IsFinite(value)?value:null,h.Identifier.ToString(),parent?.Identifier.ToString()??"",parent?.Name??"",DeviceId:h is LibreHardwareMonitor.Hardware.Gpu.GenericGpu gpu?gpu.DeviceId??"":""));
                }foreach(var child in h.SubHardware)Read(child,h);}
                foreach(var h in computer.Hardware)Read(h);
                Write("Lecture active",readings);await Task.Delay(1000);
            }
            return;
        }catch(Exception ex){Write("Capteurs indisponibles : "+ex.GetType().Name+" • nouvelle tentative",[]);try{File.WriteAllText(output+".error.txt",ex.ToString());}catch{}}
        finally{try{computer.Close();}catch{}}
        await Task.Delay(10000);
        }
    }
    public static HardwareSnapshot? Read()
    {
        HardwareSnapshot? latest=null;
        foreach(string path in ConfigStore.IsInstalled?new[]{MachineSnapshotPath}:new[]{SnapshotPath})try{var file=new FileInfo(path);if(!file.Exists||file.Length>128000)continue;
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            var data=JsonSerializer.Deserialize<HardwareSnapshot>(stream,ConfigStore.Json);
            if(data!=null&&data.Time<=DateTimeOffset.UtcNow.AddSeconds(2)&&(latest==null||data.Time>latest.Time))latest=data;
        }catch{}
        return latest;
    }
    public static PcData EnrichMapped(PcData pc,HardwareSnapshot? data,DateTimeOffset now,IReadOnlyList<FanBinding> bindings,HardwareReading? gpu=null)
    {
        bool fresh=data!=null&&data.Readings!=null&&data.Readings.Length<=256&&now-data.Time<=TimeSpan.FromSeconds(5)&&data.Time<=now.AddSeconds(2);
        var rows=new List<FanReading>();
        foreach(var role in Enum.GetValues<FanRole>()){
            var binding=bindings.SingleOrDefault(b=>b.Role==role);double? rpm=null;string state="À associer";
            if(binding!=null){
                var readings=binding.Provider=="AMD ADL"?(gpu==null?[]:new[]{gpu}):fresh?data!.Readings!:[];
                string board=binding.Provider=="AMD ADL"?gpu?.ParentName??"":data?.Board??"";
                var matches=readings.Where(r=>r.Provider==binding.Provider&&r.Type=="Fan"&&r.Id==binding.SensorId&&r.ControllerId==binding.ControllerId&&board==binding.Board&&(binding.Hardware==""||r.Hardware==binding.Hardware)&&(binding.Topology==""||binding.Topology==data?.Topology)&&(role==FanRole.Gpu?r.Kind.StartsWith("Gpu",StringComparison.Ordinal):!r.Kind.StartsWith("Gpu",StringComparison.Ordinal))).ToArray();
                state=matches.Length==1?"Mesure indisponible":matches.Length>1?"Identité ambiguë":!fresh&&role!=FanRole.Gpu?"Collecte indisponible":"Capteur absent ou matériel différent";
                if(matches.Length==1&&matches[0].Value is {} v&&double.IsFinite(v)&&v is >=0 and <=15000){rpm=v;state=v==0?"À l’arrêt":"Lecture active";}
            }
            rows.Add(new(role,CoolingLabels.Label(role),binding?.Connector??"",rpm,state,binding?.SensorId??"",binding?.ControllerId??"",binding?.Provider??""));
        }
        double? Value(FanRole r)=>rows.Single(f=>f.Role==r).Rpm;
        double? temp=fresh?data!.Readings!.FirstOrDefault(r=>r.Kind=="Cpu"&&r.Type=="Temperature"&&r.Name is "Core (Tctl/Tdie)" or "CPU Package"&&r.Value is >=0 and <=125)?.Value:null;
        return pc with{CpuTemp=temp,CpuFan=Value(FanRole.Radiator),Pump=Value(FanRole.Pump),RearFan=Value(FanRole.Rear),BottomFan=Value(FanRole.Bottom),GpuFan=Value(FanRole.Gpu),Fans=rows.ToArray(),CoolingStatus=fresh?data!.Status:"Collecteur arrêté ou données périmées"};
    }
}


