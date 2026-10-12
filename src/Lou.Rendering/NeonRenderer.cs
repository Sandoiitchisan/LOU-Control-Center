using System.Globalization;
using Lou.Core;
using SkiaSharp;
namespace Lou.Rendering;

public sealed class NeonRenderer : IDisposable
{
    private readonly Dictionary<string,SKBitmap> images=[];
    private readonly SKTypeface type=SKTypeface.FromFamilyName("Bahnschrift")??SKTypeface.Default;
    private readonly SKTypeface mono=SKTypeface.FromFamilyName("Consolas")??SKTypeface.Default;
    private readonly SKTypeface symbols=SKTypeface.FromFamilyName("Segoe UI Symbol")??SKTypeface.Default;
    private readonly string assetRoot;
    private SKBitmap? cover;
    private byte[]? lastCover;
    private readonly Queue<double?> down=[],up=[];

    private int historySecond=-1;
    private bool? historyDemo;
    private float weatherDay=-1;
    private double lastWeatherTime;
    private SKColor cyan=SKColor.Parse("#00E5FF");
    private static readonly SKColor Pink=SKColor.Parse("#FF2D95"),Purple=SKColor.Parse("#9B5CFF"),Blue=SKColor.Parse("#2E9BFF"),White=SKColor.Parse("#EEF6FF"),Muted=SKColor.Parse("#90AFCB"),Green=SKColor.Parse("#00ECA2");
    private float glow=.65f;
    private float canvasWidth=1920;
    public NeonRenderer(string assets){assetRoot=assets;}
    public SKBitmap Render(PageId page,Snapshot d,Settings s,DateTime now,double elapsed)
    {
        using var language=Localizer.Use(s.Language);d=Localizer.Present(d,s);
        if(historyDemo!=d.Demo){down.Clear();up.Clear();historySecond=-1;historyDemo=d.Demo;}
        var bitmap=new SKBitmap(s.Width,s.Height,SKColorType.Bgra8888,SKAlphaType.Premul);
        using var c=new SKCanvas(bitmap);c.Clear(SKColors.Black);
        var fit=Fit(s.Width,s.Height);canvasWidth=s.Width/fit.Scale;c.Translate(0,fit.Y);c.Scale(fit.Scale);
        cyan=SKColor.TryParse(s.Accent,out var custom)?custom:SKColor.Parse("#00E5FF");glow=(float)s.Glow;
        double t=elapsed*s.Animation*(s.IsNight(now)?.15:1);
        string bg=page switch{PageId.Ambient=>"ambient",PageId.Music=>"music",PageId.Gaming=>"gaming",_=>"city"};
        var file=s.Backgrounds.TryGetValue(page,out var path)&&File.Exists(ConfigStore.ResolveBackground(path))?ConfigStore.ResolveBackground(path):Path.Combine(assetRoot,bg+".png");
        var image=Load(file);
        if(image!=null)DrawCover(c,image,new(0,0,canvasWidth,480));
        else {Fill(c,new(0,0,canvasWidth,480),new(8,13,28));Text(c,Localizer.T("Décor manquant : ")+bg,40,450,24,Pink);}
        if(page==PageId.Weather&&!s.Backgrounds.ContainsKey(page)){
            float target=d.Weather.IsDay?1:0;float step=(float)Math.Clamp(elapsed-lastWeatherTime,0,.2)/3;lastWeatherTime=elapsed;
            weatherDay=weatherDay<0?target:Math.Clamp(weatherDay+Math.Sign(target-weatherDay)*step,0,1);
            if(weatherDay>0&&Load(Path.Combine(assetRoot,"city-day.png")) is {} daylight){using var alpha=new SKPaint{Color=SKColors.White.WithAlpha((byte)(255*weatherDay))};DrawCover(c,daylight,new(0,0,canvasWidth,480),alpha);}
            if(d.Weather.Code>=3){Fill(c,new(0,0,1920,480),new(5,15,35,35));using var cloud=new SKPaint{Color=new SKColor(56,82,111,25),IsAntialias=true};for(int i=0;i<4;i++)c.DrawOval((float)((i*620+t*5)%2400)-200,10+i%2*35,500,70,cloud);}
        }
        using(var shade=new SKPaint{Shader=SKShader.CreateLinearGradient(new(0,0),new(canvasWidth,0),[new SKColor(2,7,18,160),new SKColor(2,7,18,60),new SKColor(2,7,18,120)],[0,.55f,1],SKShaderTileMode.Clamp)})c.DrawRect(0,0,canvasWidth,480,shade);
        if(historySecond!=(int)elapsed){historySecond=(int)elapsed;down.Enqueue(d.Pc.Download);up.Enqueue(d.Pc.Upload);foreach(var queue in new[]{down,up})while(queue.Count>80)queue.Dequeue();}
        switch(page){case PageId.System:SystemPage(c,d,now);break;case PageId.Weather:WeatherPage(c,d,t);break;case PageId.Music:MusicPage(c,d,t);break;case PageId.Gaming:GamingPage(c,d);break;case PageId.Ambient:AmbientPage(c,d,now,t);break;}
        Frame(c,t);
        if(s.ShowDiagnostics){Fill(c,new(580,12,1380,48),new SKColor(3,9,20,235));Text(c,Localizer.F($"TEST : {s.UsbFps} FPS demandés • SLIDE • JPEG {s.JpegQuality}"),608,38,21,cyan);}
        if(s.IsNight(now))Fill(c,new(0,0,canvasWidth,480),new(0,0,12,75));
        Text(c,d.Demo?Localizer.T("DÉMONSTRATION • DONNÉES SIMULÉES"):"LOU // CONTROL CENTER",34,465,15,d.Demo?Pink:Muted);
        Text(c,"NEON CITY   /   "+((int)page+1).ToString("00"),canvasWidth-210,465,14,Muted);
        return bitmap;
    }
    public static (float X,float Y,float Scale) Fit(int width,int height){float scale=Math.Min(width/1920f,height/480f);return((width-1920*scale)/2,(height-480*scale)/2,scale);}
    private SKBitmap? Load(string path)
    {
        if(images.TryGetValue(path,out var bitmap))return bitmap;
        try{if(!File.Exists(path))return null;using var codec=SKCodec.Create(path);if(codec==null||codec.Info.Width>8192||codec.Info.Height>8192)return null;bitmap=SKBitmap.Decode(codec);if(bitmap!=null)images[path]=bitmap;return bitmap;}catch{return null;}
    }
    private void SystemPage(SKCanvas c,Snapshot d,DateTime now)
    {
        Panel(c,40,42,575,385,.55f);Text(c,now.ToString("ddd, dd MMM yyyy",Localizer.Culture).ToUpperInvariant(),74,102,29,cyan);
        Text(c,now.ToString("HH:mm"),65,261,150,cyan,glowing:true);Text(c,now.ToString("ss"),479,260,40,White);
        Text(c,Localizer.T("CPU  /  GPU  /  MÉMOIRE"),78,324,25,Green);
        Text(c,d.Pc.CpuTemp is >=90||d.Pc.GpuTemp is >=95?Localizer.T("TEMPÉRATURE ÉLEVÉE"):Localizer.T("SURVEILLANCE DU SYSTÈME"),78,386,26,d.Pc.CpuTemp is >=90?Pink:Green);
        var p=d.Pc;
        Gauge(c,764,194,105,"CPU",p.Cpu,p.CpuTemp is {} ct?V(ct,"°C"):Localizer.T("Temp. indisponible"),Blue);
        Gauge(c,1028,194,105,"GPU",p.Gpu,p.GpuTemp is {} gt?V(gt,"°C"):Localizer.T("Temp. indisponible"),Pink);
        Gauge(c,1292,194,105,"RAM",p.RamUsed!=null&&p.RamTotal>0?p.RamUsed/p.RamTotal*100:null,V(p.RamUsed,Localizer.T(" Go"),1),Purple);
        CoolingTile(c,651,316,225,FanName(p,FanRole.Radiator),p.CpuFan,cyan);CoolingTile(c,914,316,225,FanName(p,FanRole.Gpu),p.GpuFan,Pink);CoolingTile(c,1177,316,225,FanName(p,FanRole.Pump),p.Pump,Purple);
        CoolingTile(c,651,387,367,FanName(p,FanRole.Rear),p.RearFan,Blue);CoolingTile(c,1035,387,367,FanName(p,FanRole.Bottom),p.BottomFan,cyan);
        Text(c,"GPU "+V(p.GpuHotspot,"°C")+Localizer.T(" (point chaud)   /   ")+V(p.GpuPower," W")+"   /   VRAM "+V(p.Vram,Localizer.T(" Go"),1),78,417,17,Muted,maximum:510);
        Panel(c,1450,47,canvasWidth-1490,378,.6f);Text(c,Localizer.T("RÉSEAU"),1482,95,27,cyan);Text(c,"↓ "+V(p.Download,Localizer.T(" Mo/s"),1),1482,149,32,cyan);Text(c,"↑ "+V(p.Upload,Localizer.T(" Mo/s"),1),1482,194,29,White);
        Chart(c,1480,218,canvasWidth-1550,135,down,cyan);Chart(c,1480,218,canvasWidth-1550,135,up,Pink);Text(c,p.Adapter,1480,395,18,Muted,maximum:canvasWidth-1550);
    }
    private void WeatherPage(SKCanvas c,Snapshot d,double t)
    {
        var w=d.Weather;bool rain=w.Temperature!=null&&(w.Code is >=51 and <=67 or >=80 and <=82 or >=95);
        if(rain)Rain(c,t);
        if(w.Code is >=71 and <=77 or 85 or 86)for(int i=0;i<50;i++)Circle(c,(i*137.91f)%1920,(float)((i*83+t*22)%480),2,new SKColor(220,240,255,120));
        float currentX=40,currentW=520,hourX=currentX+currentW+18,dayW=460,dayX=canvasWidth-40-dayW,hourW=dayX-hourX-18;
        Panel(c,currentX,36,currentW,389,.48f);Text(c,Localizer.T("EN CE MOMENT"),currentX+34,84,25,cyan);WeatherIcon(c,currentX+106,188,w.Code,w.IsDay,56);
        Text(c,V(w.Temperature,"°C"),currentX+190,211,104,White,maximum:296);Text(c,w.Temperature==null?Localizer.T("Indisponible"):Condition(w.Code),currentX+34,266,32,White);
        Text(c,w.Location,currentX+34,312,25,Muted,maximum:currentW-68);Text(c,Localizer.T("Ressenti ")+V(w.FeelsLike,"°")+Localizer.T("   •   Vent ")+V(w.Wind," km/h"),currentX+34,358,22,Muted,maximum:currentW-65);Text(c,Localizer.T("Humidité ")+V(w.Humidity,"%"),currentX+34,393,21,Muted);
        Panel(c,hourX,36,hourW,389,.48f);Text(c,Localizer.T("PROCHAINES HEURES"),hourX+34,84,25,White);
        var hours=w.Hours??[];
        float hourCell=(hourW-40)/6;
        for(int i=0;i<Math.Min(hours.Length,6);i++){float x=hourX+20+hourCell*(i+.5f);Text(c,hours[i].Time.ToString("HH:mm"),x,146,23,Muted,center:true);WeatherIcon(c,x,214,hours[i].Code,hours[i].Time.Hour is >=7 and <20,30);Text(c,V(hours[i].Temperature,"°"),x,291,33,White,center:true);Text(c,V(hours[i].Rain,"%"),x,334,21,cyan,center:true);if(i>0)Line(c,x-hourCell/2,120,x-hourCell/2,355,new(60,120,145,65));}
        if(hours.Length==0)Text(c,Localizer.T("Prévisions indisponibles"),hourX+34,234,27,Muted);
        Text(c,w.Updated is {} stamp?Localizer.T("Mise à jour ")+stamp.ToLocalTime().ToString("dd/MM HH:mm")+" • "+w.Status:w.Status,hourX+34,392,18,Muted,maximum:hourW-68);
        Panel(c,dayX,36,dayW,389,.52f);Text(c,Localizer.T("LES TROIS PROCHAINS JOURS"),dayX+28,84,22,White);
        var days=w.Days??[];for(int i=0;i<Math.Min(3,days.Length);i++){float x=dayX+24+(dayW-48)/3*(i+.5f);Text(c,days[i].Time.ToString("ddd",Localizer.Culture).ToUpperInvariant(),x,149,23,Muted,center:true);WeatherIcon(c,x,216,days[i].Code,true,30);Text(c,V(days[i].Temperature,"°"),x,291,36,White,center:true);Text(c,V(days[i].Low,"°"),x,337,25,Muted,center:true);}
        Text(c,"Open-Meteo • CC BY 4.0",dayX+28,395,18,Muted);
    }
    private void MusicPage(SKCanvas c,Snapshot d,double t)
    {
        var m=d.Media;Panel(c,48,44,361,361,.8f,Pink);
        if(!ReferenceEquals(m.Cover,lastCover)){cover?.Dispose();cover=null;lastCover=m.Cover;try{if(m.Cover!=null){using var codec=SKCodec.Create(new SKMemoryStream(m.Cover));if(codec!=null&&codec.Info.Width<=4096&&codec.Info.Height<=4096)cover=SKBitmap.Decode(codec);}}catch{}}
        var art=cover??(d.Demo?Load(Path.Combine(assetRoot,"cover.png")):null);
        if(art!=null)DrawCover(c,art,new(54,50,403,399));else{using var p=new SKPaint{IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeWidth=3,Color=Pink};for(int i=0;i<5;i++)c.DrawCircle(229,224,55+i*19,p);CenterText(c,"♫",229,224,110,White);}
        float right=canvasWidth-75,center=(480+right)/2;
        Panel(c,440,46,canvasWidth-485,375,.33f);Text(c,m.Playing?Localizer.T("ÉCOUTE EN COURS"):m.Available?Localizer.T("LECTURE EN PAUSE"):Localizer.T("MUSIQUE"),475,99,28,Pink);
        Text(c,m.Title,475,165,52,White,maximum:canvasWidth-570);Text(c,m.Artist,476,208,31,cyan,maximum:canvasWidth-570);Text(c,m.Album,476,245,22,Muted,maximum:canvasWidth-1040);
        // Decorative equalizer only, deliberately labelled.
        if(m.Playing)for(int i=0;i<45;i++){float h=(float)(12+Math.Abs(Math.Sin(i*.63+t*2))*42);Fill(c,new(right-370+i*8,302-h,right-366+i*8,302),Pink.WithAlpha((byte)(120+i%5*20)));}
        Text(c,Localizer.T("VISUEL DÉCORATIF"),right-180,242,14,Muted);
        Line(c,480,310,right,310,new(55,82,104));float progress=m.Duration>0?(float)Math.Clamp(m.Position/m.Duration,0,1):0;Line(c,480,310,480+(right-480)*progress,310,cyan,5);Circle(c,480+(right-480)*progress,310,6,cyan);
        Text(c,Time(m.Position),480,342,20,White);Text(c,Time(m.Duration),right-55,342,20,White);
        CenterText(c,"‹‹",center-160,378,39,White);Circle(c,center,378,35,Pink,true,2);
        if(m.Playing){Fill(c,new(center-11,364,center-4,392),White);Fill(c,new(center+4,364,center+11,392),White);}
        else {using var triangle=new SKPath();triangle.MoveTo(center-8,364);triangle.LineTo(center+16,378);triangle.LineTo(center-8,392);triangle.Close();using var paint=new SKPaint{Color=White,IsAntialias=true};c.DrawPath(triangle,paint);}
        CenterText(c,"››",center+160,378,39,White);
    }
    private void GamingPage(SKCanvas c,Snapshot d)
    {
        var g=d.Gaming;float statX=canvasWidth*.52f,statW=(canvasWidth-statX-58)/2;
        Panel(c,40,36,statX-58,244,.38f);Text(c,g.Active?Localizer.T("JEU EN COURS"):Localizer.T("JEUX / EN ATTENTE"),74,84,26,cyan);Text(c,g.Name,74,146,49,White,maximum:statX-130);
        Text(c,g.Active?Localizer.T("SURVEILLANCE DES PERFORMANCES"):Localizer.T("Lance un jeu pour suivre ses performances"),74,217,25,cyan,maximum:statX-130);
        Panel(c,statX,36,statW,244,.65f);Text(c,Localizer.T("IMAGES PAR SECONDE"),statX+30,90,24,Muted);CenterText(c,V(g.Fps,""),statX+statW/2,185,108,cyan);
        Panel(c,statX+statW+18,36,statW,244,.65f);Text(c,Localizer.T("TEMPS PAR IMAGE"),statX+statW+48,90,24,Muted);CenterText(c,V(g.Frametime," ms",1),statX+statW*1.5f+18,185,62,White);
        Text(c,g.FpsStatus,statX+24,268,16,Muted,maximum:statW*2);
        float cell=(canvasWidth-80)/4;Panel(c,40,304,canvasWidth-80,121,.72f);MiniMetric(c,74,Localizer.T("CHARGE GPU"),V(d.Pc.Gpu,"%"),d.Pc.Gpu/100,Pink);MiniMetric(c,74+cell,Localizer.T("MÉMOIRE VIDÉO"),V(d.Pc.Vram,Localizer.T(" Go"),1),null,Purple);MiniMetric(c,74+cell*2,Localizer.T("PROCESSEUR"),V(d.Pc.Cpu,"%"),d.Pc.Cpu/100,cyan);Text(c,Localizer.T("TAILLE DE FENÊTRE"),74+cell*3,342,20,Muted);Text(c,g.Resolution,74+cell*3,388,31,White);
    }
    private void AmbientPage(SKCanvas c,Snapshot d,DateTime now,double t)
    {
        Panel(c,49,48,490,370,.38f);Text(c,now.ToString("HH:mm"),91,244,141,White);Text(c,now.ToString("ddd, dd MMM yyyy",Localizer.Culture).ToUpperInvariant(),94,302,29,White);

        float right=canvasWidth-432;Panel(c,right,75,386,327,.35f);WeatherIcon(c,right+69,197,d.Weather.Code,d.Weather.IsDay,42);Text(c,V(d.Weather.Temperature,"°C"),right+132,211,58,White);Text(c,d.Weather.Temperature==null?Localizer.T("Météo indisponible"):Condition(d.Weather.Code),right+32,271,26,White,maximum:320);Text(c,d.Weather.Location,right+32,314,21,Muted,maximum:320);Text(c,d.Weather.Status,right+32,351,17,Muted,maximum:320);
        using var p=new SKPaint{Color=new SKColor(255,159,60,(byte)(10+5*Math.Sin(t*.3))),IsAntialias=true};c.DrawOval(1130,370,430,50,p);
    }
    private void MiniMetric(SKCanvas c,float x,string label,string value,double? v,SKColor color){Text(c,label,x,342,20,Muted);Text(c,value,x,389,38,White);Bar(c,x,404,210,v,color);}
    private void LabRow(SKCanvas c,float x,float y,string label,string value,float width){Text(c,label,x,y,21,Muted);Text(c,value,x+width*.47f,y,25,White,maximum:width*.53f);}
    private void Mini(SKCanvas c,float x,float y,float w,string name,string value,SKColor color){Panel(c,x,y,w,83,.88f,color);Circle(c,x+27,y+42,11,color,true,2);Text(c,name,x+50,y+29,17,Muted);Text(c,value,x+50,y+62,24,White,maximum:w-60);}
    private static string FanName(PcData p,FanRole r)=>(p.Fans?.FirstOrDefault(f=>f.Role==r)?.Label??CoolingLabels.Label(r)).ToUpperInvariant();
    private void CoolingTile(SKCanvas c,float x,float y,float width,string label,double? rpm,SKColor color){Panel(c,x,y,width,61,.88f,color);Circle(c,x+19,y+31,7,color,true,2);Text(c,label,x+35,y+20,15,Muted,maximum:width-46);Text(c,V(rpm,Localizer.T(" tr/min")),x+35,y+48,24,White,maximum:width-46);}
    private void Gauge(SKCanvas c,float x,float y,float r,string label,double? value,string sub,SKColor color)
    {
        Circle(c,x,y,r+10,new(0,10,25,170));Circle(c,x,y,r,new(17,50,76),true,8);Circle(c,x,y,r-14,new(44,93,115,100),true,1);
        if(value!=null){using var p=new SKPaint{IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeWidth=9,StrokeCap=SKStrokeCap.Round,Color=color};using var path=new SKPath();path.AddArc(new(x-r,y-r,x+r,y+r),-90,(float)Math.Clamp(value.Value,0,100)*3.6f);using var blur=SKMaskFilter.CreateBlur(SKBlurStyle.Normal,7);p.MaskFilter=blur;p.Color=color.WithAlpha((byte)(130*glow));c.DrawPath(path,p);p.MaskFilter=null;p.Color=color;c.DrawPath(path,p);}
        CenterText(c,label,x,y-53,24,color);CenterText(c,V(value,"%"),x,y,57,White);CenterText(c,sub,x,y+58,22,color,195);
    }
    private void Chart(SKCanvas c,float x,float y,float w,float h,IEnumerable<double?> series,SKColor color)
    {
        var values=series.ToArray();for(int j=1;j<4;j++)Line(c,x,y+h*j/4,x+w,y+h*j/4,new(31,76,106,90));
        var max=Math.Max(1,values.Where(v=>v.HasValue).Select(v=>v!.Value).DefaultIfEmpty(1).Max());using var path=new SKPath();bool started=false;
        for(int i=0;i<values.Length;i++){if(values[i]==null){started=false;continue;}float xx=x+w*i/Math.Max(1,values.Length-1),yy=y+h-(float)(values[i]!.Value/max)*h*.88f;if(!started){path.MoveTo(xx,yy);started=true;}else path.LineTo(xx,yy);}
        using var p=new SKPaint{IsAntialias=true,Color=color,Style=SKPaintStyle.Stroke,StrokeWidth=2.5f};c.DrawPath(path,p);
        if(values.All(v=>v==null))Text(c,Localizer.T("Données indisponibles"),x+12,y+h/2,21,Muted);
    }
    private void Rain(SKCanvas c,double t){for(int i=0;i<75;i++){float x=(i*137.91f)%1920,y=(float)((i*89+t*(100+i%7*20))%480);Line(c,x,y,x-4,y+20,new SKColor(95,195,239,60),1);}}
    private void WeatherIcon(SKCanvas c,float x,float y,int code,bool day,float size)
    {
        if(code<3&&day){Circle(c,x,y,size*.48f,SKColor.Parse("#FFB844"),true,3);for(int i=0;i<8;i++){double a=i*Math.PI/4;Line(c,x+(float)Math.Cos(a)*size*.68f,y+(float)Math.Sin(a)*size*.68f,x+(float)Math.Cos(a)*size*.88f,y+(float)Math.Sin(a)*size*.88f,SKColor.Parse("#FFB844"),3);}}
        else if(code<3){using var p=new SKPaint{IsAntialias=true,Color=White};c.DrawCircle(x,y,size*.6f,p);p.Color=new(4,18,38);c.DrawCircle(x+size*.3f,y-size*.25f,size*.59f,p);}
        else {using var path=new SKPath();path.MoveTo(x-size*.8f,y+size*.3f);path.CubicTo(x-size*1.25f,y-size*.3f,x-size*.5f,y-size*.6f,x-size*.25f,y-size*.35f);path.CubicTo(x-size*.1f,y-size*1.15f,x+size*.8f,y-size*.7f,x+size*.7f,y-size*.12f);path.CubicTo(x+size*1.2f,y-size*.1f,x+size*1.1f,y+size*.5f,x+size*.6f,y+size*.5f);path.LineTo(x-size*.7f,y+size*.5f);using var p=new SKPaint{Color=cyan,IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeWidth=3};c.DrawPath(path,p);if(code>=51)for(int i=0;i<3;i++)Line(c,x-size*.5f+i*size*.5f,y+size*.8f,x-size*.65f+i*size*.5f,y+size*1.14f,cyan,3);}
    }
    private void Frame(SKCanvas c,double t)
    {
        using var p=new SKPaint{Color=new(25,103,134,210),Style=SKPaintStyle.Stroke,StrokeWidth=2,IsAntialias=true};using var shape=new SKPath();shape.MoveTo(10,30);shape.LineTo(30,10);shape.LineTo(canvasWidth-32,10);shape.LineTo(canvasWidth-10,32);shape.LineTo(canvasWidth-10,446);shape.LineTo(canvasWidth-30,469);shape.LineTo(14,469);shape.Close();c.DrawPath(shape,p);
        Line(c,42,16,325,16,cyan,3);Line(c,canvasWidth-352,16,canvasWidth-86,16,Pink,3);Line(c,canvasWidth-10,55,canvasWidth-10,113,Pink,3);
        for(int i=0;i<3;i++)Line(c,23,62+i*10,23,67+i*10,cyan,3);
    }
    private void Panel(SKCanvas c,float x,float y,float w,float h,float opacity,SKColor? edge=null)
    {
        using var path=new SKPath();path.MoveTo(x+12,y);path.LineTo(x+w-12,y);path.LineTo(x+w,y+12);path.LineTo(x+w,y+h-10);path.LineTo(x+w-10,y+h);path.LineTo(x,y+h);path.LineTo(x,y+12);path.Close();
        using var p=new SKPaint{Color=new(2,11,25,(byte)(255*opacity)),IsAntialias=true};c.DrawPath(path,p);p.Style=SKPaintStyle.Stroke;p.StrokeWidth=1;p.Color=(edge??cyan).WithAlpha(90);c.DrawPath(path,p);Line(c,x+12,y,x+64,y,(edge??cyan).WithAlpha(180),2);
    }
    private void CenterText(SKCanvas c,string text,float x,float y,float size,SKColor color,float maximum=float.MaxValue)
    {
        using var font=new SKFont(text.Contains("♫")?symbols:type,size);using var paint=new SKPaint{Color=color,IsAntialias=true};
        while(text.Length>1&&font.MeasureText(text)>maximum)text=text[..^2]+"…";
        font.MeasureText(text,out var bounds);c.DrawText(text,x-(bounds.Left+bounds.Right)/2,y-(bounds.Top+bounds.Bottom)/2,font,paint);
    }
    private void Text(SKCanvas c,string? text,float x,float y,float size,SKColor color,bool center=false,bool glowing=false,float maximum=float.MaxValue)
    {
        text??="";using var font=new SKFont(text.Any(ch=>ch is >= '\u2190' and <= '\u27ff')?symbols:type,size);using var p=new SKPaint{IsAntialias=true,Color=color};
        if(font.MeasureText(text)>maximum){while(text.Length>1&&font.MeasureText(text+"…")>maximum)text=text[..^1];text+="…";}
        var align=center?SKTextAlign.Center:SKTextAlign.Left;
        if(glowing){using var blur=SKMaskFilter.CreateBlur(SKBlurStyle.Normal,8);p.MaskFilter=blur;p.Color=color.WithAlpha((byte)(110*glow));c.DrawText(text,x,y,align,font,p);p.MaskFilter=null;p.Color=color;}
        c.DrawText(text,x,y,align,font,p);
    }
    private static void Fill(SKCanvas c,SKRect r,SKColor color){using var p=new SKPaint{Color=color};c.DrawRect(r,p);}
    private static void Line(SKCanvas c,float x,float y,float x2,float y2,SKColor color,float width=1){using var p=new SKPaint{Color=color,StrokeWidth=width,IsAntialias=true};c.DrawLine(x,y,x2,y2,p);}
    private static void Circle(SKCanvas c,float x,float y,float r,SKColor color,bool stroke=false,float width=1){using var p=new SKPaint{Color=color,IsAntialias=true,Style=stroke?SKPaintStyle.Stroke:SKPaintStyle.Fill,StrokeWidth=width};c.DrawCircle(x,y,r,p);}
    private static void Bar(SKCanvas c,float x,float y,float w,double? v,SKColor color){Fill(c,new(x,y,x+w,y+7),new(15,48,70));if(v!=null)Fill(c,new(x,y,x+w*(float)Math.Clamp(v.Value,0,1),y+7),color);}
    private static void DrawCover(SKCanvas c,SKBitmap b,SKRect rect,SKPaint? paint=null){float scale=Math.Max(rect.Width/b.Width,rect.Height/b.Height);float sw=rect.Width/scale,sh=rect.Height/scale;var source=new SKRect((b.Width-sw)/2,(b.Height-sh)/2,(b.Width+sw)/2,(b.Height+sh)/2);c.DrawBitmap(b,source,rect,paint);}
    public static string V(double? value,string unit,int decimals=0)=>value is {} n&&double.IsFinite(n)?n.ToString("F"+decimals,CultureInfo.InvariantCulture)+unit:"—";
    private static string Time(double sec)=>TimeSpan.FromSeconds(Math.Max(0,sec)).ToString(@"m\:ss");
    private static string Condition(int code)=>code switch{0=>Localizer.T("Ciel dégagé"),1 or 2=>Localizer.T("Éclaircies"),3=>Localizer.T("Nuageux"),45 or 48=>Localizer.T("Brouillard"),>=51 and <=57=>Localizer.T("Bruine"),>=61 and <=67=>Localizer.T("Pluie"),>=71 and <=77=>Localizer.T("Neige"),>=80 and <=82=>Localizer.T("Averses"),>=95=>Localizer.T("Orage"),_=>Localizer.T("Nuageux")};
    public void Dispose(){foreach(var b in images.Values)b.Dispose();cover?.Dispose();type.Dispose();mono.Dispose();symbols.Dispose();}
}


