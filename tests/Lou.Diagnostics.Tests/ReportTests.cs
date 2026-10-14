using Lou.Diagnostics;
using Xunit;

namespace Lou.Diagnostics.Tests;

public class ReportTests
{
    [Fact] public void UsbConnectionUsesPackedWindowsOffsets()
    {
        var bytes = new byte[35]; bytes[4] = 18; bytes[5] = 1; bytes[31] = 1;
        Assert.True(DisplayRules.ConnectionPresent(bytes));
        bytes[31] = 2; Assert.False(DisplayRules.ConnectionPresent(bytes));
        Assert.False(DisplayRules.ConnectionPresent(bytes.AsSpan(0, 34)));
    }
    [Theory]
    [InlineData("Generic PnP Monitor", "Monitor")]
    [InlineData("Gigabyte IT5702", "USB")]
    [InlineData("RGB controller", "USB")]
    [InlineData("Gaming keyboard LCD", "Keyboard")]
    [InlineData("USB mouse", "HIDClass")]
    public void UnrelatedHardwareIsExcluded(string name, string kind) => Assert.True(DisplayRules.Excluded(name, kind));
    [Theory]
    [InlineData("Thermalright Trofeo Vision 360")]
    [InlineData("USB LCD")]
    [InlineData("AMOLED panel")]
    public void NewDisplayNamesDoNotNeedAKnownUsbId(string name) => Assert.True(DisplayRules.NamedScreen(name));
    [Fact] public void InstanceSerialAndPathsCannotEnterUsbIdentity()
    {
        Assert.Equal("87AD:70DB", DisplayRules.UsbId(@"USB\VID_87ad&PID_70db\PRIVATE-SERIAL-123456"));
        Assert.Equal("", DisplayRules.UsbId(@"C:\Users\PrivateName\private.txt"));
    }
    [Fact] public void DeviceTextUsesPublicTokensInsteadOfRawPersonalNames()
    {
        string input = @"Thermalright Trofeo Vision 360 PrivateName C:\Users\SecretAccount SN012345 10.0.0.1 user@example.com";
        Assert.Equal("Thermalright Trofeo Vision 360", DisplayRules.ProductLabel(input));
        Assert.Equal("Other or unavailable", DisplayRules.ServiceLabel(@"C:\Users\SecretAccount\Driver"));
        Assert.Equal("Unavailable", DisplayRules.VersionLabel("PrivateName"));
        Assert.Equal("10.0.26100.1", DisplayRules.VersionLabel("10.0.26100.1"));
    }
    [Fact] public void ConfigurationOnlyRetainsInterfaceAndEndpointFields()
    {
        byte[] data = [9,2,32,0,1,1,0,128,50, 9,4,2,0,2,255,0,0,99, 7,5,1,2,0,2,0, 7,5,129,2,0,2,1];
        var result = new DisplayInfo(); DisplayRules.ParseConfiguration(data, result);
        Assert.Equal(new InterfaceInfo(2,0,255,0,0), Assert.Single(result.Interfaces));
        Assert.Equal(2, result.Endpoints.Count); Assert.Equal(512, result.Endpoints[0].PacketSize);
        Assert.Equal(129, result.Endpoints[1].Address);
    }
    [Theory]
    [InlineData(new byte[] { 0,4 })]
    [InlineData(new byte[] { 1,4 })]
    [InlineData(new byte[] { 9,4,0 })]
    public void MalformedDescriptorsDoNotLoopOrOverrun(byte[] bytes)
    {
        var result = new DisplayInfo(); DisplayRules.ParseConfiguration(bytes, result);
        Assert.NotEmpty(result.Notes); Assert.Empty(result.Interfaces);
    }
    [Fact] public void IssueLinkContainsNoReportOrLocalPath()
    {
        var url = new Uri(DisplayRules.IssueUrl);
        Assert.Equal("https", url.Scheme); Assert.Equal("github.com", url.Host);
        Assert.Equal("/Sandoiitchisan/LOU-Control-Center/issues/new", url.AbsolutePath);
        Assert.DoesNotContain("C:", Uri.UnescapeDataString(url.Query));
        Assert.Contains("attach", Uri.UnescapeDataString(url.Query), StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void EmptyAndPartialAreExplicitAndNeverClaimCompatibility()
    {
        var report = new DisplayReport(); string text = report.Text();
        Assert.Contains("PARTIAL", text); Assert.Contains("does not prove", text); Assert.Contains("not queried", text);
    }
}
