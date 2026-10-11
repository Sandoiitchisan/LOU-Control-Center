using Lou.Core;
using Lou.Providers;
namespace Lou.App;
public sealed class DataHub:IAsyncDisposable
{
    private readonly PcProvider pc=new();private readonly WeatherProvider weather=new();
    private readonly GamingMonitor games=new();public MediaProvider Media {get;}=new();
    private readonly CancellationTokenSource stop=new();private readonly object gate=new();
    private Settings settings;private Snapshot snapshot=Snapshot.Empty;private Task[] tasks=[];
    public Snapshot Snapshot {get{lock(gate)return snapshot;}}
    public HardwareReading? GpuFanSensor=>pc.GpuFanSensor;
    public DataHub(Settings s){settings=ConfigStore.Clone(s);}
    public void Update(Settings s)=>Volatile.Write(ref settings,ConfigStore.Clone(s));
    private void Change(Func<Snapshot,Snapshot> change){lock(gate)snapshot=change(snapshot);}
    public void Start(){tasks=[Task.Run(()=>Loop(1000,()=>{var s=Volatile.Read(ref settings);var value=pc.Read(s.NetworkAdapter,s.FanBindings,s);Change(d=>d with{Pc=value});return Task.CompletedTask;})),Task.Run(()=>Loop(5000,async()=>{var value=await weather.ReadAsync(Volatile.Read(ref settings),stop.Token);Change(d=>d with{Weather=value});})),Task.Run(()=>Loop(1000,async()=>{var value=await Media.ReadAsync(stop.Token);Change(d=>d with{Media=value});})),Task.Run(()=>Loop(1000,async()=>{var value=await games.ReadAsync(Volatile.Read(ref settings));Change(d=>d with{Gaming=value});}))];}
    private async Task Loop(int delay,Func<Task> action){while(!stop.IsCancellationRequested){try{await action();await Task.Delay(delay,stop.Token);}catch(OperationCanceledException){break;}catch(Exception ex){DashboardHost.Log("source",ex.GetType().Name);try{await Task.Delay(delay,stop.Token);}catch(OperationCanceledException){break;}}}}
    public async ValueTask DisposeAsync(){stop.Cancel();await Task.WhenAll(tasks);await games.DisposeAsync();pc.Dispose();weather.Dispose();stop.Dispose();}
}
