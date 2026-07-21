namespace GitBuild.Core.Models;

public sealed record DependencyRequirement(
    string Name,
    string Executable,
    string InstallHint,
    CommandSpec? InstallCommand = null);
