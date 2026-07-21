using GitBuild.Core.Models;

namespace GitBuild.Core.Services;

public interface IAppPaths
{
    string SettingsDirectory { get; }
    string LogsDirectory { get; }
    string RepositoriesDirectory { get; }
}

public interface IRepositoryService
{
    Task<RepositoryJob> CloneAsync(string remoteUrl, IProgress<BuildEvent> progress, CancellationToken cancellationToken);
}

public interface IBuildSystemDetector
{
    Task<DetectionResult> DetectAsync(string repositoryPath, CancellationToken cancellationToken);
}

public interface IDependencyService
{
    Task<IReadOnlyList<DependencyRequirement>> FindMissingAsync(IEnumerable<DependencyRequirement> requirements, CancellationToken cancellationToken);
    Task InstallAsync(IEnumerable<DependencyRequirement> requirements, IProgress<BuildEvent> progress, CancellationToken cancellationToken);
}

public interface IBuildExecutor
{
    Task<int> ExecuteAsync(IEnumerable<CommandSpec> commands, IProgress<BuildEvent> progress, CancellationToken cancellationToken);
}

public interface IArtifactLocator
{
    Task<IReadOnlyList<BuildArtifact>> LocateAsync(string repositoryPath, CancellationToken cancellationToken);
}

public interface IFailureExplainer
{
    BuildFailureExplanation Explain(string logText);
}
