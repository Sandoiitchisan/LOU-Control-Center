using SkiaSharp;
using Lou.Core;
namespace Lou.Rendering;
public static class ComparisonReport
{
    public static void Create(string reference,string folder,string output)
    {
        using var source=SKBitmap.Decode(reference)??throw new InvalidDataException("Unreadable reference.");
        using var sheet=new SKBitmap(1920,2200);using var canvas=new SKCanvas(sheet);canvas.Clear(new SKColor(8,13,23));
        using var font=new SKFont(SKTypeface.FromFamilyName("Segoe UI"),22);using var title=new SKFont(SKTypeface.FromFamilyName("Bahnschrift"),34);using var paint=new SKPaint{IsAntialias=true,Color=new SKColor(220,235,250)};
        canvas.DrawText("LOU // NEON CITY — VISUAL COMPARISON",35,55,SKTextAlign.Left,title,paint);
        canvas.DrawText("Original concept",35,101,SKTextAlign.Left,font,paint);canvas.DrawText("Native rendering • simulated data labeled",986,101,SKTextAlign.Left,font,paint);
        // Coordinates measured on the supplied 1672×941 concept board, scaled to actual source dimensions.
        SKRect[] crops=[new(12,189,827,349),new(846,189,1659,349),new(12,399,827,586),new(846,399,1659,586),new(12,634,827,798),new(846,634,1659,798)];
        string[] notes=["Clock, three rings, tiles and network retained; typography and city recreated.","Window, cat silhouette, weather and forecasts retained; original artwork and icons.","Cover, metadata, progress and neon road; decorative visualizer labeled.","Racks, summary, services and network; readings depend on actual permissions.","Silhouette, car and large counters; no fabricated HDR/FSR values.","White clock, plants and sunset; more detailed scene than the concept."];
        int i=0;foreach(var page in Enum.GetValues<PageId>()){
            float y=140+i*338;paint.Color=SKColor.Parse("#00E5FF");canvas.DrawText($"{i+1:00} / {page.ToString().ToUpperInvariant()}",35,y,SKTextAlign.Left,font,paint);paint.Color=new(220,235,250);
            var crop=crops[i];crop=new(crop.Left*source.Width/1672f,crop.Top*source.Height/941f,crop.Right*source.Width/1672f,crop.Bottom*source.Height/941f);
            float ch=920*crop.Height/crop.Width;canvas.DrawBitmap(source,crop,new SKRect(35,y+20+(230-ch)/2,955,y+20+(230+ch)/2));
            using var rendered=SKBitmap.Decode(Path.Combine(folder,page+".png"));if(rendered!=null){float rh=920f*rendered.Height/rendered.Width;canvas.DrawBitmap(rendered,new SKRect(985,y+20+(230-rh)/2,1905,y+20+(230+rh)/2));}
            paint.Color=new(140,168,195);canvas.DrawText(notes[i],35,y+284,SKTextAlign.Left,font,paint);i++;
        }
        using var data=sheet.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(output);data.SaveTo(file);
    }
}
