namespace GitBuild.Core.Models;

public sealed record BuildEvent(DateTimeOffset Timestamp, string Message, bool IsError = false)
{
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Message}";
}
