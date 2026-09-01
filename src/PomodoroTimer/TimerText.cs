using System.Globalization;

namespace PomodoroTimer;

public static class TimerText
{
    public static string Format(TimeSpan value)
    {
        long totalSeconds = Math.Max(0, (long)Math.Ceiling(value.TotalSeconds));
        long hours = totalSeconds / 3600;
        long minutes = totalSeconds % 3600 / 60;
        long seconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Trim().Split(':');
        if (parts.Length is not (2 or 3) ||
            parts.Any(part => part.Length == 0 || !part.All(char.IsAsciiDigit)))
        {
            return false;
        }

        if (!long.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) || seconds > 59 ||
            !long.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out long minutes) || minutes > 59)
        {
            return false;
        }

        long hours = 0;
        if (parts.Length == 3 &&
            (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours) || hours > 9999))
        {
            return false;
        }

        try
        {
            value = TimeSpan.FromSeconds(checked(hours * 3600 + minutes * 60 + seconds));
            return value > TimeSpan.Zero;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
