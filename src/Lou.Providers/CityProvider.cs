using System.Text.Json;
using Lou.Core;
namespace Lou.Providers;
public sealed record City(int Id,string Name,string Region,string Country,double Latitude,double Longitude)
{
    public string Label=>string.Join(" · ",new[]{Name,Region,Country}.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
    public override string ToString()=>Label;
}
public sealed class CityProvider:IDisposable
{
    private readonly HttpClient http;
    public CityProvider(HttpMessageHandler? handler=null){http=handler==null?new():new(handler);http.Timeout=TimeSpan.FromSeconds(12);}
    public async Task<City[]> SearchAsync(string query,CancellationToken ct)
    {
        if(query.Trim().Length<2)return [];
        using var response=await http.GetAsync("https://geocoding-api.open-meteo.com/v1/search?name="+Uri.EscapeDataString(query.Trim())+"&count=10&language="+(Localizer.Language==AppLanguage.English?"en":"fr")+"&format=json",ct);
        response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if(!json.RootElement.TryGetProperty("results",out var results))return [];
        string Text(JsonElement r,string key)=>r.TryGetProperty(key,out var x)?x.GetString()??"":"";
        return results.EnumerateArray().Take(10).Select(r=>new City(r.GetProperty("id").GetInt32(),Text(r,"name"),Text(r,"admin1"),Text(r,"country"),r.GetProperty("latitude").GetDouble(),r.GetProperty("longitude").GetDouble())).Where(c=>double.IsFinite(c.Latitude)&&double.IsFinite(c.Longitude)&&Math.Abs(c.Latitude)<=90&&Math.Abs(c.Longitude)<=180).ToArray();
    }
    public void Dispose()=>http.Dispose();
}
