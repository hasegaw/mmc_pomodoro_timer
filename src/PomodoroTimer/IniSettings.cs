using System.Globalization;
using System.IO;

namespace PomodoroTimer;

internal sealed class IniSettings
{
    public Point? WindowLocation { get; set; }
    public TimeSpan StartDuration { get; set; } = TimeSpan.FromMinutes(25);
    public string FontFamily { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 72f;
    public FontStyle FontStyle { get; set; } = FontStyle.Bold;
    public bool PlayAlarm { get; set; } = true;
    public bool AlwaysOnTop { get; set; }
    public bool CompactMode { get; set; }

    public static IniSettings Load(string path)
    {
        var settings = new IniSettings();
        if (!File.Exists(path))
        {
            return settings;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string section = string.Empty;
        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator > 0)
            {
                values[$"{section}.{line[..separator].Trim()}"] = line[(separator + 1)..].Trim();
            }
        }

        if (TryGetInt(values, "Window.Left", out int left) && TryGetInt(values, "Window.Top", out int top))
        {
            settings.WindowLocation = new Point(left, top);
        }

        if (TryGetLong(values, "Timer.StartSeconds", out long seconds) && seconds is > 0 and <= 35_999_999)
        {
            settings.StartDuration = TimeSpan.FromSeconds(seconds);
        }

        if (values.TryGetValue("Font.Family", out string? family) && family.Length > 0)
        {
            settings.FontFamily = family;
        }

        if (values.TryGetValue("Font.Size", out string? sizeText) &&
            float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float size) &&
            size is >= 8 and <= 240)
        {
            settings.FontSize = size;
        }

        if (values.TryGetValue("Font.Style", out string? styleText) &&
            Enum.TryParse(styleText, true, out FontStyle style))
        {
            settings.FontStyle = style;
        }

        if (values.TryGetValue("Alarm.Enabled", out string? alarmEnabledText) &&
            bool.TryParse(alarmEnabledText, out bool alarmEnabled))
        {
            settings.PlayAlarm = alarmEnabled;
        }

        if (values.TryGetValue("Window.TopMost", out string? topMostText) &&
            bool.TryParse(topMostText, out bool topMost))
        {
            settings.AlwaysOnTop = topMost;
        }

        if (values.TryGetValue("Window.Compact", out string? compactText) &&
            bool.TryParse(compactText, out bool compact))
        {
            settings.CompactMode = compact;
        }

        return settings;
    }

    public void Save(string path)
    {
        string contents = $""" 
            [Window]
            Left={WindowLocation?.X ?? 100}
            Top={WindowLocation?.Y ?? 100}
            TopMost={AlwaysOnTop}
            Compact={CompactMode}

            [Timer]
            StartSeconds={(long)StartDuration.TotalSeconds}

            [Font]
            Family={FontFamily}
            Size={FontSize.ToString(CultureInfo.InvariantCulture)}
            Style={FontStyle}

            [Alarm]
            Enabled={PlayAlarm}
            """;

        File.WriteAllText(path, contents + Environment.NewLine);
    }

    private static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
    {
        value = default;
        return values.TryGetValue(key, out string? text) &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetLong(Dictionary<string, string> values, string key, out long value)
    {
        value = default;
        return values.TryGetValue(key, out string? text) &&
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
