using Lou.Rgb;
using Xunit;

namespace Lou.Tests;

public class RgbCaseTests
{
    private static readonly RgbMode Static = new("Static", 80, 0, 0, 0, 255, 1, 1, 0, 255, 0, 2, [0]);
    private static RgbDevice Board(uint count = 0) => new(812, 0, "Fixture board", "Gigabyte", "IT5701-GIGABYTE fixture",
        "test", "fixture", "HID: fixture VID_048D&PID_5702", 0, [Static],
        [new("Header A", 1, 0, 120, count, 2, -1, [Static]), new("Header B", 1, 0, 120, count, 2, -1, [Static])], []);
    private static RgbAssignment Setting(RgbDevice d) => new() { CaseLighting = true, Identity = d.Identity,
        Topology = RgbCaseLighting.Fingerprint(d), Mode = Static.Signature, Color = "#00E5FF" };

    [Fact] public void GroupOnlyTargetsTheBoundMotherboardAndAllItsZoneOverrides()
    {
        var d = Board(); var ram = d with { SessionId = 3, Type = 1 }; var gpu = d with { SessionId = 4, Type = 2 };
        var resolved = RgbCaseLighting.Resolve([ram, d, gpu], Setting(d));
        var commands = RgbCaseLighting.Plan(resolved, Setting(d));
        Assert.Equal(3, commands.Length); Assert.Equal("", commands[0].Zone);
        Assert.Equal(d.Zones.Select(z => z.Signature), commands.Skip(1).Select(c => c.Zone));
        Assert.All(commands, c => { Assert.Equal(d.Identity, c.Identity); Assert.False(c.CaseLighting); });
    }
    [Fact] public void MissingOrAmbiguousCaseDoesNotFallBackToRam()
    {
        var d = Board();
        Assert.Throws<InvalidDataException>(() => RgbCaseLighting.Resolve([d with { Type = 1 }], Setting(d)));
        Assert.Throws<InvalidDataException>(() => RgbCaseLighting.Resolve([d, d with { SessionId = 99 }], Setting(d)));
    }
    [Fact] public void HeaderCoverageIsOnlyForReviewedNativeHardwareEffects()
    {
        var d = Board(); Assert.True(RgbCaseLighting.CanCoverNativeHeader(d, Static, d.Zones[0]));
        Assert.False(RgbCaseLighting.CanCoverNativeHeader(d with { Location = "HID: other" }, Static, d.Zones[0]));
        Assert.False(RgbCaseLighting.CanCoverNativeHeader(d, Static with { Flags = 1024 }, d.Zones[0]));
        Assert.Throws<InvalidDataException>(() => RgbCaseLighting.Validate(d with { Location = "HID: other" }, Setting(d)));
    }
    [Fact] public void GroupProfileSurvivesCoverageChangesButRejectsChangedConnectors()
    {
        var d = Board(); var active = Board(120);
        Assert.Equal(RgbCaseLighting.Fingerprint(d), RgbCaseLighting.Fingerprint(active));
        Assert.Same(active, RgbCaseLighting.Resolve([active], Setting(d)));
        var changed = d with { Zones = [d.Zones[0] with { Name = "Replacement header" }, d.Zones[1]] };
        Assert.Throws<InvalidDataException>(() => RgbCaseLighting.Resolve([changed], Setting(d)));
    }
    [Fact] public void IncompatibleZoneModeBlocksTheEntireGroupBeforeAnyCommand()
    {
        var d = Board(); var changed = d with { Zones = [d.Zones[0], d.Zones[1] with { Modes = [Static with { Name = "Other" }] }] };
        Assert.Empty(RgbCaseLighting.Modes(changed));
        Assert.Throws<InvalidDataException>(() => RgbCaseLighting.Plan(changed, Setting(changed)));
    }
    [Fact] public void DirectColorModeIsOfferedForUniformCoverageWhenAllZonesSupportIt()
    {
        var d = Board(); var direct = Static with { Name = "Direct", Flags = 32 };
        var compatible = d with { Modes = [direct], Zones = d.Zones.Select(z => z with { Modes = new[] { direct } }).ToArray() };
        Assert.Single(RgbCaseLighting.Modes(compatible));
        Assert.True(RgbCaseLighting.CanCoverNativeHeader(compatible, direct, compatible.Zones[0]));
    }
}
