using System.IO;
using System.Windows.Media;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: PlaybackProbe <audio-file>");
            return 2;
        }

        var player = new MediaPlayer { Volume = 1.0 };
        Exception? failure = null;
        bool opened = false;

        player.MediaOpened += (_, _) =>
        {
            opened = true;
            Console.WriteLine($"opened=true; naturalDuration={player.NaturalDuration}");
        };
        player.MediaFailed += (_, e) =>
        {
            failure = e.ErrorException;
            System.Windows.Forms.Application.ExitThread();
        };

        var timeout = new System.Windows.Forms.Timer { Interval = 1000 };
        timeout.Tick += (_, _) =>
        {
            timeout.Stop();
            Console.WriteLine($"after1s.position={player.Position.TotalMilliseconds:F0}ms");
            System.Windows.Forms.Application.ExitThread();
        };

        player.Open(new System.Uri(Path.GetFullPath(args[0]), System.UriKind.Absolute));
        player.Play();
        timeout.Start();
        System.Windows.Forms.Application.Run();
        player.Stop();
        player.Close();

        if (failure is not null)
        {
            Console.Error.WriteLine($"failed={failure.Message}");
            return 1;
        }

        if (!opened)
        {
            Console.Error.WriteLine("failed=MediaOpened was not raised");
            return 1;
        }

        return 0;
    }
}
