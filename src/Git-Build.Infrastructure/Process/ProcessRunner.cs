using System.Diagnostics;
using GitBuild.Core.Models;

namespace GitBuild.Infrastructure.Process;

public sealed class ProcessRunner
{
    public async Task<int> RunAsync(CommandSpec command, IProgress<BuildEvent> progress, CancellationToken cancellationToken)
    {
        progress.Report(new BuildEvent(DateTimeOffset.Now, $"> {command.DisplayName}: {command.FileName} {command.Arguments}"));

        try
        {
            var startInfo = CreateStartInfo(command);
            using var process = new System.Diagnostics.Process { StartInfo = startInfo, EnableRaisingEvents = true };

            if (!process.Start())
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, $"Could not start '{command.FileName}'.", true));
                return -1;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            ReportLines(await outputTask.ConfigureAwait(false), progress, false);
            ReportLines(await errorTask.ConfigureAwait(false), progress, process.ExitCode != 0);

            progress.Report(new BuildEvent(DateTimeOffset.Now, $"Command exited with code {process.ExitCode}.", process.ExitCode != 0));
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            progress.Report(new BuildEvent(DateTimeOffset.Now, "Command was cancelled.", true));
            return -2;
        }
        catch (Exception ex)
        {
            progress.Report(new BuildEvent(DateTimeOffset.Now, $"Git-Build could not run '{command.FileName}': {ex.Message}", true));
            progress.Report(new BuildEvent(DateTimeOffset.Now, ex.ToString(), true));
            return -1;
        }
    }

    private static ProcessStartInfo CreateStartInfo(CommandSpec command)
    {
        if (OperatingSystem.IsWindows())
        {
            if (IsPowerShell(command.FileName) || IsCmd(command.FileName))
            {
                return new ProcessStartInfo
                {
                    FileName = command.FileName,
                    Arguments = command.Arguments,
                    WorkingDirectory = command.WorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            var shellCommand = string.IsNullOrWhiteSpace(command.Arguments)
                ? QuoteForCmd(command.FileName)
                : QuoteForCmd(command.FileName) + " " + command.Arguments;

            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /s /c \"" + shellCommand + "\"",
                WorkingDirectory = command.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = command.Arguments,
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static bool IsPowerShell(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return name.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("pwsh", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCmd(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return name.Equals("cmd", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteForCmd(string value) => value.Contains(' ') ? "\"" + value + "\"" : value;

    private static void ReportLines(string text, IProgress<BuildEvent> progress, bool isError)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, line, isError));
            }
        }
    }
}
