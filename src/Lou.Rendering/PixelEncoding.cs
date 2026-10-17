using System.Runtime.InteropServices;
using SkiaSharp;
using Lou.Core;
namespace Lou.Rendering;

public static class PixelEncoding
{
    public static byte[] Frame(SKBitmap frame,int width,int height,int rotation,bool jpeg,bool bigEndian,int initialQuality=90)
    {
        if(rotation is not (0 or 90 or 180 or 270)||width<1||height<1||width>3840||height>2160)throw new InvalidDataException(Localizer.T("Frame invalide."));
        int logicalWidth=rotation is 90 or 270?height:width,logicalHeight=rotation is 90 or 270?width:height;
        if(frame.Width!=logicalWidth||frame.Height!=logicalHeight)throw new InvalidDataException(Localizer.T("Frame invalide."));
        using var native=new SKBitmap(width,height);using var canvas=new SKCanvas(native);canvas.Clear(SKColors.Black);
        canvas.Translate(width/2f,height/2f);canvas.RotateDegrees(rotation);canvas.Translate(-frame.Width/2f,-frame.Height/2f);canvas.DrawBitmap(frame,0,0);
        if(!jpeg)return Rgb565(native,bigEndian);
        for(int quality=Math.Clamp(initialQuality,40,100);quality>=40;quality-=10){using var encoded=native.Encode(SKEncodedImageFormat.Jpeg,quality);if(encoded.Size<450000)return encoded.ToArray();}
        throw new InvalidDataException(Localizer.T("JPEG trop volumineux : frame abandonnée."));
    }
    public static byte[] Rgb565(SKBitmap bitmap,bool bigEndian)
    {
        // A defined color type avoids platform/native bitmap channel-order assumptions.
        using var bgra=new SKBitmap(bitmap.Width,bitmap.Height,SKColorType.Bgra8888,SKAlphaType.Opaque);
        using(var canvas=new SKCanvas(bgra)){canvas.Clear(SKColors.Black);canvas.DrawBitmap(bitmap,0,0);}
        var input=new byte[bgra.ByteCount];Marshal.Copy(bgra.GetPixels(),input,0,input.Length);
        var output=new byte[checked(bitmap.Width*bitmap.Height*2)];int target=0;
        for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){
            int at=y*bgra.RowBytes+x*4;int rgb=(input[at+2]>>3)<<11|(input[at+1]>>2)<<5|input[at]>>3;
            output[target++]=(byte)(bigEndian?rgb>>8:rgb);output[target++]=(byte)(bigEndian?rgb:rgb>>8);
        }
        return output;
    }
}
