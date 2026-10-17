namespace Lou.Rgb;

/// <summary>Called by the UI thread; workers never use the UI or LCD threads.</summary>
public sealed class RgbAnimations : IDisposable
{
    private sealed record Running(string Identity, string Zone, CancellationTokenSource Stop, Task Work);
    private readonly List<Running> running = [];
    private readonly List<RgbAssignment> nativeActive = [];
    private bool disposed;
    public event Action<Exception>? Failed;
    public async Task ApplyAsync(OpenRgbClient client, RgbAssignment selection, CancellationToken stop)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await StopAsync(selection.Identity, selection.Zone);
        ObjectDisposedException.ThrowIf(disposed, this);
        var setting = selection.Copy();
        if (setting.Effect is "solid" or "off")
        { if (setting.Effect == "off") setting.Color = "#000000"; setting.Effect = ""; }
        if (setting.Effect.Length == 0) { await client.ApplyAsync(setting, stop); return; }
        if (setting.Effect == "rainbow" && !setting.CaseLighting && setting.Zone.Length == 0)
        {
            var native = RgbNativeEffects.Select(await client.DiscoverAsync(stop), setting);
            if (native != null)
            {
                await client.ApplyAsync(native, stop);
                nativeActive.Add(setting.Copy());
                return;
            }
        }
        var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = Task.Run(async () =>
        {
            try { await client.AnimateAsync(setting, () => ready.TrySetResult(), cancel.Token); }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { ready.TrySetCanceled(cancel.Token); }
            catch (Exception e) { if (!ready.TrySetException(e)) Failed?.Invoke(e); }
        });
        running.Add(new(setting.Identity, setting.Zone, cancel, work));
        await ready.Task;
    }
    public async Task StopAsync(string? identity = null, string? zone = null)
    {
        nativeActive.RemoveAll(s => identity == null || s.Identity == identity);
        var matches = running.Where(r => identity == null || (r.Identity == identity &&
            (string.IsNullOrEmpty(zone) || r.Zone.Length == 0 || r.Zone == zone))).ToArray();
        foreach (var r in matches) { running.Remove(r); r.Stop.Cancel(); }
        await Task.WhenAll(matches.Select(r => r.Work));
        foreach (var r in matches) r.Stop.Dispose();
    }
    public async Task StopEffectsAsync(OpenRgbClient client, CancellationToken stop)
    {
        var native = nativeActive.Select(s => s.Copy()).ToArray();
        await StopAsync();
        foreach (var entry in native) { entry.Effect = ""; await client.ApplyAsync(entry, stop); }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (var r in running)
        {
            r.Stop.Cancel();
            _ = r.Work.ContinueWith(_ => r.Stop.Dispose(), TaskScheduler.Default);
        }
        running.Clear();
        nativeActive.Clear();
    }
}
