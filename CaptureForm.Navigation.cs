namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
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
            Height = 200,
            Padding = new Padding(0, 18, 0, 0),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };

        ConfigureNavButton(captureNavButton, "Capture", true, () => ShowTool(ToolKind.Capture));
        ConfigureNavButton(systemInfoNavButton, "System Info", false, () => ShowTool(ToolKind.SystemInfo));
        ConfigureNavButton(metersNavButton, "Meters", false, () => ShowTool(ToolKind.Meters));
        ConfigureNavButton(inputAnalysisNavButton, "Analysis", false, () => ShowTool(ToolKind.InputAnalysis));

        navItems.Controls.Add(captureNavButton);
        navItems.Controls.Add(systemInfoNavButton);
        navItems.Controls.Add(metersNavButton);
        navItems.Controls.Add(inputAnalysisNavButton);

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

    private void ShowTool(ToolKind tool)
    {
        currentTool = tool;

        var showCapture = tool == ToolKind.Capture;
        var showSystemInfo = tool == ToolKind.SystemInfo;
        var showMeters = tool == ToolKind.Meters;
        var showInputAnalysis = tool == ToolKind.InputAnalysis;

        captureWorkspace.Visible = showCapture;
        systemInfoWorkspace.Visible = showSystemInfo;
        metersWorkspace.Visible = showMeters;
        inputAnalysisWorkspace.Visible = showInputAnalysis;

        SetNavSelected(captureNavButton, showCapture);
        SetNavSelected(systemInfoNavButton, showSystemInfo);
        SetNavSelected(metersNavButton, showMeters);
        SetNavSelected(inputAnalysisNavButton, showInputAnalysis);

        metersTimer.Enabled = showMeters || showNetworkSpeedInTaskbarCheckBox.Checked;
        SetInputAnalysisEnabled(showInputAnalysis);

        if (showCapture)
        {
            statusLabel.Text = "Capture workspace is active.";
        }
        else if (showSystemInfo)
        {
            RefreshSystemInfo();
        }
        else if (showMeters)
        {
            EnsureMetersStarted();
        }
        else
        {
            statusLabel.Text = "Input analysis is active.";
        }
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

    private static void SetNavSelected(Button button, bool selected)
    {
        button.BackColor = selected ? Color.FromArgb(51, 65, 85) : Color.FromArgb(25, 31, 43);
        button.ForeColor = selected ? Color.White : Color.FromArgb(207, 216, 230);
    }
}
