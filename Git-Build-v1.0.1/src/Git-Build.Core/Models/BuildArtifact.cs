namespace GitBuild.Core.Models;

public sealed record BuildArtifact(string Path, string DisplayName, long SizeBytes, DateTimeOffset LastModified);
