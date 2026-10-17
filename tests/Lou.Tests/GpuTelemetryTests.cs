using Lou.Core;
using Lou.Providers;
using Xunit;

namespace Lou.Tests;

public sealed class GpuTelemetryTests
{
    // Synthetic instances: the identity includes the instance suffix, not just the model.
    private const string Device = @"PCI\VEN_1002&DEV_TEST\INSTANCE_A";
    private static readonly DateTimeOffset Now = new(2026,10,15,12,0,0,TimeSpan.Zero);
    private static HardwareReading Core(double? value, string device = Device) =>
        new("/gpu-amd/0/temperature/0", "AMD Radeon RX", "GpuAmd", "GPU Core", "Temperature", value, "/gpu-amd/0", DeviceId:device);
    private static PcData Adl => new(GpuTemp:61, GpuHotspot:73, GpuPower:90, Vram:4, GpuDeviceId:Device);
    private static HardwareSnapshot Snapshot(params HardwareReading[] rows) => new(Now,"active",rows,"board");

    [Fact]
    public void Missing_collector_value_preserves_current_adl_temperature_for_same_physical_card()
    {
        var result = SensorCatalog.Enrich(Adl, Snapshot(Core(null)), new(), Now);
        Assert.Equal(61, result.GpuTemp);
        Assert.Equal(73, result.GpuHotspot);
        Assert.Equal(90, result.GpuPower);
        Assert.Equal(4, result.Vram);
    }

    [Fact]
    public void Valid_collector_value_replaces_adl_value()
    {
        Assert.Equal(65, SensorCatalog.Enrich(Adl, Snapshot(Core(65)), new(), Now).GpuTemp);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"PCI\VEN_1002&DEV_TEST\INSTANCE_B")]
    [InlineData(@"PCI\VEN_1002&DEV_TEST")]
    public void Same_model_or_missing_identity_does_not_authorize_cross_card_fallback(string identity)
    {
        var result = SensorCatalog.Enrich(Adl, Snapshot(Core(null,identity)), new(), Now);
        Assert.Null(result.GpuTemp);
        Assert.Null(result.GpuHotspot);
        Assert.Null(result.GpuPower);
        Assert.Null(result.Vram);
    }

    [Fact]
    public void Explicit_gpu_selection_can_use_same_card_fallback_even_with_an_integrated_gpu()
    {
        var integrated = Core(43, "PCI-INTEGRATED") with { ControllerId="/gpu-amd/1", Hardware="Integrated GPU" };
        var config = new Settings { GpuController="/gpu-amd/0", GpuHardware="AMD Radeon RX", GpuDeviceId=Device };
        Assert.Equal(61, SensorCatalog.Enrich(Adl, Snapshot(Core(null),integrated), config, Now).GpuTemp);
        Assert.Null(SensorCatalog.Enrich(Adl, Snapshot(Core(null),integrated), new(), Now).GpuTemp);
    }

    [Fact]
    public void Explicit_temperature_override_is_not_bypassed_by_adl_fallback()
    {
        var config = new Settings { GpuTemperature=new("LibreHardwareMonitor","board","/gpu-amd/0","missing") };
        Assert.Null(SensorCatalog.Enrich(Adl, Snapshot(Core(null)), config, Now).GpuTemp);
    }

    [Fact]
    public void Stale_collector_values_cannot_replace_current_adl_reading()
    {
        Assert.Equal(61, SensorCatalog.Enrich(Adl, Snapshot(Core(80)) with {Time=Now.AddSeconds(-10)}, new(), Now).GpuTemp);
    }

    [Fact]
    public void Lhm_fan_assignment_does_not_become_an_adl_adapter_selector()
    {
        var fan = new FanBinding(FanRole.Gpu,"LibreHardwareMonitor","board","/gpu-amd/0","/fan/0","GPU");
        Assert.Empty(AmdProvider.PreferredController([fan]));
        Assert.Equal("/amd-adl/physical", AmdProvider.PreferredController([fan with {Provider="AMD ADL",ControllerId="/amd-adl/physical"}]));
    }
}
