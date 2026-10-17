using Lou.Core;
using SkiaSharp;
namespace Lou.Rendering;

public sealed partial class UniversalRenderer
{
    private void Landscape(SKCanvas c,PageId page,Snapshot d,Settings s,DateTime now,float width)
    {
        using(var edge=new SKPaint{Color=accent.WithAlpha((byte)(45+130*s.Glow)),Style=SKPaintStyle.Stroke,StrokeWidth=1,IsAntialias=true})c.DrawRoundRect(new(12,12,width-12,468),15,15,edge);
        Text(c,Labels.Page(page).ToUpperInvariant(),28,40,15,accent);
        Text(c,now.ToString("HH:mm"),width-28,40,15,Muted,right:true);
        float left=width*.36f, x=left+24, right=width-x-28, center=(28+left)/2;
        switch(page){
            case PageId.System:
                float tile=(right-14)/2;
                Tile(c,x,70,tile,135,"CPU",V(d.Pc.Cpu," %"),V(d.Pc.CpuTemp," °C"));
                Tile(c,x+tile+14,70,tile,135,"GPU",V(d.Pc.Gpu," %"),V(d.Pc.GpuTemp," °C"));
                Tile(c,x,221,tile,135,Localizer.T("MÉMOIRE"),V(d.Pc.RamUsed,Localizer.T(" Go"),1),Localizer.T("sur ")+V(d.Pc.RamTotal,Localizer.T(" Go")));
                Tile(c,x+tile+14,221,tile,135,Localizer.T("RÉSEAU"),V(d.Pc.Download,Localizer.T(" Mo/s"),1),Localizer.T("Envoi ")+V(d.Pc.Upload,Localizer.T(" Mo/s"),1));
                int i=0;foreach(var role in new[]{FanRole.Radiator,FanRole.Pump,FanRole.Gpu,FanRole.Rear,FanRole.Bottom}){
                    var row=d.Pc.Fans?.SingleOrDefault(f=>f.Role==role);
                    double? rpm=row?.Rpm??(role switch{FanRole.Radiator=>d.Pc.CpuFan,FanRole.Pump=>d.Pc.Pump,FanRole.Gpu=>d.Pc.GpuFan,FanRole.Rear=>d.Pc.RearFan,_=>d.Pc.BottomFan});
                    float y=91+i++*66;
                    Text(c,row?.Label??CoolingLabels.Label(role),32,y,16,Muted,max:left-44);
                    Text(c,V(rpm,Localizer.T(" tr/min")),32,y+29,24,White,max:left-44);
                }
                Text(c,"GPU "+V(d.Pc.GpuHotspot,"°C")+Localizer.T(" (point chaud) · ")+V(d.Pc.GpuPower," W")+" · VRAM "+V(d.Pc.Vram,Localizer.T(" Go"),1),x+right/2,406,16,Muted,center:true,max:right);
                break;
            case PageId.Weather:
                var w=d.Weather;
                Text(c,w.Location,center,103,22,White,center:true,max:left-40);
                Text(c,V(w.Temperature,"°"),center,209,92,White,center:true,max:left-40);
                Text(c,w.Temperature==null?w.Status:Condition(w.Code),center,257,25,accent,center:true,max:left-40);
                Text(c,Localizer.T("Ressenti ")+V(w.FeelsLike,"°"),center,309,17,Muted,center:true,max:left-40);
                Text(c,V(w.Wind," km/h")+" · "+Localizer.T("Humidité ")+V(w.Humidity," %"),center,343,15,Muted,center:true,max:left-40);
                var hours=w.Hours??[];int count=Math.Min(6,hours.Length);
                for(int h=0;h<count;h++){float cx=x+right/count*(h+.5f);Text(c,hours[h].Time.ToString("HH:mm"),cx,116,15,Muted,center:true);Text(c,V(hours[h].Temperature,"°"),cx,162,29,White,center:true);Text(c,V(hours[h].Rain," %"),cx,197,14,accent,center:true);}
                var days=w.Days??[];count=Math.Min(3,days.Length);
                for(int h=0;h<count;h++){float cx=x+right/count*(h+.5f);Text(c,days[h].Time.ToString("ddd",Localizer.Culture),cx,272,19,Muted,center:true);Text(c,V(days[h].Temperature,"°")+" / "+V(days[h].Low,"°"),cx,319,27,White,center:true);}
                Text(c,"Open-Meteo · CC BY 4.0 · "+w.Status,x+right/2,400,13,Muted,center:true,max:right);
                break;
            case PageId.Music:
                var m=d.Media;UpdateCover(m);float side=Math.Min(left-40,318),cy=246;
                if(cover!=null)Cover(c,cover,new(center-side/2,cy-side/2,center+side/2,cy+side/2));
                else{
                    using var ring=new SKPaint{Color=Pink,Style=SKPaintStyle.Stroke,StrokeWidth=2,IsAntialias=true};
                    for(int r=0;r<4;r++)c.DrawCircle(center,cy,side*(.21f+r*.08f),ring);
                    using var font=new SKFont(symbols,side*.4f);using var paint=new SKPaint{Color=White,IsAntialias=true};
                    font.MeasureText("♫",out var bounds);c.DrawText("♫",center-(bounds.Left+bounds.Right)/2,cy-(bounds.Top+bounds.Bottom)/2,font,paint);
                }
                Text(c,m.Playing?Localizer.T("ÉCOUTE EN COURS"):m.Available?Localizer.T("EN PAUSE"):Localizer.T("EN ATTENTE"),x,113,17,Pink,max:right);
                Text(c,m.Title,x,178,37,White,max:right);Text(c,m.Artist,x,222,24,accent,max:right);Text(c,m.Album,x,262,17,Muted,max:right);
                using(var stroke=new SKPaint{Color=new(60,84,101),StrokeWidth=4,IsAntialias=true}){c.DrawLine(x,343,x+right,343,stroke);stroke.Color=accent;c.DrawLine(x,343,x+right*(float)Math.Clamp(m.Duration>0?m.Position/m.Duration:0,0,1),343,stroke);}
                Text(c,Time(m.Position),x,379,17,Muted);Text(c,Time(m.Duration),x+right,379,17,Muted,right:true);
                break;
            case PageId.Gaming:
                var g=d.Gaming;
                Text(c,g.Name,center,107,25,White,center:true,max:left-40);Text(c,V(g.Fps,""),center,249,100,accent,center:true,max:left-40);
                Text(c,Localizer.T("IMAGES / SECONDE"),center,287,17,Muted,center:true,max:left-40);
                Text(c,g.FpsStatus,center,372,14,Muted,center:true,max:left-40);
                Tile(c,x,76,right,109,Localizer.T("TAILLE DE FENÊTRE"),g.Resolution,"");
                Tile(c,x,201,right,109,Localizer.T("TEMPS PAR IMAGE"),V(g.Frametime," ms",1),"");
                Text(c,"CPU "+V(d.Pc.Cpu," %")+"   ·   GPU "+V(d.Pc.Gpu," %"),x,356,23,White,max:right);
                Text(c,"VRAM "+V(d.Pc.Vram,Localizer.T(" Go"),1),x,394,18,Muted,max:right);
                break;
            case PageId.Ambient:
                Text(c,now.ToString("HH:mm"),width*.3f,250,110,White,center:true,max:width*.5f);
                Text(c,now.ToString("dddd dd MMMM",Localizer.Culture),width*.3f,298,24,Muted,center:true,max:width*.5f);
                Text(c,V(d.Weather.Temperature,"°C"),width*.76f,236,64,accent,center:true,max:width*.36f);
                Text(c,d.Weather.Location,width*.76f,286,22,White,center:true,max:width*.36f);
                break;
        }
        Text(c,d.Demo?Localizer.T("DÉMONSTRATION"):"LOU // CONTROL CENTER",width/2,455,11,Muted,center:true);
    }
}
