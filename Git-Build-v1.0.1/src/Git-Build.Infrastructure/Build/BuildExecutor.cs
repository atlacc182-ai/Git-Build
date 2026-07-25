using GitBuild.Core.Models;
using GitBuild.Core.Services;
using GitBuild.Infrastructure.Process;

namespace GitBuild.Infrastructure.Build;

public sealed class BuildExecutor(ProcessRunner runner) : IBuildExecutor
{
    public async Task<int> ExecuteAsync(IEnumerable<CommandSpec> commands, IProgress<BuildEvent> progress, CancellationToken cancellationToken)
    {
        var commandList = commands.ToArray();
        foreach (var command in commandList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exitCode = await runner.RunAsync(command, progress, cancellationToken).ConfigureAwait(false);
            if (exitCode == 0)
            {
                continue;
            }

            if (IsNodePackageBuild(command))
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, "Node build could not find a local module. Git-Build will run the package install command and retry once.", true));
                var install = CreateRepairInstallCommand(command);
                var installExitCode = await runner.RunAsync(install, progress, cancellationToken).ConfigureAwait(false);
                if (installExitCode == 0)
                {
                    exitCode = await runner.RunAsync(command, progress, cancellationToken).ConfigureAwait(false);
                }
            }

            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        return 0;
    }

    private static bool IsNodePackageBuild(CommandSpec command) =>
        (command.FileName.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
         command.FileName.Equals("corepack", StringComparison.OrdinalIgnoreCase) ||
         command.FileName.Equals("bun", StringComparison.OrdinalIgnoreCase)) &&
        command.Arguments.Contains("run build", StringComparison.OrdinalIgnoreCase);

    private static CommandSpec CreateRepairInstallCommand(CommandSpec failedBuild) =>
        failedBuild.FileName.ToLowerInvariant() switch
        {
            "corepack" when failedBuild.Arguments.StartsWith("yarn", StringComparison.OrdinalIgnoreCase) =>
                new CommandSpec("corepack", "yarn install --ignore-engines", failedBuild.WorkingDirectory, "Repair Yarn packages", true),
            "corepack" when failedBuild.Arguments.StartsWith("pnpm", StringComparison.OrdinalIgnoreCase) =>
                new CommandSpec("corepack", "pnpm install", failedBuild.WorkingDirectory, "Repair pnpm packages", true),
            "bun" => new CommandSpec("bun", "install", failedBuild.WorkingDirectory, "Repair Bun packages", true),
            _ => new CommandSpec("npm", "install --legacy-peer-deps --no-audit --no-fund --prefer-offline", failedBuild.WorkingDirectory, "Repair npm packages", true)
        };
}

