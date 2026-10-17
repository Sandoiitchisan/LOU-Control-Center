using System.Security.Cryptography;
using System.Text;
using Lou.Core;
namespace Lou.Providers;

public static class SensorCatalog
{
    // Sorted hardware identities, never measurement values or enumeration positions.
    // An inventory change requires the user to confirm assignments again.
    public static string Topology(IEnumerable<HardwareReading> readings)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",readings.Select(r=>$"{r.Provider}|{r.Kind}|{r.ControllerId}|{r.Hardware}|{r.DeviceId}").Distinct().Order(StringComparer.Ordinal)))));
    public static bool Fresh(HardwareSnapshot? s,DateTimeOffset now)=>s!=null&&s.Readings!=null&&s.Readings.Length<=256&&now-s.Time<=TimeSpan.FromSeconds(5)&&s.Time<=now.AddSeconds(2);
    public static double? Temperature(HardwareSnapshot? s,SensorBinding? b,DateTimeOffset now)
    {
        if(!Fresh(s,now)||b==null||s!.Board!=b.Board||b.Topology!=""&&b.Topology!=s.Topology)return null;
        var found=s.Readings.Where(r=>r.Provider==b.Provider&&r.ControllerId==b.ControllerId&&r.Id==b.SensorId&&r.Type=="Temperature"&&(b.Hardware==""||r.Hardware==b.Hardware)).ToArray();
        return found.Length==1&&found[0].Value is {} v&&double.IsFinite(v)&&v is >=-30 and <=150?v:null;
    }
    public static PcData Enrich(PcData p,HardwareSnapshot? s,Settings config,DateTimeOffset now)
    {
        var rows=Fresh(s,now)?s!.Readings:[];
        var groups=rows.Where(r=>r.Kind.StartsWith("Gpu",StringComparison.Ordinal)).GroupBy(r=>(r.ControllerId,r.Hardware)).ToArray();
        var chosen=config.GpuController!=""?groups.SingleOrDefault(g=>g.Key.ControllerId==config.GpuController&&g.Key.Hardware==config.GpuHardware&&(config.GpuDeviceId==""||g.All(r=>r.DeviceId==config.GpuDeviceId))):groups.Length==1?groups[0]:null;
        if(chosen!=null){
            double? Value(string type,params string[] names){foreach(string name in names){var list=chosen.Where(r=>r.Type==type&&r.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();if(list.Length==1&&list[0].Value is {} v&&double.IsFinite(v)&&v>=0)return v;}return null;}
            // ADL and LibreHardwareMonitor expose the same AMD PnP instance string.
            // Keep current ADL readings only when every row belongs to that exact physical GPU.
            // A model name or PCI vendor/device pair cannot distinguish two identical cards.
            bool sameDevice=!string.IsNullOrWhiteSpace(p.GpuDeviceId)&&chosen.All(r=>string.Equals(r.DeviceId?.Trim(),p.GpuDeviceId.Trim(),StringComparison.OrdinalIgnoreCase));
            var identities=chosen.Select(r=>r.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            p=p with{Gpu=Value("Load","GPU Core")??(sameDevice?p.Gpu:null),GpuTemp=Value("Temperature","GPU Core","GPU Temperature")??(sameDevice?p.GpuTemp:null),GpuHotspot=Value("Temperature","GPU Hot Spot","GPU Hotspot")??(sameDevice?p.GpuHotspot:null),GpuPower=Value("Power","GPU Package","GPU Power")??(sameDevice?p.GpuPower:null),Vram=Value("SmallData","GPU Memory Used","GPU Memory Dedicated Used")/1024??(sameDevice?p.Vram:null),GpuName=chosen.Key.Hardware,GpuDeviceId=identities.Length==1?identities[0]:""};
            if(!config.FanBindings.Any(b=>b.Role==FanRole.Gpu)){
                var gpuFans=chosen.Where(r=>r.Type=="Fan").ToArray();
                if(gpuFans.Length==1&&gpuFans[0].Value is {} rpm&&double.IsFinite(rpm)&&rpm is >=0 and <=15000){var f=gpuFans[0];p=p with{GpuFan=rpm,Fans=p.Fans?.Select(r=>r.Role==FanRole.Gpu?r with{Rpm=rpm,Status="Détection GPU automatique",SensorId=f.Id,ControllerId=f.ControllerId,Provider=f.Provider}:r).ToArray()};}
            }
        }else if(config.GpuController!=""||groups.Length>1)p=p with{Gpu=null,GpuTemp=null,GpuHotspot=null,GpuPower=null,Vram=null,GpuDeviceId="",GpuName=config.GpuController!=""?"Carte sélectionnée indisponible":"Choisissez la carte graphique"};
        if(config.CpuTemperature!=null)p=p with{CpuTemp=Temperature(s,config.CpuTemperature,now)};
        if(config.GpuTemperature!=null)p=p with{GpuTemp=Temperature(s,config.GpuTemperature,now)};
        // GPU fan bindings already use the exact selected measurement in EnrichMapped.
        var fans=(p.Fans??[]).Select(f=>f with{Label=config.FanLabels.GetValueOrDefault(f.Role,CoolingLabels.Label(f.Role))}).ToArray();
        return p with{Fans=fans};
    }
}

