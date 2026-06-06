using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReceiptPrinterEmulator.Logging;

public static class Logger
{
    private static readonly Lock FileLock = new();
    private static bool _globalHandlersInstalled;

    /// <summary>Absolute path of the rolling log file (errors and unhandled exceptions land here).</summary>
    public static string LogFilePath { get; } = ResolveLogFilePath();

    public static void Info(params object[] values)
    {
        PrintMessage("Info", values);
    }

    public static void Exception(Exception ex, string? message = null)
    {
        PrintMessage("Exception", new object[] { message ?? string.Empty, ex });
    }

    /// <summary>
    /// Registers process-wide handlers so no exception goes unlogged — including ones thrown on
    /// background threads (e.g. the printer's read/write loops) and faulted tasks that would
    /// otherwise tear down the process silently. Call once, as early as possible at startup.
    /// </summary>
    public static void InstallGlobalHandlers()
    {
        if (_globalHandlersInstalled)
            return;
        _globalHandlersInstalled = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Exception(ex, $"Unhandled exception (terminating={e.IsTerminating})");
            else
                PrintMessage("Exception", new[] { $"Unhandled non-Exception error (terminating={e.IsTerminating}): {e.ExceptionObject}" });
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Exception(e.Exception, "Unobserved task exception");
            e.SetObserved(); // keep a faulted background task from terminating the process.
        };
    }

    private static void PrintMessage(string prefix, object[] values)
    {
        var combinedMessage = $"[{prefix}] {FormatValues(values)}";
        Console.WriteLine(combinedMessage);
        AppendToFile(combinedMessage);
    }

    private static void AppendToFile(string message)
    {
        try
        {
            lock (FileLock)
                File.AppendAllText(LogFilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never throw — if the file can't be written, the console line above stands.
        }
    }

    private static string ResolveLogFilePath()
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Path.GetTempPath();
            var dir = Path.Combine(baseDir, "CrossEscPosEmulator", "logs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "app.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "CrossEscPosEmulator.log");
        }
    }

    private static string FormatValues(params object[] values)
    {
        var sb = new StringBuilder();

        foreach (var val in values)
        {
            var asString = val.ToString();

            if (String.IsNullOrWhiteSpace(asString))
                continue;

            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(asString);
        }

        return sb.ToString();
    }
}
