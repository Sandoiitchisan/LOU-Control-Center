using System.Globalization;
using System.Text.Json;
using Lou.Core;
namespace Lou.Providers;
public sealed class WeatherProvider : IDisposable
{
    private readonly HttpClient http;
    private readonly string cacheRoot;
    private readonly Func<DateTimeOffset> utcNow;
    public WeatherProvider(HttpMessageHandler? handler=null,string? cacheRoot=null,Func<DateTimeOffset>? clock=null)
    {http=handler==null?new HttpClient():new HttpClient(handler);http.Timeout=TimeSpan.FromSeconds(12);this.cacheRoot=cacheRoot??ConfigStore.Root;utcNow=clock??(()=>DateTimeOffset.UtcNow);}
    private DateTimeOffset next=DateTimeOffset.MinValue;
    private string key="";
    private WeatherData current=new("Ville à choisir","Non configurée");
    public async Task<WeatherData> ReadAsync(Settings s,CancellationToken ct)
    {
        if(!s.CitySelected)return new("Ville à choisir","Non configurée");
        string k=FormattableString.Invariant($"{s.Latitude:F5},{s.Longitude:F5},{s.Location}");
        if(k!=key){key=k;next=DateTimeOffset.MinValue;current=new(s.Location,"En attente");try{var cache=JsonSerializer.Deserialize<Cache>(await File.ReadAllTextAsync(Path.Combine(cacheRoot,"weather.json"),ct),ConfigStore.Json);if(cache?.Key==k)current=cache.Data with{Status="Cache • actualisation"};}catch{}}
        if(utcNow()<next)return current;
        TimeSpan retry=TimeSpan.FromMinutes(2);
        try {
            var url=FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={s.Latitude}&longitude={s.Longitude}&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,wind_speed_10m&hourly=temperature_2m,precipitation_probability,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset&timezone=auto&forecast_days=4");
            using var response=await http.GetAsync(url,ct);
            if((int)response.StatusCode==429){var delay=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date-utcNow());if(delay>retry)retry=delay.Value;}
            response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            current=Parse(json.RootElement,s.Location,utcNow());
            next=utcNow().AddMinutes(s.WeatherMinutes);
            try{Directory.CreateDirectory(cacheRoot);await File.WriteAllTextAsync(Path.Combine(cacheRoot,"weather.json"),JsonSerializer.Serialize(new Cache(k,current),ConfigStore.Json),ct);}catch(IOException){}
        }catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch {next=utcNow()+retry;current=current with{Status=current.Updated!=null?"Donnée périmée • hors ligne":"Indisponible • hors ligne"};}
        return current;
    }
    public static WeatherData Parse(JsonElement root,string location,DateTimeOffset updated)
    {
        var c=root.GetProperty("current");var h=root.GetProperty("hourly");var d=root.GetProperty("daily");
        double? Num(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out double n)&&double.IsFinite(n)?n:null;
        DateTime localNow=DateTime.Parse(c.GetProperty("time").GetString()!,CultureInfo.InvariantCulture);
        var hours=new List<Forecast>();for(int i=0;i<h.GetProperty("time").GetArrayLength();i++){
            var time=DateTime.Parse(h.GetProperty("time")[i].GetString()!,CultureInfo.InvariantCulture);if(time<localNow||hours.Count>=6)continue;
            if(h.GetProperty("temperature_2m")[i].ValueKind!=JsonValueKind.Number)continue;
            hours.Add(new(time,h.GetProperty("temperature_2m")[i].GetDouble(),h.GetProperty("weather_code")[i].GetInt32(),h.GetProperty("precipitation_probability")[i].ValueKind==JsonValueKind.Number?h.GetProperty("precipitation_probability")[i].GetDouble():null));}
        var days=new List<Forecast>();for(int i=1;i<Math.Min(4,d.GetProperty("time").GetArrayLength());i++)days.Add(new(DateTime.Parse(d.GetProperty("time")[i].GetString()!,CultureInfo.InvariantCulture),d.GetProperty("temperature_2m_max")[i].GetDouble(),d.GetProperty("weather_code")[i].GetInt32(),Low:d.GetProperty("temperature_2m_min")[i].GetDouble()));
        return new(location,"À jour",updated,Num(c,"temperature_2m"),Num(c,"apparent_temperature"),(int)(Num(c,"weather_code")??0),Num(c,"is_day")==1,Num(c,"relative_humidity_2m"),Num(c,"wind_speed_10m"),hours.ToArray(),days.ToArray(),d.GetProperty("sunrise")[0].GetString(),d.GetProperty("sunset")[0].GetString());
    }
    public static string Condition(int code)=>code switch{0=>"Ciel dégagé",1 or 2=>"Éclaircies",3=>"Nuageux",45 or 48=>"Brouillard",>=51 and <=57=>"Bruine",>=61 and <=67=>"Pluie",>=71 and <=77=>"Neige",>=80 and <=82=>"Averses",>=85 and <=86=>"Averses de neige",>=95=>"Orage",_=>"Indisponible"};
    public void Dispose()=>http.Dispose();
    private sealed record Cache(string Key,WeatherData Data);
}

