using System.Collections.Concurrent;
using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>
/// Issue #905: writes a local crash report (see <see cref="CrashBundle"/>) when the .NET runtime reports an unhandled or unobserved exception, keeping the last engine errors Godot
/// logged in memory to put in it. Files go to user://crash-reports/. Installing it changes nothing about how errors behave.
/// </summary>
public static partial class CrashReports
{
    private static readonly ConcurrentQueue<string> Recent = new();
    private static Func<(string Position, string[] Events, string[] Secrets)?>? _context;
    private static int _installed;

    public static string Folder => ProjectSettings.GlobalizePath("user://crash-reports");

    private sealed partial class ErrorLog : Logger
    {
        public override void _LogError(string function, string file, int line, string code, string rationale, bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
            => Remember($"{(errorType == 1 ? "WARNING" : "ERROR")} {file}:{line} {function}: {(string.IsNullOrEmpty(rationale) ? code : rationale)}");

        public override void _LogMessage(string message, bool error)
        {
            if (error) Remember("STDERR " + message.TrimEnd());
        }
    }

    private static void Remember(string line)
    {
        Recent.Enqueue(line.Length > 600 ? line[..600] : line);
        while (Recent.Count > CrashBundle.MaxErrorLines * 2 && Recent.TryDequeue(out _)) { }
    }

    /// <summary>Hooks the runtime's last-chance exception events and Godot's error log. <paramref name="context"/> reads the viewer's own game position when a report is written; it must not throw.</summary>
    public static void Install(Func<(string Position, string[] Events, string[] Secrets)?> context)
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1) { _context = context; return; }
        _context = context;
        OS.AddLogger(new ErrorLog());
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, e) => Write(e.Exception);
    }

    public static string? Write(Exception? exception)
    {
        try
        {
            (string Position, string[] Events, string[] Secrets)? context = null;
            try { context = _context?.Invoke(); } catch { /* a report without a position beats none */ }
            var environment = $"{OS.GetName()} {OS.GetVersion()}, Godot {Engine.GetVersionInfo()["string"]}, renderer {RenderingServer.GetCurrentRenderingMethod()}, adapter {RenderingServer.GetVideoAdapterName()}";
            var text = CrashBundle.Compose(MatchEngine.ContentVersion, environment, exception, Recent.ToArray(), context?.Position, context?.Events, context?.Secrets,
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, CrashBundle.FileName(DateTimeOffset.UtcNow));
            File.WriteAllText(path, text);
            return path;
        }
        catch { return null; }
    }
}
