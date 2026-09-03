namespace PomodoroTimer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        AppCommand? command = null;
        if (args.Length > 0)
        {
            if (!AppCommand.TryParseArguments(args, out AppCommand parsedCommand, out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Pomodoro Timer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            command = parsedCommand;
        }

        using var instance = SingleInstanceCoordinator.TryAcquire();
        if (!instance.IsPrimary)
        {
            if (command is AppCommand remoteCommand && !instance.TrySend(remoteCommand))
            {
                MessageBox.Show("タイマーへの操作送信に失敗しました。", "Pomodoro Timer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (command is not null)
        {
            MessageBox.Show("タイマーが起動していません", "Pomodoro Timer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new MainForm();
        _ = form.Handle;
        instance.StartListening(form.ExecuteCommand);
        Application.Run(form);
    }
}
