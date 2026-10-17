using Lou.Rgb;
using System.Text.Json;
using Xunit;

namespace Lou.Tests;

public class RgbEffectTests
{
    [Fact] public void EveryEffectIsDeterministicBoundedAndRespectsBlackout()
    {
        foreach (var effect in RgbEffects.All)
        {
            var s = new RgbAssignment { Effect = effect.Id, Color = "#19BCFF", Brightness = 61 };
            foreach (double time in new[] { 0, .1, 1, 9, 1e8 })
            {
                var colors = RgbEffects.Render(s, 257, time);
                Assert.Equal(colors, RgbEffects.Render(s, 257, time));
                Assert.All(colors, c => { Assert.Equal(0u, c >> 24); Assert.InRange(c & 255, 0u, 156u); Assert.InRange(c >> 8 & 255, 0u, 156u); Assert.InRange(c >> 16 & 255, 0u, 156u); });
                s.Brightness = 0; Assert.All(RgbEffects.Render(s, 257, time), c => Assert.Equal(0u, c)); s.Brightness = 61;
            }
        }
    }
    [Fact] public void BreathingUsesElapsedTimeAndSpeedInsteadOfFrameCount()
    {
        var s = new RgbAssignment { Effect = "breathing", Color = "#FF0000", Speed = 50 };
        double period = RgbEffects.Period(50);
        Assert.Equal(0u, RgbEffects.Render(s, 1, 0)[0]);
        Assert.Equal(255u, RgbEffects.Render(s, 1, period / 2)[0]);
        Assert.Equal(0u, RgbEffects.Render(s, 1, period)[0]);
        Assert.True(RgbEffects.Period(100) < period && period < RgbEffects.Period(0));
    }
    [Fact] public void RainbowVariesPerLedWhileColorCycleIsUniform()
    {
        var s = new RgbAssignment { Effect = "rainbow" };
        var frame = RgbEffects.Render(s, 48, .4);
        Assert.True(frame.Distinct().Count() > 10); Assert.Equal(frame.Take(24), frame.Skip(24));
        s.Effect = "cycle"; Assert.Single(RgbEffects.Render(s, 48, .4).Distinct());
    }
    [Fact] public void LegacyProfilesDefaultToHardwareModeAndUnknownEffectsAreRejected()
    {
        var s = JsonSerializer.Deserialize<RgbAssignment>("{}")!; Assert.Equal("", s.Effect);
        s.Effect = "unknown"; Assert.Throws<InvalidDataException>(() => RgbEffects.Render(s, 1, 0));
        s.Effect = "rainbow"; Assert.Throws<InvalidDataException>(() => RgbEffects.Render(s, 20000, 0));
        Assert.Throws<InvalidDataException>(() => RgbEffects.Render(s, 1, double.NaN));
    }
    [Fact] public void EffectProfilesRoundTripWithoutMutableReferences()
    {
        var s = new RgbAssignment { Effect = "wave", Speed = 73, Brightness = 42 };
        var clone = s.Copy(); s.Speed = 1;
        var restored = JsonSerializer.Deserialize<RgbAssignment>(JsonSerializer.Serialize(clone))!;
        Assert.Equal("wave", restored.Effect); Assert.Equal(73, restored.Speed); Assert.Equal(42, restored.Brightness); Assert.Equal(30, restored.FramesPerSecond);
    }
}
