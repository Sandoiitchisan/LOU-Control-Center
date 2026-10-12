using System.Globalization;
using System.Text.Json;

namespace Lou.Core;

public enum AppLanguage { French, English }

/// <summary>Presentation only: never translate hardware identities or user-supplied metadata.</summary>
public static class Localizer
{
    private static readonly AsyncLocal<AppLanguage?> scoped = new();
    private static int preferred;
    public static AppLanguage Preferred { get => (AppLanguage)Volatile.Read(ref preferred); set => Volatile.Write(ref preferred, (int)value); }
    public static AppLanguage Language => scoped.Value ?? Preferred;
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == AppLanguage.English ? "en-US" : "fr-FR");
    public static AppLanguage SystemLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? AppLanguage.French : AppLanguage.English;
    public static IReadOnlyDictionary<string,string> English { get; } = Read("en");
    public static IReadOnlyDictionary<string,string> UiResources { get; } = Read("ui");
    private static Dictionary<string,string> Read(string name)
    {
        using var stream = typeof(Localizer).Assembly.GetManifestResourceStream($"Lou.Core.Localization.{name}.json")!;
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
    }
    public static string T(string text) => Language == AppLanguage.English && English.TryGetValue(text, out var translated) ? translated : text;
    public static string F(FormattableString text) => string.Format(Culture, T(text.Format), text.GetArguments());
    public static IDisposable Use(AppLanguage language) => new LanguageScope(language);
    private sealed class LanguageScope : IDisposable
    {
        private readonly AppLanguage? previous = scoped.Value;
        public LanguageScope(AppLanguage language) => scoped.Value = language;
        public void Dispose() => scoped.Value = previous;
    }
    public static bool IsDefaultFanLabel(FanRole role, string label)
    {
        using (Use(AppLanguage.French)) { string original = CoolingLabels.Label(role); return label == original || label == English.GetValueOrDefault(original); }
    }
    public static string FanLabel(Settings settings, FanRole role) => settings.FanLabels.TryGetValue(role, out var custom) && !IsDefaultFanLabel(role, custom) ? custom : CoolingLabels.Label(role);
    public static Snapshot Present(Snapshot data, Settings settings) => data with
    {
        Pc = data.Pc with { Fans = data.Pc.Fans?.Select(f => f with { Label = FanLabel(settings, f.Role), Status = T(f.Status) }).ToArray() },
        Weather = data.Weather with { Status = T(data.Weather.Status), Location = settings.CitySelected && !data.Demo ? data.Weather.Location : T(data.Weather.Location) },
        Media = data.Media.Available ? data.Media : data.Media with { Title = T(data.Media.Title), Artist = T(data.Media.Artist) },
        Gaming = data.Gaming with { Name = data.Gaming.Active ? data.Gaming.Name : T(data.Gaming.Name), Resolution = T(data.Gaming.Resolution), FpsStatus = T(data.Gaming.FpsStatus) }
    };
}
