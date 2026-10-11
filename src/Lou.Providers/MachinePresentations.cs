using System.Text.Json;
using Lou.Core;
namespace Lou.Providers;

public sealed record MachinePresentationSnapshot(DateTimeOffset Time,string Status,PresentationActivity[] Activity);
// Fixed, read-only output. No user-supplied executable, path or command is accepted by SYSTEM.
public static class MachinePresentations
{
    public const string Session="LouControlCenterUniversal-Machine";
    public static string SnapshotPath=>Path.Combine(Path.GetDirectoryName(HardwareSensors.MachineSnapshotPath)!,"presentations.json");
    public static MachinePresentationSnapshot? Read(){try{
        var file=new FileInfo(SnapshotPath);if(!file.Exists||file.Length>64000)return null;
        using var stream=new FileStream(SnapshotPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var data=JsonSerializer.Deserialize<MachinePresentationSnapshot>(stream,ConfigStore.Json);
        return Valid(data,DateTimeOffset.UtcNow)?data:null;
    }catch{return null;}}
    public static bool Valid(MachinePresentationSnapshot? data,DateTimeOffset now)=>data!=null&&data.Time<=now.AddSeconds(2)&&now-data.Time<=TimeSpan.FromSeconds(5)&&data.Activity is {Length:<=64}&&data.Activity.All(a=>a.ProcessId>0&&a.Fps is {} fps&&double.IsFinite(fps)&&fps>0&&a.Ms is {} ms&&double.IsFinite(ms)&&ms>0&&double.IsFinite(a.ObservedSeconds)&&a.ObservedSeconds>=0);
    public static async Task CollectAsync(CancellationToken stop){
        await using var capture=new PresentMonCapture(Session);
        while(!stop.IsCancellationRequested){try{
            var activity=await capture.ReadAsync(true);
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            File.WriteAllText(SnapshotPath+".tmp",JsonSerializer.Serialize(new MachinePresentationSnapshot(DateTimeOffset.UtcNow,capture.Status,activity.ToArray()),ConfigStore.Json));
            File.Move(SnapshotPath+".tmp",SnapshotPath,true);
        }catch(IOException){}catch(UnauthorizedAccessException){}
        try{await Task.Delay(1000,stop);}catch(OperationCanceledException){break;}}
    }
}
