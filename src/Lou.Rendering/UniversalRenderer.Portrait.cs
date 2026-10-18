using System.Globalization;
using Lou.Core;
using SkiaSharp;

namespace Lou.Rendering;

public sealed partial class UniversalRenderer
{
    private float portraitTextScale = 1.25f;

    // Each line owns a non-overlapping slot. Scale the type, never the canvas or
    // the physical USB raster. Fit readings; ellipsize names at a readable size.
    private void PortraitText(SKCanvas c, string text, SKRect slot, float size, SKColor color,
        SKTextAlign align = SKTextAlign.Center, bool fit = false)
    {
        if (string.IsNullOrEmpty(text) || slot.Width <= 0 || slot.Height <= 0) return;
        using var font = new SKFont(face, size * portraitTextScale);
        float lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        if (lineHeight > slot.Height) font.Size *= slot.Height / lineHeight;
        float width = font.MeasureText(text);
        if (width > slot.Width)
        {
            if (fit) font.Size *= slot.Width / width;
            else
            {
                // Do not split surrogate pairs or combining characters in user metadata.
                int[] starts = StringInfo.ParseCombiningCharacters(text);
                int lo = 0, hi = starts.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    string candidate = text[..(mid == starts.Length ? text.Length : starts[mid])] + "…";
                    if (font.MeasureText(candidate) <= slot.Width) lo = mid; else hi = mid - 1;
                }
                text = text[..(lo == starts.Length ? text.Length : starts[lo])] + "…";
            }
        }
        width = font.MeasureText(text, out var bounds);
        float x = align switch { SKTextAlign.Left => slot.Left, SKTextAlign.Right => slot.Right - width, _ => slot.MidX - width / 2 };
        using var ink = new SKPaint { Color = color, IsAntialias = true };
        c.Save(); c.ClipRect(slot);
        c.DrawText(text, x, slot.MidY - bounds.MidY, font, ink);
        c.Restore();
    }

    private static SKRect Band(SKRect box, float top, float bottom, float inset = 16) =>
        new(box.Left + inset, box.Top + box.Height * top, box.Right - inset, box.Top + box.Height * bottom);

    private void Portrait(SKCanvas c, PageId page, Snapshot d, Settings s, DateTime now, float height)
    {
        portraitTextScale = Math.Clamp(s.PortraitTextPercent, 100, 150) / 100f;
        using var edge = new SKPaint { Color = accent.WithAlpha((byte)(45 + 130 * s.Glow)), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        c.DrawRoundRect(new(12, 12, 468, height - 12), 15, 15, edge);
        PortraitText(c, s.CoolingOnly ? Localizer.T("REFROIDISSEMENT") : Labels.Page(page).ToUpperInvariant(), new(28, 22, 350, 52), 18, accent, SKTextAlign.Left);
        PortraitText(c, now.ToString("HH:mm"), new(362, 22, 452, 52), 18, Muted, SKTextAlign.Right, fit: true);
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
        PortraitText(c, d.Demo ? Localizer.T("DÉMONSTRATION") : "LOU // CONTROL CENTER", new(28, height - 42, 452, height - 17), 14, Muted);
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
        float r = Math.Min(136, Math.Min(box.Width * .40f, box.Height * .42f));
        float x = box.MidX, y = box.MidY;
        using var stroke = new SKPaint { Color = new(19, 51, 71), Style = SKPaintStyle.Stroke, StrokeWidth = 6, StrokeCap = SKStrokeCap.Round, IsAntialias = true };
        c.DrawCircle(x, y, r, stroke);
        if (percent is {} number && double.IsFinite(number))
        {
            stroke.Color = color;
            using var arc = new SKPath(); arc.AddArc(new(x - r, y - r, x + r, y + r), -90, (float)Math.Clamp(number, 0, 100) * 3.6f);
            c.DrawPath(arc, stroke);
        }
        PortraitText(c, label, new(x - r * .7f, y - r * .76f, x + r * .7f, y - r * .34f), 24, color);
        PortraitText(c, value, new(x - r * .86f, y - r * .32f, x + r * .86f, y + r * .32f), Math.Min(72, r * .65f), White, fit: true);
        PortraitText(c, detail, new(x - r * .7f, y + r * .34f, x + r * .7f, y + r * .76f), 24, Muted, fit: true);
    }

    private void PortraitMetric(SKCanvas c, SKRect box, string label, string value, string detail)
    {
        PortraitPanel(c, box);
        PortraitText(c, label, Band(box, .06f, .30f), 19, Muted, SKTextAlign.Left);
        PortraitText(c, value, Band(box, .32f, string.IsNullOrEmpty(detail) ? .92f : .68f), 44, White, SKTextAlign.Left, fit: true);
        PortraitText(c, detail, Band(box, .72f, .94f), 20, accent, SKTextAlign.Right, fit: true);
    }

    private void PortraitSystem(SKCanvas c, PcData p, DateTime now, SKRect body)
    {
        if (body.Height > 850)
        {
            float space = body.Height - 60, y = body.Top;
            SKRect Next(float weight) { var rect = new SKRect(body.Left, y, body.Right, y + space * weight); y = rect.Bottom + 12; return rect; }
            var clock = Next(.10f);
            PortraitText(c, now.ToString("HH:mm"), Band(clock, 0, .68f), 84, White, fit: true);
            PortraitText(c, now.ToString("ddd dd MMM yyyy", Localizer.Culture), Band(clock, .70f, 1), 22, Muted);
            PortraitGauge(c, Next(.225f), "CPU", p.Cpu, V(p.Cpu, "%"), V(p.CpuTemp, " °C"), new(46, 155, 255));
            PortraitGauge(c, Next(.225f), "GPU", p.Gpu, V(p.Gpu, "%"), V(p.GpuTemp, " °C"), Pink);
            PortraitMetric(c, Next(.10f), Localizer.T("MÉMOIRE"), V(p.RamUsed, Localizer.T(" Go"), 1), Localizer.T("sur ") + V(p.RamTotal, Localizer.T(" Go")));
            PortraitMetric(c, Next(.10f), Localizer.T("RÉSEAU · RÉCEPTION"), V(p.Download, Localizer.T(" Mo/s"), 1), Localizer.T("Envoi ") + V(p.Upload, Localizer.T(" Mo/s"), 1));
            PortraitFans(c, p, Next(.25f));
        }
        else
        {
            float gaugeBottom = body.Top + body.Height * .36f, metricTop = gaugeBottom + 12, metricBottom = metricTop + body.Height * .22f;
            PortraitGauge(c, new(28, body.Top, 233, gaugeBottom), "CPU", p.Cpu, V(p.Cpu, "%"), V(p.CpuTemp, " °C"), new(46, 155, 255));
            PortraitGauge(c, new(247, body.Top, 452, gaugeBottom), "GPU", p.Gpu, V(p.Gpu, "%"), V(p.GpuTemp, " °C"), Pink);
            PortraitMetric(c, new(28, metricTop, 233, metricBottom), Localizer.T("MÉMOIRE"), V(p.RamUsed, Localizer.T(" Go"), 1), Localizer.T("sur ") + V(p.RamTotal, Localizer.T(" Go")));
            PortraitMetric(c, new(247, metricTop, 452, metricBottom), Localizer.T("RÉSEAU"), V(p.Download, Localizer.T(" Mo/s"), 1), Localizer.T("Envoi ") + V(p.Upload, Localizer.T(" Mo/s"), 1));
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
            var row = new SKRect(box.Left, y, box.Right, y + rowHeight);
            var color = role == FanRole.Pump ? Pink : White;
            if (rowHeight >= 60)
            {
                PortraitText(c, label, Band(row, .02f, .45f), 22, Muted, SKTextAlign.Left);
                PortraitText(c, V(rpm, Localizer.T(" tr/min")), Band(row, .48f, .96f), 30, color, SKTextAlign.Left, fit: true);
            }
            else
            {
                PortraitText(c, label, new(box.Left + 14, y + 2, box.Left + 230, y + rowHeight - 2), 22, Muted, SKTextAlign.Left);
                PortraitText(c, V(rpm, Localizer.T(" tr/min")), new(box.Left + 242, y + 2, box.Right - 14, y + rowHeight - 2), 24, color, SKTextAlign.Right, fit: true);
            }
        }
    }

    private void PortraitCooling(SKCanvas c, PcData p, SKRect body)
    {
        float end = body.Top + body.Height * .50f;
        PortraitGauge(c, new(body.Left, body.Top, body.Right, end), Localizer.T("PROCESSEUR"), p.CpuTemp, V(p.CpuTemp, "°"), "CPU " + V(p.Cpu, "%"), p.CpuTemp >= 90 ? Pink : accent);
        PortraitFans(c, p, new(body.Left, end + 16, body.Right, body.Bottom));
    }

    private void PortraitWeather(SKCanvas c, WeatherData w, SKRect body)
    {
        float available = body.Height - 56;
        var current = new SKRect(28, body.Top, 452, body.Top + available * .39f);
        PortraitPanel(c, current);
        PortraitText(c, w.Location, Band(current, .02f, .18f), 26, White);
        PortraitText(c, V(w.Temperature, "°C"), Band(current, .19f, .52f), 105, White, fit: true);
        PortraitText(c, w.Temperature == null ? w.Status : Condition(w.Code), Band(current, .53f, .70f), 29, accent);
        PortraitText(c, Localizer.T("Ressenti ") + V(w.FeelsLike, "°") + " · " + V(w.Wind, " km/h"), Band(current, .71f, .84f), 23, Muted, fit: true);
        PortraitText(c, Localizer.T("Humidité ") + V(w.Humidity, " %"), Band(current, .85f, .98f), 23, Muted, fit: true);

        float hoursTop = current.Bottom + 12, hoursHeight = available * .32f;
        PortraitPanel(c, new(28, hoursTop, 452, hoursTop + hoursHeight));
        float heading = Math.Min(44, hoursHeight * .23f);
        PortraitText(c, Localizer.T("PROCHAINES HEURES"), new(44, hoursTop + 3, 436, hoursTop + heading - 2), 19, Muted, SKTextAlign.Left);
        var hours = w.Hours ?? [];
        if (hours.Length == 0) PortraitText(c, Localizer.T("Prévisions indisponibles"), new(44, hoursTop + heading, 436, hoursTop + hoursHeight - 8), 24, Muted);
        for (int i = 0; i < Math.Min(6, hours.Length); i++)
        {
            if (hoursHeight < 220)
            {
                // Short panels need wider rows: retain all six forecasts without
                // squeezing three text lines into each tiny grid cell.
                float rowHeight = (hoursHeight - heading - 6) / 3;
                float left = 36 + 208 * (i % 2), top = hoursTop + heading + i / 2 * rowHeight;
                PortraitText(c, hours[i].Time.ToString("HH:mm"), new(left, top + 2, left + 72, top + rowHeight - 2), 22, Muted, fit: true);
                PortraitText(c, V(hours[i].Temperature, "°"), new(left + 76, top + 2, left + 130, top + rowHeight - 2), 29, White, fit: true);
                PortraitText(c, V(hours[i].Rain, "%"), new(left + 134, top + 2, left + 196, top + rowHeight - 2), 21, accent, fit: true);
                continue;
            }
            float cell = (hoursHeight - heading - 6) / 2, x = 28 + 424f / 3 * (i % 3), y = hoursTop + heading + (i / 3) * cell;
            var box = new SKRect(x, y, x + 424f / 3, y + cell);
            PortraitText(c, hours[i].Time.ToString("HH:mm"), Band(box, .02f, .30f, 6), 24, Muted, fit: true);
            PortraitText(c, V(hours[i].Temperature, "°"), Band(box, .32f, .68f, 6), 36, White, fit: true);
            PortraitText(c, V(hours[i].Rain, " %"), Band(box, .70f, .98f, 6), 23, accent, fit: true);
        }
        float daysTop = hoursTop + hoursHeight + 12, daysHeight = available * .29f;
        PortraitPanel(c, new(28, daysTop, 452, daysTop + daysHeight));
        heading = Math.Min(44, daysHeight * .23f);
        PortraitText(c, Localizer.T("LES TROIS PROCHAINS JOURS"), new(44, daysTop + 3, 436, daysTop + heading - 2), 19, Muted, SKTextAlign.Left);
        daysTop += heading; daysHeight -= heading + 6;
        var days = w.Days ?? [];
        if (days.Length == 0) PortraitText(c, w.Status, new(44, daysTop, 436, daysTop + daysHeight), 24, Muted);
        for (int i = 0; i < Math.Min(3, days.Length); i++)
        {
            var day = days[i];
            if (daysHeight > 200)
            {
                float row = daysHeight / 3, y = daysTop + i * row;
                PortraitText(c, day.Time.ToString("dddd", Localizer.Culture), new(48, y, 432, y + row * .43f), 26, Muted, SKTextAlign.Left);
                PortraitText(c, Condition(day.Code), new(48, y + row * .47f, 238, y + row * .96f), 21, accent, SKTextAlign.Left);
                PortraitText(c, V(day.Temperature, "°") + " / " + V(day.Low, "°"), new(250, y + row * .47f, 432, y + row * .96f), 32, White, SKTextAlign.Right, fit: true);
            }
            else
            {
                float x = 28 + 424f / 3 * i;
                var box = new SKRect(x, daysTop, x + 424f / 3, daysTop + daysHeight);
                PortraitText(c, day.Time.ToString("ddd", Localizer.Culture), Band(box, .02f, .30f, 6), 22, Muted);
                PortraitText(c, V(day.Temperature, "°"), Band(box, .32f, .68f, 6), 34, White, fit: true);
                PortraitText(c, V(day.Low, "°"), Band(box, .70f, .98f, 6), 23, accent, fit: true);
            }
        }
        PortraitText(c, "Open-Meteo · CC BY 4.0", new(28, body.Bottom - 26, 452, body.Bottom), 15, Muted);
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
        float top = body.Top + imageArea + 8;
        var info = new SKRect(28, top, 452, body.Bottom);
        PortraitPanel(c, info);
        PortraitText(c, m.Playing ? Localizer.T("ÉCOUTE EN COURS") : m.Available ? Localizer.T("EN PAUSE") : Localizer.T("EN ATTENTE"), Band(info, .03f, .17f), 22, Pink);
        PortraitText(c, m.Title, Band(info, .19f, .38f), 42, White);
        PortraitText(c, m.Artist, Band(info, .40f, .55f), 29, accent);
        PortraitText(c, m.Album, Band(info, .57f, .69f), 22, Muted);
        float progress = top + info.Height * .79f;
        using var stroke = new SKPaint { Color = new(60, 84, 101), StrokeWidth = 3, IsAntialias = true };
        c.DrawLine(48, progress, 432, progress, stroke); stroke.Color = accent;
        c.DrawLine(48, progress, 48 + (float)(384 * Math.Clamp(m.Duration > 0 ? m.Position / m.Duration : 0, 0, 1)), progress, stroke);
        PortraitText(c, Time(m.Position), new(48, top + info.Height * .85f, 220, top + info.Height * .98f), 22, Muted, SKTextAlign.Left, fit: true);
        PortraitText(c, Time(m.Duration), new(260, top + info.Height * .85f, 432, top + info.Height * .98f), 22, Muted, SKTextAlign.Right, fit: true);
    }

    private void PortraitGaming(SKCanvas c, Snapshot d, SKRect body)
    {
        var g = d.Gaming;
        float heroHeight = body.Height * .42f;
        var hero = new SKRect(28, body.Top, 452, body.Top + heroHeight);
        PortraitPanel(c, hero);
        PortraitText(c, g.Name, Band(hero, .05f, .25f), 32, White);
        PortraitText(c, V(g.Fps, ""), Band(hero, .27f, .70f), 120, accent, fit: true);
        PortraitText(c, Localizer.T("IMAGES / SECONDE"), Band(hero, .73f, .94f), 23, Muted);
        float top = body.Top + heroHeight + 12, metricHeight = body.Height * .22f - 12;
        PortraitMetric(c, new(28, top, 452, top + metricHeight), Localizer.T("TAILLE DE FENÊTRE"), g.Resolution, "");
        top += metricHeight + 12;
        PortraitMetric(c, new(28, top, 452, top + metricHeight), Localizer.T("TEMPS PAR IMAGE"), V(g.Frametime, " ms", 1), "");
        var stats = new SKRect(28, top + metricHeight + 4, 452, body.Bottom);
        PortraitText(c, "CPU " + V(d.Pc.Cpu, " %") + "   ·   GPU " + V(d.Pc.Gpu, " %"), Band(stats, .02f, .34f, 4), 25, White, fit: true);
        PortraitText(c, "VRAM " + V(d.Pc.Vram, Localizer.T(" Go"), 1), Band(stats, .36f, .67f, 4), 22, Muted, fit: true);
        PortraitText(c, g.FpsStatus, Band(stats, .69f, 1, 4), 17, Muted);
    }

    private void PortraitAmbient(SKCanvas c, Snapshot d, DateTime now, SKRect body)
    {
        PortraitText(c, now.ToString("HH:mm"), Band(body, .17f, .37f, 0), 106, White, fit: true);
        PortraitText(c, now.ToString("dddd dd MMMM", Localizer.Culture), Band(body, .39f, .49f, 6), 28, Muted);
        PortraitText(c, V(d.Weather.Temperature, "°C"), Band(body, .63f, .76f), 64, accent, fit: true);
        PortraitText(c, d.Weather.Location, Band(body, .78f, .90f, 6), 27, White);
    }
}
