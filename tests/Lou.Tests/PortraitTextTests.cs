using System.Text.Json;
using Lou.Core;
using Lou.Rendering;
using Xunit;

namespace Lou.Tests;

public sealed class PortraitTextTests
{
    [Fact]
    public void Old_configuration_gets_larger_text_without_resetting_other_settings()
    {
        var s = JsonSerializer.Deserialize<Settings>("""{"PageIntervalSeconds":29,"Displays":[{"InstanceId":"screen","Rotation":90,"PageIntervalSeconds":37,"UsbFps":24}]}""", ConfigStore.Json)!;
        s.Validate();
        var rendered = s.ForDisplay(s.Displays[0]);
        Assert.Equal((125, 37, 24, 462, 1920), (rendered.PortraitTextPercent, rendered.PageIntervalSeconds, rendered.UsbFps, rendered.Width, rendered.Height));
        Assert.Equal(29, s.PageIntervalSeconds);
    }

    [Fact]
    public void Text_size_is_persisted_and_isolated_per_display()
    {
        var s = new Settings { Displays = [new() { InstanceId = "large", PortraitTextPercent = 150 }, new() { InstanceId = "aio", PortraitTextPercent = 100 }] };
        var copy = ConfigStore.Clone(s); copy.Validate();
        Assert.Equal(150, copy.ForDisplay(copy.Displays[0]).PortraitTextPercent);
        Assert.Equal(100, copy.ForDisplay(copy.Displays[1]).PortraitTextPercent);
        copy.Displays[0].PortraitTextPercent = 125;
        Assert.Equal(150, s.Displays[0].PortraitTextPercent);
    }

    [Theory] [InlineData(99)] [InlineData(151)] [InlineData(int.MaxValue)]
    public void Invalid_sizes_are_rejected_at_both_settings_levels(int percent)
    {
        Assert.Throws<InvalidDataException>(() => new Settings { PortraitTextPercent = percent }.Validate());
        Assert.Throws<InvalidDataException>(() => new Settings { Displays = [new() { InstanceId = "screen", PortraitTextPercent = percent }] }.Validate());
    }

    [Theory] [InlineData(240, 320)] [InlineData(462, 1920)] [InlineData(172, 640)] [InlineData(480, 500)]
    public void Scaling_changes_portrait_text_and_does_not_leak_between_frames(int width, int height)
    {
        using var renderer = new UniversalRenderer("missing-assets");
        var now = new DateTime(2026, 10, 18, 14, 37, 0);
        var data = Snapshot.Simulation();
        foreach (var lang in Enum.GetValues<AppLanguage>())
        foreach (var page in Enum.GetValues<PageId>())
        {
            var s = new Settings { Width = width, Height = height, Night = false, Language = lang, PortraitTextPercent = 100 };
            using var small = renderer.Render(page, data, s, now, 0);
            s.PortraitTextPercent = 150;
            using var large = renderer.Render(page, data, s, now, 0);
            Assert.Equal((width, height), (large.Width, large.Height));
            Assert.False(small.Bytes.SequenceEqual(large.Bytes));
            s.PortraitTextPercent = 100;
            using var again = renderer.Render(page, data, s, now, 0);
            Assert.Equal(small.Bytes, again.Bytes);
        }
    }

    [Theory] [InlineData(1920, 462)] [InlineData(480, 480)] [InlineData(320, 240)]
    public void Landscape_and_square_pixels_are_unchanged_by_portrait_setting(int width, int height)
    {
        using var renderer = new UniversalRenderer("missing-assets");
        var now = new DateTime(2026, 10, 18, 14, 37, 0);
        foreach (var page in Enum.GetValues<PageId>())
        {
            var s = new Settings { Width = width, Height = height, Night = false, PortraitTextPercent = 100 };
            using var a = renderer.Render(page, Snapshot.Empty, s, now, 0);
            s.PortraitTextPercent = 150;
            using var b = renderer.Render(page, Snapshot.Empty, s, now, 0);
            Assert.Equal(a.Bytes, b.Bytes);
        }
    }
}
