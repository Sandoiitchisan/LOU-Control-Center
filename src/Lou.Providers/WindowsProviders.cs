using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Lou.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;
namespace Lou.Providers;

public sealed class PcProvider : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Memory { public uint Length,Load; public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref Memory m);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
    private ulong idle,kernel,user;
    private long rx,tx;
    private string adapter="";
    private long sampled;
    private PerformanceCounter[] gpu=[];
    private int ticks;
    private readonly AmdProvider amd=new();
    public HardwareReading? GpuFanSensor=>amd.FanSensor;
    public object[] GpuControllers=>amd.AdapterInventory;
    public PcData Read(string preferred,IReadOnlyList<FanBinding>? bindings=null,Settings? configuration=null)
    {
        double? cpu=null,load=null;
        if(GetSystemTimes(out var i,out var k,out var u)) { var total=(k-kernel)+(u-user); if(kernel>0&&total>0) cpu=Math.Clamp(100.0*(total-(i-idle))/total,0,100);idle=i;kernel=k;user=u; }
        var mem=new Memory{Length=(uint)Marshal.SizeOf<Memory>()};var ok=GlobalMemoryStatusEx(ref mem);
        try {
            if(ticks++%30==0){foreach(var c in gpu)c.Dispose();var cat=new PerformanceCounterCategory("GPU Engine");gpu=cat.GetInstanceNames().Where(n=>n.EndsWith("engtype_3D",StringComparison.OrdinalIgnoreCase)).Take(300).Select(n=>new PerformanceCounter("GPU Engine","Utilization Percentage",n,true)).ToArray();foreach(var c in gpu)c.NextValue();}
            else if(gpu.Length>0)load=Math.Clamp(gpu.Sum(c=>(double)c.NextValue()),0,100);
        }catch { load=null; }
        double? down=null,up=null;string name="Indisponible";
        try {
            var adapters=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel).ToArray();
            var nic=adapters.FirstOrDefault(n=>n.Id==preferred||n.Name==preferred)??adapters.OrderByDescending(n=>n.Speed).FirstOrDefault();
            if(nic!=null){var stat=nic.GetIPv4Statistics();long now=Stopwatch.GetTimestamp();double elapsed=(now-sampled)/(double)Stopwatch.Frequency;
                if(adapter==nic.Id&&sampled>0&&elapsed>0&&stat.BytesReceived>=rx&&stat.BytesSent>=tx){down=NetworkMegabytesPerSecond(stat.BytesReceived-rx,elapsed);up=NetworkMegabytesPerSecond(stat.BytesSent-tx,elapsed);}
                rx=stat.BytesReceived;tx=stat.BytesSent;sampled=now;adapter=nic.Id;name=nic.Name;
            }
        }catch{}
        var data=amd.Enrich(new(Cpu:cpu,Gpu:load,RamUsed:ok?(mem.Total-mem.Available)/1073741824d:null,RamTotal:ok?mem.Total/1073741824d:null,Download:down,Upload:up,Adapter:name),AmdProvider.PreferredController(bindings));
        var hardware=HardwareSensors.Read();
        data=HardwareSensors.EnrichMapped(data,hardware,DateTimeOffset.UtcNow,bindings??[],amd.FanSensor);
        return configuration==null?data:SensorCatalog.Enrich(data,hardware,configuration,DateTimeOffset.UtcNow);
    }
    public static double NetworkMegabytesPerSecond(long bytes,double seconds)=>bytes/seconds/1e6;
    public void Dispose(){foreach(var c in gpu)c.Dispose();amd.Dispose();}
}

public sealed class MediaProvider
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private string coverKey="";
    private byte[]? cover;
    public async Task<MediaData> ReadAsync(CancellationToken ct)
    {
        try {
            manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(ct);
            session=manager.GetCurrentSession();if(session==null){cover=null;coverKey="";return new();}
            var props=await session.TryGetMediaPropertiesAsync().AsTask(ct);
            var info=session.GetPlaybackInfo();var time=session.GetTimelineProperties();
            string key=session.SourceAppUserModelId+props.Title+props.Artist+props.AlbumTitle;
            if(key!=coverKey){cover=null;coverKey=key;if(props.Thumbnail!=null){using var stream=await props.Thumbnail.OpenReadAsync().AsTask(ct);if(stream.Size<=8*1024*1024){using var reader=new DataReader(stream);await reader.LoadAsync((uint)stream.Size).AsTask(ct);cover=new byte[(int)stream.Size];reader.ReadBytes(cover);}}}
            var playing=info.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            double pos=time.Position.TotalSeconds;
            if(playing)pos+=Math.Clamp((DateTimeOffset.Now-time.LastUpdatedTime).TotalSeconds,0,5);
            var duration=Math.Max(0,time.EndTime.TotalSeconds-time.StartTime.TotalSeconds);
            return new(props.Title,props.Artist,props.AlbumTitle,playing,Math.Clamp(pos,0,Math.Max(duration,0)),duration,cover,session.SourceAppUserModelId,true);
        }catch(OperationCanceledException){throw;}catch{return new(Title:"Session indisponible",Artist:"Aucun lecteur Windows accessible");}
    }
    public async Task<bool> ControlAsync(string command)
    {
        if(session==null)return false;
        return command switch {"play"=>await session.TryTogglePlayPauseAsync(),"next"=>await session.TrySkipNextAsync(),"previous"=>await session.TrySkipPreviousAsync(),_=>false};
    }
}

public static class GamingProvider
{
    public static GamingData Read(Settings settings,GameProcess? detected=null)
    {
        if(detected==null)return new();string game=detected.Executable;
        var result=new GamingData(detected.Name,true,Resolution:GameDiscovery.WindowResolution(detected.Window),ProcessId:detected.Id,Executable:detected.Executable);
        if(string.IsNullOrWhiteSpace(settings.PresentMonCsv))return result;
        try {
            var file=new FileInfo(settings.PresentMonCsv);if(!file.Exists||DateTime.UtcNow-file.LastWriteTimeUtc>TimeSpan.FromSeconds(5))return result with{FpsStatus="Capture PresentMon périmée"};
            using var f=new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            using var reader=new StreamReader(f);var header=reader.ReadLine();if(header==null)return result;
            if(f.Length>65536){f.Seek(-65536,SeekOrigin.End);reader.DiscardBufferedData();reader.ReadLine();}
            var lines=new List<string>();while(reader.ReadLine() is {} l)lines.Add(l);
            var names=Csv(header);int app=Array.FindIndex(names,n=>n.Equals("Application",StringComparison.OrdinalIgnoreCase)), ms=Array.FindIndex(names,n=>n.Equals("MsBetweenPresents",StringComparison.OrdinalIgnoreCase));
            if(ms<0)return result with{FpsStatus="CSV : MsBetweenPresents absent"};
            var samples=lines.Select(Csv).Where(r=>r.Length>ms&&app>=0&&r.Length>app&&Path.GetFileNameWithoutExtension(r[app]).Equals(game,StringComparison.OrdinalIgnoreCase)).TakeLast(120)
                .Select(r=>double.TryParse(r[ms],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var v)?v:0).Where(v=>double.IsFinite(v)&&v>0&&v<1000).ToArray();
            return samples.Length>0?result with{Fps=1000/samples.Average(),Frametime=samples.Average(),FpsStatus="Capture externe • cadence de présentation"}:result;
        }catch{return result with{FpsStatus="Capture PresentMon inaccessible"};}
    }
    public static string[] Csv(string line)
    {
        var values=new List<string>();var cell=new System.Text.StringBuilder();bool quoted=false;
        for(int i=0;i<line.Length;i++){char c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}else if(c==','&&!quoted){values.Add(cell.ToString());cell.Clear();}else cell.Append(c);}
        values.Add(cell.ToString());return values.ToArray();
    }
}

