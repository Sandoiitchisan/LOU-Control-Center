namespace Lou.Core;

public enum PageId { System, Weather, Music, Gaming, Ambient }
public enum Transition { Slide, Glitch, Scanline, None }
public enum RotationMode { Automatic, Smart, Fixed }
public sealed record PcData(double? Cpu=null, double? CpuTemp=null, double? Gpu=null, double? GpuTemp=null,
    double? RamUsed=null, double? RamTotal=null, double? Vram=null, double? Download=null, double? Upload=null,
    double? CpuFan=null, double? GpuFan=null, double? Pump=null, string Adapter="", double? CpuMhz=null,
    double? GpuHotspot=null,double? GpuPower=null,string GpuName="GPU 3D / Windows",
    double? RearFan=null,double? BottomFan=null,string CoolingStatus="En attente",FanReading[]? Fans=null,string GpuDeviceId="");
public enum FanRole { Radiator, Rear, Bottom, Pump, Gpu }
public sealed record FanBinding(FanRole Role,string Provider,string Board,string ControllerId,string SensorId,string Connector,string Evidence="Association utilisateur",string Hardware="",string Topology="");
public sealed record FanReading(FanRole Role,string Label,string Connector,double? Rpm,string Status,string SensorId,string ControllerId,string Provider);
public static class CoolingLabels
{
    public static string Label(FanRole role)=>role switch{FanRole.Radiator=>Localizer.T("Radiateur AiO"),FanRole.Rear=>Localizer.T("Boîtier 1"),FanRole.Bottom=>Localizer.T("Boîtier 2"),FanRole.Pump=>Localizer.T("Pompe AiO"),_=>Localizer.T("Carte graphique")};
}
public sealed record Forecast(DateTime Time, double Temperature, int Code, double? Rain=null, double? Low=null);
public sealed record WeatherData(string Location, string Status, DateTimeOffset? Updated=null, double? Temperature=null,
    double? FeelsLike=null, int Code=0, bool IsDay=true, double? Humidity=null, double? Wind=null,
    Forecast[]? Hours=null, Forecast[]? Days=null, string? Sunrise=null, string? Sunset=null);
public sealed record MediaData(string Title="Aucune lecture", string Artist="Ouvre ton lecteur multimédia", string Album="",
    bool Playing=false, double Position=0, double Duration=0, byte[]? Cover=null, string Source="", bool Available=false);
public sealed record GamingData(string Name="Aucun jeu détecté", bool Active=false, double? Fps=null, double? Frametime=null,
    string Resolution="Indisponible", string FpsStatus="En attente de mesure",int ProcessId=0,string Executable="");
public sealed record Snapshot(PcData Pc, WeatherData Weather, MediaData Media, GamingData Gaming, bool Demo=false)
{
    public static Snapshot Empty => new(new(), new("Ville à choisir", "Non configurée"), new(), new());
    public static Snapshot Simulation(double t=0) => new(
        new(18+Math.Sin(t)*8,52,62+Math.Sin(t/2)*6,67,12.4,32,6.8,(320+Math.Sin(t*1.4)*80+Math.Cos(t*2.8)*22)/8,(118+Math.Sin(t*.7)*52)/8,980,1420,2680,"DEMO • 10 GbE",4800),
        new("Ville de démonstration", "Démonstration",DateTimeOffset.Now,18,17,61,false,78,12,
            Enumerable.Range(1,6).Select(i=>new Forecast(DateTime.Now.AddHours(i),18-i, i<4?61:2,70)).ToArray(),
            Enumerable.Range(1,3).Select(i=>new Forecast(DateTime.Today.AddDays(i),22-i,i==2?61:1,20,12-i)).ToArray(),"07:42","19:08"),
        new("Midnight Drive","Luna Circuit","Neon City Sessions",true,134+t%60,248,null,"Session de démonstration",true),
        new("NIGHT CITY RUN",true,142,7.1,"3440 × 1440","Démonstration"),true);
}

