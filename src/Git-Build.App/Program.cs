using System.IO;
using System.Threading;
using System.Windows.Forms;
using GitBuild.App.UI;

namespace GitBuild.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowUnexpectedError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                ShowUnexpectedError(exception);
            }
        };

        Application.Run(new MainForm());
    }

    private static void ShowUnexpectedError(Exception exception)
    {
        var logPath = WriteCrashLog(exception);
        MessageBox.Show(
            exception.Message + Environment.NewLine + Environment.NewLine + "Crash log: " + logPath,
            "Git-Build crashed",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static string WriteCrashLog(Exception exception)
    {
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Git-Build", "Logs");
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, $"Git-Build-crash-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, exception.ToString());
            return path;
        }
        catch
        {
            return "Could not write crash log.";
        }
    }
}
