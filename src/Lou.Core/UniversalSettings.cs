using System.Text.Json;
using System.Text.Json.Serialization;
namespace Lou.Core;
public enum AioView { Cooling, Visual, Clock }
public sealed class PageSettings
{
    public PageId Id {get;set;}
    [JsonIgnore] public string Name=>Labels.Page(Id);
    public bool Enabled {get;set;}=true;
    public int Order {get;set;}
    public Transition Transition {get;set;}=Transition.Slide;
    public bool HideUnavailable {get;set;}
}
public class PlaybackSettings
{
    public List<PageSettings> Pages {get;set;}=Enum.GetValues<PageId>().Select((p,i)=>new PageSettings{Id=p,Order=i}).ToList();
    public RotationMode Mode {get;set;}=RotationMode.Automatic;
    public PageId FixedPage {get;set;}=PageId.System;
    public int PageIntervalSeconds {get;set;}=15;
    public double TransitionSeconds {get;set;}=1.2;
    public int UsbFps {get;set;}=24;
    public int PreviewFps {get;set;}=24;
    public int JpegQuality {get;set;}=90;
    public bool CoolingOnly {get;set;}
    public void ValidatePlayback(){
        if(PageIntervalSeconds is <3 or >3600||UsbFps is <1 or >30||PreviewFps is <1 or >30||JpegQuality is <70 or >95||!double.IsFinite(TransitionSeconds)||TransitionSeconds is <0 or >5)throw new InvalidDataException(Localizer.T("Durée : 3 à 3 600 s ; cadence : 1 à 30 i/s ; transition : 0 à 5 s."));
        if(Pages==null||Pages.Count!=5||Pages.Select(p=>p.Id).Distinct().Count()!=5||Pages.Any(p=>!Enum.IsDefined(p.Id)||!Enum.IsDefined(p.Transition))||!Pages.Any(p=>p.Enabled))throw new InvalidDataException(Localizer.T("Choisissez au moins une des cinq pages."));
        if(!Enum.IsDefined(Mode)||!Enum.IsDefined(FixedPage)||Mode==RotationMode.Fixed&&!Pages.Any(p=>p.Id==FixedPage&&p.Enabled))throw new InvalidDataException(Localizer.T("La page fixe doit être activée."));
    }
}
public sealed class DisplaySettings:PlaybackSettings
{
    public string InstanceId {get;set;}="";
    public string Name {get;set;}=Localizer.T("Écran Thermalright");
    public ConfirmedDisplay? Profile {get;set;}
    public bool Enabled {get;set;}=true;
    public int Rotation {get;set;}
    public int PreviewWidth {get;set;}=1920;
    public int PreviewHeight {get;set;}=462;
    public override string ToString()=>Name;
}
public sealed class Settings:PlaybackSettings
{
    public int Version {get;set;}=1;
    public AppLanguage Language {get;set;}=AppLanguage.French;
    public bool CheckUpdatesOnStartup {get;set;}=true;
    public List<DisplaySettings> Displays {get;set;}=[];
    public int Width {get;set;}=1920;
    public int Height {get;set;}=462;
    public string Accent {get;set;}="#00E5FF";
    public double Animation {get;set;}=.6;
    public double Glow {get;set;}=.65;
    public bool Demo {get;set;}
    public bool ShowDiagnostics {get;set;}
    public bool Night {get;set;}=true;
    public int NightStart {get;set;}=23;
    public int NightEnd {get;set;}=7;
    public bool MinimizeToTray {get;set;}=true;
    public bool HardwareMonitoring {get;set;}
    public string Location {get;set;}="";
    public double Latitude {get;set;}
    public double Longitude {get;set;}
    public bool CitySelected {get;set;}
    public int WeatherMinutes {get;set;}=20;
    public string NetworkAdapter {get;set;}="";
    public List<FanBinding> FanBindings {get;set;}=[];
    public Dictionary<FanRole,string> FanLabels {get;set;}=[];
    public SensorBinding? CpuTemperature {get;set;}
    public SensorBinding? GpuTemperature {get;set;}
    public string GpuController {get;set;}="";
    public string GpuHardware {get;set;}="";
    public string GpuDeviceId {get;set;}="";
    public string[] Games {get;set;}=[];
    public string[] IgnoredGames {get;set;}=[];
    public string PresentMonCsv {get;set;}="";
    public bool AutomaticFps {get;set;}=true;
    public Dictionary<PageId,string> Backgrounds {get;set;}=[];
    public void Validate(){
        ValidatePlayback();if(!Enum.IsDefined(Language))throw new InvalidDataException("Invalid language.");if(Version!=1)throw new InvalidDataException(Localizer.T("Version de configuration inconnue."));
        if(Width is <240 or >3840||Height is <172 or >2160)throw new InvalidDataException(Localizer.T("Dimensions hors limites."));
        if(!double.IsFinite(Latitude)||!double.IsFinite(Longitude)||Math.Abs(Latitude)>90||Math.Abs(Longitude)>180||CitySelected&&string.IsNullOrWhiteSpace(Location))throw new InvalidDataException(Localizer.T("Ville invalide."));
        if(WeatherMinutes is <5 or >1440||!double.IsFinite(Animation)||!double.IsFinite(Glow)||Animation is <0 or >1||Glow is <0 or >1||NightStart is <0 or >23||NightEnd is <0 or >23)throw new InvalidDataException(Localizer.T("Réglage hors limites."));
        if(FanBindings==null||FanBindings.Count>5||FanBindings.Any(b=>!Enum.IsDefined(b.Role)||string.IsNullOrWhiteSpace(b.Board)||string.IsNullOrWhiteSpace(b.ControllerId)||string.IsNullOrWhiteSpace(b.SensorId))||FanBindings.Select(b=>b.Role).Distinct().Count()!=FanBindings.Count||FanBindings.Select(b=>(b.Provider,b.Board,b.ControllerId,b.SensorId)).Distinct().Count()!=FanBindings.Count)throw new InvalidDataException(Localizer.T("Chaque rôle doit avoir un capteur distinct et identifié."));
        if(FanLabels==null||FanLabels.Any(p=>!Enum.IsDefined(p.Key)||p.Value==null||p.Value.Length>40)||Backgrounds==null||Games==null||IgnoredGames==null||Games.Length>100||IgnoredGames.Length>100)throw new InvalidDataException(Localizer.T("Configuration incomplète."));
        if(Displays==null||Displays.Count>8||Displays.Select(d=>d.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Displays.Count)throw new InvalidDataException(Localizer.T("Chaque écran doit avoir une identité unique."));
        foreach(var d in Displays){d.ValidatePlayback();if(string.IsNullOrWhiteSpace(d.InstanceId)||d.Name.Length>80||d.Rotation is not (0 or 90 or 180 or 270)||d.PreviewWidth is <240 or >3840||d.PreviewHeight is <172 or >2160||d.Profile is {} p&&(p.InstanceId!=d.InstanceId||p.Width is <240 or >3840||p.Height is <172 or >2160||p.Rotation is not (0 or 90 or 180 or 270)))throw new InvalidDataException(Localizer.T("Profil écran invalide."));}
    }
    public Settings ForDisplay(DisplaySettings d){var s=ConfigStore.Clone(this);s.Displays=[];s.Pages=ConfigStore.Clone(d.Pages);s.Mode=d.Mode;s.FixedPage=d.FixedPage;s.PageIntervalSeconds=d.PageIntervalSeconds;s.TransitionSeconds=d.TransitionSeconds;s.UsbFps=d.UsbFps;s.PreviewFps=d.UsbFps;s.JpegQuality=d.JpegQuality;s.CoolingOnly=d.CoolingOnly;int w=d.Profile?.Width??d.PreviewWidth,h=d.Profile?.Height??d.PreviewHeight;s.Width=d.Rotation is 90 or 270?h:w;s.Height=d.Rotation is 90 or 270?w:h;return s;}
    public bool IsNight(DateTime t)=>Night&&(NightStart>NightEnd?t.Hour>=NightStart||t.Hour<NightEnd:t.Hour>=NightStart&&t.Hour<NightEnd);
}
public sealed record SensorBinding(string Provider,string Board,string ControllerId,string SensorId,string Hardware="",string Topology="");
public sealed record ConfirmedDisplay(string InstanceId,int Width,int Height,int Pm,int Sub,int Rotation);
public static class ConfigStore
{
    public static bool IsInstalled=>File.Exists(Path.Combine(AppContext.BaseDirectory,"installed.mode"));
    public static string Root=>DataRoot(AppContext.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),IsInstalled);
    public static string DataRoot(string executableDirectory,string localData,bool installed)=>installed?Path.Combine(localData,"LouControlCenterUniversal"):Path.Combine(executableDirectory,"Data");
    // Keep portable relative paths valid after moving the folder or migrating to an installation.
    public static string ResolveBackground(string path)=>ResolveBackground(path,Root,AppContext.BaseDirectory);
    public static string ResolveBackground(string path,string dataRoot,string executableDirectory){
        string normalized=path.Replace('/',Path.DirectorySeparatorChar);
        if(normalized.StartsWith("Data"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(Path.Combine(dataRoot,normalized[5..]));
        return Path.IsPathRooted(path)?path:Path.GetFullPath(Path.Combine(executableDirectory,path));
    }
    public static JsonSerializerOptions Json {get;}=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    public static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Json),Json)!;
    public static Settings Load(string? path=null){path??=Path.Combine(Root,"settings.json");if(!File.Exists(path))return new(){Language=Localizer.SystemLanguage};var s=JsonSerializer.Deserialize<Settings>(File.ReadAllText(path),Json)??throw new InvalidDataException(Localizer.T("Configuration vide."));s.Validate();return s;}
    public static void Save(Settings s,string? path=null){s.Validate();path??=Path.Combine(Root,"settings.json");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(s,Json));if(File.Exists(path))File.Copy(path,path+".bak",true);File.Move(path+".tmp",path,true);}
}
public sealed class PageScheduler
{
    public PageId Current {get;private set;}=PageId.System;
    private DateTimeOffset since=DateTimeOffset.MinValue;
    public PageId Select(PlaybackSettings s,Snapshot data,DateTimeOffset now){
        var pages=s.Pages.Where(p=>p.Enabled&&(!p.HideUnavailable||p.Id switch{PageId.Music=>data.Media.Available,PageId.Gaming=>data.Gaming.Active,_=>true})).OrderBy(p=>p.Order).ToArray();if(pages.Length==0)return Current=PageId.Ambient;
        PageId? forced=s.Mode==RotationMode.Fixed?s.FixedPage:null;if(s.Mode==RotationMode.Smart){if(data.Gaming.Active)forced=PageId.Gaming;else if(data.Media.Playing)forced=PageId.Music;}
        if(forced!=null&&pages.Any(p=>p.Id==forced)){if(Current!=forced){Current=forced.Value;since=now;}return Current;}
        int i=Array.FindIndex(pages,p=>p.Id==Current);if(i<0){Current=pages[0].Id;since=now;}else if(since==DateTimeOffset.MinValue)since=now;else if((now-since).TotalSeconds>=s.PageIntervalSeconds){Current=pages[(i+1)%pages.Length].Id;since=now;}return Current;
    }
}
