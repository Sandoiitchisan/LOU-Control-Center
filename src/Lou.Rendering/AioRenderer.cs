using System.Globalization;
using Lou.Core;
using SkiaSharp;
namespace Lou.Rendering;

public sealed class AioRenderer : IDisposable
{
    private readonly SKTypeface face=SKTypeface.FromFamilyName("Bahnschrift");
    private static readonly SKColor Cyan=new(0,229,255),Pink=new(255,32,149),White=new(231,242,252),Muted=new(119,151,175);
    private void Label(SKCanvas c,string text,float x,float y,float size,SKColor color){using var font=new SKFont(face,size);using var paint=new SKPaint{Color=color,IsAntialias=true};SKRect bounds=default;font.MeasureText(text,out bounds);c.DrawText(text,x-(bounds.Left+bounds.Right)/2,y-(bounds.Top+bounds.Bottom)/2,SKTextAlign.Left,font,paint);}
    public SKBitmap Render(PcData pc,AioView view,bool night,double seconds)
    {
        var bitmap=new SKBitmap(480,480);using var c=new SKCanvas(bitmap);c.Clear(new SKColor(6,12,24));
        using(var gradient=new SKPaint{Shader=SKShader.CreateRadialGradient(new(240,180),340,[new SKColor(8,36,54),new SKColor(6,12,24)],null,SKShaderTileMode.Clamp)})c.DrawRect(0,0,480,480,gradient);
        using var stroke=new SKPaint{Color=new SKColor(28,66,87),Style=SKPaintStyle.Stroke,StrokeWidth=2,IsAntialias=true};c.DrawRoundRect(new SKRect(14,14,466,466),18,18,stroke);
        Label(c,Localizer.T("LOU  //  REFROIDISSEMENT"),240,35,17,Muted);
        float centerY=view==AioView.Clock?244:205;
        for(int i=0;i<60;i++){double a=(i*6-90)*Math.PI/180;stroke.Color=i%5==0?Muted:new SKColor(27,63,81);stroke.StrokeWidth=i%5==0?2:1;c.DrawLine(240+(float)Math.Cos(a)*164,centerY+(float)Math.Sin(a)*164,240+(float)Math.Cos(a)*(i%5==0?173:168),centerY+(float)Math.Sin(a)*(i%5==0?173:168),stroke);}
        stroke.StrokeWidth=8;stroke.Color=new SKColor(15,49,65);c.DrawCircle(240,centerY,147,stroke);
        double percent=Math.Clamp((pc.CpuTemp??0)/100,0,1);stroke.Color=pc.CpuTemp>=90?Pink:Cyan;stroke.StrokeCap=SKStrokeCap.Round;
        using(var arc=new SKPath()){arc.AddArc(new(93,centerY-147,387,centerY+147),-90,(float)(360*percent));c.DrawPath(arc,stroke);}
        if(view==AioView.Visual){stroke.Color=Pink.WithAlpha(110);stroke.StrokeWidth=2;using var orbit=new SKPath();orbit.AddArc(new(106,centerY-134,374,centerY+134),(float)(seconds*12%360),65);c.DrawPath(orbit,stroke);}
        Label(c,Localizer.T("PROCESSEUR"),240,centerY-62,19,Muted);
        Label(c,pc.CpuTemp is {} temp?temp.ToString("0",CultureInfo.InvariantCulture)+"°":"—",240,centerY+4,view==AioView.Visual?75:88,White);
        Label(c,pc.CpuTemp==null?Localizer.T("TEMPÉRATURE INDISPONIBLE"):pc.Cpu is {} load?Localizer.F($"CHARGE  {load:0} %"):Localizer.T("CHARGE  —"),240,centerY+70,17,Cyan);
        if(view==AioView.Clock){Label(c,DateTime.Now.ToString("HH:mm"),240,83,40,White);Label(c,"CPU_FAN  "+Rpm(pc.CpuFan)+Localizer.T("   /   POMPE  ")+Rpm(pc.Pump),240,440,15,Muted);}
        else{
            stroke.StrokeWidth=1;stroke.Color=new SKColor(29,66,86);c.DrawLine(34,389,446,389,stroke);c.DrawLine(240,404,240,447,stroke);
            Label(c,Localizer.T("RADIATEUR AIO"),133,407,14,Muted);Label(c,Localizer.T("POMPE AIO"),347,407,14,Muted);
            Label(c,Rpm(pc.CpuFan),133,434,23,Cyan);Label(c,Rpm(pc.Pump),347,434,23,Pink);
        }
        if(night){using var dim=new SKPaint{Color=new SKColor(0,0,12,75)};c.DrawRect(0,0,480,480,dim);}return bitmap;
    }
    private static string Rpm(double? value)=>value is {} v?Localizer.F($"{v:0} tr/min"):"—";
    public SKBitmap Calibration()
    {
        var bitmap=new SKBitmap(480,480);using var c=new SKCanvas(bitmap);c.Clear(new SKColor(6,12,24));
        using var p=new SKPaint{Color=White,Style=SKPaintStyle.Stroke,StrokeWidth=4,IsAntialias=true};c.DrawRect(3,3,474,474,p);
        Label(c,Localizer.T("LOU // HAUT"),240,59,36,Cyan);Label(c,Localizer.T("ÉCRAN AIO"),240,148,42,White);Label(c,"480 × 480",240,208,35,White);Label(c,Localizer.T("BAS"),240,333,30,Pink);
        SKColor[] colors=[SKColors.Red,SKColors.Lime,SKColors.Blue,SKColors.Cyan,SKColors.Magenta,SKColors.Yellow];p.Style=SKPaintStyle.Fill;for(int i=0;i<6;i++){p.Color=colors[i];c.DrawRect(12+i*76,375,76,90,p);}return bitmap;
    }
    public void Dispose()=>face.Dispose();
}
