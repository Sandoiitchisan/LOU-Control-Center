using System.Net;
using System.Net.Http.Headers;
using Lou.Core;
using Lou.Providers;
using Xunit;
namespace Lou.Tests;
public class ProviderTests
{
    private const string Forecast="""{"current":{"time":"2026-10-09T00:00","temperature_2m":6,"relative_humidity_2m":80,"apparent_temperature":3,"is_day":0,"weather_code":1,"wind_speed_10m":11},"hourly":{"time":["2026-10-09T01:00"],"temperature_2m":[5],"precipitation_probability":[0],"weather_code":[1]},"daily":{"time":["2026-10-09","2026-10-10"],"temperature_2m_max":[14,15],"temperature_2m_min":[5,6],"weather_code":[1,2],"sunrise":["2026-10-09T07:50"],"sunset":["2026-10-09T19:07"]}}""";
    [Fact] public async Task Weather_retains_timestamp_and_values_on_failure_and_recovers(){var handler=new Handler();DateTimeOffset now=DateTimeOffset.UtcNow;using var provider=new WeatherProvider(handler,Path.Combine(Path.GetTempPath(),"lou-weather-"+Guid.NewGuid()),()=>now);var s=new Settings{WeatherMinutes=5,CitySelected=true,Location="Test"};var data=await provider.ReadAsync(s,default);Assert.Equal(6,data.Temperature);var stamp=data.Updated;handler.Status=HttpStatusCode.ServiceUnavailable;now=now.AddMinutes(6);var stale=await provider.ReadAsync(s,default);Assert.Contains("périmée",stale.Status);Assert.Equal(stamp,stale.Updated);Assert.Equal(6,stale.Temperature);handler.Status=HttpStatusCode.OK;now=now.AddMinutes(3);Assert.Equal("À jour",(await provider.ReadAsync(s,default)).Status);}
    [Fact] public async Task Weather_respects_cache_interval(){var handler=new Handler();using var provider=new WeatherProvider(handler,Path.Combine(Path.GetTempPath(),"lou-weather-"+Guid.NewGuid()));await provider.ReadAsync(new Settings{CitySelected=true,Location="Test"},default);await provider.ReadAsync(new Settings{CitySelected=true,Location="Test"},default);Assert.Equal(1,handler.Calls);}
    [Fact] public async Task Weather_rate_limit_honors_retry_after(){var handler=new Handler{Status=HttpStatusCode.TooManyRequests};DateTimeOffset now=DateTimeOffset.UtcNow;using var provider=new WeatherProvider(handler,Path.Combine(Path.GetTempPath(),"lou-weather-"+Guid.NewGuid()),()=>now);Assert.Null((await provider.ReadAsync(new Settings{CitySelected=true,Location="Test"},default)).Temperature);now=now.AddMinutes(3);await provider.ReadAsync(new Settings{CitySelected=true,Location="Test"},default);Assert.Equal(1,handler.Calls);now=now.AddMinutes(8);await provider.ReadAsync(new Settings{CitySelected=true,Location="Test"},default);Assert.Equal(2,handler.Calls);}
    [Fact] public async Task Weather_invalid_data_and_timeout_return_unavailable(){foreach(bool timeout in new[]{false,true}){var handler=new Handler{Invalid=true,Timeout=timeout};using var provider=new WeatherProvider(handler,Path.Combine(Path.GetTempPath(),"lou-weather-"+Guid.NewGuid()));var result=await provider.ReadAsync(new Settings{CitySelected=true,Location="Test"},default);Assert.Null(result.Temperature);Assert.Contains("Indisponible",result.Status);}}
    [Fact] public void Csv_quoted_application_is_parsed(){Assert.Equal(new[]{"game, name.exe","10","a\"b"},GamingProvider.Csv("\"game, name.exe\",10,\"a\"\"b\""));}
    private sealed class Handler : HttpMessageHandler
    {
        public HttpStatusCode Status=HttpStatusCode.OK;public bool Invalid,Timeout;public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;if(Timeout)throw new TaskCanceledException();var response=new HttpResponseMessage(Status){Content=new StringContent(Invalid?"{broken":Forecast)};if(Status==HttpStatusCode.TooManyRequests)response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(10));return Task.FromResult(response);}
    }
}
