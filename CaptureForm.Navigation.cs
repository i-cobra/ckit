namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private Panel CreateNavigation()
    {
        var nav = new Panel
        {
            Dock = DockStyle.Left,
            Width = 190,
            BackColor = FluentNavBackground,
            Padding = new Padding(14, 16, 14, 16),
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "cKit",
            Font = FluentFont(18, FontStyle.Bold),
            ForeColor = FluentText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var subtitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Screen Capture",
            Font = FluentFont(9),
            ForeColor = FluentTextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var navItems = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 288,
            Padding = new Padding(0, 18, 0, 0),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };

        ConfigureNavButton(captureNavButton, "Capture", "\uE722", true, () => ShowTool(ToolKind.Capture));
        ConfigureNavButton(systemInfoNavButton, "System Info", "\uE946", false, () => ShowTool(ToolKind.SystemInfo));
        ConfigureNavButton(metersNavButton, "Meters", "\uE9D9", false, () => ShowTool(ToolKind.Meters));
        ConfigureNavButton(netNavButton, "Net", "\uE839", false, () => ShowTool(ToolKind.Net));
        ConfigureNavButton(clipboardNavButton, "Clipboard", "\uE8D5", false, () => ShowTool(ToolKind.Clipboard));
        ConfigureNavButton(inputAnalysisNavButton, "Analysis", "\uE9D2", false, () => ShowTool(ToolKind.InputAnalysis));

        navItems.Controls.Add(captureNavButton);
        navItems.Controls.Add(systemInfoNavButton);
        navItems.Controls.Add(metersNavButton);
        navItems.Controls.Add(netNavButton);
        navItems.Controls.Add(clipboardNavButton);
        navItems.Controls.Add(inputAnalysisNavButton);

        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Text = "Win64 desktop",
            Font = FluentFont(8),
            ForeColor = FluentTextSecondary,
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
        var showNet = tool == ToolKind.Net;
        var showClipboard = tool == ToolKind.Clipboard;
        var showInputAnalysis = tool == ToolKind.InputAnalysis;

        captureWorkspace.Visible = showCapture;
        systemInfoWorkspace.Visible = showSystemInfo;
        metersWorkspace.Visible = showMeters;
        netWorkspace.Visible = showNet;
        clipboardWorkspace.Visible = showClipboard;
        inputAnalysisWorkspace.Visible = showInputAnalysis;

        SetNavSelected(captureNavButton, showCapture);
        SetNavSelected(systemInfoNavButton, showSystemInfo);
        SetNavSelected(metersNavButton, showMeters);
        SetNavSelected(netNavButton, showNet);
        SetNavSelected(clipboardNavButton, showClipboard);
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
            _ = EnsureMetersStartedAsync();
        }
        else if (showNet)
        {
            RefreshNetInfo();
        }
        else if (showClipboard)
        {
            RefreshClipboardHistory();
            statusLabel.Text = "Clipboard history is active.";
        }
        else
        {
            statusLabel.Text = "Input analysis is active.";
        }
    }

    private static void ConfigureNavButton(Button button, string text, string iconGlyph, bool selected, Action clickAction)
    {
        button.Text = text;
        button.Width = 160;
        button.Height = 36;
        button.Margin = new Padding(0, 0, 0, 8);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(12, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = selected ? FluentNavSelected : FluentNavBackground;
        button.ForeColor = FluentText;
        button.Font = FluentFont(9);
        button.Image = CreateFluentIcon(iconGlyph, FluentText);
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = FluentCardHover;
        button.Click += (_, _) => clickAction();
    }

    private static void SetNavSelected(Button button, bool selected)
    {
        button.BackColor = selected ? FluentNavSelected : FluentNavBackground;
        button.ForeColor = FluentText;
    }
}
