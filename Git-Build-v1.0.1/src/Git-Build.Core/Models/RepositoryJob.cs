namespace GitBuild.Core.Models;

public sealed record RepositoryJob(string RemoteUrl, string LocalPath, DateTimeOffset CreatedAt);
