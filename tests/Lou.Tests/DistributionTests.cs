using Lou.Core;
using Lou.Providers;
using Xunit;
namespace Lou.Tests;
public sealed class DistributionTests
{
    [Fact]public void Portable_and_installed_data_are_separate_and_never_use_personal_folders(){
        Assert.Equal(@"C:\Package\Data",ConfigStore.DataRoot(@"C:\Package",@"C:\Account",false));
        Assert.Equal(@"C:\Account\LouControlCenterUniversal",ConfigStore.DataRoot(@"C:\Program Files\LOU",@"C:\Account",true));
    }
    [Fact]public void Portable_backgrounds_survive_moving_or_installing(){
        Assert.Equal(@"C:\Moved\Data\Backgrounds\image.png",ConfigStore.ResolveBackground(@"Data\Backgrounds\image.png",@"C:\Moved\Data",@"C:\Moved"));
        Assert.Equal(@"C:\Account\LouControlCenterUniversal\Backgrounds\image.png",ConfigStore.ResolveBackground("Data/Backgrounds/image.png",@"C:\Account\LouControlCenterUniversal",@"C:\Program Files\LOU"));
        Assert.Equal(@"D:\Pictures\original.jpg",ConfigStore.ResolveBackground(@"D:\Pictures\original.jpg",@"C:\Account",@"C:\Package"));
    }
    [Fact]public void Installed_presentation_cache_rejects_stale_future_or_invalid_values(){
        var now=DateTimeOffset.UtcNow;var data=new MachinePresentationSnapshot(now,"active",[new(123,120,1000d/120,4)]);
        Assert.True(MachinePresentations.Valid(data,now));Assert.False(MachinePresentations.Valid(data,now.AddSeconds(6)));
        Assert.False(MachinePresentations.Valid(data,now.AddSeconds(-3)));Assert.False(MachinePresentations.Valid(data with{Activity=[new(123,double.NaN,8,1)]},now));
        Assert.False(MachinePresentations.Valid(data with{Activity=Enumerable.Repeat(new PresentationActivity(123,120,8,4),65).ToArray()},now));
    }
}
