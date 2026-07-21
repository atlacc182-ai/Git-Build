using GitBuild.Core.Models;
using GitBuild.Core.Services;

namespace GitBuild.Infrastructure.Logging;

public sealed class SessionLogWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public string LogFilePath { get; }

    public SessionLogWriter(IAppPaths paths)
    {
        var logsDirectory = paths.LogsDirectory;
        try
        {
            Directory.CreateDirectory(logsDirectory);
        }
        catch
        {
            logsDirectory = Path.Combine(AppContext.BaseDirectory, "Git-Build-Data", "Logs");
            Directory.CreateDirectory(logsDirectory);
        }

        LogFilePath = Path.Combine(logsDirectory, $"Git-Build-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        _writer = new StreamWriter(new FileStream(LogFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public void Write(BuildEvent buildEvent)
    {
        _writer.WriteLine(buildEvent.ToString());
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
