using System.Net;
using System.Text.Json;
using Lou.Core;
using Lou.Providers;
using Lou.Rendering;
using Xunit;

namespace Lou.Tests;

public sealed class LocalizationUpdateTests
{
    [Fact] public void Every_ui_resource_has_an_English_translation()
    { Assert.All(Localizer.UiResources.Values, value => Assert.True(Localizer.English.ContainsKey(value), value)); }

    [Fact] public async Task Languages_are_isolated_across_render_workers()
    {
        async Task<string> Label(AppLanguage language) { using var scope=Localizer.Use(language); await Task.Delay(20); return Labels.Page(PageId.Weather); }
        Assert.Equal(new[]{"Weather","Météo"}, await Task.WhenAll(Label(AppLanguage.English),Label(AppLanguage.French)));
    }
    [Fact] public void Translation_preserves_user_metadata_and_sensor_identities()
    {
        using var scope=Localizer.Use(AppLanguage.English);
        var s=new Settings{Language=AppLanguage.English,CitySelected=true,FanLabels=new(){[FanRole.Pump]="Pluie"}};
        var source=Snapshot.Empty with {Media=new("Pluie","Éclaircies",Available:true),Gaming=new("Ambiance",Active:true),Weather=new("Nuageux","À jour"),Pc=new(Fans:[new(FanRole.Pump,"Pluie","POMPE",1200,"Lecture active","/sensor","/controller","provider")])};
        var result=Localizer.Present(source,s);
        Assert.Equal(source.Media,result.Media);Assert.Equal("Ambiance",result.Gaming.Name);Assert.Equal("Nuageux",result.Weather.Location);
        Assert.Equal("Up to date",result.Weather.Status);Assert.Equal("Pluie",result.Pc.Fans![0].Label);Assert.Equal("/sensor",result.Pc.Fans[0].SensorId);
        Assert.Equal("Nothing playing",Localizer.Present(Snapshot.Empty,s).Media.Title);
    }
    [Fact] public void Existing_settings_keep_French_and_bindings_new_settings_follow_OS()
    {
        string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try {var settings=new Settings{FanBindings=[new(FanRole.Pump,"provider","board","controller","sensor","PUMP")]};
            string json=JsonSerializer.Serialize(settings,ConfigStore.Json);using var doc=JsonDocument.Parse(json);
            File.WriteAllText(path,JsonSerializer.Serialize(doc.RootElement.EnumerateObject().Where(p=>p.Name is not "Language" and not "CheckUpdatesOnStartup").ToDictionary(p=>p.Name,p=>p.Value)));
            var loaded=ConfigStore.Load(path);Assert.Equal(AppLanguage.French,loaded.Language);Assert.Equal(settings.FanBindings,loaded.FanBindings);
            loaded.Language=AppLanguage.English;loaded.CheckUpdatesOnStartup=false;ConfigStore.Save(loaded,path);Assert.Equal(AppLanguage.English,ConfigStore.Load(path).Language);Assert.False(ConfigStore.Load(path).CheckUpdatesOnStartup);
            Assert.Equal(AppLanguage.English,loaded.ForDisplay(new(){InstanceId="display"}).Language);
            Assert.Equal(Localizer.SystemLanguage,ConfigStore.Load(path+"-missing").Language);
        } finally {File.Delete(path);File.Delete(path+".bak");}
    }
    [Theory][InlineData(AppLanguage.French,1920,462)][InlineData(AppLanguage.English,1920,462)][InlineData(AppLanguage.French,480,480)][InlineData(AppLanguage.English,480,480)][InlineData(AppLanguage.English,320,240)]
    public void All_pages_render_in_both_languages(AppLanguage language,int width,int height)
    {
        using var renderer=new UniversalRenderer("missing-assets");
        var s=new Settings{Language=language,Width=width,Height=height};
        foreach(var page in Enum.GetValues<PageId>())foreach(var data in new[]{Snapshot.Empty,Snapshot.Simulation()})using(renderer.Render(page,data,s,DateTime.Now,0)){}
        s.CoolingOnly=true;using var cooling=renderer.Render(PageId.System,Snapshot.Empty,s,DateTime.Now,0);Assert.Equal(width,cooling.Width);
    }
    [Fact] public void Formatted_translation_uses_language_culture()
    { using var scope=Localizer.Use(AppLanguage.English);Assert.Equal("Connected · 480 × 480 · 23.9 FPS",Localizer.F($"Connecté · {480} × {480} · {23.9:0.0} i/s")); }

    [Fact] public void Cached_empty_data_can_switch_back_to_French_without_a_provider_refresh()
    {
        Snapshot source;
        using(Localizer.Use(AppLanguage.English))source=Snapshot.Empty;
        using(Localizer.Use(AppLanguage.French))Assert.Equal("Ville à choisir",Localizer.Present(source,new()).Weather.Location);
        using(Localizer.Use(AppLanguage.English))Assert.Equal("Choose a city",Localizer.Present(source,new()).Weather.Location);
    }

    private static string Feed(string tag="v2.2.0",bool installed=true,string? url=null,bool prerelease=false,bool draft=false,bool asset=true)
    {
        string version=tag.TrimStart('v');string name=$"LOU-Control-Center-{(installed?"Setup":"Portable")}-{version}-win-x64.{(installed?"exe":"zip")}";
        return JsonSerializer.Serialize(new{tag_name=tag,html_url=ReleaseUpdates.Repository+"/releases/tag/"+tag,prerelease,draft,assets=asset?new[]{new{name,state="uploaded",browser_download_url=url??ReleaseUpdates.Repository+"/releases/download/"+tag+"/"+name}}:[]});
    }
    [Theory][InlineData(true,".exe")][InlineData(false,".zip")]
    public void New_release_selects_the_matching_distribution(bool installed,string suffix)
    {var result=ReleaseUpdates.Parse(Feed(installed:installed),"2.1.1",installed);Assert.Equal(UpdateState.Newer,result.State);Assert.EndsWith(suffix,result.Download!.AbsoluteUri);}
    [Theory][InlineData("v2.1.1")][InlineData("2.1.1")][InlineData("v2.1.0")]
    public void Equal_and_older_releases_never_offer_a_downgrade(string tag)
    {var result=ReleaseUpdates.Parse(Feed(tag),"2.1.1",true);Assert.Equal(UpdateState.Current,result.State);Assert.Null(result.Download);}
    [Theory][InlineData("v2.10.0")][InlineData("v10.0.0")]
    public void Versions_are_compared_numerically(string tag)=>Assert.Equal(UpdateState.Newer,ReleaseUpdates.Parse(Feed(tag),"2.9.0",true).State);
    [Theory][InlineData("v2.2.0-beta")][InlineData("v2.2")][InlineData("invalid")][InlineData("../../other")]
    public void Invalid_versions_are_not_updates(string tag)=>Assert.Equal(UpdateState.Unavailable,ReleaseUpdates.Parse(Feed(tag),"2.1.1",true).State);
    [Theory][InlineData(true,false)][InlineData(false,true)]
    public void Drafts_and_prereleases_are_not_updates(bool draft,bool prerelease)=>Assert.Equal(UpdateState.Unavailable,ReleaseUpdates.Parse(Feed(draft:draft,prerelease:prerelease),"2.1.1",true).State);
    [Theory][InlineData("https://evil.example/update.exe")][InlineData("https://github.com/other/project/releases/download/v2.2.0/update.exe")][InlineData("file:///C:/update.exe")]
    public void Untrusted_download_links_fall_back_to_the_project_release(string url)
    {var result=ReleaseUpdates.Parse(Feed(url:url),"2.1.1",true);Assert.Null(result.Download);Assert.Equal(ReleaseUpdates.Repository+"/releases/tag/v2.2.0",result.Page!.AbsoluteUri);}
    [Fact] public void Missing_or_wrong_distribution_uses_release_page()
    {Assert.Null(ReleaseUpdates.Parse(Feed(asset:false),"2.1.1",true).Download);Assert.Null(ReleaseUpdates.Parse(Feed(installed:false),"2.1.1",true).Download);}
    [Theory][InlineData(403)][InlineData(404)][InlineData(500)]
    public async Task Network_failure_does_not_claim_the_app_is_current(int status)
    {using var service=new ReleaseUpdates(new Handler(_=>new((HttpStatusCode)status)));Assert.Equal(UpdateState.Unavailable,(await service.CheckAsync("2.1.1",true,default)).State);}
    [Fact] public async Task Release_request_is_anonymous_and_carries_UserAgent()
    {
        using var service=new ReleaseUpdates(new Handler(r=>{Assert.Equal(ReleaseUpdates.Endpoint,r.RequestUri!.AbsoluteUri);Assert.Null(r.Headers.Authorization);Assert.NotEmpty(r.Headers.UserAgent);return new(HttpStatusCode.OK){Content=new StringContent(Feed())};}));
        Assert.Equal(UpdateState.Newer,(await service.CheckAsync("2.1.1",true,default)).State);
    }
    [Theory][InlineData("null")][InlineData("[]")][InlineData("{}")][InlineData("not json")]
    public void Malformed_feed_is_unavailable(string json)=>Assert.Equal(UpdateState.Unavailable,ReleaseUpdates.Parse(json,"2.1.1",true).State);
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(send(request));}
}
