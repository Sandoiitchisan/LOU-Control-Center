using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Lou.Providers;

// Windows closes this handle even when Task Scheduler terminates the collector.
internal sealed class ChildProcessLifetime:IDisposable
{
    private readonly SafeFileHandle job;
    public ChildProcessLifetime(){
        job=CreateJobObject(IntPtr.Zero,null);
        if(job.IsInvalid)throw new System.ComponentModel.Win32Exception();
        var info=new ExtendedLimits{Basic=new(){Flags=0x2000}}; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if(!SetInformationJobObject(job,9,ref info,(uint)Marshal.SizeOf<ExtendedLimits>())){job.Dispose();throw new System.ComponentModel.Win32Exception();}
    }
    public void Add(Process process){if(!AssignProcessToJobObject(job,process.Handle))throw new System.ComponentModel.Win32Exception();}
    public void Dispose()=>job.Dispose();
    [StructLayout(LayoutKind.Sequential)]private struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinimumWorkingSet,MaximumWorkingSet;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)]private struct IoCounters{public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)]private struct ExtendedLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateJobObject(IntPtr attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref ExtendedLimits info,uint size);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
}
