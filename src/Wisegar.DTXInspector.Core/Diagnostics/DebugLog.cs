using System.Text;

namespace Wisegar.DTXInspector.Diagnostics;

internal static class DebugLog
{
    private static readonly object Sync = new();
    private static readonly AsyncLocal<string?> PathContext = new();
    private static string? _path { get => PathContext.Value; set => PathContext.Value = value; }
    internal static readonly AsyncLocal<Action<string, string, string>?> Observer = new();

    internal static IDisposable Begin(string path, Action<string, string, string> observer)
    {
        var scope = new Scope(_path, Observer.Value);
        try { Observer.Value = observer; TryInitialize(path); return scope; }
        catch { scope.Dispose(); throw; }
    }

    private sealed class Scope(string? previousPath, Action<string, string, string>? previousObserver) : IDisposable
    {
        public void Dispose() { _path = previousPath; Observer.Value = previousObserver; }
    }

    public static bool IsEnabled => _path is not null;

    public static void TryInitialize(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(logPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _path = fullPath;
        Info("File log inizializzato.", new Dictionary<string, string?>
        {
            ["path"] = fullPath,
            ["processId"] = Environment.ProcessId.ToString()
        });
    }

    public static void Info(string message, IReadOnlyDictionary<string, string?>? details = null) =>
        Write("INFO", message, details, null);

    public static void Debug(string message, IReadOnlyDictionary<string, string?>? details = null) =>
        Write("DEBUG", message, details, null);

    public static void Exception(Exception exception, string message) =>
        Write("ERROR", message, null, exception);

    private static void Write(
        string level,
        string message,
        IReadOnlyDictionary<string, string?>? details,
        Exception? exception)
    {
        if (_path is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.Now.ToString("O"));
        builder.Append(" [");
        builder.Append(level);
        builder.Append("] ");
        builder.AppendLine(message);

        if (details is not null)
        {
            foreach (var detail in details)
            {
                builder.Append("  ");
                builder.Append(detail.Key);
                builder.Append(": ");
                builder.AppendLine(detail.Value ?? "(null)");
            }
        }

        if (exception is not null)
        {
            builder.AppendLine(exception.ToString());
        }

        Observer.Value?.Invoke(level, message, builder.ToString());

        lock (Sync)
        {
            File.AppendAllText(_path, builder.ToString(), Encoding.UTF8);
        }
    }
}
