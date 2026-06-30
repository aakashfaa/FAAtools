using System.IO;

namespace FaaTools.Core.Logging;

/// <summary>
/// Simple rolling-by-day file logger. Replaces pyRevit's built-in output console, which no
/// longer exists once the add-in runs outside pyRevit - needed for field debugging of COM/Excel
/// and Revit transaction failures.
/// </summary>
public static class Logger
{
    // Plain object, not System.Threading.Lock - that type is net9.0+ only and this project
    // multi-targets net8.0-windows (Revit 2025/2026) as well.
    private static readonly object SyncLock = new();

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FaaTools", "logs");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (SyncLock)
            {
                Directory.CreateDirectory(LogDirectory);
                var file = Path.Combine(LogDirectory, $"faatools-{DateTime.Now:yyyy-MM-dd}.log");
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(file, line);
            }
        }
        catch
        {
            // logging must never crash the command it's instrumenting
        }
    }
}
