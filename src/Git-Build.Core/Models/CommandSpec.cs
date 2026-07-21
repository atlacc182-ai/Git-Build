namespace GitBuild.Core.Models;

public sealed record CommandSpec(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    string DisplayName,
    bool RequiresConfirmation = false);
