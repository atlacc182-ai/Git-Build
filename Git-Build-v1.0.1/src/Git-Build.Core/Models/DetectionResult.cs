namespace GitBuild.Core.Models;

public sealed record DetectionResult(
    BuildSystemKind Kind,
    string DisplayName,
    string Reason,
    IReadOnlyList<DependencyRequirement> RequiredDependencies,
    IReadOnlyList<CommandSpec> BuildCommands);
