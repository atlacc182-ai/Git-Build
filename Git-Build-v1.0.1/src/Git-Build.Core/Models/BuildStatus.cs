namespace GitBuild.Core.Models;

public enum BuildStatus
{
    Idle,
    Cloning,
    Detecting,
    WaitingForConfirmation,
    InstallingDependencies,
    Building,
    Succeeded,
    Failed,
    Cancelled
}
