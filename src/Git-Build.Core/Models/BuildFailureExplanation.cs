namespace GitBuild.Core.Models;

public sealed record BuildFailureExplanation(string Summary, IReadOnlyList<string> LikelyCauses, IReadOnlyList<string> SuggestedFixes);
