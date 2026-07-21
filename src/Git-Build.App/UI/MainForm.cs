using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GitBuild.Core.Models;
using GitBuild.Core.Services;
using GitBuild.Infrastructure.Artifacts;
using GitBuild.Infrastructure.Build;
using GitBuild.Infrastructure.Dependencies;
using GitBuild.Infrastructure.Detection;
using GitBuild.Infrastructure.Failures;
using GitBuild.Infrastructure.Git;
using GitBuild.Infrastructure.Logging;
using GitBuild.Infrastructure.Paths;
using GitBuild.Infrastructure.Process;

namespace GitBuild.App.UI;

public sealed class MainForm : Form
{
    private readonly IAppPaths _paths;
    private readonly IRepositoryService _repositoryService;
    private readonly IBuildSystemDetector _detector;
    private readonly IDependencyService _dependencyService;
    private readonly IBuildExecutor _buildExecutor;
    private readonly IArtifactLocator _artifactLocator;
    private readonly IFailureExplainer _failureExplainer;
    private readonly TextBox _urlBox = new();
    private readonly Button _buildButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _foldersButton = new();
    private readonly Button _aboutButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _detectedLabel = new();
    private readonly RichTextBox _logBox = new();
    private readonly DataGridView _artifactGrid = new();
    private readonly TextBox _explanationBox = new();
    private CancellationTokenSource? _cancellation;
    private string _latestRepositoryPath = "";
    private string _latestLog = "";

    private static class Theme
    {
        public static readonly Color Background = Color.FromArgb(18, 22, 28);
        public static readonly Color Surface = Color.FromArgb(29, 34, 43);
        public static readonly Color SurfaceAlt = Color.FromArgb(24, 29, 37);
        public static readonly Color LogSurface = Color.FromArgb(12, 16, 22);
        public static readonly Color Button = Color.FromArgb(38, 45, 56);
        public static readonly Color DisabledButton = Color.FromArgb(31, 36, 45);
        public static readonly Color ButtonHover = Color.FromArgb(49, 58, 72);
        public static readonly Color ButtonPressed = Color.FromArgb(61, 72, 88);
        public static readonly Color Border = Color.FromArgb(76, 86, 104);
        public static readonly Color Text = Color.FromArgb(235, 239, 245);
        public static readonly Color MutedText = Color.FromArgb(160, 170, 185);
        public static readonly Color ErrorText = Color.FromArgb(255, 132, 132);
    }

    public MainForm()
    {
        Text = "Git-Build";
        ApplyWindowIcon();
        Width = 1180;
        Height = 760;
        MinimumSize = new Size(960, 620);
        StartPosition = FormStartPosition.CenterScreen;

        var runner = new ProcessRunner();
        _paths = new AppPaths();
        _repositoryService = new GitRepositoryService(_paths, runner);
        _detector = new BuildSystemDetector();
        _dependencyService = new DependencyService(runner);
        _buildExecutor = new BuildExecutor(runner);
        _artifactLocator = new ArtifactLocator();
        _failureExplainer = new FailureExplainer();

        BuildLayout();
    }

    private void ApplyWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Git-Build.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }
    }

    private void BuildLayout()
    {
        Font = new Font("Segoe UI", 10);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        _urlBox.PlaceholderText = "Choose a local project folder";
        _urlBox.Dock = DockStyle.Fill;
        _urlBox.Font = new Font("Segoe UI", 11);
        _urlBox.Margin = new Padding(0, 26, 10, 14);
        _urlBox.BackColor = Theme.Surface;
        _urlBox.ForeColor = Theme.Text;
        _urlBox.BorderStyle = BorderStyle.FixedSingle;
        _urlBox.ReadOnly = true;

        ConfigureButton(_buildButton, "Build");
        ConfigureButton(_cancelButton, "Cancel");
        ConfigureButton(_browseButton, "Browse");
        ConfigureButton(_foldersButton, "Folders");
        ConfigureButton(_aboutButton, "About");
        _cancelButton.Enabled = false;

        _buildButton.Click += async (_, _) => await StartBuildFromClickAsync();
        _cancelButton.Click += (_, _) => _cancellation?.Cancel();
        _browseButton.Click += (_, _) => BrowseLocalFolder();
        _foldersButton.Click += (_, _) => OpenFolder(_paths.SettingsDirectory);
        _aboutButton.Click += (_, _) => MessageBox.Show(this, "Git-Build 1.0.0\nA modern Git repository build manager.", "About Git-Build", MessageBoxButtons.OK, MessageBoxIcon.Information);

        input.Controls.Add(_urlBox, 0, 0);
        input.Controls.Add(_buildButton, 1, 0);
        input.Controls.Add(_cancelButton, 2, 0);
        input.Controls.Add(_browseButton, 3, 0);
        input.Controls.Add(_foldersButton, 4, 0);
        input.Controls.Add(_aboutButton, 5, 0);
        root.Controls.Add(input, 0, 0);

        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        _statusLabel.Text = "Ready";
        _detectedLabel.Text = "Build system: not detected";
        _statusLabel.ForeColor = Theme.Text;
        _detectedLabel.ForeColor = Theme.Text;
        statusPanel.Controls.Add(_statusLabel, 0, 0);
        statusPanel.Controls.Add(_detectedLabel, 1, 0);
        root.Controls.Add(statusPanel, 0, 1);

        _logBox.Dock = DockStyle.Fill;
        _logBox.ReadOnly = true;
        _logBox.BackColor = Theme.LogSurface;
        _logBox.ForeColor = Theme.Text;
        _logBox.Font = new Font("Consolas", 9.5f);
        root.Controls.Add(_logBox, 0, 2);

        var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.Controls.Add(lower, 0, 3);

        _artifactGrid.Dock = DockStyle.Fill;
        _artifactGrid.AllowUserToAddRows = false;
        _artifactGrid.AllowUserToDeleteRows = false;
        _artifactGrid.AllowUserToResizeRows = false;
        _artifactGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _artifactGrid.BackgroundColor = Theme.Surface;
        _artifactGrid.BorderStyle = BorderStyle.FixedSingle;
        _artifactGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _artifactGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _artifactGrid.EnableHeadersVisualStyles = false;
        _artifactGrid.GridColor = Theme.Border;
        _artifactGrid.MultiSelect = false;
        _artifactGrid.ReadOnly = true;
        _artifactGrid.RowHeadersVisible = false;
        _artifactGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _artifactGrid.DefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.DefaultCellStyle.ForeColor = Theme.Text;
        _artifactGrid.DefaultCellStyle.SelectionBackColor = Theme.ButtonPressed;
        _artifactGrid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        _artifactGrid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        _artifactGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface;
        _artifactGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Text;
        _artifactGrid.RowsDefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.AlternatingRowsDefaultCellStyle.BackColor = Theme.SurfaceAlt;
        _artifactGrid.Columns.Add("Artifact", "Artifact");
        _artifactGrid.Columns.Add("Size", "Size");
        _artifactGrid.Columns.Add("Modified", "Modified");
        _artifactGrid.Columns[0].FillWeight = 55;
        _artifactGrid.Columns[1].FillWeight = 15;
        _artifactGrid.Columns[2].FillWeight = 30;
        _artifactGrid.DoubleClick += (_, _) => OpenSelectedArtifact();
        lower.Controls.Add(_artifactGrid, 0, 0);

        _explanationBox.Dock = DockStyle.Fill;
        _explanationBox.Multiline = true;
        _explanationBox.ReadOnly = true;
        _explanationBox.ScrollBars = ScrollBars.Vertical;
        _explanationBox.BackColor = Theme.Surface;
        _explanationBox.ForeColor = Theme.Text;
        _explanationBox.BorderStyle = BorderStyle.FixedSingle;
        lower.Controls.Add(_explanationBox, 1, 0);

        var footer = new Label
        {
            Text = $"Git-Build stores settings and logs in {_paths.SettingsDirectory}",
            Dock = DockStyle.Fill,
            ForeColor = Theme.MutedText
        };
        root.Controls.Add(footer, 0, 4);
    }

    private static void PaintDarkButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button || button.Enabled)
        {
            return;
        }

        using var background = new SolidBrush(Theme.DisabledButton);
        using var border = new Pen(Theme.Border);
        using var text = new SolidBrush(Theme.Text);
        e.Graphics.FillRectangle(background, button.ClientRectangle);
        e.Graphics.DrawRectangle(border, 0, 0, button.Width - 1, button.Height - 1);
        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static void ApplyButtonTheme(Button button)
    {
        button.BackColor = button.Enabled ? Theme.Button : Theme.DisabledButton;
        button.ForeColor = Theme.Text;
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(8, 26, 0, 14);
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Theme.Button;
        button.ForeColor = Theme.Text;
        button.FlatAppearance.BorderColor = Theme.Border;
        button.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
        button.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
        button.EnabledChanged += (_, _) => ApplyButtonTheme(button);
        button.Paint += PaintDarkButton;
        ApplyButtonTheme(button);
    }




    private sealed class BuildProgress : IProgress<BuildEvent>
    {
        private readonly Control _owner;
        private readonly SessionLogWriter _sessionLog;
        private readonly Action<BuildEvent> _uiHandler;

        public BuildProgress(Control owner, SessionLogWriter sessionLog, Action<BuildEvent> uiHandler)
        {
            _owner = owner;
            _sessionLog = sessionLog;
            _uiHandler = uiHandler;
        }

        public void Report(BuildEvent value)
        {
            try
            {
                _sessionLog.Write(value);
            }
            catch
            {
                // Logging should never stop the build process.
            }

            if (_owner.IsDisposed)
            {
                return;
            }

            try
            {
                if (_owner.InvokeRequired)
                {
                    _owner.BeginInvoke((MethodInvoker)(() => _uiHandler(value)));
                }
                else
                {
                    _uiHandler(value);
                }
            }
            catch
            {
                // UI updates should never stop the build process.
            }
        }
    }

    private void BrowseLocalFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a local project folder for Git-Build",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _urlBox.Text = dialog.SelectedPath;
        }
    }

    private async Task StartBuildFromClickAsync()
    {
        try
        {
            await StartBuildAsync();
        }
        catch (Exception ex)
        {
            ShowFatalBuildError(ex);
        }
    }

    private void ShowFatalBuildError(Exception ex)
    {
        try
        {
            AppendLog(new BuildEvent(DateTimeOffset.Now, ex.Message, true));
            _explanationBox.Text = "Git-Build hit an unexpected error before the build could continue." + Environment.NewLine + Environment.NewLine + ex.Message;
            SetStatus(BuildStatus.Failed, "Unexpected error.");
        }
        catch
        {
            MessageBox.Show(this, ex.Message, "Git-Build crashed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartBuildAsync()
    {
        if (string.IsNullOrWhiteSpace(_urlBox.Text))
        {
            MessageBox.Show(this, "Choose a local project folder first.", "Git-Build", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var inputText = _urlBox.Text.Trim();
        _cancellation = new CancellationTokenSource();
        _buildButton.Enabled = false;
        _cancelButton.Enabled = true;
        _artifactGrid.Rows.Clear();
        _explanationBox.Clear();
        _logBox.Clear();
        _latestLog = "";

        using var sessionLog = new SessionLogWriter(_paths);
        IProgress<BuildEvent> progress = new BuildProgress(this, sessionLog, buildEvent =>
        {
            _latestLog += buildEvent + Environment.NewLine;
            AppendLog(buildEvent);
        });

        try
        {
            if (!Directory.Exists(inputText))
            {
                MessageBox.Show(this, "Choose a real local project folder. Git-Build is currently set to local-folder mode only.", "Git-Build", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus(BuildStatus.Cancelled, "No local folder selected.");
                return;
            }

            SetStatus(BuildStatus.Detecting, "Using local project folder...");
            _latestRepositoryPath = inputText;

            SetStatus(BuildStatus.Detecting, "Detecting build system...");
            var detection = await _detector.DetectAsync(_latestRepositoryPath, _cancellation.Token);
            _detectedLabel.Text = $"Build system: {detection.DisplayName} ({detection.Reason})";

            if (detection.Kind == BuildSystemKind.Unknown)
            {
                throw new InvalidOperationException("Git-Build could not detect a supported build system in this repository.");
            }

            var missing = await _dependencyService.FindMissingAsync(detection.RequiredDependencies, _cancellation.Token);
            if (missing.Count > 0)
            {
                var installable = missing.Where(item => item.InstallCommand is not null).ToArray();
                var message = "Git-Build found missing tools:" + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, missing.Select(item => $"- {item.Name}: {item.InstallHint}"));

                if (installable.Length > 0)
                {
                    var choice = MessageBox.Show(
                        this,
                        message + Environment.NewLine + Environment.NewLine + "Install supported tools now? Choose No to continue without installing.",
                        "Git-Build dependencies",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (choice == DialogResult.Cancel)
                    {
                        SetStatus(BuildStatus.Cancelled, "Cancelled before build.");
                        return;
                    }

                    if (choice == DialogResult.Yes)
                    {
                        SetStatus(BuildStatus.InstallingDependencies, "Installing missing tools...");
                        progress.Report(new BuildEvent(DateTimeOffset.Now, "Installing missing tools selected by Git-Build."));
                        await _dependencyService.InstallAsync(installable, progress, _cancellation.Token);
                        progress.Report(new BuildEvent(DateTimeOffset.Now, "Dependency installation finished. Git-Build refreshed PATH and will continue."));
                    }
                    else
                    {
                        progress.Report(new BuildEvent(DateTimeOffset.Now, "User chose to continue without installing missing tools.", true));
                    }
                }
                else
                {
                    MessageBox.Show(
                        this,
                        message + Environment.NewLine + Environment.NewLine + "Install these tools manually, or choose OK to try building anyway.",
                        "Git-Build dependencies",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    progress.Report(new BuildEvent(DateTimeOffset.Now, "Missing tools were detected, but Git-Build does not have an automatic installer for them.", true));
                }
            }
            else
            {
                progress.Report(new BuildEvent(DateTimeOffset.Now, "All detected build tools are available."));
            }

            var confirmCommands = detection.BuildCommands.Where(command => command.RequiresConfirmation).ToArray();
            if (confirmCommands.Length > 0)
            {
                SetStatus(BuildStatus.WaitingForConfirmation, "Waiting for dependency-step confirmation...");
                var choice = MessageBox.Show(this,
                    "Git-Build needs to run dependency commands for this project:\n\n" +
                    string.Join("\n", confirmCommands.Select(command => $"- {command.DisplayName}: {command.FileName} {command.Arguments}")) +
                    "\n\nContinue?",
                    "Git-Build build steps",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (choice != DialogResult.Yes)
                {
                    SetStatus(BuildStatus.Cancelled, "Cancelled before dependency steps.");
                    return;
                }
            }

            SetStatus(BuildStatus.Building, "Building project...");
            var exitCode = await _buildExecutor.ExecuteAsync(detection.BuildCommands, progress, _cancellation.Token);
            if (exitCode != 0)
            {
                ShowExplanation();
                SetStatus(BuildStatus.Failed, "Build failed. Git-Build explained the likely cause.");
                return;
            }

            await LoadArtifactsAsync();
            SetStatus(BuildStatus.Succeeded, "Build succeeded.");
        }
        catch (OperationCanceledException)
        {
            SetStatus(BuildStatus.Cancelled, "Cancelled.");
        }
        catch (Exception ex)
        {
            var buildEvent = new BuildEvent(DateTimeOffset.Now, ex.Message, true);
            _latestLog += buildEvent + Environment.NewLine;
            AppendLog(buildEvent);
            ShowExplanation();
            SetStatus(BuildStatus.Failed, "Build failed.");
        }
        finally
        {
            _buildButton.Enabled = true;
            _cancelButton.Enabled = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task LoadArtifactsAsync()
    {
        _artifactGrid.Rows.Clear();
        if (string.IsNullOrWhiteSpace(_latestRepositoryPath))
        {
            return;
        }

        var artifacts = await _artifactLocator.LocateAsync(_latestRepositoryPath, CancellationToken.None);
        foreach (var artifact in artifacts)
        {
            var rowIndex = _artifactGrid.Rows.Add(artifact.DisplayName, FormatSize(artifact.SizeBytes), artifact.LastModified.ToString("g"));
            _artifactGrid.Rows[rowIndex].Tag = artifact.Path;
        }

        if (artifacts.Count == 0)
        {
            _explanationBox.Text = "Build completed, but Git-Build did not find a runnable artifact yet.";
        }
        else
        {
            _explanationBox.Text = $"Found {artifacts.Count} artifact(s). Double-click one to open it.";
        }
    }

    private void ShowExplanation()
    {
        var explanation = _failureExplainer.Explain(_latestLog);
        _explanationBox.Text = explanation.Summary + Environment.NewLine + Environment.NewLine +
            "Likely causes:" + Environment.NewLine +
            string.Join(Environment.NewLine, explanation.LikelyCauses.Select(item => "- " + item)) +
            Environment.NewLine + Environment.NewLine +
            "Suggested fixes:" + Environment.NewLine +
            string.Join(Environment.NewLine, explanation.SuggestedFixes.Select(item => "- " + item));
    }

    private void AppendLog(BuildEvent buildEvent)
    {
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = buildEvent.IsError ? Theme.ErrorText : Theme.Text;
        _logBox.AppendText(buildEvent + Environment.NewLine);
        _logBox.ScrollToCaret();
    }

    private void SetStatus(BuildStatus status, string message)
    {
        _statusLabel.Text = status switch
        {
            BuildStatus.Succeeded => "Success",
            BuildStatus.Failed => "Failed",
            BuildStatus.Cancelled => "Cancelled",
            _ => $"{status}: {message}"
        };
    }

    private void OpenSelectedArtifact()
    {
        if (_artifactGrid.CurrentRow?.Tag is not string artifactPath)
        {
            return;
        }

        OpenFolder(artifactPath);
    }

    private static void OpenFolder(string folderPath)
    {
        var target = File.Exists(folderPath) ? $"/select,\"{folderPath}\"" : $"\"{folderPath}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }
}


