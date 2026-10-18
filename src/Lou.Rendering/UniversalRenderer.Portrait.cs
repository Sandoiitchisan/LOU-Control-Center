using Lou.Core;
using SkiaSharp;

namespace Lou.Rendering;

public sealed partial class UniversalRenderer
{
    // Width is the design unit. Height follows the oriented raster; text and circles
    // keep their proportions instead of stretching a square page to fill the panel.
    private void Portrait(SKCanvas c, PageId page, Snapshot d, Settings s, DateTime now, float height)
    {
        using var edge = new SKPaint { Color = accent.WithAlpha((byte)(45 + 130 * s.Glow)), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        c.DrawRoundRect(new(12, 12, 468, height - 12), 15, 15, edge);
        Text(c, s.CoolingOnly ? Localizer.T("REFROIDISSEMENT") : Labels.Page(page).ToUpperInvariant(), 28, 40, 15, accent, max: 325);
        Text(c, now.ToString("HH:mm"), 452, 40, 15, Muted, right: true);
        var body = new SKRect(28, 64, 452, height - 52);
        if (s.CoolingOnly) PortraitCooling(c, d.Pc, body);
        else switch (page)
        {
            case PageId.System: PortraitSystem(c, d.Pc, now, body); break;
            case PageId.Weather: PortraitWeather(c, d.Weather, body); break;
            case PageId.Music: PortraitMusic(c, d.Media, body); break;
            case PageId.Gaming: PortraitGaming(c, d, body); break;
            case PageId.Ambient: PortraitAmbient(c, d, now, body); break;
        }
        Text(c, d.Demo ? Localizer.T("DÉMONSTRATION") : "LOU // CONTROL CENTER", 240, height - 25, 11, Muted, center: true);
    }

    private void PortraitPanel(SKCanvas c, SKRect bounds)
    {
        using var paint = new SKPaint { Color = new(4, 16, 31, 185), IsAntialias = true };
        c.DrawRoundRect(bounds, 12, 12, paint);
        paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1; paint.Color = accent.WithAlpha(45);
        c.DrawRoundRect(bounds, 12, 12, paint);
    }

    private void PortraitGauge(SKCanvas c, SKRect box, string label, double? percent, string value, string detail, SKColor color)
    {
        PortraitPanel(c, box);
        float r = Math.Min(box.Width * .36f, box.Height * .39f);
        r = Math.Min(136, r);
        float x = box.MidX, y = box.MidY;
        using var stroke = new SKPaint { Color = new(19, 51, 71), Style = SKPaintStyle.Stroke, StrokeWidth = 6, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
        c.DrawCircle(x, y, r, stroke);
        if (percent is {} number && double.IsFinite(number))
        {
            stroke.Color = color;
            using var arc = new SKPath(); arc.AddArc(new(x - r, y - r, x + r, y + r), -90, (float)Math.Clamp(number, 0, 100) * 3.6f);
            c.DrawPath(arc, stroke);
        }
        Text(c, label, x, y - r * .49f, Math.Min(20, r * .22f), color, center: true, max: r * 1.65f);
        // Optical centering uses actual glyph bounds, including missing-value dashes.
        using var font = new SKFont(face, Math.Min(67, r * .58f));
        using var ink = new SKPaint { Color = White, IsAntialias = true };
        font.MeasureText(value, out var bounds);
        c.DrawText(value, x - bounds.MidX, y - bounds.MidY, font, ink);
        Text(c, detail, x, y + r * .61f, Math.Min(19, r * .21f), Muted, center: true, max: r * 1.7f);
    }

    private void PortraitMetric(SKCanvas c, SKRect box, string label, string value, string detail)
    {
        PortraitPanel(c, box);
        Text(c, label, box.Left + 16, box.Top + 22, 13, Muted, max: box.Width - 32);
        Text(c, value, box.Left + 16, box.Top + Math.Max(53, box.Height * .57f), Math.Min(40, box.Height * .28f), White, max: box.Width - 32);
        Text(c, detail, box.Right - 16, box.Bottom - 13, 14, accent, right: true, max: box.Width - 32);
    }

    private void PortraitSystem(SKCanvas c, PcData p, DateTime now, SKRect body)
    {
        if (body.Height > 850)
        {
            float space = body.Height - 60, y = body.Top;
            SKRect Next(float weight) { var rect = new SKRect(body.Left, y, body.Right, y + space * weight); y = rect.Bottom + 12; return rect; }
            var clock = Next(.10f);
            Text(c, now.ToString("HH:mm"), 240, clock.Top + clock.Height * .61f, Math.Min(84, clock.Height * .58f), White, center: true);
            Text(c, now.ToString("ddd dd MMM yyyy", Localizer.Culture), 240, clock.Bottom - 5, 17, Muted, center: true, max: 400);
            PortraitGauge(c, Next(.235f), "CPU", p.Cpu, V(p.Cpu, "%"), V(p.CpuTemp, " °C"), new(46, 155, 255));
            PortraitGauge(c, Next(.235f), "GPU", p.Gpu, V(p.Gpu, "%"), V(p.GpuTemp, " °C"), Pink);
            PortraitMetric(c, Next(.105f), Localizer.T("MÉMOIRE"), V(p.RamUsed, Localizer.T(" Go"), 1), Localizer.T("sur ") + V(p.RamTotal, Localizer.T(" Go")));
            PortraitMetric(c, Next(.105f), Localizer.T("RÉSEAU · RÉCEPTION"), V(p.Download, Localizer.T(" Mo/s"), 1), Localizer.T("Envoi ") + V(p.Upload, Localizer.T(" Mo/s"), 1));
            PortraitFans(c, p, Next(.22f));
        }
        else
        {
            float gaugeBottom = body.Top + body.Height * .36f, metricTop = gaugeBottom + 12, metricBottom = metricTop + body.Height * .22f;
            PortraitGauge(c, new(28, body.Top, 233, gaugeBottom), "CPU", p.Cpu, V(p.Cpu, "%"), V(p.CpuTemp, " °C"), new(46, 155, 255));
            PortraitGauge(c, new(247, body.Top, 452, gaugeBottom), "GPU", p.Gpu, V(p.Gpu, "%"), V(p.GpuTemp, " °C"), Pink);
            PortraitMetric(c, new(28, metricTop, 233, metricBottom), Localizer.T("MÉMOIRE"), V(p.RamUsed, Localizer.T(" Go"), 1), Localizer.T("sur ") + V(p.RamTotal, Localizer.T(" Go")));
            PortraitMetric(c, new(247, metricTop, 452, metricBottom), Localizer.T("RÉSEAU · RÉCEPTION"), V(p.Download, Localizer.T(" Mo/s"), 1), Localizer.T("Envoi ") + V(p.Upload, Localizer.T(" Mo/s"), 1));
            PortraitFans(c, p, new(28, metricBottom + 12, 452, body.Bottom));
        }
    }

    private void PortraitFans(SKCanvas c, PcData p, SKRect box)
    {
        PortraitPanel(c, box);
        var roles = new[] { FanRole.Radiator, FanRole.Pump, FanRole.Gpu, FanRole.Rear, FanRole.Bottom };
        float rowHeight = (box.Height - 16) / roles.Length;
        for (int i = 0; i < roles.Length; i++)
        {
            var role = roles[i]; var reading = p.Fans?.SingleOrDefault(f => f.Role == role);
            double? rpm = reading != null ? reading.Rpm : role switch { FanRole.Radiator => p.CpuFan, FanRole.Pump => p.Pump, FanRole.Gpu => p.GpuFan, FanRole.Rear => p.RearFan, _ => p.BottomFan };
            string label = reading?.Label ?? CoolingLabels.Label(role);
            float y = box.Top + 8 + i * rowHeight;
            if (rowHeight >= 54)
            {
                Text(c, label, box.Left + 16, y + rowHeight * .34f, 16, Muted, max: box.Width - 32);
                Text(c, V(rpm, Localizer.T(" tr/min")), box.Left + 16, y + rowHeight * .79f, Math.Min(28, rowHeight * .34f), role == FanRole.Pump ? Pink : White, max: box.Width - 32);
            }
            else
            {
                Text(c, label, box.Left + 14, y + rowHeight * .68f, Math.Min(16, rowHeight * .65f), Muted, max: 232);
                Text(c, V(rpm, Localizer.T(" tr/min")), box.Right - 14, y + rowHeight * .68f, Math.Min(18, rowHeight * .65f), White, right: true, max: 150);
            }
        }
    }

    private void PortraitCooling(SKCanvas c, PcData p, SKRect body)
    {
        float end = body.Top + body.Height * .56f;
        PortraitGauge(c, new(body.Left, body.Top, body.Right, end), Localizer.T("PROCESSEUR"), p.CpuTemp, V(p.CpuTemp, "°"), "CPU " + V(p.Cpu, "%"), p.CpuTemp >= 90 ? Pink : accent);
        PortraitFans(c, p, new(body.Left, end + 16, body.Right, body.Bottom));
    }

    private void PortraitWeather(SKCanvas c, WeatherData w, SKRect body)
    {
        float available = body.Height - 48;
        var current = new SKRect(28, body.Top, 452, body.Top + available * .39f);
        PortraitPanel(c, current);
        Text(c, w.Location, 240, current.Top + current.Height * .13f + 8, 20, White, center: true, max: 392);
        Text(c, V(w.Temperature, "°C"), 240, current.Top + current.Height * .48f + 7, Math.Min(105, current.Height * .36f), White, center: true);
        Text(c, w.Temperature == null ? w.Status : Condition(w.Code), 240, current.Top + current.Height * .67f + 5, Math.Min(25, current.Height * .13f), accent, center: true, max: 390);
        Text(c, Localizer.T("Ressenti ") + V(w.FeelsLike, "°") + " · " + V(w.Wind, " km/h"), 240, current.Top + current.Height * .81f, Math.Min(18, current.Height * .09f), Muted, center: true, max: 390);
        Text(c, Localizer.T("Humidité ") + V(w.Humidity, " %"), 240, current.Bottom - 9, Math.Min(18, current.Height * .085f), Muted, center: true);

        float hoursTop = current.Bottom + 12, hoursHeight = available * .32f;
        PortraitPanel(c, new(28, hoursTop, 452, hoursTop + hoursHeight));
        Text(c, Localizer.T("PROCHAINES HEURES"), 44, hoursTop + 21, 13, Muted, max: 392);
        var hours = w.Hours ?? [];
        if (hours.Length == 0) Text(c, Localizer.T("Prévisions indisponibles"), 240, hoursTop + hoursHeight / 2, 18, Muted, center: true, max: 390);
        for (int i = 0; i < Math.Min(6, hours.Length); i++)
        {
            float cell = (hoursHeight - 28) / 2, x = 28 + 424f / 3 * (i % 3 + .5f), y = hoursTop + 28 + (i / 3) * cell;
            Text(c, hours[i].Time.ToString("HH:mm"), x, y + cell * .26f, Math.Min(20, cell * .23f), Muted, center: true);
            Text(c, V(hours[i].Temperature, "°"), x, y + cell * .59f, Math.Min(30, cell * .4f), White, center: true);
            Text(c, V(hours[i].Rain, " %"), x, y + cell * .86f, Math.Min(18, cell * .22f), accent, center: true);
        }
        float daysTop = hoursTop + hoursHeight + 12, daysHeight = available * .29f;
        PortraitPanel(c, new(28, daysTop, 452, daysTop + daysHeight));
        Text(c, Localizer.T("LES TROIS PROCHAINS JOURS"), 44, daysTop + 21, 13, Muted, max: 392);
        daysTop += 28; daysHeight -= 28;
        var days = w.Days ?? [];
        if (days.Length == 0) Text(c, w.Status, 240, daysTop + daysHeight / 2, 18, Muted, center: true, max: 390);
        for (int i = 0; i < Math.Min(3, days.Length); i++)
        {
            var day = days[i];
            if (daysHeight > 200)
            {
                float row = daysHeight / 3, y = daysTop + i * row;
                Text(c, day.Time.ToString("dddd", Localizer.Culture), 48, y + row * .40f, 21, Muted, max: 384);
                Text(c, Condition(day.Code), 48, y + row * .71f, 15, accent, max: 200);
                Text(c, V(day.Temperature, "°") + " / " + V(day.Low, "°"), 432, y + row * .71f, 27, White, right: true, max: 180);
            }
            else
            {
                float x = 28 + 424f / 3 * (i + .5f);
                Text(c, day.Time.ToString("ddd", Localizer.Culture), x, daysTop + daysHeight * .35f, Math.Min(15, daysHeight * .18f), Muted, center: true);
                Text(c, V(day.Temperature, "°"), x, daysTop + daysHeight * .63f, Math.Min(26, daysHeight * .27f), White, center: true);
                Text(c, V(day.Low, "°"), x, daysTop + daysHeight * .84f, Math.Min(16, daysHeight * .17f), accent, center: true);
            }
        }
        Text(c, "Open-Meteo · CC BY 4.0", 240, body.Bottom - 3, 11, Muted, center: true);
    }

    private void PortraitMusic(SKCanvas c, MediaData m, SKRect body)
    {
        UpdateCover(m);
        float imageArea = body.Height * .52f, side = Math.Min(400, imageArea - 16), cy = body.Top + imageArea / 2;
        if (cover != null) Cover(c, cover, new(240 - side / 2, cy - side / 2, 240 + side / 2, cy + side / 2));
        else
        {
            using var ring = new SKPaint { Color = Pink, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
            for (int i = 0; i < 4; i++) c.DrawCircle(240, cy, side * (.21f + i * .08f), ring);
            using var font = new SKFont(symbols, side * .4f); using var ink = new SKPaint { Color = White, IsAntialias = true };
            font.MeasureText("♫", out var bounds); c.DrawText("♫", 240 - bounds.MidX, cy - bounds.MidY, font, ink);
        }
        float top = body.Top + imageArea + 8, remaining = body.Bottom - top;
        PortraitPanel(c, new(28, top, 452, body.Bottom));
        Text(c, m.Playing ? Localizer.T("ÉCOUTE EN COURS") : m.Available ? Localizer.T("EN PAUSE") : Localizer.T("EN ATTENTE"), 240, top + remaining * .13f, 14, Pink, center: true);
        Text(c, m.Title, 240, top + remaining * .33f, Math.Min(38, remaining * .16f), White, center: true, max: 390);
        Text(c, m.Artist, 240, top + remaining * .48f, Math.Min(23, remaining * .115f), accent, center: true, max: 390);
        Text(c, m.Album, 240, top + remaining * .61f, 15, Muted, center: true, max: 390);
        float progress = top + remaining * .79f;
        using var stroke = new SKPaint { Color = new(60, 84, 101), StrokeWidth = 3, IsAntialias = true };
        c.DrawLine(48, progress, 432, progress, stroke); stroke.Color = accent;
        c.DrawLine(48, progress, 48 + (float)(384 * Math.Clamp(m.Duration > 0 ? m.Position / m.Duration : 0, 0, 1)), progress, stroke);
        Text(c, Time(m.Position), 48, top + remaining * .93f, 14, Muted); Text(c, Time(m.Duration), 432, top + remaining * .93f, 14, Muted, right: true);
    }

    private void PortraitGaming(SKCanvas c, Snapshot d, SKRect body)
    {
        var g = d.Gaming;
        float heroHeight = body.Height * .42f;
        PortraitPanel(c, new(28, body.Top, 452, body.Top + heroHeight));
        Text(c, g.Name, 240, body.Top + heroHeight * .19f, 26, White, center: true, max: 390);
        Text(c, V(g.Fps, ""), 240, body.Top + heroHeight * .63f, Math.Min(120, heroHeight * .40f), accent, center: true);
        Text(c, Localizer.T("IMAGES / SECONDE"), 240, body.Top + heroHeight * .82f, 15, Muted, center: true);
        float top = body.Top + heroHeight + 12, metricHeight = body.Height * .22f - 12;
        PortraitMetric(c, new(28, top, 452, top + metricHeight), Localizer.T("TAILLE DE FENÊTRE"), g.Resolution, "");
        top += metricHeight + 12;
        PortraitMetric(c, new(28, top, 452, top + metricHeight), Localizer.T("TEMPS PAR IMAGE"), V(g.Frametime, " ms", 1), "");
        float statsTop = top + metricHeight, statsHeight = body.Bottom - statsTop;
        Text(c, "CPU " + V(d.Pc.Cpu, " %") + "   ·   GPU " + V(d.Pc.Gpu, " %"), 240, statsTop + statsHeight * .34f, 20, White, center: true, max: 400);
        Text(c, "VRAM " + V(d.Pc.Vram, Localizer.T(" Go"), 1), 240, statsTop + statsHeight * .65f, 16, Muted, center: true);
        Text(c, g.FpsStatus, 240, body.Bottom, 12, Muted, center: true, max: 400);
    }

    private void PortraitAmbient(SKCanvas c, Snapshot d, DateTime now, SKRect body)
    {
        float y = body.Top + body.Height * .36f;
        Text(c, now.ToString("HH:mm"), 240, y, 106, White, center: true, max: 420);
        Text(c, now.ToString("dddd dd MMMM", Localizer.Culture), 240, y + 45, 21, Muted, center: true, max: 410);
        float weather = body.Top + body.Height * .75f;
        Text(c, V(d.Weather.Temperature, "°C"), 240, weather, 54, accent, center: true);
        Text(c, d.Weather.Location, 240, weather + 41, 20, White, center: true, max: 410);
    }
}
