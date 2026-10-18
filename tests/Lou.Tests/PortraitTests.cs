using Lou.Core;
using Lou.Devices;
using Lou.Rendering;
using SkiaSharp;
using Xunit;

namespace Lou.Tests;

public sealed class PortraitTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 14, 37, 0);

    [Theory]
    [InlineData(462, 1920)] [InlineData(480, 1280)] [InlineData(720, 1600)]
    [InlineData(480, 854)] [InlineData(320, 960)] [InlineData(240, 320)]
    [InlineData(172, 640)] [InlineData(480, 500)]
    public void Portrait_frame_uses_the_full_height_instead_of_a_centered_square(int width, int height)
    {
        using var renderer = new UniversalRenderer("missing-assets");
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var page in Enum.GetValues<PageId>())
        foreach (var data in new[] { Snapshot.Empty, Snapshot.Simulation() })
        {
            var settings = new Settings { Width = width, Height = height, Language = language, Night = false };
            using var bitmap = renderer.Render(page, data, settings, Now, 0);
            Assert.Equal((width, height), (bitmap.Width, bitmap.Height));
            // The full-height side rail must be present well outside the old square.
            int x = (int)Math.Round(12 * width / 480f);
            foreach (int y in new[] { height / 6, height * 5 / 6 })
                Assert.True(Enumerable.Range(Math.Max(0, x - 1), 3).Any(px => bitmap.GetPixel(px, y).Green > 35), $"{page} {width}x{height}, rail at {y}");
        }
    }

    [Theory] [InlineData(90)] [InlineData(270)]
    public void Portrait_pages_encode_back_to_the_negotiated_physical_raster(int rotation)
    {
        using var renderer = new UniversalRenderer("missing-assets");
        foreach (var physical in new[] { (1920, 462), (1280, 480), (320, 240), (640, 172) })
        {
            var display = new DisplaySettings { InstanceId = "test-display", Rotation = rotation, PreviewWidth = physical.Item1, PreviewHeight = physical.Item2, PageIntervalSeconds = 27, UsbFps = 24 };
            var settings = new Settings { Night = false }.ForDisplay(display);
            Assert.Equal((physical.Item2, physical.Item1, 27, 24), (settings.Width, settings.Height, settings.PageIntervalSeconds, settings.UsbFps));
            foreach (var page in Enum.GetValues<PageId>())
            {
                using var bitmap = renderer.Render(page, Snapshot.Simulation(), settings, Now, 0);
                var encoded = PixelEncoding.Frame(bitmap, physical.Item1, physical.Item2, rotation, true, false);
                Assert.InRange(encoded.Length, 1, 449999);
                using var decoded = SKBitmap.Decode(encoded);
                Assert.Equal(physical, (decoded.Width, decoded.Height));
                if (physical == (320, 240))
                    Assert.Equal(320 * 240 * 2, PixelEncoding.Frame(bitmap, 320, 240, rotation, false, true).Length);
            }
        }
    }

    [Fact]
    public void Explicit_missing_fan_reading_does_not_fall_back_to_an_unrelated_rpm()
    {
        using var renderer = new UniversalRenderer("missing-assets");
        var s = new Settings { Width = 462, Height = 1920, Night = false };
        var data = Snapshot.Empty with { Pc = new(Pump: 3000, Fans: [new(FanRole.Pump, "Pump", "Header", null, "Missing", "sensor", "controller", "provider")]) };
        using var a = renderer.Render(PageId.System, data, s, Now, 0);
        using var b = renderer.Render(PageId.System, data with { Pc = data.Pc with { Pump = null } }, s, Now, 0);
        Assert.Equal(a.Bytes, b.Bytes);
    }

    [Theory] [InlineData(240, 320)] [InlineData(462, 1920)]
    public void Dedicated_cooling_page_and_night_dimming_cover_portrait_canvas(int width, int height)
    {
        using var renderer = new UniversalRenderer("missing-assets");
        var s = new Settings { Width = width, Height = height, CoolingOnly = true, Night = false, Language = AppLanguage.English };
        using var day = renderer.Render(PageId.System, Snapshot.Empty, s, Now, 0);
        s.Night = true; s.NightStart = 0; s.NightEnd = 23;
        using var night = renderer.Render(PageId.System, Snapshot.Empty, s, Now, 0);
        long Sum(SKBitmap bitmap) => bitmap.Pixels.Sum(p => (long)p.Red + p.Green + p.Blue);
        Assert.True(Sum(night) < Sum(day));
        using var scope = Localizer.Use(AppLanguage.English);
        Assert.Equal("COOLING", Localizer.T("REFROIDISSEMENT"));
    }

    [Fact]
    public void Long_metadata_and_cover_art_remain_inside_the_portrait_frame()
    {
        using var renderer = new UniversalRenderer("missing-assets");
        using var art = new SKBitmap(64, 64); art.Erase(SKColors.Lime);
        using var png = art.Encode(SKEncodedImageFormat.Png, 100);
        string title = new('W', 250);
        foreach (var size in new[] { (240, 320), (462, 1920) })
        {
            var settings = new Settings { Width = size.Item1, Height = size.Item2, Night = false };
            var source = Snapshot.Empty with { Media = new(title, title, title, Cover: png.ToArray(), Available: true) };
            using var bitmap = renderer.Render(PageId.Music, source, settings, Now, 0);
            int coverCenter = (int)((64 + (size.Item2 * 480f / size.Item1 - 116) * .26f) * size.Item1 / 480f);
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(size.Item1 / 2, coverCenter));
            // Outside the framed content, long unbroken names must not draw glyphs.
            Assert.All(Enumerable.Range(0, size.Item2), y => Assert.True(bitmap.GetPixel(0, y).Green < 20));
        }
    }
}
