using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace GitBuild.Installer;

internal static class Program
{
    private const string AppName = "Git-Build";
    private const string ExeName = "Git-Build.exe";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(arg => arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            InstallerActions.Uninstall(showUi: true);
            return;
        }

        using var form = new InstallerForm();
        Application.Run(form);
    }

    private static class InstallerActions
    {
        private static readonly string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Git-Build";
        private static readonly string AppPathsRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\App Paths\Git-Build.exe";

        public static string GetDefaultInstallDirectory()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Combine(programFiles, AppName);
        }

        public static string GetPerUserInstallDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "App");
        }

        public static void CopyUninstaller(string installDir)
        {
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
            {
                return;
            }

            File.Copy(currentExe, Path.Combine(installDir, "Git-Build-Uninstall.exe"), overwrite: true);
        }

        public static void RegisterWindowsIntegration(string installDir, string exePath)
        {
            var uninstallExe = Path.Combine(installDir, "Git-Build-Uninstall.exe");

            using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
            {
                key?.SetValue("DisplayName", AppName);
                key?.SetValue("DisplayVersion", "1.0.0");
                key?.SetValue("Publisher", "Git-Build");
                key?.SetValue("InstallLocation", installDir);
                key?.SetValue("DisplayIcon", exePath);
                key?.SetValue("UninstallString", $"\"{uninstallExe}\" /uninstall");
                key?.SetValue("QuietUninstallString", $"\"{uninstallExe}\" /uninstall /quiet");
                key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }

            using (var appPath = Registry.CurrentUser.CreateSubKey(AppPathsRegistryPath))
            {
                appPath?.SetValue("", exePath);
                appPath?.SetValue("Path", installDir);
            }
        }

        public static void Uninstall(bool showUi)
        {
            try
            {
                var installDir = GetRegisteredInstallDirectory();
                RemoveShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName));

                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(AppPathsRegistryPath, throwOnMissingSubKey: false);

                if (!string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir))
                {
                    ScheduleDirectoryRemoval(installDir);
                }

                if (showUi)
                {
                    MessageBox.Show("Git-Build was uninstalled.", "Git-Build Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (showUi)
                {
                    MessageBox.Show(ex.Message, "Git-Build uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string GetRegisteredInstallDirectory()
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
            return key?.GetValue("InstallLocation") as string ?? GetPerUserInstallDirectory();
        }

        private static void RemoveShortcut(string directory)
        {
            var shortcut = Path.Combine(directory, AppName + ".lnk");
            if (File.Exists(shortcut))
            {
                File.Delete(shortcut);
            }
        }

        private static void ScheduleDirectoryRemoval(string installDir)
        {
            var script = $"""
                timeout /t 2 /nobreak >nul
                rmdir /s /q "{installDir}"
                """;
            var cmd = Path.Combine(Path.GetTempPath(), "git-build-uninstall.cmd");
            File.WriteAllText(cmd, script, Encoding.ASCII);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + cmd + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
    }

    private sealed class InstallerForm : Form
    {
        private readonly Button _installButton = new();
        private readonly Button _browseButton = new();
        private readonly Button _uninstallButton = new();
        private readonly Button _closeButton = new();
        private readonly CheckBox _desktopShortcut = new();
        private readonly CheckBox _startMenuShortcut = new();
        private readonly TextBox _installPathBox = new();
        private readonly TextBox _log = new();

        public InstallerForm()
        {
            Text = "Git-Build Setup";
            Width = 620;
            Height = 430;
            MinimumSize = new Size(560, 380);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(8, 18, 12);
            ForeColor = Color.FromArgb(238, 255, 242);
            Font = new Font("Segoe UI", 10);
            Icon = TryLoadIcon();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                RowCount = 6,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            root.Controls.Add(new Label
            {
                Text = "Install Git-Build\r\nA local repository build manager for Windows.",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 15),
                ForeColor = Color.FromArgb(94, 255, 150)
            }, 0, 0);

            var pathRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            _installPathBox.Dock = DockStyle.Fill;
            _installPathBox.Text = InstallerActions.GetDefaultInstallDirectory();
            _installPathBox.BackColor = Color.FromArgb(2, 8, 5);
            _installPathBox.ForeColor = ForeColor;
            _installPathBox.BorderStyle = BorderStyle.FixedSingle;
            ConfigureButton(_browseButton, "Browse");
            _browseButton.Click += (_, _) => BrowseInstallFolder();
            pathRow.Controls.Add(_installPathBox, 0, 0);
            pathRow.Controls.Add(_browseButton, 1, 0);
            root.Controls.Add(pathRow, 0, 1);

            _desktopShortcut.Text = "Create desktop shortcut";
            _desktopShortcut.Checked = true;
            _desktopShortcut.Dock = DockStyle.Fill;
            _desktopShortcut.ForeColor = ForeColor;
            _desktopShortcut.BackColor = BackColor;
            root.Controls.Add(_desktopShortcut, 0, 2);

            _startMenuShortcut.Text = "Create Start menu shortcut";
            _startMenuShortcut.Checked = true;
            _startMenuShortcut.Dock = DockStyle.Fill;
            _startMenuShortcut.ForeColor = ForeColor;
            _startMenuShortcut.BackColor = BackColor;
            root.Controls.Add(_startMenuShortcut, 0, 3);

            _log.Dock = DockStyle.Fill;
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.FromArgb(2, 8, 5);
            _log.ForeColor = Color.FromArgb(238, 255, 242);
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.Text = "Ready to install Git-Build.";
            root.Controls.Add(_log, 0, 4);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            ConfigureButton(_installButton, "Install");
            ConfigureButton(_uninstallButton, "Uninstall");
            ConfigureButton(_closeButton, "Close");
            _installButton.Click += (_, _) => Install();
            _uninstallButton.Click += (_, _) => InstallerActions.Uninstall(showUi: true);
            _closeButton.Click += (_, _) => Close();
            actions.Controls.Add(_closeButton);
            actions.Controls.Add(_uninstallButton);
            actions.Controls.Add(_installButton);
            root.Controls.Add(actions, 0, 5);

            Controls.Add(root);
        }

        private void BrowseInstallFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose where Git-Build will be installed",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(_installPathBox.Text) ? _installPathBox.Text : InstallerActions.GetDefaultInstallDirectory()
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _installPathBox.Text = Path.Combine(dialog.SelectedPath, AppName);
            }
        }

        private void Install()
        {
            try
            {
                _installButton.Enabled = false;
                var installDir = string.IsNullOrWhiteSpace(_installPathBox.Text)
                    ? InstallerActions.GetDefaultInstallDirectory()
                    : _installPathBox.Text.Trim();
                Log("Installing to " + installDir);
                try
                {
                    Directory.CreateDirectory(installDir);
                }
                catch (UnauthorizedAccessException)
                {
                    installDir = InstallerActions.GetPerUserInstallDirectory();
                    _installPathBox.Text = installDir;
                    Log("No permission for Program Files. Installing per-user to " + installDir);
                    Directory.CreateDirectory(installDir);
                }

                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
                    ?? throw new InvalidOperationException("Installer payload is missing.");
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                archive.ExtractToDirectory(installDir, overwriteFiles: true);

                var exePath = Path.Combine(installDir, ExeName);
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException("Installed app executable was not found.", exePath);
                }

                if (_desktopShortcut.Checked)
                {
                    CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exePath);
                }

                if (_startMenuShortcut.Checked)
                {
                    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName);
                    Directory.CreateDirectory(startMenu);
                    CreateShortcut(startMenu, exePath);
                }

                InstallerActions.CopyUninstaller(installDir);
                InstallerActions.RegisterWindowsIntegration(installDir, exePath);

                Log("Installation complete.");
                var choice = MessageBox.Show(this, "Git-Build installed successfully. Open it now?", "Git-Build Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (choice == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                    Close();
                }
            }
            catch (Exception ex)
            {
                Log("Install failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Git-Build Setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _installButton.Enabled = true;
            }
        }

        private static string GetInstallDirectory()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            try
            {
                var dir = Path.Combine(programFiles, AppName);
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "App");
            }
        }

        private static void CreateShortcut(string directory, string targetPath)
        {
            Directory.CreateDirectory(directory);
            var shortcut = Path.Combine(directory, AppName + ".lnk");
            var script = $"""
                $shell = New-Object -ComObject WScript.Shell
                $shortcut = $shell.CreateShortcut('{EscapePowerShell(shortcut)}')
                $shortcut.TargetPath = '{EscapePowerShell(targetPath)}'
                $shortcut.WorkingDirectory = '{EscapePowerShell(Path.GetDirectoryName(targetPath) ?? "")}'
                $shortcut.IconLocation = '{EscapePowerShell(targetPath)},0'
                $shortcut.Save()
                """;
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
        }

        private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

        private void Log(string message)
        {
            _log.AppendText(Environment.NewLine + message);
        }

        private static void ConfigureButton(Button button, string text)
        {
            button.Text = text;
            button.Width = 116;
            button.Height = 36;
            button.Margin = new Padding(8, 4, 0, 4);
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = Color.FromArgb(13, 43, 25);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(39, 112, 66);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 68, 39);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(27, 93, 52);
        }

        private static Icon? TryLoadIcon()
        {
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Git-Build.ico");
                return File.Exists(iconPath) ? new Icon(iconPath) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
