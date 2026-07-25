using GitBuild.Core.Models;
using GitBuild.Core.Services;
using GitBuild.Infrastructure.Process;

namespace GitBuild.Infrastructure.Git;

public sealed class GitRepositoryService(IAppPaths paths, ProcessRunner runner) : IRepositoryService
{
    public async Task<RepositoryJob> CloneAsync(string remoteUrl, IProgress<BuildEvent> progress, CancellationToken cancellationToken)
    {
        if (!IsSupportedRemote(remoteUrl))
        {
            throw new ArgumentException("Enter a valid public Git repository URL or SSH remote.", nameof(remoteUrl));
        }

        var repoName = GetRepositoryName(remoteUrl);

        var folderName = $"{Sanitize(repoName)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var localPath = Path.Combine(paths.RepositoriesDirectory, folderName);
        Directory.CreateDirectory(localPath);

        var command = new CommandSpec("git", $"clone --progress \"{remoteUrl}\" \"{localPath}\"", paths.RepositoriesDirectory, "Clone repository");
        var exitCode = await runner.RunAsync(command, progress, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException("Git could not clone the repository. Check that the URL is public and reachable.");
        }

        return new RepositoryJob(remoteUrl, localPath, DateTimeOffset.Now);
    }

    private static bool IsSupportedRemote(string remoteUrl)
    {
        if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Scheme is "http" or "https" or "ssh" or "git";
        }

        return remoteUrl.Contains('@') && remoteUrl.Contains(':') && remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRepositoryName(string remoteUrl)
    {
        var trimmed = remoteUrl.TrimEnd('/');
        var lastSeparator = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf(':'));
        var name = lastSeparator >= 0 ? trimmed[(lastSeparator + 1)..] : trimmed;
        name = Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrWhiteSpace(name) ? "repository" : name;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
    }
}
