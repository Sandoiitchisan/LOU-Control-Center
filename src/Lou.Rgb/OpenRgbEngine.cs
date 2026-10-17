using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Lou.Rgb;

/// <summary>Owns a bundled, localhost-only OpenRGB process. Never controls an external server's lifetime.</summary>
public sealed class OpenRgbEngine : IDisposable
{
    public const int Port = 16742;
    private EventWaitHandle? stop;
    private Process? helper;
    public bool IsOwned => helper is { HasExited: false };
    public static string[] Conflicts() => Process.GetProcesses().Select(p =>
    {
        using (p) { try { return p.ProcessName; } catch { return ""; } }
    }).Where(n => new[] { "OpenRGB", "GCC", "AsrPolychromeRGB", "RGBFusion", "LightingService", "iCUE", "SignalRgb" }
        .Contains(n, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task StartAsync(bool elevated, CancellationToken cancel)
    {
        if (IsOwned) return;
        if (Conflicts().Length != 0) throw new InvalidOperationException("RGB_CONFLICT");
        VerifyBundle();
        using (var probe = new TcpClient())
        {
            try { await probe.ConnectAsync(IPAddress.Loopback, Port, cancel); throw new IOException("RGB_PORT_BUSY"); }
            catch (SocketException) { }
        }
        string token = Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\Lou.Rgb.Ready." + token);
        stop?.Dispose(); stop = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\Lou.Rgb.Stop." + token);
        using var current = Process.GetCurrentProcess();
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = $"--rgb-host {current.Id} {current.StartTime.ToUniversalTime().Ticks} {token}",
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (elevated) info.Verb = "runas";
        helper = Process.Start(info) ?? throw new IOException("RGB_START_FAILED");
        try
        {
            for (int i = 0; i < 90; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (helper.HasExited) throw new IOException("RGB_START_FAILED");
                if (!ready.WaitOne(0)) { await Task.Delay(1000, cancel); continue; }
                try { using var probe = new TcpClient(); await probe.ConnectAsync(IPAddress.Loopback, Port, cancel); return; }
                catch (SocketException) { await Task.Delay(1000, cancel); }
            }
            throw new TimeoutException("RGB_START_TIMEOUT");
        }
        catch { Stop(); throw; }
    }
    public void Stop() { stop?.Set(); helper?.Dispose(); helper = null; stop?.Dispose(); stop = null; }
    public void Dispose() => Stop();

    // Called before WPF/LCD startup. Elevation stays confined to the RGB host and its bundled engine.
    public static async Task RunHostAsync(string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[1], out int parentId) || !long.TryParse(args[2], out long start) ||
            !Guid.TryParseExact(args[3], "N", out _)) throw new InvalidDataException("RGB_HOST_ARGUMENTS");
        using var parent = Process.GetProcessById(parentId);
        if (parent.StartTime.ToUniversalTime().Ticks != start ||
            !string.Equals(parent.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RGB_HOST_PARENT");
        using var requestStop = EventWaitHandle.OpenExisting("Local\\Lou.Rgb.Stop." + args[3]);
        using var ready = EventWaitHandle.OpenExisting("Local\\Lou.Rgb.Ready." + args[3]);
        using var lease = new Mutex(true, "Local\\Lou.ControlCenter.RgbEngine", out bool first);
        if (!first) throw new IOException("RGB_PORT_BUSY");
        VerifyBundle();
        // A fresh isolated OpenRGB configuration cannot load the user's existing plugins or automatic profiles.
        string config = Path.Combine(Path.GetTempPath(), "Lou-Rgb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(config);
        using var job = new RgbJob();
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "tools", "OpenRGB", "OpenRGB.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = config,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "--server", "--server-host", "127.0.0.1", "--server-port", Port.ToString(), "--noautoconnect", "--very-verbose", "--loglevel", "6", "--config", config }) info.ArgumentList.Add(arg);
        using var child = Process.Start(info) ?? throw new IOException("RGB_START_FAILED");
        var log = new Queue<string>();
        void Record(string? line) { if (line == null) return; lock (log) { log.Enqueue(line.Length > 2048 ? line[..2048] : line); while (log.Count > 500) log.Dequeue(); } }
        try
        {
            job.Add(child);
            // Drain output without unbounded buffers; the SDK provides user-facing status.
            // Stable completion marker of the pinned 1.0 binary: listening starts before device detection.
            child.OutputDataReceived += (_, line) => { Record(line.Data); if (line.Data?.Contains("Detection completed", StringComparison.Ordinal) == true) ready.Set(); };
            child.ErrorDataReceived += (_, line) => Record(line.Data);
            child.BeginOutputReadLine(); child.BeginErrorReadLine();
            while (!parent.HasExited && !child.HasExited && !requestStop.WaitOne(0)) await Task.Delay(500);
        }
        finally
        {
            if (!child.HasExited) child.Kill(true);
            await child.WaitForExitAsync();
            try {
                string diagnostic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LouControlCenterRgb");
                Directory.CreateDirectory(diagnostic);
                lock (log) File.WriteAllLines(Path.Combine(diagnostic, "engine-last.log"), new[] { $"UTC {DateTimeOffset.UtcNow:o}; parent {parentId}; engine {child.Id}" }.Concat(log));
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            // Only remove this freshly created directory, never a path supplied by configuration.
            if (Path.GetDirectoryName(Path.GetFullPath(config)) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
                try { Directory.Delete(config, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    public static void VerifyBundle()
    {
        using var manifest = typeof(OpenRgbEngine).Assembly.GetManifestResourceStream("Lou.Rgb.OpenRGB.SHA256.json")
            ?? throw new InvalidDataException("RGB_BUNDLE_INVALID");
        var files = JsonSerializer.Deserialize<Dictionary<string, string>>(manifest)!;
        string root = Path.Combine(AppContext.BaseDirectory, "tools", "OpenRGB");
        foreach (var (relative, expected) in files)
        {
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidDataException("RGB_BUNDLE_INVALID");
            using var input = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(input)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("RGB_BUNDLE_INVALID");
        }
    }
    private sealed class RgbJob : IDisposable
    {
        private readonly SafeFileHandle handle = CreateJobObject(IntPtr.Zero, null);
        public RgbJob()
        {
            var limits = new Limits { Basic = new() { Flags = 0x2000 } };
            if (handle.IsInvalid || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<Limits>()))
                throw new System.ComponentModel.Win32Exception();
        }
        public void Add(Process child) { if (!AssignProcessToJobObject(handle, child.Handle)) throw new System.ComponentModel.Win32Exception(); }
        public void Dispose() => handle.Dispose();
        [StructLayout(LayoutKind.Sequential)] private struct Basic { public long ProcessTime, JobTime; public uint Flags; public UIntPtr MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint Priority, Scheduling; }
        [StructLayout(LayoutKind.Sequential)] private struct Io { public ulong Read, Write, Other, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct Limits { public Basic Basic; public Io Io; public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref Limits info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
