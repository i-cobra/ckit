using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CKitScreenCapture;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CaptureForm());
    }
}

internal sealed class CaptureForm : Form
{
    private readonly Panel mainPanel = new();
    private readonly Panel captureWorkspace = new();
    private readonly Panel systemInfoWorkspace = new();
    private readonly Button captureNavButton = new();
    private readonly Button systemInfoNavButton = new();
    private readonly ComboBox screenSelector = new();
    private readonly Button captureButton = new();
    private readonly Button refreshScreensButton = new();
    private readonly Button saveButton = new();
    private readonly Button copyButton = new();
    private readonly Button openFolderButton = new();
    private readonly Button refreshSystemInfoButton = new();
    private readonly PictureBox preview = new();
    private readonly TextBox systemInfoText = new();
    private readonly Label statusLabel = new();
    private Bitmap? currentCapture;

    public CaptureForm()
    {
        Text = "cKit Screen Capture";
        MinimumSize = new Size(980, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 247, 250);

        var nav = CreateNavigation();
        mainPanel.Dock = DockStyle.Fill;
        mainPanel.BackColor = Color.FromArgb(246, 247, 250);
        mainPanel.Padding = new Padding(0);

        BuildCaptureWorkspace();
        BuildSystemInfoWorkspace();

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 32;
        statusLabel.Padding = new Padding(12, 7, 12, 0);
        statusLabel.BackColor = Color.White;
        statusLabel.ForeColor = Color.FromArgb(65, 70, 82);
        statusLabel.Text = "Ready. Choose a screen target, then click Capture.";

        mainPanel.Controls.Add(captureWorkspace);
        mainPanel.Controls.Add(systemInfoWorkspace);
        mainPanel.Controls.Add(statusLabel);

        Controls.Add(mainPanel);
        Controls.Add(nav);

        LoadScreens();
        ShowTool(ToolKind.Capture);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        currentCapture?.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildCaptureWorkspace()
    {
        captureWorkspace.Dock = DockStyle.Fill;
        captureWorkspace.BackColor = Color.FromArgb(246, 247, 250);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            WrapContents = false,
        };

        ConfigureScreenSelector();
        ConfigureButton(captureButton, "Capture");
        ConfigureButton(refreshScreensButton, "Refresh Screens");
        ConfigureButton(saveButton, "Save PNG");
        ConfigureButton(copyButton, "Copy");
        ConfigureButton(openFolderButton, "Open Folder");

        saveButton.Enabled = false;
        copyButton.Enabled = false;

        captureButton.Click += (_, _) => CaptureScreen();
        refreshScreensButton.Click += (_, _) => LoadScreens();
        saveButton.Click += (_, _) => SaveCapture();
        copyButton.Click += (_, _) => CopyCapture();
        openFolderButton.Click += (_, _) => OpenCaptureFolder();

        toolbar.Controls.Add(screenSelector);
        toolbar.Controls.Add(captureButton);
        toolbar.Controls.Add(refreshScreensButton);
        toolbar.Controls.Add(saveButton);
        toolbar.Controls.Add(copyButton);
        toolbar.Controls.Add(openFolderButton);

        preview.Dock = DockStyle.Fill;
        preview.BackColor = Color.FromArgb(31, 35, 43);
        preview.SizeMode = PictureBoxSizeMode.Zoom;
        preview.BorderStyle = BorderStyle.FixedSingle;

        captureWorkspace.Controls.Add(preview);
        captureWorkspace.Controls.Add(toolbar);
    }

    private void BuildSystemInfoWorkspace()
    {
        systemInfoWorkspace.Dock = DockStyle.Fill;
        systemInfoWorkspace.BackColor = Color.FromArgb(246, 247, 250);
        systemInfoWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            WrapContents = false,
        };

        ConfigureButton(refreshSystemInfoButton, "Refresh");
        refreshSystemInfoButton.Click += (_, _) => RefreshSystemInfo();
        toolbar.Controls.Add(refreshSystemInfoButton);

        systemInfoText.Dock = DockStyle.Fill;
        systemInfoText.ReadOnly = true;
        systemInfoText.Multiline = true;
        systemInfoText.ScrollBars = ScrollBars.Vertical;
        systemInfoText.BorderStyle = BorderStyle.FixedSingle;
        systemInfoText.BackColor = Color.White;
        systemInfoText.ForeColor = Color.FromArgb(31, 35, 43);
        systemInfoText.Font = new Font("Consolas", 10);
        systemInfoText.Margin = new Padding(0);

        systemInfoWorkspace.Controls.Add(systemInfoText);
        systemInfoWorkspace.Controls.Add(toolbar);
    }

    private void ShowTool(ToolKind tool)
    {
        var showCapture = tool == ToolKind.Capture;

        captureWorkspace.Visible = showCapture;
        systemInfoWorkspace.Visible = !showCapture;
        captureNavButton.BackColor = showCapture ? Color.FromArgb(51, 65, 85) : Color.FromArgb(25, 31, 43);
        captureNavButton.ForeColor = showCapture ? Color.White : Color.FromArgb(207, 216, 230);
        systemInfoNavButton.BackColor = showCapture ? Color.FromArgb(25, 31, 43) : Color.FromArgb(51, 65, 85);
        systemInfoNavButton.ForeColor = showCapture ? Color.FromArgb(207, 216, 230) : Color.White;

        if (showCapture)
        {
            statusLabel.Text = "Capture workspace is active.";
        }
        else
        {
            RefreshSystemInfo();
        }
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Height = 34;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Padding = new Padding(12, 0, 12, 0);
        button.FlatStyle = FlatStyle.System;
        button.UseVisualStyleBackColor = true;
    }

    private Panel CreateNavigation()
    {
        var nav = new Panel
        {
            Dock = DockStyle.Left,
            Width = 190,
            BackColor = Color.FromArgb(25, 31, 43),
            Padding = new Padding(14, 16, 14, 16),
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "cKit",
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Screen Capture",
            Font = new Font(Font.FontFamily, 9),
            ForeColor = Color.FromArgb(174, 184, 201),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var navItems = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 156,
            Padding = new Padding(0, 18, 0, 0),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };

        ConfigureNavButton(captureNavButton, "Capture", true, () => ShowTool(ToolKind.Capture));
        ConfigureNavButton(systemInfoNavButton, "System Info", false, () => ShowTool(ToolKind.SystemInfo));

        navItems.Controls.Add(captureNavButton);
        navItems.Controls.Add(systemInfoNavButton);

        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Text = "Win64 desktop",
            Font = new Font(Font.FontFamily, 8),
            ForeColor = Color.FromArgb(134, 146, 166),
            TextAlign = ContentAlignment.BottomLeft,
        };

        nav.Controls.Add(footer);
        nav.Controls.Add(navItems);
        nav.Controls.Add(subtitle);
        nav.Controls.Add(title);

        return nav;
    }

    private static void ConfigureNavButton(Button button, string text, bool selected, Action clickAction)
    {
        button.Text = text;
        button.Width = 160;
        button.Height = 36;
        button.Margin = new Padding(0, 0, 0, 8);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(12, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = selected ? Color.FromArgb(51, 65, 85) : Color.FromArgb(25, 31, 43);
        button.ForeColor = selected ? Color.White : Color.FromArgb(207, 216, 230);

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(43, 53, 70);
        button.Click += (_, _) => clickAction();
    }

    private void ConfigureScreenSelector()
    {
        screenSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        screenSelector.Width = 280;
        screenSelector.Height = 34;
        screenSelector.Margin = new Padding(0, 0, 8, 0);
        screenSelector.IntegralHeight = false;
        screenSelector.MaxDropDownItems = 8;
    }

    private void LoadScreens()
    {
        var previousSelection = (screenSelector.SelectedItem as CaptureTarget)?.Id;

        screenSelector.BeginUpdate();
        screenSelector.Items.Clear();
        screenSelector.Items.Add(CaptureTarget.Merged());

        var screens = Screen.AllScreens;
        for (var i = 0; i < screens.Length; i++)
        {
            screenSelector.Items.Add(CaptureTarget.ForScreen(i, screens[i]));
        }

        screenSelector.EndUpdate();

        SelectTarget(previousSelection);
        statusLabel.Text = $"Found {screens.Length} screen{(screens.Length == 1 ? string.Empty : "s")}.";
    }

    private void SelectTarget(string? preferredId)
    {
        if (preferredId is not null)
        {
            for (var i = 0; i < screenSelector.Items.Count; i++)
            {
                if ((screenSelector.Items[i] as CaptureTarget)?.Id == preferredId)
                {
                    screenSelector.SelectedIndex = i;
                    return;
                }
            }
        }

        screenSelector.SelectedIndex = 0;
    }

    private void CaptureScreen()
    {
        if (screenSelector.SelectedItem is not CaptureTarget target)
        {
            LoadScreens();
            target = (CaptureTarget)screenSelector.SelectedItem!;
        }

        var bounds = target.Bounds;
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        var oldCapture = currentCapture;
        currentCapture = bitmap;
        preview.Image = currentCapture;
        oldCapture?.Dispose();

        saveButton.Enabled = true;
        copyButton.Enabled = true;
        statusLabel.Text = $"Captured {target.Name} ({bounds.Width} x {bounds.Height}) at {DateTime.Now:T}.";
    }

    private void SaveCapture()
    {
        if (currentCapture is null)
        {
            return;
        }

        Directory.CreateDirectory(DefaultCaptureFolder);
        var fileName = $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        var path = Path.Combine(DefaultCaptureFolder, fileName);

        currentCapture.Save(path, ImageFormat.Png);
        statusLabel.Text = $"Saved {path}";
    }

    private void CopyCapture()
    {
        if (currentCapture is null)
        {
            return;
        }

        Clipboard.SetImage(currentCapture);
        statusLabel.Text = "Copied capture to clipboard.";
    }

    private void RefreshSystemInfo()
    {
        var screens = Screen.AllScreens;
        var screenLines = screens
            .Select((screen, index) =>
            {
                var bounds = screen.Bounds;
                var primary = screen.Primary ? "primary, " : string.Empty;

                return $"  Screen {index + 1}: {primary}{bounds.Width} x {bounds.Height}, position {bounds.Left},{bounds.Top}";
            });

        systemInfoText.Text = string.Join(Environment.NewLine, new[]
        {
            "System Info",
            "",
            $"Machine name:        {Environment.MachineName}",
            $"User name:           {Environment.UserName}",
            $"OS version:          {Environment.OSVersion}",
            $"OS description:      {RuntimeInformation.OSDescription}",
            $"Architecture:        {RuntimeInformation.OSArchitecture}",
            $"Process arch:        {RuntimeInformation.ProcessArchitecture}",
            $"64-bit OS:           {Environment.Is64BitOperatingSystem}",
            $"64-bit process:      {Environment.Is64BitProcess}",
            $"Processor count:     {Environment.ProcessorCount}",
            $"Working set:         {FormatBytes(Environment.WorkingSet)}",
            $".NET runtime:        {RuntimeInformation.FrameworkDescription}",
            $"App base directory:  {AppContext.BaseDirectory}",
            $"Current directory:   {Environment.CurrentDirectory}",
            "",
            "Displays",
            $"  Total screens:      {screens.Length}",
            $"  Virtual desktop:    {SystemInformation.VirtualScreen.Width} x {SystemInformation.VirtualScreen.Height}",
        }.Concat(screenLines));

        statusLabel.Text = $"System info refreshed at {DateTime.Now:T}.";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static void OpenCaptureFolder()
    {
        Directory.CreateDirectory(DefaultCaptureFolder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DefaultCaptureFolder,
            UseShellExecute = true,
        });
    }

    private static string DefaultCaptureFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "cKitCaptures");

    private enum ToolKind
    {
        Capture,
        SystemInfo,
    }

    private sealed record CaptureTarget(string Id, string Name, Rectangle Bounds)
    {
        public static CaptureTarget Merged() =>
            new("merged", "Merged screens", SystemInformation.VirtualScreen);

        public static CaptureTarget ForScreen(int index, Screen screen)
        {
            var bounds = screen.Bounds;
            var primary = screen.Primary ? " primary" : string.Empty;
            var name = $"Screen {index + 1}{primary} ({bounds.Width} x {bounds.Height})";

            return new($"screen-{index}", name, bounds);
        }

        public override string ToString() => Name;
    }
}
