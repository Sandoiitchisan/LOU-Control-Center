using System.Globalization;
using Lou.Core;
using SkiaSharp;
namespace Lou.Rendering;

public sealed partial class UniversalRenderer:IDisposable
{
    private readonly NeonRenderer wide;private readonly AioRenderer cooling=new();
    private readonly string assets;private readonly Dictionary<string,SKBitmap?> images=[];
    private readonly SKTypeface face=SKTypeface.FromFamilyName("Bahnschrift");
    private readonly SKTypeface symbols=SKTypeface.FromFamilyName("Segoe UI Symbol");
    private SKBitmap? cover;private byte[]? coverBytes;
    private static readonly SKColor White=new(237,245,252),Muted=new(152,176,194),Pink=new(255,45,149);
    private SKColor accent=new(0,229,255);
    public UniversalRenderer(string assets){this.assets=assets;wide=new(assets);}
    public SKBitmap Render(PageId page,Snapshot d,Settings s,DateTime now,double elapsed)
    {
        using var language=Localizer.Use(s.Language);d=Localizer.Present(d,s);
        if((float)s.Width/s.Height>=3.5f&&!s.CoolingOnly)return wide.Render(page,d,s,now,elapsed);
        var result=new SKBitmap(s.Width,s.Height,SKColorType.Bgra8888,SKAlphaType.Premul);using var c=new SKCanvas(result);
        c.Clear(new SKColor(5,12,24));
        string path=s.Backgrounds.GetValueOrDefault(page,Path.Combine(assets,page switch{PageId.Music=>"music.png",PageId.Gaming=>"gaming.png",PageId.Ambient=>"ambient.png",_=>"city.png"}));
        path=ConfigStore.ResolveBackground(path);
        if(Image(path) is {} background){float drift=(float)(Math.Sin(elapsed*.12)*s.Animation*7);Cover(c,background,new(-8+drift,-8,s.Width+8+drift,s.Height+8));}
        using(var shade=new SKPaint{Color=new(3,9,20,180)})c.DrawRect(0,0,s.Width,s.Height,shade);
        accent=SKColor.TryParse(s.Accent,out var color)?color:new(0,229,255);
        if(!s.CoolingOnly&&(float)s.Width/s.Height>=1.6f){
            c.Scale(s.Height/480f);float width=s.Width*480f/s.Height;
            Landscape(c,page,d,s,now,width);
            if(s.IsNight(now)){using var dim=new SKPaint{Color=new(0,0,12,65)};c.DrawRect(0,0,width,480,dim);}
            return result;
        }
        float scale=Math.Min(s.Width/480f,s.Height/480f);c.Translate((s.Width-scale*480)/2,(s.Height-scale*480)/2);c.Scale(scale);
        if(s.CoolingOnly){using var bitmap=cooling.Render(d.Pc,AioView.Cooling,false,elapsed);c.DrawBitmap(bitmap,0,0);}
        else{
            using(var edge=new SKPaint{Color=accent.WithAlpha((byte)(45+130*s.Glow)),Style=SKPaintStyle.Stroke,StrokeWidth=1,IsAntialias=true})c.DrawRoundRect(new(12,12,468,468),15,15,edge);
            Text(c,Labels.Page(page).ToUpperInvariant(),28,40,15,accent);Text(c,now.ToString("HH:mm"),452,40,15,Muted,right:true);
            switch(page){
                case PageId.System:System(c,d.Pc);break;
                case PageId.Weather:Weather(c,d.Weather);break;
                case PageId.Music:Music(c,d.Media,elapsed);break;
                case PageId.Gaming:Gaming(c,d);break;
                case PageId.Ambient:Ambient(c,d,now);break;
            }
            Text(c,d.Demo?Localizer.T("DÉMONSTRATION"):"LOU // CONTROL CENTER",240,455,11,Muted,center:true);
        }
        if(s.IsNight(now)){using var dim=new SKPaint{Color=new(0,0,12,65)};c.DrawRect(0,0,480,480,dim);}return result;
    }
    private void System(SKCanvas c,PcData p)
    {
        Tile(c,26,65,207,102,"CPU",V(p.Cpu," %"),V(p.CpuTemp," °C"));Tile(c,247,65,207,102,"GPU",V(p.Gpu," %"),V(p.GpuTemp," °C"));
        Tile(c,26,179,207,90,Localizer.T("MÉMOIRE"),V(p.RamUsed,Localizer.T(" Go"),1),Localizer.T("sur ")+V(p.RamTotal,Localizer.T(" Go")));Tile(c,247,179,207,90,Localizer.T("RÉSEAU"),V(p.Download,Localizer.T(" Mo/s"),1),Localizer.T("Envoi ")+V(p.Upload,Localizer.T(" Mo/s"),1));
        int i=0;foreach(var role in new[]{FanRole.Radiator,FanRole.Pump,FanRole.Gpu,FanRole.Rear,FanRole.Bottom}){var row=p.Fans?.SingleOrDefault(f=>f.Role==role);double? rpm=row?.Rpm??(role switch{FanRole.Radiator=>p.CpuFan,FanRole.Pump=>p.Pump,FanRole.Gpu=>p.GpuFan,FanRole.Rear=>p.RearFan,_=>p.BottomFan});float y=291+i++*27;Text(c,row?.Label??CoolingLabels.Label(role),32,y,16,Muted,max:255);Text(c,V(rpm,Localizer.T(" tr/min")),447,y,18,White,right:true);}
        Text(c,"GPU "+V(p.GpuHotspot,"°C")+Localizer.T(" (point chaud) · ")+V(p.GpuPower," W")+" · VRAM "+V(p.Vram,Localizer.T(" Go"),1),240,430,12,Muted,center:true,max:416);
    }
    private void Weather(SKCanvas c,WeatherData w)
    {
        Text(c,w.Location,240,79,20,White,center:true,max:418);
        Text(c,V(w.Temperature,"°"),240,165,76,White,center:true);Text(c,w.Temperature==null?w.Status:Condition(w.Code),240,204,22,accent,center:true,max:416);
        Text(c,Localizer.T("Ressenti ")+V(w.FeelsLike,"°")+"  ·  "+V(w.Wind," km/h"),240,236,16,Muted,center:true);
        Text(c,Localizer.T("Humidité ")+V(w.Humidity," %"),240,255,12,Muted,center:true);
        var hours=w.Hours??[];for(int i=0;i<Math.Min(4,hours.Length);i++){float x=74+i*111;Text(c,hours[i].Time.ToString("HH:mm"),x,281,14,Muted,center:true);Text(c,V(hours[i].Temperature,"°"),x,314,26,White,center:true);Text(c,V(hours[i].Rain," %"),x,337,13,accent,center:true);}
        var days=w.Days??[];for(int i=0;i<Math.Min(3,days.Length);i++){float x=94+i*146;Text(c,days[i].Time.ToString("ddd",Localizer.Culture),x,375,15,Muted,center:true);Text(c,V(days[i].Temperature,"°")+" / "+V(days[i].Low,"°"),x,402,22,White,center:true);}
        Text(c,"Open-Meteo · CC BY 4.0 · "+w.Status,240,432,11,Muted,center:true,max:420);
    }
    private void UpdateCover(MediaData m)
    {
        if(!ReferenceEquals(coverBytes,m.Cover)){cover?.Dispose();cover=null;coverBytes=m.Cover;try{if(m.Cover!=null){using var codec=SKCodec.Create(new SKMemoryStream(m.Cover));if(codec!=null&&codec.Info.Width<=4096&&codec.Info.Height<=4096)cover=SKBitmap.Decode(codec);}}catch{}}
    }
    private void Music(SKCanvas c,MediaData m,double t)
    {
        UpdateCover(m);
        if(cover!=null)Cover(c,cover,new(145,66,335,256));else{
            using var ring=new SKPaint{IsAntialias=true,Color=Pink,Style=SKPaintStyle.Stroke,StrokeWidth=2};for(int i=0;i<4;i++)c.DrawCircle(240,161,47+i*15,ring);
            using var font=new SKFont(symbols,93);using var paint=new SKPaint{Color=White,IsAntialias=true};font.MeasureText("♫",out var bounds);c.DrawText("♫",240-(bounds.Left+bounds.Right)/2,161-(bounds.Top+bounds.Bottom)/2,font,paint);
        }
        Text(c,m.Playing?Localizer.T("ÉCOUTE EN COURS"):m.Available?Localizer.T("EN PAUSE"):Localizer.T("EN ATTENTE"),240,284,13,Pink,center:true);
        Text(c,m.Title,240,317,25,White,center:true,max:420);Text(c,m.Artist,240,344,17,accent,center:true,max:420);Text(c,m.Album,240,366,12,Muted,center:true,max:416);
        using var stroke=new SKPaint{Color=new(60,84,101),StrokeWidth=3,IsAntialias=true};c.DrawLine(36,380,444,380,stroke);stroke.Color=accent;c.DrawLine(36,380,36+(float)(408*Math.Clamp(m.Duration>0?m.Position/m.Duration:0,0,1)),380,stroke);
        Text(c,Time(m.Position),36,408,14,Muted);Text(c,Time(m.Duration),444,408,14,Muted,right:true);
    }
    private void Gaming(SKCanvas c,Snapshot d)
    {
        var g=d.Gaming;Text(c,g.Name,240,87,25,White,center:true,max:418);
        Text(c,V(g.Fps,""),240,185,89,accent,center:true);Text(c,Localizer.T("IMAGES / SECONDE"),240,218,15,Muted,center:true);
        Tile(c,26,246,207,84,Localizer.T("TEMPS PAR IMAGE"),V(g.Frametime," ms",1),"");Tile(c,247,246,207,84,Localizer.T("TAILLE DE FENÊTRE"),g.Resolution,"");
        Text(c,"CPU "+V(d.Pc.Cpu," %")+"   ·   GPU "+V(d.Pc.Gpu," %"),240,370,22,White,center:true);Text(c,"VRAM "+V(d.Pc.Vram,Localizer.T(" Go"),1),240,400,17,Muted,center:true);Text(c,g.FpsStatus,240,430,12,Muted,center:true,max:420);
    }
    private void Ambient(SKCanvas c,Snapshot d,DateTime now)
    {
        Text(c,now.ToString("HH:mm"),240,233,89,White,center:true);Text(c,now.ToString("dddd dd MMMM",Localizer.Culture),240,277,21,Muted,center:true);
        Text(c,V(d.Weather.Temperature,"°C"),240,355,36,accent,center:true);Text(c,d.Weather.Location,240,390,17,White,center:true,max:410);
    }
    private void Tile(SKCanvas c,float x,float y,float w,float h,string label,string value,string sub){using var paint=new SKPaint{Color=new(4,16,31,210),IsAntialias=true};c.DrawRoundRect(new(x,y,x+w,y+h),10,10,paint);Text(c,label,x+13,y+23,12,Muted,max:w-26);Text(c,value,x+13,y+58,value.Length>15?16:27,White,max:w-26);if(sub!="")Text(c,sub,x+w-13,y+h-12,14,accent,right:true,max:w-26);}
    private void Text(SKCanvas c,string text,float x,float y,float size,SKColor color,bool center=false,bool right=false,float max=480){using var f=new SKFont(face,size);using var p=new SKPaint{Color=color,IsAntialias=true};while(text.Length>1&&f.MeasureText(text)>max)text=text[..^2]+"…";float w=f.MeasureText(text);c.DrawText(text,x-(center?w/2:right?w:0),y,f,p);}
    private SKBitmap? Image(string path){if(images.TryGetValue(path,out var result))return result;try{using var codec=SKCodec.Create(path);result=codec!=null&&codec.Info.Width<=8192&&codec.Info.Height<=8192?SKBitmap.Decode(codec):null;}catch{result=null;}images[path]=result;return result;}
    private static void Cover(SKCanvas c,SKBitmap b,SKRect target){float s=Math.Max(target.Width/b.Width,target.Height/b.Height);float w=target.Width/s,h=target.Height/s;c.DrawBitmap(b,new SKRect((b.Width-w)/2,(b.Height-h)/2,(b.Width+w)/2,(b.Height+h)/2),target);}
    private static string V(double? v,string suffix,int decimals=0)=>NeonRenderer.V(v,suffix,decimals);
    private static string Time(double s)=>TimeSpan.FromSeconds(Math.Max(0,s)).ToString(@"m\:ss");
    private static string Condition(int code)=>code switch{0=>Localizer.T("Ciel dégagé"),1 or 2=>Localizer.T("Éclaircies"),3=>Localizer.T("Nuageux"),45 or 48=>Localizer.T("Brouillard"),>=51 and <=57=>Localizer.T("Bruine"),>=61 and <=67=>Localizer.T("Pluie"),>=71 and <=77=>Localizer.T("Neige"),>=80 and <=82=>Localizer.T("Averses"),>=85 and <=86=>Localizer.T("Neige"),>=95=>Localizer.T("Orage"),_=>Localizer.T("Indisponible")};
    public static SKBitmap Calibration(int width,int height){var b=new SKBitmap(width,height);using var c=new SKCanvas(b);c.Clear(new SKColor(4,15,27));using var p=new SKPaint{Color=SKColors.White,IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeWidth=4};c.DrawRect(3,3,width-6,height-6,p);p.Style=SKPaintStyle.Fill;using var font=new SKFont(SKTypeface.Default,Math.Min(width/18f,height/10f));c.DrawText(Localizer.T("LOU // HAUT"),width/2,height*.22f,SKTextAlign.Center,font,p);c.DrawText($"{width} × {height}",width/2,height*.43f,SKTextAlign.Center,font,p);c.DrawText(Localizer.T("BAS"),width/2,height*.60f,SKTextAlign.Center,font,p);SKColor[] colors=[SKColors.Red,SKColors.Lime,SKColors.Blue,SKColors.Cyan,SKColors.Magenta,SKColors.Yellow];for(int i=0;i<6;i++){p.Color=colors[i];c.DrawRect(6+i*(width-12)/6f,height*.69f,(width-12)/6f,height*.29f,p);}return b;}
    public void Dispose(){wide.Dispose();cooling.Dispose();cover?.Dispose();foreach(var b in images.Values)b?.Dispose();face.Dispose();symbols.Dispose();}
}


