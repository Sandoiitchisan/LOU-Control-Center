using Lou.Core;
using Xunit;
namespace Lou.Tests;
public class FramePacerTests
{
    [Fact] public void Cancellation_interrupts_a_long_wait()
    {
        using var cancellation=new CancellationTokenSource();using var pacer=new FramePacer(cancellation.Token);
        cancellation.CancelAfter(30);var elapsed=System.Diagnostics.Stopwatch.StartNew();
        Assert.False(pacer.Wait(30));Assert.True(elapsed.Elapsed.TotalSeconds<2);
    }
    [Fact] public void One_shot_timer_can_be_rearmed()
    {
        using var cancellation=new CancellationTokenSource();using var pacer=new FramePacer(cancellation.Token);
        Assert.True(pacer.Wait(.002));Assert.True(pacer.Wait(.003));cancellation.Cancel();Assert.False(pacer.Wait(0));
    }
}
