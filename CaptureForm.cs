using System.Diagnostics;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm : Form
{
    private const string DefaultWindowTitle = "cKit Screen Capture";

    private readonly Panel mainPanel = new();
    private readonly Panel captureWorkspace = new();
    private readonly Panel systemInfoWorkspace = new();
    private readonly Panel metersWorkspace = new();
    private readonly Panel netWorkspace = new();
    private readonly Panel clipboardWorkspace = new();
    private readonly Panel inputAnalysisWorkspace = new();
    private readonly Button captureNavButton = new();
    private readonly Button systemInfoNavButton = new();
    private readonly Button metersNavButton = new();
    private readonly Button netNavButton = new();
    private readonly Button clipboardNavButton = new();
    private readonly Button inputAnalysisNavButton = new();
    private readonly ComboBox screenSelector = new();
    private readonly Button captureButton = new();
    private readonly Button refreshScreensButton = new();
    private readonly Button saveButton = new();
    private readonly Button copyButton = new();
    private readonly Button openFolderButton = new();
    private readonly Button refreshSystemInfoButton = new();
    private readonly Button refreshMetersButton = new();
    private readonly Button refreshNetButton = new();
    private readonly Button refreshClipboardButton = new();
    private readonly Button openClipboardDbFolderButton = new();
    private readonly Button resetInputAnalysisButton = new();
    private readonly Button openAnalysisDbFolderButton = new();
    private readonly Label inputAnalysisStatusValue = new();
    private readonly CheckBox showNetworkSpeedInTaskbarCheckBox = new();
    private readonly Form networkSpeedTaskbarWindow = new();
    private readonly Label networkSpeedTaskbarWindowLabel = new();
    private readonly NotifyIcon appTrayIcon = new();
    private readonly ContextMenuStrip appTrayMenu = new();
    private readonly PictureBox preview = new();
    private readonly TextBox systemInfoText = new();
    private readonly TextBox netText = new();
    private readonly ListView clipboardHistoryList = new();
    private readonly TabControl clipboardPreviewTabs = new();
    private readonly TabPage clipboardDetailsPreviewTab = new("Details");
    private readonly TabPage clipboardHtmlPreviewTab = new("HTML");
    private readonly TabPage clipboardImagePreviewTab = new("Image");
    private readonly TextBox clipboardPreviewText = new();
    private readonly WebBrowser clipboardHtmlPreviewBrowser = new();
    private readonly PictureBox clipboardImagePreview = new();
    private readonly Label clipboardStatusValue = new();
    private readonly Label captureDetailsLabel = new();
    private readonly Label captureEmptyStateLabel = new();
    private readonly Label cpuMeterValue = new();
    private readonly Label gpuMeterValue = new();
    private readonly Label downloadMeterValue = new();
    private readonly Label uploadMeterValue = new();
    private readonly ProgressBar cpuMeterBar = new();
    private readonly ProgressBar gpuMeterBar = new();
    private readonly ProgressBar downloadMeterBar = new();
    private readonly ProgressBar uploadMeterBar = new();
    private readonly Label keyPressCountValue = new();
    private readonly Label mouseClickCountValue = new();
    private readonly Label lastInputValue = new();
    private readonly Label leftMouseClickCountValue = new();
    private readonly Label rightMouseClickCountValue = new();
    private readonly Label statusLabel = new();
    private readonly System.Windows.Forms.Timer metersTimer = new();
    private PerformanceCounter? cpuCounter;
    private List<PerformanceCounter> gpuCounters = [];
    private List<System.Net.NetworkInformation.NetworkInterface> activeNetworkInterfaces = [];
    private long previousBytesReceived;
    private long previousBytesSent;
    private double observedDownloadPeakBytesPerSecond = 1024 * 1024;
    private double observedUploadPeakBytesPerSecond = 1024 * 1024;
    private DateTime previousNetworkSampleAt;
    private DateTime networkInterfacesRefreshedAt;
    private string? lastClipboardSignature;
    private bool clipboardListenerRegistered;
    private Bitmap? currentCapture;
    private Icon? appTrayIconImage;
    private ToolKind currentTool = ToolKind.Capture;
    private bool metersInitialized;
    private bool metersInitializing;
    private bool metersSampleInProgress;
    private bool exitRequested;
    private int keyPressCount;
    private int mouseClickCount;
    private int leftMouseClickCount;
    private int rightMouseClickCount;
    private readonly Dictionary<Keys, int> keyCounts = [];
    private readonly Dictionary<Keys, Label> keyCountLabels = [];
    private IntPtr keyboardHookHandle;
    private IntPtr mouseHookHandle;
    private LowLevelHookProc? keyboardHookProc;
    private LowLevelHookProc? mouseHookProc;

    public CaptureForm()
    {
        Text = DefaultWindowTitle;
        MinimumSize = new Size(980, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = FluentAppBackground;
        Font = FluentFont(9);

        var nav = CreateNavigation();
        mainPanel.Dock = DockStyle.Fill;
        mainPanel.BackColor = FluentAppBackground;
        mainPanel.Padding = new Padding(0);

        BuildCaptureWorkspace();
        BuildSystemInfoWorkspace();
        BuildMetersWorkspace();
        BuildNetWorkspace();
        BuildClipboardWorkspace();
        InitializeInputAnalysisDatabase();
        BuildInputAnalysisWorkspace();
        InitializeAppTrayIcon();
        InitializeClipboardDatabase();

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 32;
        statusLabel.Padding = new Padding(12, 7, 12, 0);
        statusLabel.BackColor = FluentSurface;
        statusLabel.ForeColor = FluentTextSecondary;
        statusLabel.Font = FluentFont(9);
        statusLabel.Text = "Ready. Choose a screen target, then click Capture.";

        mainPanel.Controls.Add(captureWorkspace);
        mainPanel.Controls.Add(systemInfoWorkspace);
        mainPanel.Controls.Add(metersWorkspace);
        mainPanel.Controls.Add(netWorkspace);
        mainPanel.Controls.Add(clipboardWorkspace);
        mainPanel.Controls.Add(inputAnalysisWorkspace);
        mainPanel.Controls.Add(statusLabel);

        Controls.Add(mainPanel);
        Controls.Add(nav);

        LoadScreens();
        ShowTool(ToolKind.Capture);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        metersTimer.Stop();
        StopInputAnalysis();
        cpuCounter?.Dispose();
        foreach (var counter in gpuCounters)
        {
            counter.Dispose();
        }

        currentCapture?.Dispose();
        clipboardImagePreview.Image?.Dispose();
        networkSpeedTaskbarWindow.Close();
        networkSpeedTaskbarWindow.Dispose();
        appTrayIcon.Visible = false;
        appTrayIcon.Dispose();
        appTrayMenu.Dispose();
        appTrayIconImage?.Dispose();
        base.OnFormClosed(e);
    }

    private void InitializeAppTrayIcon()
    {
        appTrayMenu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        appTrayMenu.Items.Add("Exit", null, (_, _) => ExitFromTray());

        appTrayIcon.Text = DefaultWindowTitle;
        appTrayIconImage = CreateAppTrayIcon();
        appTrayIcon.Icon = appTrayIconImage;
        appTrayIcon.ContextMenuStrip = appTrayMenu;
        appTrayIcon.Visible = true;
        appTrayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                RestoreFromTray();
            }
        };
    }

    private void HideToTray()
    {
        appTrayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
        appTrayIcon.Visible = true;
    }

    private void ExitFromTray()
    {
        exitRequested = true;
        appTrayIcon.Visible = false;
        Close();
    }

    private static Icon CreateAppTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var backgroundBrush = new SolidBrush(Color.FromArgb(25, 31, 43));
        using var accentBrush = new SolidBrush(Color.FromArgb(59, 130, 246));
        using var font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 13, FontStyle.Bold, GraphicsUnit.Pixel);

        graphics.FillRoundedRectangle(backgroundBrush, new Rectangle(2, 2, 28, 28), new Size(6, 6));
        graphics.FillEllipse(accentBrush, 21, 5, 6, 6);
        TextRenderer.DrawText(graphics, "cK", font, new Rectangle(3, 8, 26, 18), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
