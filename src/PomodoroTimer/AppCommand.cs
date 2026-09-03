using System.Globalization;

namespace PomodoroTimer;

internal enum AppCommandKind
{
    Start,
    Stop,
    Click,
    Reset,
    Set
}

internal readonly record struct AppCommand(AppCommandKind Kind, TimeSpan? Duration = null)
{
    public static bool TryParseArguments(string[] args, out AppCommand command, out string errorMessage)
    {
        command = default;
        errorMessage = string.Empty;
        AppCommandKind? operation = null;
        TimeSpan? duration = null;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index].ToLowerInvariant();
            if (argument == "/set")
            {
                if (duration is not null || index + 1 >= args.Length ||
                    !TimerText.TryParse(args[++index], out TimeSpan parsedDuration))
                {
                    errorMessage = "時間フォーマットが正しくありません";
                    return false;
                }

                duration = parsedDuration;
                continue;
            }

            AppCommandKind? parsedOperation = argument switch
            {
                "/start" => AppCommandKind.Start,
                "/stop" => AppCommandKind.Stop,
                "/click" => AppCommandKind.Click,
                "/reset" => AppCommandKind.Reset,
                _ => null
            };
            if (parsedOperation is null || operation is not null)
            {
                errorMessage = CommandUsageMessage;
                return false;
            }

            operation = parsedOperation;
        }

        if (duration is not null && operation == AppCommandKind.Click)
        {
            errorMessage = "/click と /set は同時に指定できません。";
            return false;
        }

        if (operation is null && duration is null)
        {
            errorMessage = CommandUsageMessage;
            return false;
        }

        command = new AppCommand(operation ?? AppCommandKind.Set, duration);
        return true;
    }

    public string Serialize() => Duration is TimeSpan duration
        ? $"{Kind}|{(long)duration.TotalSeconds}"
        : Kind.ToString();

    public static bool TryDeserialize(string? text, out AppCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split('|');
        if (!Enum.TryParse(parts[0], true, out AppCommandKind kind))
        {
            return false;
        }

        if (parts.Length == 1 && kind != AppCommandKind.Set)
        {
            command = new AppCommand(kind);
            return true;
        }

        if (parts.Length != 2 ||
            kind == AppCommandKind.Click ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) ||
            seconds <= 0 || seconds > 35_999_999)
        {
            return false;
        }

        command = new AppCommand(kind, TimeSpan.FromSeconds(seconds));
        return true;
    }

    private const string CommandUsageMessage =
        "コマンドライン引数が正しくありません。/start /stop /click /reset /set {TIME_STR} を指定してください。";
}
