using GitBuild.Core.Models;
using GitBuild.Core.Services;

namespace GitBuild.Infrastructure.Artifacts;

public sealed class ArtifactLocator : IArtifactLocator
{
    private static readonly string[] ArtifactExtensions =
    [
        ".exe", ".dll", ".msi", ".zip", ".tar", ".gz", ".jar", ".war", ".nupkg",
        ".apk", ".aab", ".ipa", ".deb", ".rpm", ".appimage", ".dmg", ".cmd", ".bat", ".ps1",
        ".lib", ".so", ".dylib", ".a", ".ear"
    ];

    public Task<IReadOnlyList<BuildArtifact>> LocateAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var artifacts = Directory.EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => ArtifactExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists && info.Length > 0)
            .OrderByDescending(info => info.FullName.Contains($"{Path.DirectorySeparatorChar}Git-Build-Artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(info => info.LastWriteTimeUtc)
            .Take(30)
            .Select(info => new BuildArtifact(info.FullName, Path.GetFileName(info.FullName), info.Length, info.LastWriteTime))
            .ToArray();

        return Task.FromResult<IReadOnlyList<BuildArtifact>>(artifacts);
    }
}
