using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private void BuildMetersWorkspace()
    {
        metersWorkspace.Dock = DockStyle.Fill;
        metersWorkspace.BackColor = Color.FromArgb(246, 247, 250);
        metersWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            WrapContents = false,
        };

        ConfigureButton(refreshMetersButton, "Reset Meters");
        refreshMetersButton.Click += (_, _) => ResetMeters();
        toolbar.Controls.Add(refreshMetersButton);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 330,
            Padding = new Padding(18),
            BackColor = Color.FromArgb(246, 247, 250),
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
        metersTimer.Tick += (_, _) => UpdateMeters();

        metersWorkspace.Controls.Add(content);
        metersWorkspace.Controls.Add(toolbar);
    }

    private static Panel CreateMeterRow(string title, Label valueLabel, ProgressBar progressBar)
    {
        var row = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 10),
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 120,
            Text = title,
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 43),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        valueLabel.AutoSize = false;
        valueLabel.Dock = DockStyle.Right;
        valueLabel.Width = 220;
        valueLabel.Text = "Waiting...";
        valueLabel.ForeColor = Color.FromArgb(65, 70, 82);
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

    private void ResetMeters()
    {
        cpuCounter?.Dispose();
        foreach (var counter in gpuCounters)
        {
            counter.Dispose();
        }

        gpuCounters = [];
        cpuCounter = null;

        try
        {
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = cpuCounter.NextValue();
            cpuMeterValue.Text = "Sampling...";
            cpuMeterBar.Value = 0;
        }
        catch
        {
            cpuMeterValue.Text = "Not available";
            cpuMeterBar.Value = 0;
        }

        try
        {
            var gpuCategory = new PerformanceCounterCategory("GPU Engine");
            gpuCounters = gpuCategory
                .GetInstanceNames()
                .Where(name => name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .Select(name => new PerformanceCounter("GPU Engine", "Utilization Percentage", name))
                .ToList();

            foreach (var counter in gpuCounters)
            {
                _ = counter.NextValue();
            }

            gpuMeterValue.Text = gpuCounters.Count == 0 ? "Not available" : "Sampling...";
            gpuMeterBar.Value = 0;
        }
        catch
        {
            foreach (var counter in gpuCounters)
            {
                counter.Dispose();
            }

            gpuCounters = [];
            gpuMeterValue.Text = "Not available";
            gpuMeterBar.Value = 0;
        }

        (previousBytesReceived, previousBytesSent) = GetNetworkTotals();
        previousNetworkSampleAt = DateTime.UtcNow;
        observedDownloadPeakBytesPerSecond = 1024 * 1024;
        observedUploadPeakBytesPerSecond = 1024 * 1024;
        downloadMeterValue.Text = "Sampling...";
        uploadMeterValue.Text = "Sampling...";
        downloadMeterBar.Value = 0;
        uploadMeterBar.Value = 0;

        UpdateMeters();
        statusLabel.Text = "Meters are live.";
    }

    private void UpdateMeters()
    {
        if (cpuCounter is not null)
        {
            TryUpdatePercentMeter(cpuCounter, cpuMeterValue, cpuMeterBar);
        }

        if (gpuCounters.Count > 0)
        {
            try
            {
                var gpuPercent = gpuCounters.Sum(counter => counter.NextValue());
                SetPercentMeter(gpuPercent, gpuMeterValue, gpuMeterBar);
            }
            catch
            {
                gpuMeterValue.Text = "Not available";
                gpuMeterBar.Value = 0;
            }
        }

        UpdateNetworkMeter();
    }

    private static void TryUpdatePercentMeter(PerformanceCounter counter, Label valueLabel, ProgressBar progressBar)
    {
        try
        {
            SetPercentMeter(counter.NextValue(), valueLabel, progressBar);
        }
        catch
        {
            valueLabel.Text = "Not available";
            progressBar.Value = 0;
        }
    }

    private static void SetPercentMeter(float percent, Label valueLabel, ProgressBar progressBar)
    {
        var normalized = Math.Clamp(percent, 0, 100);

        progressBar.Value = (int)Math.Round(normalized);
        valueLabel.Text = $"{normalized:0}%";
    }

    private void UpdateNetworkMeter()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Max((now - previousNetworkSampleAt).TotalSeconds, 0.001);
        var (bytesReceived, bytesSent) = GetNetworkTotals();
        var receivedRate = Math.Max(0, (bytesReceived - previousBytesReceived) / elapsedSeconds);
        var sentRate = Math.Max(0, (bytesSent - previousBytesSent) / elapsedSeconds);

        observedDownloadPeakBytesPerSecond = Math.Max(observedDownloadPeakBytesPerSecond, receivedRate);
        observedUploadPeakBytesPerSecond = Math.Max(observedUploadPeakBytesPerSecond, sentRate);

        downloadMeterBar.Value = CalculateRateMeterValue(receivedRate, observedDownloadPeakBytesPerSecond);
        uploadMeterBar.Value = CalculateRateMeterValue(sentRate, observedUploadPeakBytesPerSecond);
        downloadMeterValue.Text = FormatRate(receivedRate);
        uploadMeterValue.Text = FormatRate(sentRate);

        previousBytesReceived = bytesReceived;
        previousBytesSent = bytesSent;
        previousNetworkSampleAt = now;
    }

    private static int CalculateRateMeterValue(double rate, double observedPeak) =>
        (int)Math.Clamp(Math.Round(rate / observedPeak * 100), 0, 100);

    private static (long Received, long Sent) GetNetworkTotals()
    {
        long received = 0;
        long sent = 0;

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var stats = networkInterface.GetIPv4Statistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }

        return (received, sent);
    }
}
