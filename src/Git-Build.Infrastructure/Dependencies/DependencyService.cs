using GitBuild.Core.Models;
using GitBuild.Core.Services;
using GitBuild.Infrastructure.Process;

namespace GitBuild.Infrastructure.Dependencies;

public sealed class DependencyService(ProcessRunner runner) : IDependencyService
{
    public async Task<IReadOnlyList<DependencyRequirement>> FindMissingAsync(IEnumerable<DependencyRequirement> requirements, CancellationToken cancellationToken)
    {
        var missing = new List<DependencyRequirement>();
        foreach (var requirement in requirements.DistinctBy(r => r.Executable))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await ExistsOnPathAsync(requirement.Executable, cancellationToken).ConfigureAwait(false))
            {
                missing.Add(requirement);
            }
        }

        return missing;
    }

    public async Task InstallAsync(IEnumerable<DependencyRequirement> requirements, IProgress<BuildEvent> progress, CancellationToken cancellationToken)
    {
        foreach (var requirement in requirements)
        {
            if (requirement.InstallCommand is null)
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, $"{requirement.Name}: {requirement.InstallHint}", true));
                continue;
            }

            var exitCode = await runner.RunAsync(requirement.InstallCommand, progress, cancellationToken).ConfigureAwait(false);
            RefreshProcessPath(progress);

            if (exitCode != 0 && requirement.InstallCommand.FileName.Equals("winget", StringComparison.OrdinalIgnoreCase) && IsHarmlessWingetExitCode(exitCode))
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, "winget reported that the package is already installed or no upgrade is available. Git-Build refreshed PATH and will continue.", false));
            }
            else if (exitCode != 0)
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, $"{requirement.Name} installer failed. Git-Build cannot continue with this missing dependency until it is installed.", true));
                throw new InvalidOperationException($"{requirement.Name} installer failed. Install it manually, then run the build again.");
            }

            if (await ExistsOnPathAsync(requirement.Executable, cancellationToken).ConfigureAwait(false))
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, $"{requirement.Name} is now available to Git-Build."));
            }
            else
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, $"{requirement.Name} was installed, but Git-Build still cannot find '{requirement.Executable}'. The installer may require a Windows sign-out/reboot or a custom install path.", true));
                throw new InvalidOperationException($"{requirement.Name} is still not available after installation.");
            }
        }
    }

    private static void RefreshProcessPath(IProgress<BuildEvent> progress)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? string.Empty;
            var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
            var currentPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process) ?? string.Empty;
            var paths = SplitPath(machinePath)
                .Concat(SplitPath(userPath))
                .Concat(SplitPath(currentPath))
                .Concat(CommonWindowsToolPaths())
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            Environment.SetEnvironmentVariable("Path", string.Join(Path.PathSeparator, paths), EnvironmentVariableTarget.Process);
            progress.Report(new BuildEvent(DateTimeOffset.Now, "Git-Build refreshed PATH for this session."));
        }
        catch (Exception ex)
        {
            progress.Report(new BuildEvent(DateTimeOffset.Now, $"Git-Build could not refresh PATH automatically: {ex.Message}", true));
        }
    }

    private static bool IsHarmlessWingetExitCode(int exitCode) =>
        exitCode is -1978335189;

    private static IEnumerable<string> SplitPath(string value) =>
        value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Environment.ExpandEnvironmentVariables);

    private static IEnumerable<string> CommonWindowsToolPaths()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(programFiles, "Go", "bin");
        yield return Path.Combine(programFiles, "nodejs");
        yield return Path.Combine(programFiles, "dotnet");
        yield return Path.Combine(localAppData, "Python", "pythoncore-3.14-64");
        yield return Path.Combine(localAppData, "Python", "pythoncore-3.14-64", "Scripts");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python314");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python314", "Scripts");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python313");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python313", "Scripts");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python312");
        yield return Path.Combine(localAppData, "Programs", "Python", "Python312", "Scripts");

        foreach (var gitBuildToolPath in GitBuildToolPaths(localAppData))
        {
            yield return gitBuildToolPath;
        }

        foreach (var visualStudioPath in VisualStudioToolPaths())
        {
            yield return visualStudioPath;
        }
    }

    private static IEnumerable<string> GitBuildToolPaths(string localAppData)
    {
        var toolsDirectory = Path.Combine(localAppData, "Git-Build", "Tools");
        if (!Directory.Exists(toolsDirectory))
        {
            yield break;
        }

        foreach (var gradleDirectory in Directory.EnumerateDirectories(toolsDirectory, "gradle-*"))
        {
            yield return Path.Combine(gradleDirectory, "bin");
        }
    }

    private static IEnumerable<string> VisualStudioToolPaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vswhere = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            yield break;
        }

        foreach (var executable in FindWithVsWhere(vswhere, "VC\\Tools\\MSVC\\**\\bin\\Hostx64\\x64\\link.exe"))
        {
            var directory = Path.GetDirectoryName(executable);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return directory;
            }
        }
    }

    private static async Task<bool> ExistsOnPathAsync(string executable, CancellationToken cancellationToken)
    {
        if (executable.StartsWith("dotnet-sdk-", StringComparison.OrdinalIgnoreCase))
        {
            var versionText = executable["dotnet-sdk-".Length..];
            return int.TryParse(versionText, out var major) &&
                await HasDotNetSdkMajorAsync(major, cancellationToken).ConfigureAwait(false);
        }

        if (executable.Equals("android-sdk", StringComparison.OrdinalIgnoreCase))
        {
            return FindAndroidSdkPath() is not null;
        }

        if (OperatingSystem.IsWindows() && executable.Equals("link.exe", StringComparison.OrdinalIgnoreCase))
        {
            return await ExistsOnPathNormallyAsync(executable, cancellationToken).ConfigureAwait(false) || VisualStudioToolPaths().Any();
        }

        return await ExistsOnPathNormallyAsync(executable, cancellationToken).ConfigureAwait(false);
    }

    private static string? FindAndroidSdkPath()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
        };

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) &&
            Directory.Exists(path) &&
            (Directory.Exists(Path.Combine(path, "platforms")) || Directory.Exists(Path.Combine(path, "cmdline-tools"))));
    }

    private static async Task<bool> HasDotNetSdkMajorAsync(int major, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-sdks",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0 &&
                output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(line => line.StartsWith($"{major}.", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ExistsOnPathNormallyAsync(string executable, CancellationToken cancellationToken)
    {
        var command = OperatingSystem.IsWindows() ? "where" : "which";
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = executable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> FindWithVsWhere(string vswhere, string pattern)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = vswhere,
                    Arguments = $"-latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find \"{pattern}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            if (process.ExitCode != 0)
            {
                yield break;
            }

            foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (File.Exists(line))
                {
                    yield return line;
                }
            }
        }
        finally
        {
        }
    }
}
