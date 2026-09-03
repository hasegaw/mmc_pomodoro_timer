using System.IO;
using System.IO.Pipes;
using System.Text;

namespace PomodoroTimer;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\PomodoroTimer.8F287817-1A57-43C0-A43E-DB48B29052AE";
    private const string PipeName = "PomodoroTimer.8F287817-1A57-43C0-A43E-DB48B29052AE";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;

    private SingleInstanceCoordinator(Mutex mutex, bool isPrimary)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static SingleInstanceCoordinator TryAcquire()
    {
        var mutex = new Mutex(true, MutexName, out bool createdNew);
        return new SingleInstanceCoordinator(mutex, createdNew);
    }

    public void StartListening(Action<AppCommand> commandHandler)
    {
        if (!IsPrimary || _listenerTask is not null)
        {
            throw new InvalidOperationException("The command listener can only be started once by the primary instance.");
        }

        _listenerTask = ListenAsync(commandHandler, _cancellation.Token);
    }

    public bool TrySend(AppCommand command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            client.Connect(2000);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine(command.Serialize());
            return reader.ReadLine() == "OK";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task ListenAsync(Action<AppCommand> commandHandler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                string? request = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (AppCommand.TryDeserialize(request, out AppCommand command))
                {
                    commandHandler(command);
                    await writer.WriteLineAsync("OK").ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync("ERROR").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // A disconnected client must not stop subsequent Stream Deck commands.
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _listenerTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the expected way to stop the pipe listener.
        }

        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
