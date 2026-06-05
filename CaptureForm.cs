using System.Diagnostics;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm : Form
{
    private readonly Panel mainPanel = new();
    private readonly Panel captureWorkspace = new();
    private readonly Panel systemInfoWorkspace = new();
    private readonly Panel metersWorkspace = new();
    private readonly Button captureNavButton = new();
    private readonly Button systemInfoNavButton = new();
    private readonly Button metersNavButton = new();
    private readonly ComboBox screenSelector = new();
    private readonly Button captureButton = new();
    private readonly Button refreshScreensButton = new();
    private readonly Button saveButton = new();
    private readonly Button copyButton = new();
    private readonly Button openFolderButton = new();
    private readonly Button refreshSystemInfoButton = new();
    private readonly Button refreshMetersButton = new();
    private readonly PictureBox preview = new();
    private readonly TextBox systemInfoText = new();
    private readonly Label cpuMeterValue = new();
    private readonly Label gpuMeterValue = new();
    private readonly Label downloadMeterValue = new();
    private readonly Label uploadMeterValue = new();
    private readonly ProgressBar cpuMeterBar = new();
    private readonly ProgressBar gpuMeterBar = new();
    private readonly ProgressBar downloadMeterBar = new();
    private readonly ProgressBar uploadMeterBar = new();
    private readonly Label statusLabel = new();
    private readonly System.Windows.Forms.Timer metersTimer = new();
    private PerformanceCounter? cpuCounter;
    private List<PerformanceCounter> gpuCounters = [];
    private long previousBytesReceived;
    private long previousBytesSent;
    private double observedDownloadPeakBytesPerSecond = 1024 * 1024;
    private double observedUploadPeakBytesPerSecond = 1024 * 1024;
    private DateTime previousNetworkSampleAt;
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
        BuildMetersWorkspace();

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 32;
        statusLabel.Padding = new Padding(12, 7, 12, 0);
        statusLabel.BackColor = Color.White;
        statusLabel.ForeColor = Color.FromArgb(65, 70, 82);
        statusLabel.Text = "Ready. Choose a screen target, then click Capture.";

        mainPanel.Controls.Add(captureWorkspace);
        mainPanel.Controls.Add(systemInfoWorkspace);
        mainPanel.Controls.Add(metersWorkspace);
        mainPanel.Controls.Add(statusLabel);

        Controls.Add(mainPanel);
        Controls.Add(nav);

        LoadScreens();
        ShowTool(ToolKind.Capture);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        metersTimer.Stop();
        cpuCounter?.Dispose();
        foreach (var counter in gpuCounters)
        {
            counter.Dispose();
        }

        currentCapture?.Dispose();
        base.OnFormClosed(e);
    }
}
