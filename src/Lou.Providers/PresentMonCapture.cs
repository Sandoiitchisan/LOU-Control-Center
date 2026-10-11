using System.Diagnostics;
using System.Globalization;
using Lou.Core;
namespace Lou.Providers;

public sealed class FpsWindow
{
    private sealed record Sample(double Time,double Ms,string Chain);
    private readonly Queue<Sample> samples=[];private string[] header=[];private DateTimeOffset received,first;
    private readonly object gate=new();
    public void Add(string line,int processId,DateTimeOffset now)
    {
        var cells=GamingProvider.Csv(line);lock(gate){
            if(cells.Any(c=>c.Equals("MsBetweenPresents",StringComparison.OrdinalIgnoreCase))){header=cells;return;}
            int Index(string key)=>Array.FindIndex(header,h=>h.Equals(key,StringComparison.OrdinalIgnoreCase));
            string Get(string key){int i=Index(key);return i>=0&&i<cells.Length?cells[i]:"";}
            bool Number(string value,out double n)=>double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&double.IsFinite(n);
            if(!int.TryParse(Get("ProcessID"),out int id)||id!=processId||!Number(Get("TimeInSeconds"),out double time)||!Number(Get("MsBetweenPresents"),out double ms)||ms<=0||ms>10000)return;
            if(first==default||now-received>TimeSpan.FromSeconds(3)){first=now;samples.Clear();}
            samples.Enqueue(new(time,ms,Get("SwapChainAddress")));received=now;
            while(samples.Count>4096||samples.Count>0&&samples.Peek().Time<time-2)samples.Dequeue();
        }
    }
    public (double? Fps,double? Ms) Read(DateTimeOffset now)
    {
        lock(gate){if(samples.Count<2||now-received>TimeSpan.FromSeconds(3))return(null,null);
            double latest=samples.Last().Time;var main=samples.Where(s=>s.Time>=latest-1.5).GroupBy(s=>s.Chain).OrderByDescending(g=>g.Count()).FirstOrDefault();
            if(main==null||main.Count()<2)return(null,null);double ms=main.Average(s=>s.Ms);return(1000/ms,ms);
        }
    }
    public double ObservedSeconds(DateTimeOffset now){lock(gate)return now-received>TimeSpan.FromSeconds(3)?0:(received-first).TotalSeconds;}
}
public sealed record PresentationActivity(int ProcessId,double? Fps,double? Ms,double ObservedSeconds);
public sealed class PresentationTracker
{
    private sealed record Entry(FpsWindow Window,DateTimeOffset Seen);
    private readonly object gate=new();private readonly Dictionary<int,Entry> entries=[];private string header="";private int pidColumn=-1;
    public void Add(string line,DateTimeOffset now)
    {
        var cells=GamingProvider.Csv(line);lock(gate){
            int pid=Array.FindIndex(cells,c=>c.Equals("ProcessID",StringComparison.OrdinalIgnoreCase));
            if(pid>=0){header=line;pidColumn=pid;entries.Clear();return;}
            if(pidColumn<0||pidColumn>=cells.Length||!int.TryParse(cells[pidColumn],out pid)||pid<=0)return;
            if(!entries.TryGetValue(pid,out var entry)){
                if(entries.Count>=64)entries.Remove(entries.MinBy(e=>e.Value.Seen).Key);
                var window=new FpsWindow();window.Add(header,pid,now);entry=new(window,now);
            }
            entry.Window.Add(line,pid,now);entries[pid]=entry with{Seen=now};
        }
    }
    public IReadOnlyList<PresentationActivity> Read(DateTimeOffset now)
    {
        lock(gate){foreach(int id in entries.Where(e=>now-e.Value.Seen>TimeSpan.FromSeconds(3)).Select(e=>e.Key).ToArray())entries.Remove(id);
            return entries.Select(e=>{var value=e.Value.Window.Read(now);return new PresentationActivity(e.Key,value.Fps,value.Ms,e.Value.Window.ObservedSeconds(now));}).Where(a=>a.Fps.HasValue).ToArray();}
    }
}
public sealed class PresentMonCapture : IAsyncDisposable
{
    private readonly string? fixedSession;
    private ChildProcessLifetime? lifetime;
    public PresentMonCapture(string? fixedSession=null){this.fixedSession=fixedSession;}
    private readonly string executable=Path.Combine(AppContext.BaseDirectory,"tools","PresentMon","PresentMon.exe");
    private Process? process;private Task? reader,errors;private PresentationTracker tracker=new();private string session="";private DateTime retryAfter;private volatile string status="Démarrage de la mesure…";
    public string Status=>status;
    public object? Diagnostics(){try{if(process==null||process.HasExited)return null;process.Refresh();return new{process.Id,CpuSeconds=process.TotalProcessorTime.TotalSeconds,MemoryMiB=process.WorkingSet64/1048576d};}catch{return null;}}
    public async Task<IReadOnlyList<PresentationActivity>> ReadAsync(bool enabled)
    {
        if(!enabled){await StopAsync();tracker=new();return [];}
        if(process?.HasExited==true){status=process.ExitCode==0?"Mesure interrompue":"Mesure FPS indisponible • vérifier les droits Windows";await StopAsync();retryAfter=DateTime.UtcNow.AddSeconds(30);}
        if(process==null&&DateTime.UtcNow>=retryAfter){
            if(!File.Exists(executable)){status="Composant PresentMon absent";return [];}
            session=fixedSession??("LouControlCenter-"+Environment.ProcessId+"-"+Guid.NewGuid().ToString("N"));tracker=new();status="Observation de l’activité graphique";
            try{var info=StartInfo();foreach(string arg in new[]{"--session_name",session,"--v1_metrics","--output_stdout","--no_console_stats","--no_track_input","--no_track_gpu","--no_track_display","--exclude","Lou.ControlCenter.exe","--exclude","dwm.exe"})info.ArgumentList.Add(arg);
                if(fixedSession!=null){info.ArgumentList.Add("--stop_existing_session");lifetime=new();}
                process=Process.Start(info)??throw new IOException();lifetime?.Add(process);var capture=process;var activeTracker=tracker;
                reader=Task.Run(async()=>{while(await capture.StandardOutput.ReadLineAsync() is {} line)activeTracker.Add(line,DateTimeOffset.UtcNow);});
                errors=Task.Run(async()=>{while(await capture.StandardError.ReadLineAsync() is {} line){if(line.Contains("error",StringComparison.OrdinalIgnoreCase)||line.Contains("denied",StringComparison.OrdinalIgnoreCase))status="Mesure FPS indisponible • vérifier les droits Windows";}});
            }catch{await StopAsync();status="Impossible de démarrer la mesure FPS";retryAfter=DateTime.UtcNow.AddSeconds(30);}
        }
        return tracker.Read(DateTimeOffset.UtcNow);
    }
    private ProcessStartInfo StartInfo()=>new(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    private async Task StopAsync()
    {
        if(process==null){lifetime?.Dispose();lifetime=null;return;}var owned=process;process=null;
        try{if(!owned.HasExited){var info=StartInfo();info.ArgumentList.Add("--session_name");info.ArgumentList.Add(session);info.ArgumentList.Add("--terminate_existing_session");using var stopper=Process.Start(info);if(stopper!=null){var output=stopper.StandardOutput.ReadToEndAsync();var error=stopper.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(3000);try{await stopper.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){stopper.Kill();}await Task.WhenAll(output,error);}
                using var deadline=new CancellationTokenSource(2000);try{await owned.WaitForExitAsync(deadline.Token);}catch(OperationCanceledException){owned.Kill();await owned.WaitForExitAsync();}}}
        catch{try{if(!owned.HasExited)owned.Kill();}catch{}}
        finally{lifetime?.Dispose();lifetime=null;if(reader!=null)try{await reader;}catch{}if(errors!=null)try{await errors;}catch{}owned.Dispose();reader=errors=null;}
    }
    public async ValueTask DisposeAsync()=>await StopAsync();
}
public sealed class GamingMonitor : IAsyncDisposable
{
    private readonly PresentMonCapture capture=new();private readonly GameDiscovery discovery=new();
    public IReadOnlyList<PresentationActivity> Activity {get;private set;}=[];
    public object? Diagnostics()=>capture.Diagnostics();
    public async Task<GamingData> ReadAsync(Settings settings)
    {
        var machine=ConfigStore.IsInstalled?MachinePresentations.Read():null;
        Activity=ConfigStore.IsInstalled?(!settings.Demo&&settings.AutomaticFps?machine?.Activity??[]:[]):await capture.ReadAsync(!settings.Demo&&settings.AutomaticFps);
        if(settings.Demo)return new();
        if(!settings.AutomaticFps)return new(FpsStatus:"Détection et mesure automatiques désactivées");
        var game=discovery.Find(settings,Activity);if(game==null)return new(FpsStatus:ConfigStore.IsInstalled?machine?.Status??"Collecte FPS installée indisponible":capture.Status);
        var data=GamingProvider.Read(settings,game);
        if(!string.IsNullOrWhiteSpace(settings.PresentMonCsv))return data;
        var measured=Activity.FirstOrDefault(a=>a.ProcessId==game.Id);
        return data with{Fps=measured?.Fps,Frametime=measured?.Ms,FpsStatus=measured?.Fps!=null?"Cadence de présentation • images générées non distinguées":"En attente des images du jeu"};
    }
    public ValueTask DisposeAsync()=>capture.DisposeAsync();
}
