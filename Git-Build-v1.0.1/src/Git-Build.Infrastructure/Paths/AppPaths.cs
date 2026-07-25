using GitBuild.Core.Services;

namespace GitBuild.Infrastructure.Paths;

public sealed class AppPaths : IAppPaths
{
    public string SettingsDirectory { get; }
    public string LogsDirectory { get; }
    public string RepositoriesDirectory { get; }

    public AppPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "Git-Build-Data")
            : Path.Combine(localAppData, "Git-Build");

        SettingsDirectory = Path.Combine(root, "Settings");
        LogsDirectory = Path.Combine(root, "Logs");
        RepositoriesDirectory = Path.Combine(root, "Repositories");

        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RepositoriesDirectory);
    }
}
