using System.IO;
using System.Text.Json;
using System.Windows;
using Lou.Core;
using Lou.Providers;
using Lou.Rendering;
using SkiaSharp;
using MessageBox=System.Windows.MessageBox;
namespace Lou.App;
public partial class App:System.Windows.Application
{
    private Mutex? lease;
    private EventWaitHandle? exitSignal;
    private RegisteredWaitHandle? exitWait;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);Localizer.Preferred=Localizer.SystemLanguage;
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        if(e.Args.FirstOrDefault()=="--rgb-host"){try{await Lou.Rgb.OpenRgbEngine.RunHostAsync(e.Args);Shutdown();}catch{Shutdown(1);}return;}
        try{
            if(e.Args.Contains("--shutdown")){try{using var signal=EventWaitHandle.OpenExisting("Local\\Lou.ControlCenter.UniversalExit");signal.Set();}catch(WaitHandleCannotBeOpenedException){}Shutdown();return;}
            if(e.Args.Contains("--sensor-collector")){
                int at=Array.IndexOf(e.Args,"--parent-pid");bool machine=e.Args.Contains("--machine");
                if(machine&&(!ConfigStore.IsInstalled||!HardwareSensors.IsAdministrator))throw new InvalidOperationException(Localizer.T("La collecte système nécessite la version installée et les droits administrateur."));
                using var collectorLease=new Mutex(true,machine?"Global\\Lou.ControlCenter.UniversalCollector":"Local\\Lou.ControlCenter.UniversalCollector",out bool collectorFirst);
                if(!collectorFirst){Shutdown();return;}
                using var stop=new CancellationTokenSource();var presentations=machine?MachinePresentations.CollectAsync(stop.Token):Task.CompletedTask;
                try{await HardwareSensors.CollectAsync(at>=0&&at+1<e.Args.Length?int.Parse(e.Args[at+1]):0,machine:machine);}finally{stop.Cancel();await presentations;}
                Shutdown();return;
            }
            if(e.Args.Contains("--verify-rgb")){
                Directory.CreateDirectory(ConfigStore.Root);
                using var rgb=new Lou.Rgb.OpenRgbEngine();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(100));
                if(!e.Args.Contains("--existing-lou-rgb"))await Task.Run(()=>rgb.StartAsync(e.Args.Contains("--rgb-admin"),stop.Token));
                var inventory=await new Lou.Rgb.OpenRgbClient().DiscoverAsync(stop.Token);
                if(e.Args.Contains("--apply-rgb-test")){
                    var path=Path.Combine(ConfigStore.Root,"rgb-test-selection.json");
                    if(new FileInfo(path).Length>16384)throw new InvalidDataException("RGB test selection too large.");
                    var selection=JsonSerializer.Deserialize<Lou.Rgb.RgbAssignment>(File.ReadAllText(path))??throw new InvalidDataException("RGB test selection missing.");
                    if(e.Args.Contains("--case-lighting")){
                        var board=Lou.Rgb.RgbIdentity.Resolve(inventory,selection);
                        if(board.Type!=0)throw new InvalidDataException("Case test requires a motherboard controller.");
                        selection.CaseLighting=true;selection.Topology=Lou.Rgb.RgbCaseLighting.Fingerprint(board);
                    }
                    var rgbClient=new Lou.Rgb.OpenRgbClient();
                    if(e.Args.Contains("--animate-rgb-test")){
                        var results=new List<object>();
                        foreach(string effect in new[]{"breathing","rainbow","wave","comet"}){
                            selection.Effect=effect;
                            using var duration=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                            var timer=new System.Diagnostics.Stopwatch();long before=rgbClient.AnimationFrames;
                            double commandBefore=rgbClient.AnimationCommandMilliseconds,checkBefore=rgbClient.AnimationCheckMilliseconds;
                            try{await rgbClient.AnimateAsync(selection,()=>{timer.Start();duration.CancelAfter(TimeSpan.FromSeconds(10));},duration.Token);}
                            catch(OperationCanceledException) when(duration.IsCancellationRequested&&!stop.IsCancellationRequested){}
                            timer.Stop();results.Add(new{Effect=effect,Frames=rgbClient.AnimationFrames-before,Seconds=timer.Elapsed.TotalSeconds,CommandMs=rgbClient.AnimationCommandMilliseconds-commandBefore,CheckMs=rgbClient.AnimationCheckMilliseconds-checkBefore});
                        }
                        selection.Effect="";
                        selection.CaseLedLimit=0;
                        File.WriteAllText(Path.Combine(ConfigStore.Root,"rgb-effects-test.json"),JsonSerializer.Serialize(new{Time=DateTimeOffset.UtcNow,Pid=Environment.ProcessId,Results=results},new JsonSerializerOptions{WriteIndented=true}));
                    }
                    await rgbClient.ApplyAsync(selection,stop.Token);
                    inventory=await new Lou.Rgb.OpenRgbClient().DiscoverAsync(stop.Token);
                }
                File.WriteAllText(Path.Combine(ConfigStore.Root,"rgb-inventory.json"),JsonSerializer.Serialize(new{Time=DateTimeOffset.UtcNow,Pid=Environment.ProcessId,Version=typeof(App).Assembly.GetName().Version?.ToString(),Devices=inventory},new JsonSerializerOptions{WriteIndented=true}));
                if(e.Args.Contains("--hold-rgb-test"))await Task.Delay(TimeSpan.FromSeconds(30),stop.Token);
                Shutdown();return;
            }
            if(e.Args.Contains("--export")){Export(e.Args.Contains("--english")?AppLanguage.English:AppLanguage.French);Shutdown();return;}
            bool preview=e.Args.Contains("--preview")||e.Args.Contains("--review-ui");
            // Same mutex as the personal app: one LCD writer, regardless of edition.
            lease=new Mutex(true,preview?"Local\\Lou.ControlCenter.UniversalPreview":"Local\\Lou.ControlCenter.App",out bool first);
            if(!first){if(!e.Args.Contains("--tray"))MessageBox.Show(Localizer.T("LOU Control Center est déjà ouvert. Quittez-le depuis la zone de notification avant de lancer cette version."),"LOU Control Center");Shutdown();return;}
            Directory.CreateDirectory(ConfigStore.Root);
            Settings settings;try{settings=ConfigStore.Load();}catch(Exception ex){MessageBox.Show(Localizer.F($"Le fichier de réglages est illisible. Il est conservé ; restaurez settings.json.bak dans {ConfigStore.Root} avant de relancer.\n\n{ex.Message}"),"LOU Control Center");Shutdown();return;}
            if(preview&&e.Args.Contains("--english"))settings.Language=AppLanguage.English;
            var window=new MainWindow(settings,preview);MainWindow=window;window.Show();
            if(!preview){exitSignal=new EventWaitHandle(false,EventResetMode.ManualReset,"Local\\Lou.ControlCenter.UniversalExit");exitSignal.Reset();exitWait=ThreadPool.RegisterWaitForSingleObject(exitSignal,(_,_)=>Dispatcher.BeginInvoke(async()=>{await window.ShutdownHost();Shutdown();}),null,Timeout.Infinite,true);}
            if(e.Args.Contains("--tray")&&!preview)window.Hide();
            if(e.Args.Contains("--review-ui")){await Task.Delay(6000);await window.CaptureReview();await window.ShutdownHost();Shutdown();}
            if(e.Args.Contains("--verify-runtime")){await Task.Delay(7000);await window.VerifyRuntime();await window.ShutdownHost();Shutdown();}
        }catch(Exception ex){try{Directory.CreateDirectory(ConfigStore.Root);File.WriteAllText(Path.Combine(ConfigStore.Root,"startup-error.txt"),ex.ToString());}catch{}if(!e.Args.Contains("--sensor-collector")&&!e.Args.Contains("--review-ui")&&!e.Args.Contains("--export")&&!e.Args.Contains("--verify-rgb"))MessageBox.Show(ex.Message,"LOU Control Center");Shutdown(1);}
    }
    private static void Export(AppLanguage language){string folder=Path.Combine(ConfigStore.IsInstalled?ConfigStore.Root:AppContext.BaseDirectory,"Review");Directory.CreateDirectory(folder);using var renderer=new UniversalRenderer(Path.Combine(AppContext.BaseDirectory,"assets"));foreach(var size in new[]{(1920,462),(480,480),(320,240),(462,1920),(1280,480),(1600,720),(854,480),(960,320),(360,360),(240,320),(640,172)})foreach(var page in Enum.GetValues<PageId>())foreach(bool empty in new[]{false,true}){var s=new Settings{Width=size.Item1,Height=size.Item2,Night=false,Language=language};using var b=renderer.Render(page,empty?Snapshot.Empty:Snapshot.Simulation(),s,DateTime.Now,5);using var png=b.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(folder,$"{page}-{size.Item1}x{size.Item2}-{(empty?"empty":"demo")}.png"));png.SaveTo(file);}}
    protected override void OnExit(ExitEventArgs e){exitWait?.Unregister(null);exitSignal?.Dispose();lease?.Dispose();base.OnExit(e);}
}

