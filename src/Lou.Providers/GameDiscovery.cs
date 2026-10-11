using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Lou.Core;
namespace Lou.Providers;

public sealed record GameProcess(int Id,string Executable,string Name,string Path,IntPtr Window);
public sealed record GraphicsEvidence(bool Engine,bool Graphics,bool Input,bool DesktopApp,bool LargeWindow,bool Foreground,bool Fullscreen);

// Rendering activity is required; there is no game catalogue. Native 3D tools can
// resemble games, so the classifier is deliberately described as a heuristic.
public sealed class GameDiscovery
{
    private sealed record Facts(string Exe,string Path,string Product,string[] Modules,DateTimeOffset Checked,long Started);
    private readonly Dictionary<int,Facts> cache=[];
    private int selected;
    public static string FriendlyName(string exe,string? title=null)
    {
        if(!string.IsNullOrWhiteSpace(title))return Regex.Replace(title.Trim(),@"\s+\((?:64|32)[ -]?bit.*\)$","",RegexOptions.IgnoreCase);
        string name=Regex.Replace(Path.GetFileNameWithoutExtension(exe),@"[-_](?:Win(?:32|64)|Shipping|Release)(?:[-_].*)?$","",RegexOptions.IgnoreCase);
        return Regex.Replace(name,@"(?<=[a-z])(?=[A-Z0-9])"," ").Replace('_',' ');
    }
    public static GraphicsEvidence Evidence(string exe,string product,string windowClass,IEnumerable<string> modules,bool large,bool foreground,bool fullscreen)
    {
        var names=modules.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool engine=windowClass is "UnrealWindow" or "UnityWndClass" || names.Contains("UnityPlayer.dll");
        bool graphics=names.Overlaps(["d3d9.dll","d3d11.dll","d3d12.dll","vulkan-1.dll","opengl32.dll"]);
        bool input=names.Any(n=>n!=null&&(n.StartsWith("xinput",StringComparison.OrdinalIgnoreCase)||n.Equals("gameinput.dll",StringComparison.OrdinalIgnoreCase)||n.StartsWith("steam_api",StringComparison.OrdinalIgnoreCase)));
        bool desktop=windowClass.StartsWith("Chrome_",StringComparison.OrdinalIgnoreCase)||windowClass.StartsWith("HwndWrapper",StringComparison.OrdinalIgnoreCase)
            || (!engine&&names.Overlaps(["chrome.dll","chrome_elf.dll","libcef.dll","Qt5WebEngineCore.dll","Qt6WebEngineCore.dll","libvlc.dll","libmpv-2.dll"]))
            || Regex.IsMatch(exe,@"^(?:dwm|explorer|Lou\.ControlCenter|PresentMon|ApplicationFrameHost|vlc|mpv|obs64|blender|UnrealEditor|Unity|Godot.*|steam|steamwebhelper|RadeonSoftware|Photoshop|acad)$",RegexOptions.IgnoreCase)
            || product.Contains("Editor",StringComparison.OrdinalIgnoreCase);
        return new(engine,graphics,input,desktop,large,foreground,fullscreen);
    }
    public static bool IsGame(GraphicsEvidence e,double fps,double seconds,bool alreadySelected=false,bool force=false)
    {
        if(!e.LargeWindow)return false;
        bool rendering=double.IsFinite(fps)&&fps>=8&&seconds>=1.5;
        if(force)return rendering||alreadySelected||e.Engine;
        if(e.DesktopApp)return false;
        return e.Engine || e.Graphics && (alreadySelected || rendering && e.Foreground && (e.Input||e.Fullscreen));
    }
    public GameProcess? Find(Settings settings,IReadOnlyList<PresentationActivity> activity)
    {
        var now=DateTimeOffset.UtcNow;
        foreach(int stale in cache.Where(c=>now-c.Value.Checked>TimeSpan.FromSeconds(20)).Select(c=>c.Key).ToArray())cache.Remove(stale);
        GetWindowThreadProcessId(GetForegroundWindow(),out uint foreground);
        var candidates=new List<(GameProcess Game,int Score)>();
        var activeIds=activity.Select(a=>a.ProcessId).ToHashSet();var windows=new HashSet<int>();
        EnumWindows((hwnd,_)=>{if(IsWindowVisible(hwnd)){GetWindowThreadProcessId(hwnd,out uint id);var cls=new StringBuilder(256);GetClassName(hwnd,cls,cls.Capacity);if(activeIds.Contains((int)id)||id==selected||cls.ToString() is "UnityWndClass" or "UnrealWindow")windows.Add((int)id);}return true;},IntPtr.Zero);
        foreach(int id in windows.Concat(activity.Select(a=>a.ProcessId)).Distinct().OrderByDescending(id=>id==foreground).Take(128)){
            if(id==Environment.ProcessId)continue;
            var a=activity.FirstOrDefault(a=>a.ProcessId==id);
            try{using var p=Process.GetProcessById(id);var hwnd=p.MainWindowHandle;
                if(hwnd==IntPtr.Zero||!IsWindowVisible(hwnd))continue;
                long started=0;try{started=p.StartTime.ToUniversalTime().Ticks;}catch{}
                if(!cache.TryGetValue(p.Id,out var f)||f.Started!=started||now-f.Checked>TimeSpan.FromSeconds(5)){
                    string path="",product="";string[] modules=[];
                    try{path=p.MainModule?.FileName??"";product=FileVersionInfo.GetVersionInfo(path).ProductName??"";}catch{}
                    try{modules=p.Modules.Cast<ProcessModule>().Take(512).Select(m=>m.ModuleName).ToArray();}catch{}
                    f=new(p.ProcessName,path,product,modules,now,started);if(cache.Count>=128)cache.Remove(cache.Keys.First());cache[p.Id]=f;
                }
                if(settings.IgnoredGames.Any(g=>Path.GetFileNameWithoutExtension(g).Equals(f.Exe,StringComparison.OrdinalIgnoreCase)))continue;
                var name=new StringBuilder(256);GetClassName(hwnd,name,name.Capacity);GetClientRect(hwnd,out var rect);
                var monitor=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};bool full=GetMonitorInfo(MonitorFromWindow(hwnd,2),ref monitor)&&GetWindowRect(hwnd,out var outer)&&Math.Abs(outer.Left-monitor.Monitor.Left)<=8&&Math.Abs(outer.Top-monitor.Monitor.Top)<=8&&Math.Abs(outer.Right-monitor.Monitor.Right)<=8&&Math.Abs(outer.Bottom-monitor.Monitor.Bottom)<=8;
                var placement=new WindowPlacement{Length=Marshal.SizeOf<WindowPlacement>()};bool restored=IsIconic(hwnd)&&GetWindowPlacement(hwnd,ref placement)&&placement.Normal.Right-placement.Normal.Left>=400&&placement.Normal.Bottom-placement.Normal.Top>=240;
                var evidence=Evidence(f.Exe,f.Product,name.ToString(),f.Modules,restored||rect.Right>=400&&rect.Bottom>=240,p.Id==foreground,full);
                bool force=settings.Games.Any(g=>Path.GetFileNameWithoutExtension(g).Equals(f.Exe,StringComparison.OrdinalIgnoreCase));
                if(!IsGame(evidence,a?.Fps??0,a?.ObservedSeconds??0,p.Id==selected,force))continue;
                string title=p.MainWindowTitle;if(string.IsNullOrWhiteSpace(title))title=f.Product;
                candidates.Add((new(p.Id,f.Exe,FriendlyName(f.Exe,title),f.Path,hwnd),(evidence.Foreground?100:0)+(p.Id==selected?20:0)+(evidence.Engine?10:0)));
            }catch{ /* Protected/exited processes are not evidence of a game. */ }
        }
        var result=candidates.OrderByDescending(c=>c.Score).Select(c=>c.Game).FirstOrDefault();selected=result?.Id??0;return result;
    }
    public static string WindowResolution(IntPtr window)
    {
        if(window==IntPtr.Zero||IsIconic(window)||!GetClientRect(window,out var r)||r.Right<=0||r.Bottom<=0)return "Indisponible";
        return $"{r.Right} × {r.Bottom}";
    }
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct WindowPlacement{public int Length,Flags,Show,MinX,MinY,MaxX,MaxY;public Rect Normal;}
    private delegate bool WindowCallback(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")]private static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll")]private static extern bool GetWindowPlacement(IntPtr window,ref WindowPlacement placement);
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
    [DllImport("user32.dll")]private static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(IntPtr window,StringBuilder name,int max);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
}
