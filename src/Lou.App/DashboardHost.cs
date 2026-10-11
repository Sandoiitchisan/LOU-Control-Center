using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lou.Core;
using Lou.Devices;
using Lou.Providers;
using Lou.Rendering;
using SkiaSharp;
namespace Lou.App;

public sealed class DashboardHost : IAsyncDisposable
{
    private readonly CancellationTokenSource stop=new();
    private readonly Func<Snapshot> source;


    private readonly UniversalRenderer renderer=new(System.IO.Path.Combine(AppContext.BaseDirectory,"assets"));
    private readonly PageScheduler scheduler=new();
    private readonly object deviceGate=new(),frameGate=new();
    private DisplaySettings display;
    private readonly bool previewOnly;
    public string Status {get;private set;}="Connexion en cours";
    public bool Calibration {get;set;}
    public event Action<ConfirmedDisplay>? ProfileDetected;
    private Settings settings;
    private LcdSession? device;
    private volatile bool streaming;
    private bool reconnectWanted;
    private bool suspended;
    private double reconnectAt;
    private ConfirmedDisplay? approved;
    private UsbDevice? selected;
    private Task[] tasks=[];
    private SKBitmap? previous;
    private SKBitmap? latest;
    private long latestSequence,lastSentSequence;
    private sealed record EncodedFrame(byte[] Bytes,long Sequence,PanelProfile Profile);
    private EncodedFrame? latestEncoded;
    private volatile PanelProfile? encodingProfile;
    private double transitionStart;
    private PageId renderedPage=PageId.System;
    private readonly Stopwatch elapsed=Stopwatch.StartNew();
    private double lastUsb;
    public Settings Settings=>Volatile.Read(ref settings);

    public long RenderedFrames=>Interlocked.Read(ref latestSequence);
    public Snapshot Snapshot=>source();
    public PageId CurrentPage{get;private set;}
    public double RenderMs{get;private set;}
    public double EncodeMs{get;private set;}
    public double UsbMs{get;private set;}
    public double UsbWriteMs{get;private set;}
    public double UsbAckMs{get;private set;}
    public int JpegBytes{get;private set;}
    public double UsbActualFps{get;private set;}
    public long Frames{get;private set;}
    public long UniqueFrames{get;private set;}
    public double UniqueFps{get;private set;}
    public double TransmittedFps{get;private set;}
    public bool IsStreaming=>streaming;
    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? Error;
    public DashboardHost(Settings global,DisplaySettings display,Func<Snapshot> source,bool previewOnly=false){this.source=source;this.display=ConfigStore.Clone(display);this.previewOnly=previewOnly;displayWasProfiled=display.Profile!=null;settings=global.ForDisplay(display);approved=display.Profile;reconnectWanted=display.Enabled&&!previewOnly;}
    public void Update(Settings global,DisplaySettings next){next.ValidatePlayback();lock(deviceGate){if(display.Rotation!=next.Rotation||display.Enabled!=next.Enabled||display.Profile!=next.Profile){streaming=false;device?.Dispose();device=null;encodingProfile=null;reconnectAt=0;lock(frameGate)latestEncoded=null;}display=ConfigStore.Clone(next);Volatile.Write(ref settings,global.ForDisplay(next));approved=next.Profile;reconnectWanted=next.Enabled&&!previewOnly;if(!next.Enabled)Status="Écran en pause";}}
    public void Start(){tasks=[Task.Factory.StartNew(RenderLoop,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default),Task.Factory.StartNew(UsbLoop,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)];}
    private void RenderLoop()
    {
        using var pacer=new FramePacer(stop.Token);double deadline=elapsed.Elapsed.TotalSeconds;
        while(!stop.IsCancellationRequested)
        {
            var tick=Stopwatch.StartNew();try{
                var config=Settings;var data=config.Demo?Snapshot.Simulation(elapsed.Elapsed.TotalSeconds):Snapshot;double now=elapsed.Elapsed.TotalSeconds;
                CurrentPage=scheduler.Select(config,data,DateTimeOffset.Now);
                if(renderedPage!=CurrentPage){previous?.Dispose();lock(frameGate)previous=latest?.Copy();transitionStart=now;renderedPage=CurrentPage;}
                using var raw=Calibration?UniversalRenderer.Calibration(config.Width,config.Height):renderer.Render(CurrentPage,data,config,DateTime.Now,now);
                SKBitmap frame=raw;SKBitmap? composed=null;
                var effect=config.Pages.First(p=>p.Id==CurrentPage).Transition;
                if(previous!=null&&previous.Width==raw.Width&&previous.Height==raw.Height&&!Calibration&&now-transitionStart<config.TransitionSeconds&&config.TransitionSeconds>0)frame=composed=TransitionRenderer.Compose(previous,raw,(now-transitionStart)/config.TransitionSeconds,effect);
                else {previous?.Dispose();previous=null;}
                long sequence;lock(frameGate){latest?.Dispose();latest=frame.Copy();sequence=++latestSequence;}RenderMs=tick.Elapsed.TotalMilliseconds;
                var profile=encodingProfile;
                if(streaming&&profile!=null&&frame.Width==(profile.Rotation is 90 or 270?profile.Height:profile.Width)&&frame.Height==(profile.Rotation is 90 or 270?profile.Width:profile.Height)){var encodeWatch=Stopwatch.StartNew();var bytes=Encode(frame,profile,config.JpegQuality);EncodeMs=encodeWatch.Elapsed.TotalMilliseconds;JpegBytes=bytes.Length;lock(frameGate)latestEncoded=new(bytes,sequence,profile);}
                var bmp=BitmapSource.Create(frame.Width,frame.Height,96,96,PixelFormats.Bgra32,null,frame.GetPixels(),frame.ByteCount,frame.RowBytes);bmp.Freeze();
                FrameReady?.Invoke(bmp);
                composed?.Dispose();
            }catch(Exception ex){Log("render",ex.GetType().Name+": "+ex.Message);Error?.Invoke("Rendu : "+ex.Message);}
            deadline+=1.0/Settings.PreviewFps;double remaining=deadline-elapsed.Elapsed.TotalSeconds;
            if(remaining<0){deadline=elapsed.Elapsed.TotalSeconds;remaining=0;}
            if(!pacer.Wait(remaining))break;
        }
    }
    private void UsbLoop()
    {
        using var pacer=new FramePacer(stop.Token);
        double deadline=elapsed.Elapsed.TotalSeconds;
        while(!stop.IsCancellationRequested)
        {
            double now=elapsed.Elapsed.TotalSeconds;TryReconnect(now);
            EncodedFrame? frame=null;bool active;lock(deviceGate)active=streaming&&device!=null;
            if(active){lock(frameGate)frame=latestEncoded;}
            if(frame!=null){lock(deviceGate){if(streaming&&device!=null&&frame.Profile==encodingProfile){try{
                device.Send(frame.Bytes);UsbMs=device.LastTransferMs;UsbWriteMs=device.LastWriteMs;UsbAckMs=device.LastAckMs;double sent=elapsed.Elapsed.TotalSeconds;if(lastUsb>0)UsbActualFps=1/(sent-lastUsb);lastUsb=sent;Frames++;Status=$"Connecté · {device.Profile.Width} × {device.Profile.Height} · {UsbActualFps:0.0} i/s";if(frame.Sequence!=lastSentSequence){UniqueFrames++;lastSentSequence=frame.Sequence;}
            }catch(Exception ex){streaming=false;device.Dispose();device=null;UsbActualFps=0;reconnectAt=now+5;Log("usb",ex.GetType().Name);Error?.Invoke("USB interrompu ; reconnexion au même écran dans 5 s.");}}}}
            // Deadline-based pacing: transfer/encoding time does not accumulate as extra frame delay.
            deadline+=active?1.0/Settings.UsbFps:.5;
            double remaining=deadline-elapsed.Elapsed.TotalSeconds;if(remaining<0){deadline=elapsed.Elapsed.TotalSeconds;remaining=0;}
            if(!pacer.Wait(remaining))break;
        }
    }
    public void Disconnect(){lock(deviceGate){reconnectWanted=false;streaming=false;device?.Dispose();device=null;UsbActualFps=0;}}
    public void Suspend(){lock(deviceGate){suspended=true;streaming=false;device?.Dispose();device=null;UsbActualFps=0;}}
    public void Resume(){lock(deviceGate){suspended=false;reconnectAt=elapsed.Elapsed.TotalSeconds+2;}}
    private void TryReconnect(double now)
    {
        lock(deviceGate){if(!reconnectWanted||suspended||device!=null||now<reconnectAt)return;reconnectAt=now+10;
            try{
                if(DeviceDiscovery.VendorRunning()){Status="Fermez TRCC pour libérer cet écran";return;}
                var target=DeviceDiscovery.Scan().SingleOrDefault(d=>d.InstanceId.Equals(display.InstanceId,StringComparison.OrdinalIgnoreCase));if(target==null){Status="Écran débranché · reconnexion automatique";return;}
                var connected=LcdSession.Connect(target);var p=connected.Profile;
                if(!p.Jpeg){connected.Dispose();throw new NotSupportedException("Format RGB565 non pris en charge dans cette version.");}
                if(approved!=null&&!DeviceValidation.Matches(approved,target.InstanceId,p)){connected.Dispose();reconnectWanted=false;throw new InvalidDataException("Profil modifié : retirez cet écran des réglages puis relancez sa détection.");}
                device=connected;selected=target;approved=new(target.InstanceId,p.Width,p.Height,p.Pm,p.Sub,p.Rotation);display.Profile=approved;
                // First discovery uses the device's native rotation, then user orientation persists.
                if(!displayWasProfiled){display.Rotation=p.Rotation;displayWasProfiled=true;}
                var next=ConfigStore.Clone(Settings);next.Width=display.Rotation is 90 or 270?p.Height:p.Width;next.Height=display.Rotation is 90 or 270?p.Width:p.Height;Volatile.Write(ref settings,next);
                encodingProfile=p with{Rotation=display.Rotation};lock(frameGate)latestEncoded=null;streaming=true;lastUsb=0;Status="Écran connecté";ProfileDetected?.Invoke(approved);
            }catch(Exception ex){Status=ex.Message;Log("usb-reconnect",ex.GetType().Name+": "+ex.Message);}
        }
    }
    private bool displayWasProfiled;
    public static byte[] Encode(SKBitmap frame,PanelProfile p,int initialQuality=90)
    {
        if(!p.Jpeg)throw new NotSupportedException("RGB565 non validé : envoi bloqué.");
        using var native=new SKBitmap(p.Width,p.Height);using var c=new SKCanvas(native);c.Clear(SKColors.Black);
        if(p.Rotation!=0){c.Translate(p.Width/2f,p.Height/2f);c.RotateDegrees(p.Rotation);c.Translate(-frame.Width/2f,-frame.Height/2f);}
        if(p.Rotation!=0)c.DrawBitmap(frame,0,0);else c.DrawBitmap(frame,new SKRect(0,0,p.Width,p.Height));
        for(int quality=initialQuality;quality>=40;quality-=10){using var encoded=native.Encode(SKEncodedImageFormat.Jpeg,quality);if(encoded.Size<450000)return encoded.ToArray();}
        throw new InvalidDataException("JPEG trop volumineux : frame abandonnée.");
    }
    public static void Log(string category,string message)
    {
        try{lock(logGate){Directory.CreateDirectory(ConfigStore.Root);string path=System.IO.Path.Combine(ConfigStore.Root,"events.jsonl");if(File.Exists(path)&&new FileInfo(path).Length>2*1024*1024)File.Move(path,path+".1",true);File.AppendAllText(path,JsonSerializer.Serialize(new{time=DateTimeOffset.Now,category,message})+Environment.NewLine);}}catch{}
    }
    private static readonly object logGate=new();
    public async ValueTask DisposeAsync(){stop.Cancel();await Task.WhenAll(tasks);Disconnect();previous?.Dispose();latest?.Dispose();renderer.Dispose();stop.Dispose();}
}

