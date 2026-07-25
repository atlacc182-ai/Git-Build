using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
    private readonly ComboBox _themeBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _detectedLabel = new();
    private readonly RichTextBox _logBox = new();
    private readonly DataGridView _artifactGrid = new();
    private readonly TextBox _explanationBox = new();
    private readonly TabControl _detailsTabs = new();
    private readonly TabPage _summaryTab = new("Summary");
    private readonly TabPage _settingsTab = new("Settings");
    private readonly TabPage _toolsTab = new("Tools");
    private readonly TabPage _advancedTab = new("Advanced");
    private readonly Panel _titleBar = new();
    private readonly Label _titleText = new();
    private readonly Button _minimizeWindowButton = new();
    private readonly Button _maximizeWindowButton = new();
    private readonly Button _closeWindowButton = new();
    private readonly System.Windows.Forms.Timer _pulseTimer = new();
    private int _pulseStep;
    private const int ResizeGripSize = 8;
    private CancellationTokenSource? _cancellation;
    private string _latestRepositoryPath = "";
    private string _latestLog = "";

    private static class Theme
    {
        public static Color Background = Color.FromArgb(8, 13, 24);
        public static Color Surface = Color.FromArgb(11, 19, 32);
        public static Color SurfaceAlt = Color.FromArgb(15, 25, 40);
        public static Color LogSurface = Color.FromArgb(5, 10, 18);
        public static Color Header = Color.FromArgb(9, 17, 30);
        public static Color Button = Color.FromArgb(20, 34, 56);
        public static Color DisabledButton = Color.FromArgb(18, 26, 39);
        public static Color ButtonHover = Color.FromArgb(28, 48, 78);
        public static Color ButtonPressed = Color.FromArgb(36, 62, 98);
        public static Color Border = Color.FromArgb(31, 57, 93);
        public static Color SubtleBorder = Color.FromArgb(24, 43, 70);
        public static Color Text = Color.FromArgb(239, 246, 255);
        public static Color MutedText = Color.FromArgb(151, 165, 188);
        public static Color Accent = Color.FromArgb(56, 189, 248);
        public static Color Success = Color.FromArgb(74, 222, 128);
        public static Color ErrorText = Color.FromArgb(251, 113, 133);

        public static void UseBlueBlack()
        {
            Background = Color.FromArgb(8, 13, 24);
            Surface = Color.FromArgb(11, 19, 32);
            SurfaceAlt = Color.FromArgb(15, 25, 40);
            LogSurface = Color.FromArgb(5, 10, 18);
            Header = Color.FromArgb(9, 17, 30);
            Button = Color.FromArgb(20, 34, 56);
            DisabledButton = Color.FromArgb(18, 26, 39);
            ButtonHover = Color.FromArgb(28, 48, 78);
            ButtonPressed = Color.FromArgb(36, 62, 98);
            Border = Color.FromArgb(31, 57, 93);
            SubtleBorder = Color.FromArgb(24, 43, 70);
            Text = Color.FromArgb(239, 246, 255);
            MutedText = Color.FromArgb(151, 165, 188);
            Accent = Color.FromArgb(56, 189, 248);
            Success = Color.FromArgb(74, 222, 128);
            ErrorText = Color.FromArgb(251, 113, 133);
        }

        public static void UseGreenBlack()
        {
            Background = Color.FromArgb(3, 10, 6);
            Surface = Color.FromArgb(8, 21, 14);
            SurfaceAlt = Color.FromArgb(12, 31, 20);
            LogSurface = Color.FromArgb(1, 7, 4);
            Header = Color.FromArgb(7, 18, 12);
            Button = Color.FromArgb(13, 43, 25);
            DisabledButton = Color.FromArgb(12, 24, 16);
            ButtonHover = Color.FromArgb(20, 68, 39);
            ButtonPressed = Color.FromArgb(27, 93, 52);
            Border = Color.FromArgb(39, 112, 66);
            SubtleBorder = Color.FromArgb(22, 58, 36);
            Text = Color.FromArgb(239, 255, 244);
            MutedText = Color.FromArgb(159, 196, 170);
            Accent = Color.FromArgb(78, 238, 132);
            Success = Color.FromArgb(78, 238, 132);
            ErrorText = Color.FromArgb(255, 111, 111);
        }

        public static void UseLight()
        {
            Background = Color.FromArgb(244, 247, 251);
            Surface = Color.FromArgb(255, 255, 255);
            SurfaceAlt = Color.FromArgb(236, 242, 249);
            LogSurface = Color.FromArgb(248, 250, 252);
            Header = Color.FromArgb(255, 255, 255);
            Button = Color.FromArgb(232, 240, 250);
            DisabledButton = Color.FromArgb(229, 233, 240);
            ButtonHover = Color.FromArgb(219, 234, 254);
            ButtonPressed = Color.FromArgb(191, 219, 254);
            Border = Color.FromArgb(190, 205, 224);
            SubtleBorder = Color.FromArgb(219, 228, 239);
            Text = Color.FromArgb(15, 23, 42);
            MutedText = Color.FromArgb(91, 107, 129);
            Accent = Color.FromArgb(37, 99, 235);
            Success = Color.FromArgb(22, 163, 74);
            ErrorText = Color.FromArgb(220, 38, 38);
        }

        public static void UsePurple()
        {
            Background = Color.FromArgb(13, 10, 25);
            Surface = Color.FromArgb(25, 19, 43);
            SurfaceAlt = Color.FromArgb(33, 25, 55);
            LogSurface = Color.FromArgb(10, 7, 19);
            Header = Color.FromArgb(21, 15, 38);
            Button = Color.FromArgb(46, 34, 78);
            DisabledButton = Color.FromArgb(30, 25, 43);
            ButtonHover = Color.FromArgb(67, 50, 112);
            ButtonPressed = Color.FromArgb(84, 64, 139);
            Border = Color.FromArgb(91, 74, 142);
            SubtleBorder = Color.FromArgb(55, 43, 88);
            Text = Color.FromArgb(248, 245, 255);
            MutedText = Color.FromArgb(178, 164, 204);
            Accent = Color.FromArgb(196, 125, 255);
            Success = Color.FromArgb(74, 222, 128);
            ErrorText = Color.FromArgb(251, 113, 133);
        }
    }

    private static class UiFonts
    {
        public static readonly string Family = FontFamily.Families.Any(family => family.Name.Equals("Segoe UI Variable Text", StringComparison.OrdinalIgnoreCase))
            ? "Segoe UI Variable Text"
            : "Segoe UI";

        public static Font Regular(float size) => new(Family, size, FontStyle.Regular);

        public static Font Semibold(float size) => new(Family, size, FontStyle.Bold);

        public static Font Mono(float size)
        {
            var family = FontFamily.Families.Any(item => item.Name.Equals("Cascadia Mono", StringComparison.OrdinalIgnoreCase))
                ? "Cascadia Mono"
                : "Consolas";
            return new Font(family, size, FontStyle.Regular);
        }
    }

    public MainForm()
    {
        Text = "Git-Build";
        ApplyWindowIcon();
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        Width = 1280;
        Height = 820;
        MinimumSize = new Size(1040, 680);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
        Opacity = 1.0;

        var runner = new ProcessRunner();
        _paths = new AppPaths();
        _repositoryService = new GitRepositoryService(_paths, runner);
        _detector = new BuildSystemDetector();
        _dependencyService = new DependencyService(runner);
        _buildExecutor = new BuildExecutor(runner);
        _artifactLocator = new ArtifactLocator();
        _failureExplainer = new FailureExplainer();
        Theme.UseGreenBlack();
        _pulseTimer.Interval = 60;
        _pulseTimer.Tick += (_, _) => PulseActiveSurfaces();

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
            Font = UiFonts.Regular(9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
                Padding = new Padding(22),
            ColumnCount = 1,
            RowCount = 5
        };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var appShell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Theme.Background
            };
            appShell.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            appShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.Controls.Add(CreateNavigationRail(), 0, 0);
            shell.Controls.Add(root, 1, 0);
            BuildTitleBar();
            appShell.Controls.Add(_titleBar, 0, 0);
            appShell.Controls.Add(shell, 0, 1);
            Controls.Add(appShell);

            var input = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                BackColor = Theme.Header,
                Padding = new Padding(20, 8, 20, 10),
                Margin = new Padding(0, 0, 0, 10)
            };
            input.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            input.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

        _urlBox.PlaceholderText = "Choose a local project folder";
        _urlBox.Dock = DockStyle.Fill;
        _urlBox.Font = UiFonts.Regular(9.75f);
        _urlBox.Margin = new Padding(0, 8, 12, 8);
        _urlBox.BackColor = Theme.Surface;
        _urlBox.ForeColor = Theme.Text;
        _urlBox.BorderStyle = BorderStyle.FixedSingle;
        _urlBox.ReadOnly = true;

        ConfigureButton(_buildButton, "Build");
        ConfigureButton(_cancelButton, "Cancel");
        ConfigureButton(_browseButton, "Browse");
        ConfigureButton(_foldersButton, "Folders");
        ConfigureButton(_aboutButton, "Settings");
        _cancelButton.Enabled = false;
        ConfigureThemeBox();

        _buildButton.Click += async (_, _) => await StartBuildFromClickAsync();
        _cancelButton.Click += (_, _) => _cancellation?.Cancel();
        _browseButton.Click += (_, _) => BrowseLocalFolder();
        _foldersButton.Click += (_, _) => OpenFolder(_paths.SettingsDirectory);
        _aboutButton.Click += (_, _) => ShowSettingsDialog();

        var headerTitle = new Label
        {
            Text = "Git-Build",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(13.5f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var settingsStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Theme.Header
        };
        settingsStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settingsStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        settingsStrip.Controls.Add(new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(9f),
            ForeColor = Theme.MutedText,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 10, 0)
        }, 0, 0);
        settingsStrip.Controls.Add(new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            Font = UiFonts.Regular(8.5f),
            ForeColor = Theme.MutedText,
            TextAlign = ContentAlignment.MiddleRight
        }, 1, 0);
        input.Controls.Add(headerTitle, 0, 0);
        input.SetColumnSpan(headerTitle, 3);
        input.Controls.Add(settingsStrip, 3, 0);
        input.SetColumnSpan(settingsStrip, 3);
        input.Controls.Add(_urlBox, 0, 1);
        input.Controls.Add(_buildButton, 1, 1);
        input.Controls.Add(_cancelButton, 2, 1);
        input.Controls.Add(_browseButton, 3, 1);
        input.Controls.Add(_foldersButton, 4, 1);
        input.Controls.Add(_aboutButton, 5, 1);
        root.Controls.Add(input, 0, 0);

        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        _statusLabel.Text = "Ready";
        _detectedLabel.Text = "Build system: not detected";
        _statusLabel.Font = UiFonts.Semibold(9.5f);
        _detectedLabel.Font = UiFonts.Regular(9.25f);
        _statusLabel.ForeColor = Theme.Text;
        _detectedLabel.ForeColor = Theme.MutedText;
        statusPanel.Controls.Add(_statusLabel, 0, 0);
        statusPanel.Controls.Add(_detectedLabel, 1, 0);
        root.Controls.Add(statusPanel, 0, 1);

        _logBox.Dock = DockStyle.Fill;
        _logBox.ReadOnly = true;
        _logBox.BackColor = Theme.LogSurface;
        _logBox.ForeColor = Theme.Text;
        _logBox.Font = UiFonts.Mono(9.5f);
        _logBox.BorderStyle = BorderStyle.None;
        _logBox.HideSelection = false;
        _logBox.Text = "[ready] Select a local project folder to begin." + Environment.NewLine +
            "[hint] Git-Build supports Node, Python, .NET, Gradle, Maven, Rust, Go, CMake, Make, Docker, Ruby, and Visual Studio C++ projects.";

        var logPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(22),
            Margin = new Padding(0, 4, 0, 14)
        };
        logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Theme.Surface
        };
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        logHeader.Controls.Add(new Label
        {
            Text = "Build Logs",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(11.5f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        logHeader.Controls.Add(new Label
        {
            Text = "Live output",
            Dock = DockStyle.Fill,
            Font = UiFonts.Regular(9f),
            ForeColor = Theme.MutedText,
            TextAlign = ContentAlignment.MiddleCenter
        }, 1, 0);
        logHeader.Controls.Add(new Label
        {
            Text = "Local session",
            Dock = DockStyle.Fill,
            Font = UiFonts.Regular(9f),
            ForeColor = Theme.MutedText,
            TextAlign = ContentAlignment.MiddleRight
        }, 2, 0);
        logPanel.Controls.Add(logHeader, 0, 0);
        logPanel.Controls.Add(_logBox, 0, 1);
        root.Controls.Add(logPanel, 0, 2);

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
        _artifactGrid.BorderStyle = BorderStyle.None;
        _artifactGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _artifactGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _artifactGrid.EnableHeadersVisualStyles = false;
        _artifactGrid.GridColor = Theme.SubtleBorder;
        _artifactGrid.MultiSelect = false;
        _artifactGrid.ReadOnly = true;
        _artifactGrid.RowHeadersVisible = false;
        _artifactGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _artifactGrid.DefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.DefaultCellStyle.ForeColor = Theme.Text;
        _artifactGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(48, 73, 105);
        _artifactGrid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        _artifactGrid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        _artifactGrid.ColumnHeadersDefaultCellStyle.Font = UiFonts.Semibold(9.25f);
        _artifactGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface;
        _artifactGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Text;
        _artifactGrid.RowsDefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.DefaultCellStyle.Font = UiFonts.Regular(9.25f);
        _artifactGrid.AlternatingRowsDefaultCellStyle.BackColor = Theme.SurfaceAlt;
        _artifactGrid.Columns.Add("Artifact", "Artifact");
        _artifactGrid.Columns.Add("Size", "Size");
        _artifactGrid.Columns.Add("Modified", "Modified");
        _artifactGrid.Columns[0].FillWeight = 55;
        _artifactGrid.Columns[1].FillWeight = 15;
        _artifactGrid.Columns[2].FillWeight = 30;
        _artifactGrid.DoubleClick += (_, _) => OpenSelectedArtifact();

        var artifactsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 10, 0)
        };
        artifactsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        artifactsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        artifactsPanel.Controls.Add(new Label
        {
            Text = "Artifacts",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(10.5f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        artifactsPanel.Controls.Add(_artifactGrid, 0, 1);
        lower.Controls.Add(artifactsPanel, 0, 0);

        _explanationBox.Dock = DockStyle.Fill;
        _explanationBox.Multiline = true;
        _explanationBox.ReadOnly = true;
        _explanationBox.ScrollBars = ScrollBars.None;
        _explanationBox.BackColor = Theme.Surface;
        _explanationBox.ForeColor = Theme.Text;
        _explanationBox.BorderStyle = BorderStyle.None;
        _explanationBox.Font = UiFonts.Regular(10.5f);
        _explanationBox.Text = "Choose a project folder, then press Build." + Environment.NewLine + Environment.NewLine +
            "Git-Build will detect the build system, check dependencies, stream logs, and collect artifacts here.";
        var detailsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Theme.Surface,
            Padding = new Padding(16),
            Margin = new Padding(10, 0, 0, 0)
        };
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        detailsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsPanel.Controls.Add(new Label
        {
            Text = "Build Details",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(10.5f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        detailsPanel.Controls.Add(_explanationBox, 0, 1);
        lower.Controls.Add(detailsPanel, 1, 0);

        var footer = new Label
        {
            Text = $"Git-Build stores settings and logs in {_paths.SettingsDirectory}",
            Dock = DockStyle.Fill,
            ForeColor = Theme.MutedText,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(footer, 0, 4);
    }

    private static Panel CreateNavigationRail()
    {
        var rail = new Panel
        {
            Dock = DockStyle.Left,
            Width = 104,
            BackColor = Color.FromArgb(5, 10, 19),
            Padding = new Padding(10, 18, 10, 18)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        stack.Controls.Add(CreateNavItem("GB", true, 48, "Segoe UI Semibold", 11f));
        stack.Controls.Add(CreateNavItem("Build", true, 42, "Segoe UI Semibold", 9f));
        stack.Controls.Add(CreateNavItem("Files", false, 42, "Segoe UI", 8.75f));
        stack.Controls.Add(CreateNavItem("Logs", false, 42, "Segoe UI", 8.75f));
        stack.Controls.Add(CreateNavItem("Tools", false, 42, "Segoe UI", 8.75f));
        stack.Controls.Add(CreateNavItem("Settings", false, 42, "Segoe UI", 8.75f));

        rail.Controls.Add(stack);
        return rail;
    }

    private void BuildTitleBar()
    {
        _titleBar.Dock = DockStyle.Fill;
        _titleBar.BackColor = Theme.Header;
        _titleBar.Padding = new Padding(14, 0, 10, 0);
        _titleBar.MouseDown += TitleBar_MouseDown;
        _titleBar.DoubleClick += (_, _) => ToggleMaximize();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            BackColor = Theme.Header
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));

        var logo = new Label
        {
            Text = "▣",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UiFonts.Semibold(13f),
            ForeColor = Theme.Accent,
            BackColor = Theme.Header
        };
        logo.MouseDown += TitleBar_MouseDown;
        logo.DoubleClick += (_, _) => ToggleMaximize();

        _titleText.Text = "Git-Build";
        _titleText.Dock = DockStyle.Fill;
        _titleText.TextAlign = ContentAlignment.MiddleLeft;
        _titleText.Font = UiFonts.Semibold(10.5f);
        _titleText.ForeColor = Theme.Text;
        _titleText.BackColor = Theme.Header;
        _titleText.MouseDown += TitleBar_MouseDown;
        _titleText.DoubleClick += (_, _) => ToggleMaximize();

        ConfigureWindowButton(_minimizeWindowButton, "−", () => WindowState = FormWindowState.Minimized);
        ConfigureWindowButton(_maximizeWindowButton, "□", ToggleMaximize);
        ConfigureWindowButton(_closeWindowButton, "×", Close);

        layout.Controls.Add(logo, 0, 0);
        layout.Controls.Add(_titleText, 1, 0);
        layout.Controls.Add(_minimizeWindowButton, 2, 0);
        layout.Controls.Add(_maximizeWindowButton, 3, 0);
        layout.Controls.Add(_closeWindowButton, 4, 0);
        _titleBar.Controls.Add(layout);
    }

    private void ConfigureWindowButton(Button button, string text, Action action)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3, 5, 0, 5);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Theme.Header;
        button.ForeColor = Theme.Text;
        button.Font = UiFonts.Semibold(12f);
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
        button.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
        button.Click += (_, _) => action();
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        _maximizeWindowButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_maximizeWindowButton.IsDisposed)
        {
            _maximizeWindowButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNchittest = 0x84;
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;

        if (FormBorderStyle == FormBorderStyle.None && m.Msg == wmNchittest && WindowState != FormWindowState.Maximized)
        {
            var screenPoint = new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF));
            var point = PointToClient(screenPoint);
            var left = point.X <= ResizeGripSize;
            var right = point.X >= ClientSize.Width - ResizeGripSize;
            var top = point.Y <= ResizeGripSize;
            var bottom = point.Y >= ClientSize.Height - ResizeGripSize;

            if (left && top) { m.Result = htTopLeft; return; }
            if (right && top) { m.Result = htTopRight; return; }
            if (left && bottom) { m.Result = htBottomLeft; return; }
            if (right && bottom) { m.Result = htBottomRight; return; }
            if (left) { m.Result = htLeft; return; }
            if (right) { m.Result = htRight; return; }
            if (top) { m.Result = htTop; return; }
            if (bottom) { m.Result = htBottom; return; }
        }

        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyModernWindowEffects();
    }

    private void ApplyModernWindowEffects()
    {
        ApplyModernWindowEffects(Handle);
    }

    private static void ApplyModernWindowEffects(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        try
        {
            var darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int));

            var rounded = 2;
            NativeMethods.DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));

            var backdrop = 2;
            NativeMethods.DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
        }
        catch
        {
            // Older Windows builds simply ignore the modern backdrop attributes.
        }
    }

    private static void TryApplyAcrylicBlur(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        try
        {
            var accent = new NativeMethods.AccentPolicy
            {
                AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2,
                GradientColor = unchecked((int)0xCC101825)
            };
            var accentSize = System.Runtime.InteropServices.Marshal.SizeOf(accent);
            var accentPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(accentSize);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(accent, accentPtr, false);
                var data = new NativeMethods.WindowCompositionAttributeData
                {
                    Attribute = NativeMethods.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentSize,
                    Data = accentPtr
                };
                NativeMethods.SetWindowCompositionAttribute(handle, ref data);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(accentPtr);
            }
        }
        catch
        {
            // The DWM backdrop path below remains the fallback.
        }
    }

    private static Label CreateNavItem(string text, bool active, int height, string fontFamily, float fontSize)
    {
        return new Label
        {
            Width = 82,
            Height = height,
            Margin = new Padding(0, 0, 0, 10),
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = fontFamily.Contains("Semibold", StringComparison.OrdinalIgnoreCase) ? UiFonts.Semibold(fontSize) : UiFonts.Regular(fontSize),
            ForeColor = active ? Theme.Accent : Theme.MutedText,
            BackColor = active ? Theme.SurfaceAlt : Color.Transparent
        };
    }

    private static void PaintDarkButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button || button.Enabled)
        {
            return;
        }

        using var background = new SolidBrush(Theme.DisabledButton);
            using var border = new Pen(Theme.SubtleBorder);
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

    private void ConfigureThemeBox()
    {
        _themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _themeBox.Items.Clear();
        _themeBox.Items.Add("Green Black");
        _themeBox.Items.Add("Blue Black");
        _themeBox.Items.Add("Purple");
        _themeBox.SelectedIndex = 0;
        _themeBox.Dock = DockStyle.Fill;
        _themeBox.Margin = new Padding(0, 5, 0, 5);
        _themeBox.Font = new Font("Segoe UI", 8.5f);
        _themeBox.BackColor = Theme.Surface;
        _themeBox.ForeColor = Theme.Text;
        _themeBox.FlatStyle = FlatStyle.Flat;
        _themeBox.SelectedIndexChanged += (_, _) =>
        {
            switch (_themeBox.SelectedIndex)
            {
                case 0:
                    Theme.UseGreenBlack();
                    break;
                case 1:
                    Theme.UseBlueBlack();
                    break;
                case 2:
                    Theme.UsePurple();
                    break;
                default:
                    Theme.UseGreenBlack();
                    break;
            }

            ApplyThemeToControls();
            foreach (Form form in Application.OpenForms)
            {
                ApplyThemeRecursive(form);
                form.BackColor = Theme.Background;
                form.ForeColor = Theme.Text;
                form.Invalidate(true);
            }
        };
    }

    private void ApplyThemeToControls()
    {
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        _titleBar.BackColor = Theme.Header;
        _titleText.BackColor = Theme.Header;
        _titleText.ForeColor = Theme.Text;
        _minimizeWindowButton.BackColor = Theme.Header;
        _minimizeWindowButton.ForeColor = Theme.Text;
        _minimizeWindowButton.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
        _minimizeWindowButton.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
        _maximizeWindowButton.BackColor = Theme.Header;
        _maximizeWindowButton.ForeColor = Theme.Text;
        _maximizeWindowButton.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
        _maximizeWindowButton.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
        _closeWindowButton.BackColor = Theme.Header;
        _closeWindowButton.ForeColor = Theme.Text;
        _closeWindowButton.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
        _closeWindowButton.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
        ApplyThemeRecursive(this);

        _urlBox.BackColor = Theme.Surface;
        _urlBox.ForeColor = Theme.Text;
        _themeBox.BackColor = Theme.Surface;
        _themeBox.ForeColor = Theme.Text;
        _logBox.BackColor = Theme.LogSurface;
        _logBox.ForeColor = Theme.Text;
        _artifactGrid.BackgroundColor = Theme.Surface;
        _artifactGrid.GridColor = Theme.SubtleBorder;
        _artifactGrid.DefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.DefaultCellStyle.ForeColor = Theme.Text;
        _artifactGrid.DefaultCellStyle.SelectionBackColor = Theme.ButtonPressed;
        _artifactGrid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        _artifactGrid.RowsDefaultCellStyle.BackColor = Theme.Surface;
        _artifactGrid.AlternatingRowsDefaultCellStyle.BackColor = Theme.SurfaceAlt;
        _explanationBox.BackColor = Theme.Surface;
        _explanationBox.ForeColor = Theme.Text;

        foreach (var button in new[] { _buildButton, _cancelButton, _browseButton, _foldersButton, _aboutButton })
        {
            button.FlatAppearance.BorderColor = Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
            ApplyButtonTheme(button);
        }

        Invalidate(true);
    }

    private void PulseActiveSurfaces()
    {
        _pulseStep = (_pulseStep + 1) % 80;
        var wave = Math.Abs(40 - _pulseStep);
        var intensity = 18 + (40 - wave);
        var pulse = Blend(Theme.Header, Theme.Accent, intensity / 255f);
        _titleBar.BackColor = pulse;
        _titleText.BackColor = pulse;
    }

    private static Color Blend(Color baseColor, Color accent, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            baseColor.R + (int)((accent.R - baseColor.R) * amount),
            baseColor.G + (int)((accent.G - baseColor.G) * amount),
            baseColor.B + (int)((accent.B - baseColor.B) * amount));
    }

    private void BuildSettingsTab()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16),
            BackColor = Theme.Surface
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateSettingsRow("Theme", _themeBox), 0, 0);
        panel.Controls.Add(CreateCheckbox("Ask before installing dependencies", true), 0, 1);
        panel.Controls.Add(CreateCheckbox("Open artifacts after successful build", false), 0, 2);
        panel.Controls.Add(CreateCheckbox("Keep full command output in logs", true), 0, 3);
        panel.Controls.Add(CreateCheckbox("Prefer release builds", true), 0, 4);
        panel.Controls.Add(CreateInfoText("These settings shape how Git-Build runs local builds. Build-system detection and artifact discovery still happen automatically."), 0, 5);

        _settingsTab.Controls.Add(panel);
    }

    private void BuildToolsTab()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            BackColor = Theme.Surface
        };

        panel.Controls.Add(CreateToolButton("Open settings folder", () => OpenFolder(_paths.SettingsDirectory)));
        panel.Controls.Add(CreateToolButton("Open project folder", () => OpenFolder(_latestRepositoryPath)));
        panel.Controls.Add(CreateToolButton("Clear build log", () => _logBox.Clear()));
        panel.Controls.Add(CreateToolButton("Clear artifacts list", () => _artifactGrid.Rows.Clear()));
        panel.Controls.Add(CreateInfoText("Tools are shortcuts for common Git-Build maintenance actions."));

        _toolsTab.Controls.Add(panel);
    }

    private void BuildAdvancedTab()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16),
            BackColor = Theme.Surface
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateSettingsRow("Dependency mode", CreateOptionBox("Ask first", "Auto install", "Never install")), 0, 0);
        panel.Controls.Add(CreateSettingsRow("Artifact scan", CreateOptionBox("Smart", "Deep", "Fast")), 0, 1);
        panel.Controls.Add(CreateSettingsRow("Log detail", CreateOptionBox("Normal", "Verbose", "Errors only")), 0, 2);
        panel.Controls.Add(CreateSettingsRow("Build target", CreateOptionBox("Auto", "Release", "Debug")), 0, 3);
        panel.Controls.Add(CreateSettingsRow("After build", CreateOptionBox("Show artifacts", "Open folder", "Do nothing")), 0, 4);
        panel.Controls.Add(CreateInfoText("Advanced options are prepared for future build behavior controls. The current build engine still keeps its automatic safe defaults."), 0, 5);

        _advancedTab.Controls.Add(panel);
    }

    private static Control CreateSettingsRow(string labelText, Control editor)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Theme.Surface
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        row.Controls.Add(new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = UiFonts.Semibold(9f)
        }, 0, 0);
        row.Controls.Add(editor, 1, 0);
        return row;
    }

    private static CheckBox CreateCheckbox(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            BackColor = Theme.Surface,
            Font = UiFonts.Regular(9f)
        };
    }

    private static ComboBox CreateOptionBox(params string[] options)
    {
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 0, 5),
            Font = UiFonts.Regular(8.5f),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            FlatStyle = FlatStyle.Flat
        };
        box.Items.AddRange(options);
        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }

        return box;
    }

    private static Label CreateInfoText(string text)
    {
        return new Label
        {
            Text = text,
            Width = 360,
            Height = 72,
            ForeColor = Theme.MutedText,
            BackColor = Theme.Surface,
            Font = UiFonts.Regular(9f)
        };
    }

    private Button CreateToolButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 260,
            Height = 34,
            Margin = new Padding(0, 0, 0, 10)
        };
        ConfigureButton(button, text);
        button.Dock = DockStyle.None;
        button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        button.Width = 260;
        button.Height = 34;
        button.Click += (_, _) => action();
        return button;
    }

    private void ShowSettingsDialog()
    {
        using var dialog = new Form
        {
            Text = "Git-Build Settings",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(860, 660),
            MinimumSize = new Size(740, 560),
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = UiFonts.Regular(9.5f),
            FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };
        dialog.HandleCreated += (_, _) => ApplyModernWindowEffects(dialog.Handle);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = Theme.Background
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 9,
            ColumnCount = 1,
            BackColor = Theme.LogSurface,
            Padding = new Padding(18)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        for (var i = 0; i < 7; i++)
        {
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        }
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(new Label
        {
            Text = "Settings",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(15f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        left.Controls.Add(CreateSettingsNavLabel("General", true), 0, 1);
        left.Controls.Add(CreateSettingsNavLabel("Appearance", false), 0, 2);
        left.Controls.Add(CreateSettingsNavLabel("Build Tools", false), 0, 3);
        left.Controls.Add(CreateSettingsNavLabel("Storage", false), 0, 4);
        left.Controls.Add(CreateSettingsNavLabel("Default Options", false), 0, 5);
        left.Controls.Add(CreateSettingsNavLabel("Privacy", false), 0, 6);
        left.Controls.Add(CreateSettingsNavLabel("About", false), 0, 7);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Background,
            Padding = new Padding(24, 14, 0, 0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        right.Controls.Add(new Label
        {
            Text = "General\r\nApplication settings and build preferences",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(15f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        right.Controls.Add(CreateDialogThemeRow(), 0, 1);

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Theme.Surface,
            Padding = new Padding(18)
        };
        for (var i = 0; i < 6; i++)
        {
            options.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        }
        options.Controls.Add(CreateOptionSwitchRow("Ask before installing dependencies", "Show confirmation before Git-Build installs tools.", true), 0, 0);
        options.Controls.Add(CreateOptionSwitchRow("Prefer release builds", "Run release/package commands when available.", true), 0, 1);
        options.Controls.Add(CreateOptionSwitchRow("Keep full command output", "Store complete logs for debugging failed builds.", true), 0, 2);
        options.Controls.Add(CreateOptionSwitchRow("Open artifacts after build", "Open the output folder after a successful build.", false), 0, 3);
        options.Controls.Add(CreateOptionSwitchRow("Deep artifact scan", "Search more folders for generated binaries.", false), 0, 4);
        options.Controls.Add(CreateOptionSwitchRow("Portable mode", "Keep Git-Build data next to the app when possible.", false), 0, 5);
        right.Controls.Add(options, 0, 2);

        var done = new Button { Text = "Done", Width = 130, Height = 40, Anchor = AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 12, 0, 0) };
        ConfigureButton(done, "Done");
        done.Text = "Done";
        done.Width = 130;
        done.Height = 40;
        done.Margin = new Padding(0, 10, 0, 0);
        done.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        done.Click += (_, _) => dialog.Close();
        right.Controls.Add(done, 0, 3);

        left.Visible = false;
        shell.Controls.Add(left, 0, 0);
        shell.Controls.Add(right, 1, 0);
        dialog.Controls.Add(shell);
        dialog.ShowDialog(this);
    }

    private static Label CreateSettingsNavLabel(string text, bool active)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 0, 0, 0),
            Font = UiFonts.Semibold(10.5f),
            ForeColor = active ? Theme.Accent : Theme.Text,
            BackColor = active ? Theme.SurfaceAlt : Theme.LogSurface,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Control CreateDialogThemeRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Theme.Surface,
            Padding = new Padding(18)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        row.Controls.Add(new Label
        {
            Text = "Theme\r\nChoose the Git-Build color theme",
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(10.5f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var themeSelector = CreateOptionBox("Green Black", "Blue Black", "Purple");
        themeSelector.SelectedIndex = _themeBox.SelectedIndex < 0 ? 0 : _themeBox.SelectedIndex;
        themeSelector.SelectedIndexChanged += (_, _) =>
        {
            _themeBox.SelectedIndex = themeSelector.SelectedIndex;
        };
        row.Controls.Add(themeSelector, 1, 0);
        return row;
    }

    private static Control CreateOptionSwitchRow(string title, string description, bool enabled)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Theme.Surface
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        row.Controls.Add(new Label
        {
            Text = title + "\r\n" + description,
            Dock = DockStyle.Fill,
            Font = UiFonts.Semibold(9.5f),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        row.Controls.Add(new CheckBox
        {
            Checked = enabled,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Theme.Surface,
            ForeColor = Theme.Accent
        }, 1, 0);
        return row;
    }

    private static void ApplyThemeRecursive(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is Button)
            {
                child.BackColor = Theme.Button;
                child.ForeColor = Theme.Text;
            }

            if (child is Label label)
            {
                label.ForeColor = label.Text is "GB" or "Build" or "Git-Build" ? Theme.Accent : Theme.MutedText;
                if (label.Text == "Build")
                {
                    label.BackColor = Theme.SurfaceAlt;
                }
            }

            if (child is CheckBox checkBox)
            {
                checkBox.ForeColor = Theme.Text;
                checkBox.BackColor = Theme.Surface;
            }

            if (child is ComboBox comboBox)
            {
                comboBox.ForeColor = Theme.Text;
                comboBox.BackColor = Theme.Surface;
            }

            if (child is TextBox textBox)
            {
                textBox.ForeColor = Theme.Text;
                textBox.BackColor = Theme.Surface;
            }

            if (child is RichTextBox richTextBox)
            {
                richTextBox.ForeColor = Theme.Text;
                richTextBox.BackColor = Theme.LogSurface;
            }

            if (child is DataGridView grid)
            {
                grid.BackgroundColor = Theme.Surface;
                grid.GridColor = Theme.SubtleBorder;
                grid.DefaultCellStyle.BackColor = Theme.Surface;
                grid.DefaultCellStyle.ForeColor = Theme.Text;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
            }

            if (child is TabControl tabControl)
            {
                tabControl.BackColor = Theme.Surface;
                tabControl.ForeColor = Theme.Text;
            }

            if (child is TabPage tabPage)
            {
                tabPage.BackColor = Theme.Surface;
                tabPage.ForeColor = Theme.Text;
            }

            if (child is TableLayoutPanel or Panel or FlowLayoutPanel)
            {
                child.BackColor = child.Height <= 120 && child.Top <= 140 ? Theme.Header : Theme.Background;
                if (child.Dock == DockStyle.Left)
                {
                    child.BackColor = Theme.Surface;
                }
            }

            ApplyThemeRecursive(child);
        }
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Height = 34;
            button.Margin = new Padding(8, 5, 0, 5);
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = Theme.Button;
            button.ForeColor = Theme.Text;
            button.Font = new Font("Segoe UI Semibold", 8.25f);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Theme.Border;
        button.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
        button.FlatAppearance.MouseDownBackColor = Theme.ButtonPressed;
        button.MouseEnter += (_, _) =>
        {
            if (button.Enabled)
            {
                button.Padding = new Padding(0, 0, 0, 1);
            }
        };
        button.MouseLeave += (_, _) => button.Padding = Padding.Empty;
        button.MouseDown += (_, _) =>
        {
            if (button.Enabled)
            {
                button.Padding = new Padding(0, 1, 0, 0);
            }
        };
        button.MouseUp += (_, _) => button.Padding = Padding.Empty;
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

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        public enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        public enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
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
        _pulseTimer.Start();
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
            _pulseTimer.Stop();
            _titleBar.BackColor = Theme.Header;
            _titleText.BackColor = Theme.Header;
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
            _explanationBox.Text = "Build completed." + Environment.NewLine + Environment.NewLine +
                "No runnable artifact was found yet. Some projects only produce files after a package, publish, release, or installer command.";
        }
        else
        {
            _explanationBox.Text = $"Found {artifacts.Count} artifact(s)." + Environment.NewLine + Environment.NewLine +
                "Double-click an artifact to open it or reveal it in File Explorer.";
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

        _statusLabel.ForeColor = status switch
        {
            BuildStatus.Succeeded => Theme.Success,
            BuildStatus.Failed => Theme.ErrorText,
            BuildStatus.Cancelled => Theme.MutedText,
            BuildStatus.Building => Theme.Accent,
            BuildStatus.InstallingDependencies => Theme.Accent,
            BuildStatus.Detecting => Theme.Accent,
            _ => Theme.Text
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


