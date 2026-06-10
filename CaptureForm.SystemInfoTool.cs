using System.Runtime.InteropServices;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private void BuildSystemInfoWorkspace()
    {
        systemInfoWorkspace.Dock = DockStyle.Fill;
        systemInfoWorkspace.BackColor = FluentAppBackground;
        systemInfoWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = FluentSurface,
            WrapContents = false,
        };

        ConfigureButton(refreshSystemInfoButton, "Refresh", "\uE72C");
        refreshSystemInfoButton.Click += (_, _) => RefreshSystemInfo();
        toolbar.Controls.Add(refreshSystemInfoButton);

        systemInfoText.Dock = DockStyle.Fill;
        systemInfoText.ReadOnly = true;
        systemInfoText.Multiline = true;
        systemInfoText.ScrollBars = ScrollBars.Vertical;
        ConfigureTextSurface(systemInfoText);
        systemInfoText.Margin = new Padding(0);

        systemInfoWorkspace.Controls.Add(systemInfoText);
        systemInfoWorkspace.Controls.Add(toolbar);
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
            "",
            "Displays",
            $"  Total screens:      {screens.Length}",
            $"  Virtual desktop:    {SystemInformation.VirtualScreen.Width} x {SystemInformation.VirtualScreen.Height}",
        }.Concat(screenLines));

        statusLabel.Text = $"System info refreshed at {DateTime.Now:T}.";
    }
}
