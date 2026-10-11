using Lou.Core;
using SkiaSharp;
namespace Lou.Rendering;
public static class TransitionRenderer
{
    public static SKBitmap Compose(SKBitmap previous,SKBitmap next,double progress,Transition effect)
    {
        var result=new SKBitmap(next.Info);using var c=new SKCanvas(result);c.Clear(SKColors.Black);float t=(float)Math.Clamp(progress,0,1);float w=next.Width,h=next.Height;
        if(t>=1||effect==Transition.None){c.DrawBitmap(next,0,0);return result;}
        if(effect==Transition.Slide){float e=t*t*(3-2*t);c.DrawBitmap(previous,-w*e,0);c.DrawBitmap(next,w*(1-e),0);}
        else if(effect==Transition.Scanline){c.DrawBitmap(previous,0,0);float eased=t*t*(3-2*t);using var alpha=new SKPaint{Color=SKColors.White.WithAlpha((byte)(255*eased))};c.DrawBitmap(next,0,0,alpha);using var p=new SKPaint{Color=new(0,229,255,(byte)(55*Math.Sin(t*Math.PI))),StrokeWidth=2};c.DrawLine(w*t,0,w*t,h,p);}
        else {c.DrawBitmap(previous,0,0);for(int i=0;i<24;i++){float row=h*i/24;float reveal=Math.Clamp(t*1.3f-(i%5)*.06f,0,1)*w;c.Save();c.ClipRect(new(0,row,reveal,row+h/24+1));c.DrawBitmap(next,(float)Math.Sin(i*17+t*29)*(1-t)*9,0);c.Restore();}}
        return result;
    }
}
