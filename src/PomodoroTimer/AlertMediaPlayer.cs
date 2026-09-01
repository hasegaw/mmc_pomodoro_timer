using System.Diagnostics;
using System.IO;

namespace PomodoroTimer;

internal sealed class AlertMediaPlayer : IDisposable
{
    private readonly System.Windows.Media.MediaPlayer _player = new();

    public AlertMediaPlayer()
    {
        _player.Volume = 1.0;
        _player.MediaFailed += (_, e) =>
            Debug.WriteLine($"Notification playback failed: {e.ErrorException.Message}");
    }

    public void Play(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return;
        }

        _player.Stop();
        _player.Close();
        _player.Open(new System.Uri(Path.GetFullPath(path), System.UriKind.Absolute));
        _player.Play();
    }

    public void Dispose()
    {
        _player.Stop();
        _player.Close();
        GC.SuppressFinalize(this);
    }
}
