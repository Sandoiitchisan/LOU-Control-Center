using System.IO;

namespace Lou.Rgb;

public sealed record RgbEffect(string Id, string French, string English, bool UsesColor);

/// <summary>Time-based, bounded RGB generation. No device or cooling access.</summary>
public static class RgbEffects
{
    public static readonly RgbEffect[] All =
    [
        new("solid", "Couleur fixe", "Static color", true),
        new("off", "Éteint", "Off", false),
        new("breathing", "Respiration", "Breathing", true),
        new("rainbow", "Arc-en-ciel", "Rainbow", false),
        new("cycle", "Cycle de couleurs", "Color cycle", false),
        new("wave", "Vague", "Wave", true),
        new("comet", "Comète", "Comet", true),
        new("pulse", "Pulsation", "Pulse", true),
        new("sparkle", "Scintillement", "Sparkle", true),
        new("rainbow-wave", "Vague arc-en-ciel", "Rainbow wave", false),
        new("chase", "Défilement", "Chase", true),
        new("scanner", "Scanner aller-retour", "Scanner", true),
        new("blink", "Clignotement", "Blink", true),
        new("alternate", "Alternance", "Alternating", true),
        new("fire", "Feu", "Fire", false),
        new("aurora", "Aurore", "Aurora", false),
        new("ripple", "Ondulation", "Ripple", true)
    ];
    public static bool IsValid(string? id) => id == "" || All.Any(e => e.Id == id);
    public static bool IsDirect(RgbMode m) => m.PerLed && (m.Flags & (64 | 1024)) == 0;
    public static double Period(int speed) => 12 * Math.Pow(1.0 / 12, Math.Clamp(speed, 0, 100) / 100.0);

    public static uint[] Render(RgbAssignment setting, int count, double seconds, int offset = 0)
    {
        if (count is < 0 or > 16384 || !double.IsFinite(seconds) || seconds < 0 || !IsValid(setting.Effect) ||
            setting.Speed is < 0 or > 100 || setting.Brightness is < 0 or > 100)
            throw new InvalidDataException("RGB_SETTINGS_INVALID");
        uint selected = RgbSettings.ParseColor(setting.Color);
        double phase = seconds / Period(setting.Speed);
        var colors = new uint[count];
        for (int i = 0; i < count; i++)
        {
            // Repeat every 24 LEDs: unknown header capacity must not masquerade as a physical strip length.
            double position = (i + offset) / 24.0;
            double level = 1;
            uint color = selected;
            switch (setting.Effect)
            {
                case "off": level = 0; break;
                case "breathing": level = .5 - .5 * Math.Cos(phase * Math.Tau); break;
                case "rainbow": color = Hue(position + phase); break;
                case "cycle": color = Hue(phase); break;
                case "wave": level = .12 + .88 * (.5 + .5 * Math.Sin((position - phase) * Math.Tau)); break;
                case "comet": level = Math.Pow(1 - Fraction(position - phase), 6); break;
                case "pulse": level = Math.Pow(.5 + .5 * Math.Sin(phase * Math.Tau), 5); break;
                case "sparkle": level = .05 + .95 * Math.Pow(.5 + .5 * Math.Sin(phase * Math.Tau + (i + offset) * 2.399963), 12); break;
                case "rainbow-wave": color = Hue(position + phase); level = .15 + .85 * (.5 + .5 * Math.Sin((position - phase) * Math.Tau)); break;
                case "chase": level = Fraction(position * 4 - phase) < .3 ? 1 : .04; break;
                case "scanner": level = Math.Exp(-Math.Pow((Fraction(position) - (1 - Math.Abs(Fraction(phase) * 2 - 1))) * 9, 2)); break;
                case "blink": level = Fraction(phase) < .5 ? 1 : 0; break;
                case "alternate": level = ((i + offset) / 3 + (int)Math.Floor(Fraction(phase) * 2)) % 2 == 0 ? 1 : .03; break;
                case "fire":
                    double heat = .5 + .25 * Math.Sin(phase * 19 + (i + offset) * 1.7) + .25 * Math.Sin(phase * 31 - (i + offset) * 2.3);
                    color = 255u | ((uint)Math.Round(heat * heat * 150) << 8); level = .2 + .8 * heat; break;
                case "aurora": color = Hue(.32 + .38 * (.5 + .5 * Math.Sin(position * 2 + phase))); level = .2 + .8 * (.5 + .5 * Math.Sin(position * 4 - phase * Math.Tau)); break;
                case "ripple": level = Math.Pow(.5 + .5 * Math.Cos((Math.Abs(Fraction(position) - .5) * 2 - phase) * Math.Tau), 4); break;
            }
            colors[i] = Scale(color, level * setting.Brightness / 100.0);
        }
        return colors;
    }
    private static double Fraction(double n) => n - Math.Floor(n);
    private static uint Scale(uint c, double level) => (uint)Math.Round((c & 255) * level) |
        ((uint)Math.Round((c >> 8 & 255) * level) << 8) | ((uint)Math.Round((c >> 16 & 255) * level) << 16);
    private static uint Hue(double phase)
    {
        double h = Fraction(phase) * 6, x = 1 - Math.Abs(h % 2 - 1);
        (double r, double g, double b) = (int)h switch
        { 0 => (1.0, x, 0.0), 1 => (x, 1.0, 0.0), 2 => (0.0, 1.0, x), 3 => (0.0, x, 1.0), 4 => (x, 0.0, 1.0), _ => (1.0, 0.0, x) };
        return (uint)Math.Round(r * 255) | ((uint)Math.Round(g * 255) << 8) | ((uint)Math.Round(b * 255) << 16);
    }
}
