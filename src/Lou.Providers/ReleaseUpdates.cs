using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lou.Providers;

public enum UpdateState { Current, Newer, Unavailable }
public sealed record ReleaseUpdate(UpdateState State, string? Version = null, Uri? Page = null, Uri? Download = null);

/// <summary>Anonymous, bounded GitHub release check. Never downloads or executes code.</summary>
public sealed class ReleaseUpdates : IDisposable
{
    public const string Repository = "https://github.com/Sandoiitchisan/LOU-Control-Center";
    public const string Endpoint = "https://api.github.com/repos/Sandoiitchisan/LOU-Control-Center/releases/latest";
    private readonly HttpClient http;
    public ReleaseUpdates(HttpMessageHandler? handler = null)
    {
        http = handler == null ? new() : new(handler);
        http.Timeout = TimeSpan.FromSeconds(12);
        http.MaxResponseContentBufferSize = 1024 * 1024;
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LOU-Control-Center", "2.2.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    public async Task<ReleaseUpdate> CheckAsync(string currentVersion, bool installed, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(Endpoint, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new(UpdateState.Unavailable);
            return Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false), currentVersion, installed);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(UpdateState.Unavailable); }
        catch (HttpRequestException) { return new(UpdateState.Unavailable); }
        catch (JsonException) { return new(UpdateState.Unavailable); }
    }
    public static ReleaseUpdate Parse(string json, string currentVersion, bool installed)
    {
        try
        {
            if (json.Length > 1024 * 1024 || !TryVersion(currentVersion, out var current)) return new(UpdateState.Unavailable);
            using var document = JsonDocument.Parse(json);
            var release = document.RootElement;
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return new(UpdateState.Unavailable);
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!TryVersion(tag, out var latest)) return new(UpdateState.Unavailable);
            var page = new Uri(Repository + "/releases/tag/" + tag);
            if (release.GetProperty("html_url").GetString() != page.AbsoluteUri) return new(UpdateState.Unavailable);
            if (latest <= current) return new(UpdateState.Current, latest.ToString(3), page);
            string name = $"LOU-Control-Center-{(installed ? "Setup" : "Portable")}-{latest.ToString(3)}-win-x64.{(installed ? "exe" : "zip")}";
            var expected = new Uri(Repository + "/releases/download/" + tag + "/" + name);
            Uri? download = null;
            if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var asset in assets.EnumerateArray())
                    if (asset.TryGetProperty("name", out var n) && n.GetString() == name &&
                        asset.TryGetProperty("state", out var state) && state.GetString() == "uploaded" &&
                        asset.TryGetProperty("browser_download_url", out var url) && url.GetString() == expected.AbsoluteUri)
                        download = expected;
            return new(UpdateState.Newer, latest.ToString(3), page, download);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return new(UpdateState.Unavailable); }
    }
    private static bool TryVersion(string text, out Version version)
    {
        version = new Version(0, 0, 0);
        return Regex.IsMatch(text, @"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) &&
               Version.TryParse(text.TrimStart('v'), out version!);
    }
    public void Dispose() => http.Dispose();
}
