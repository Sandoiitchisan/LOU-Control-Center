using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Lou.Core;

// Local high-resolution wait: no busy spin and no global timer-resolution change.
// https://learn.microsoft.com/windows/win32/api/synchapi/nf-synchapi-createwaitabletimerexw
public sealed class FramePacer : IDisposable
{
    private sealed class TimerHandle : WaitHandle
    {
        public TimerHandle(SafeWaitHandle handle){SafeWaitHandle=handle;}
    }
    private readonly TimerHandle timer;
    private readonly CancellationToken cancellation;
    private readonly WaitHandle[] waits;
    public FramePacer(CancellationToken cancellation)
    {
        this.cancellation=cancellation;
        var handle=CreateWaitableTimerExW(IntPtr.Zero,null,2,0x1F0003);
        if(handle.IsInvalid){handle.Dispose();throw new Win32Exception();}
        timer=new(handle);waits=[timer,cancellation.WaitHandle];
    }
    public bool Wait(double seconds)
    {
        if(cancellation.IsCancellationRequested)return false;
        if(seconds<=0)return true;
        long due=-Math.Max(1,(long)(seconds*10_000_000));
        if(!SetWaitableTimer(timer.SafeWaitHandle,ref due,0,IntPtr.Zero,IntPtr.Zero,false))throw new Win32Exception();
        return WaitHandle.WaitAny(waits)==0&&!cancellation.IsCancellationRequested;
    }
    public void Dispose()=>timer.Dispose();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeWaitHandle CreateWaitableTimerExW(IntPtr attributes,string? name,uint flags,uint access);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(SafeWaitHandle handle,ref long due,int period,IntPtr completion,IntPtr argument,[MarshalAs(UnmanagedType.Bool)]bool resume);
}
