using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private void BuildMetersWorkspace()
    {
        metersWorkspace.Dock = DockStyle.Fill;
        metersWorkspace.BackColor = FluentAppBackground;
        metersWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = FluentSurface,
            WrapContents = false,
        };

        ConfigureButton(refreshMetersButton, "Reset Meters", "\uE72C");
        refreshMetersButton.Click += async (_, _) => await ResetMetersAsync();
        toolbar.Controls.Add(refreshMetersButton);

        InitializeNetworkSpeedTaskbarWindow();

        showNetworkSpeedInTaskbarCheckBox.AutoSize = true;
        showNetworkSpeedInTaskbarCheckBox.Text = "Show network speed in taskbar";
        showNetworkSpeedInTaskbarCheckBox.Margin = new Padding(8, 7, 0, 0);
        showNetworkSpeedInTaskbarCheckBox.ForeColor = FluentTextSecondary;
        showNetworkSpeedInTaskbarCheckBox.Font = FluentFont(9);
        showNetworkSpeedInTaskbarCheckBox.CheckedChanged += (_, _) => ToggleNetworkSpeedInTaskbar();
        toolbar.Controls.Add(showNetworkSpeedInTaskbarCheckBox);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 330,
            Padding = new Padding(18),
            BackColor = FluentAppBackground,
            ColumnCount = 1,
            RowCount = 4,
        };

        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        content.Controls.Add(CreateMeterRow("CPU", cpuMeterValue, cpuMeterBar), 0, 0);
        content.Controls.Add(CreateMeterRow("GPU", gpuMeterValue, gpuMeterBar), 0, 1);
        content.Controls.Add(CreateMeterRow("Download", downloadMeterValue, downloadMeterBar), 0, 2);
        content.Controls.Add(CreateMeterRow("Upload", uploadMeterValue, uploadMeterBar), 0, 3);

        metersTimer.Interval = 1000;
        metersTimer.Tick += async (_, _) => await UpdateMetersAsync();

        metersWorkspace.Controls.Add(content);
        metersWorkspace.Controls.Add(toolbar);
    }

    private static Panel CreateMeterRow(string title, Label valueLabel, ProgressBar progressBar)
    {
        var row = CreateFluentCard();
        row.Dock = DockStyle.Fill;
        row.Padding = new Padding(14, 10, 14, 10);
        row.Margin = new Padding(0, 0, 0, 10);

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 120,
            Text = title,
            Font = FluentFont(11, FontStyle.Bold),
            ForeColor = FluentText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        valueLabel.AutoSize = false;
        valueLabel.Dock = DockStyle.Right;
        valueLabel.Width = 220;
        valueLabel.Text = "Waiting...";
        valueLabel.ForeColor = FluentTextSecondary;
        valueLabel.Font = FluentFont(9);
        valueLabel.TextAlign = ContentAlignment.MiddleRight;

        progressBar.Dock = DockStyle.Fill;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Margin = new Padding(0, 12, 0, 12);

        row.Controls.Add(progressBar);
        row.Controls.Add(valueLabel);
        row.Controls.Add(titleLabel);

        return row;
    }

    private async Task ResetMetersAsync()
    {
        if (metersInitializing)
        {
            return;
        }

        metersInitializing = true;
        refreshMetersButton.Enabled = false;
        cpuMeterValue.Text = "Starting...";
        gpuMeterValue.Text = "Starting...";
        downloadMeterValue.Text = "Starting...";
        uploadMeterValue.Text = "Starting...";
        cpuMeterBar.Value = 0;
        gpuMeterBar.Value = 0;
        downloadMeterBar.Value = 0;
        uploadMeterBar.Value = 0;

        try
        {
            cpuCounter?.Dispose();
            foreach (var counter in gpuCounters)
            {
                counter.Dispose();
            }

            gpuCounters = [];
            cpuCounter = null;

            var setup = await Task.Run(CreateMeterCounters);
            cpuCounter = setup.CpuCounter;
            gpuCounters = setup.GpuCounters;

            cpuMeterValue.Text = cpuCounter is null ? "Not available" : "Sampling...";
            gpuMeterValue.Text = gpuCounters.Count == 0 ? "Not available" : "Sampling...";

            networkInterfacesRefreshedAt = DateTime.MinValue;
            var totals = await Task.Run(GetNetworkTotals);
            previousBytesReceived = totals.Received;
            previousBytesSent = totals.Sent;
            previousNetworkSampleAt = DateTime.UtcNow;
            observedDownloadPeakBytesPerSecond = 1024 * 1024;
            observedUploadPeakBytesPerSecond = 1024 * 1024;
            downloadMeterValue.Text = "Sampling...";
            uploadMeterValue.Text = "Sampling...";
            downloadMeterBar.Value = 0;
            uploadMeterBar.Value = 0;

            metersInitialized = true;
            await UpdateMetersAsync();
            statusLabel.Text = "Meters are live.";
        }
        finally
        {
            metersInitializing = false;
            refreshMetersButton.Enabled = true;
        }
    }

    private static MeterCounterSetup CreateMeterCounters()
    {
        PerformanceCounter? cpu = null;
        var gpu = new List<PerformanceCounter>();

        try
        {
            cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = cpu.NextValue();
        }
        catch
        {
            cpu?.Dispose();
            cpu = null;
        }

        try
        {
            var gpuCategory = new PerformanceCounterCategory("GPU Engine");
            gpu = gpuCategory
                .GetInstanceNames()
                .Where(name => name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .Select(name => new PerformanceCounter("GPU Engine", "Utilization Percentage", name))
                .ToList();

            foreach (var counter in gpu)
            {
                _ = counter.NextValue();
            }
        }
        catch
        {
            foreach (var counter in gpu)
            {
                counter.Dispose();
            }

            gpu = [];
        }

        return new MeterCounterSetup(cpu, gpu);
    }

    private async Task EnsureMetersStartedAsync()
    {
        if (!metersInitialized)
        {
            await ResetMetersAsync();
            return;
        }

        await UpdateMetersAsync();
        statusLabel.Text = "Meters are live.";
    }

    private async Task UpdateMetersAsync()
    {
        if (!metersInitialized || metersSampleInProgress)
        {
            return;
        }

        metersSampleInProgress = true;

        try
        {
            var sample = await Task.Run(SampleMeters);
            ApplyMeterSample(sample);
        }
        finally
        {
            metersSampleInProgress = false;
        }
    }

    private MeterSample SampleMeters()
    {
        float? cpuPercent = null;
        float? gpuPercent = null;

        if (cpuCounter is not null)
        {
            try
            {
                cpuPercent = cpuCounter.NextValue();
            }
            catch
            {
                cpuPercent = null;
            }
        }

        if (gpuCounters.Count > 0)
        {
            try
            {
                gpuPercent = gpuCounters.Sum(counter => counter.NextValue());
            }
            catch
            {
                gpuPercent = null;
            }
        }

        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Max((now - previousNetworkSampleAt).TotalSeconds, 0.001);
        var (bytesReceived, bytesSent) = GetNetworkTotals();
        var receivedRate = Math.Max(0, (bytesReceived - previousBytesReceived) / elapsedSeconds);
        var sentRate = Math.Max(0, (bytesSent - previousBytesSent) / elapsedSeconds);

        return new MeterSample(cpuPercent, gpuPercent, bytesReceived, bytesSent, receivedRate, sentRate, now);
    }

    private void ApplyMeterSample(MeterSample sample)
    {
        if (sample.CpuPercent.HasValue)
        {
            SetPercentMeter(sample.CpuPercent.Value, cpuMeterValue, cpuMeterBar);
        }
        else if (cpuCounter is null)
        {
            cpuMeterValue.Text = "Not available";
            cpuMeterBar.Value = 0;
        }

        if (sample.GpuPercent.HasValue)
        {
            SetPercentMeter(sample.GpuPercent.Value, gpuMeterValue, gpuMeterBar);
        }
        else if (gpuCounters.Count == 0)
        {
            gpuMeterValue.Text = "Not available";
            gpuMeterBar.Value = 0;
        }

        observedDownloadPeakBytesPerSecond = Math.Max(observedDownloadPeakBytesPerSecond, sample.DownloadRate);
        observedUploadPeakBytesPerSecond = Math.Max(observedUploadPeakBytesPerSecond, sample.UploadRate);

        downloadMeterBar.Value = CalculateRateMeterValue(sample.DownloadRate, observedDownloadPeakBytesPerSecond);
        uploadMeterBar.Value = CalculateRateMeterValue(sample.UploadRate, observedUploadPeakBytesPerSecond);
        downloadMeterValue.Text = FormatRate(sample.DownloadRate);
        uploadMeterValue.Text = FormatRate(sample.UploadRate);
        UpdateNetworkSpeedTaskbarWindow(sample.DownloadRate, sample.UploadRate);

        previousBytesReceived = sample.BytesReceived;
        previousBytesSent = sample.BytesSent;
        previousNetworkSampleAt = sample.SampledAt;
    }

    private static void SetPercentMeter(float percent, Label valueLabel, ProgressBar progressBar)
    {
        var normalized = Math.Clamp(percent, 0, 100);

        progressBar.Value = (int)Math.Round(normalized);
        valueLabel.Text = $"{normalized:0}%";
    }

    private void UpdateNetworkMeter()
    {
        try
        {
            var sample = SampleMeters();
            ApplyMeterSample(sample);
        }
        catch
        {
            downloadMeterValue.Text = "Not available";
            uploadMeterValue.Text = "Not available";
            downloadMeterBar.Value = 0;
            uploadMeterBar.Value = 0;
        }
    }

    private void ToggleNetworkSpeedInTaskbar()
    {
        if (showNetworkSpeedInTaskbarCheckBox.Checked)
        {
            PositionNetworkSpeedTaskbarWindow();
            networkSpeedTaskbarWindow.Show();
            _ = EnsureMetersStartedAsync();
            metersTimer.Enabled = true;
            statusLabel.Text = "Network speed is anchored near the Windows taskbar clock.";
            return;
        }

        networkSpeedTaskbarWindow.Hide();
        metersTimer.Enabled = currentTool == ToolKind.Meters;
        statusLabel.Text = currentTool == ToolKind.Meters
            ? "Meters are live."
            : "Taskbar network speed is off.";
    }

    private void InitializeNetworkSpeedTaskbarWindow()
    {
        networkSpeedTaskbarWindow.FormBorderStyle = FormBorderStyle.None;
        networkSpeedTaskbarWindow.ShowInTaskbar = false;
        networkSpeedTaskbarWindow.StartPosition = FormStartPosition.Manual;
        networkSpeedTaskbarWindow.TopMost = true;
        networkSpeedTaskbarWindow.Size = new Size(146, 38);
        networkSpeedTaskbarWindow.BackColor = Color.FromArgb(32, 32, 32);

        networkSpeedTaskbarWindowLabel.Dock = DockStyle.Fill;
        networkSpeedTaskbarWindowLabel.Font = FluentFont(8.5f, FontStyle.Bold);
        networkSpeedTaskbarWindowLabel.ForeColor = Color.White;
        networkSpeedTaskbarWindowLabel.Padding = new Padding(8, 0, 8, 0);
        networkSpeedTaskbarWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        networkSpeedTaskbarWindowLabel.Text = "Down 0 B/s\nUp 0 B/s";
        networkSpeedTaskbarWindow.Controls.Add(networkSpeedTaskbarWindowLabel);
    }

    private void UpdateNetworkSpeedTaskbarWindow(double downloadRate, double uploadRate)
    {
        if (!showNetworkSpeedInTaskbarCheckBox.Checked)
        {
            return;
        }

        networkSpeedTaskbarWindowLabel.Text = $"D {FormatRate(downloadRate)}\nU {FormatRate(uploadRate)}";
        PositionNetworkSpeedTaskbarWindow();

        if (!networkSpeedTaskbarWindow.Visible)
        {
            networkSpeedTaskbarWindow.Show();
        }
    }

    private void PositionNetworkSpeedTaskbarWindow()
    {
        var trayBounds = GetTaskbarTrayBounds();
        var clockBounds = GetTaskbarClockBounds();
        var taskbarBounds = GetTaskbarBounds();
        var displayBounds = Screen.PrimaryScreen?.Bounds ?? Bounds;
        var targetBounds = trayBounds ?? clockBounds ?? taskbarBounds ?? new Rectangle(displayBounds.Right - 240, displayBounds.Bottom - 48, 240, 48);
        var x = trayBounds is not null || clockBounds is not null
            ? targetBounds.Left - networkSpeedTaskbarWindow.Width - 8
            : targetBounds.Right - networkSpeedTaskbarWindow.Width - 220;
        x = Math.Clamp(x, displayBounds.Left, displayBounds.Right - networkSpeedTaskbarWindow.Width);
        var y = targetBounds.Top + Math.Max(0, (targetBounds.Height - networkSpeedTaskbarWindow.Height) / 2);

        networkSpeedTaskbarWindow.Location = new Point(x, y);
    }

    private static Rectangle? GetTaskbarTrayBounds()
    {
        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        if (taskbarHandle == IntPtr.Zero)
        {
            return null;
        }

        var trayHandle = FindChildWindowByClass(taskbarHandle, "TrayNotifyWnd");
        return trayHandle != IntPtr.Zero && GetWindowRect(trayHandle, out var rect)
            ? rect.ToRectangle()
            : null;
    }

    private static Rectangle? GetTaskbarClockBounds()
    {
        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        if (taskbarHandle == IntPtr.Zero)
        {
            return null;
        }

        var clockHandle = FindChildWindowByClass(taskbarHandle, "TrayClockWClass");
        return clockHandle != IntPtr.Zero && GetWindowRect(clockHandle, out var rect)
            ? rect.ToRectangle()
            : null;
    }

    private static Rectangle? GetTaskbarBounds()
    {
        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        return taskbarHandle != IntPtr.Zero && GetWindowRect(taskbarHandle, out var rect)
            ? rect.ToRectangle()
            : null;
    }

    private static IntPtr FindChildWindowByClass(IntPtr parentHandle, string className)
    {
        var childHandle = IntPtr.Zero;
        while ((childHandle = FindWindowEx(parentHandle, childHandle, null, null)) != IntPtr.Zero)
        {
            if (GetClassName(childHandle, className))
            {
                return childHandle;
            }

            var nestedHandle = FindChildWindowByClass(childHandle, className);
            if (nestedHandle != IntPtr.Zero)
            {
                return nestedHandle;
            }
        }

        return IntPtr.Zero;
    }

    private static bool GetClassName(IntPtr handle, string expectedClassName)
    {
        var className = new char[256];
        var length = GetClassName(handle, className, className.Length);
        return length > 0 && new string(className, 0, length) == expectedClassName;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowName);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr handle, char[] className, int maxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRectangle rectangle);

    private readonly struct NativeRectangle
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public Rectangle ToRectangle() =>
            Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    private static int CalculateRateMeterValue(double rate, double observedPeak) =>
        (int)Math.Clamp(Math.Round(rate / observedPeak * 100), 0, 100);

    private (long Received, long Sent) GetNetworkTotals()
    {
        long received = 0;
        long sent = 0;

        RefreshNetworkInterfacesIfNeeded();

        foreach (var networkInterface in activeNetworkInterfaces)
        {
            try
            {
                var stats = networkInterface.GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch
            {
                networkInterfacesRefreshedAt = DateTime.MinValue;
            }
        }

        return (received, sent);
    }

    private void RefreshNetworkInterfacesIfNeeded()
    {
        if (activeNetworkInterfaces.Count > 0 &&
            DateTime.UtcNow - networkInterfacesRefreshedAt < TimeSpan.FromSeconds(10))
        {
            return;
        }

        activeNetworkInterfaces = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .ToList();
        networkInterfacesRefreshedAt = DateTime.UtcNow;
    }

    private sealed record MeterCounterSetup(PerformanceCounter? CpuCounter, List<PerformanceCounter> GpuCounters);

    private sealed record MeterSample(
        float? CpuPercent,
        float? GpuPercent,
        long BytesReceived,
        long BytesSent,
        double DownloadRate,
        double UploadRate,
        DateTime SampledAt);
}
