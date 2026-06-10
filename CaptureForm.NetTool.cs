using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private void BuildNetWorkspace()
    {
        netWorkspace.Dock = DockStyle.Fill;
        netWorkspace.BackColor = FluentAppBackground;
        netWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = FluentSurface,
            WrapContents = false,
        };

        ConfigureButton(refreshNetButton, "Refresh", "\uE72C");
        refreshNetButton.Click += (_, _) => RefreshNetInfo();
        toolbar.Controls.Add(refreshNetButton);

        netText.Dock = DockStyle.Fill;
        netText.ReadOnly = true;
        netText.Multiline = true;
        netText.ScrollBars = ScrollBars.Vertical;
        ConfigureTextSurface(netText);
        netText.Margin = new Padding(0);

        netWorkspace.Controls.Add(netText);
        netWorkspace.Controls.Add(toolbar);
    }

    private void RefreshNetInfo()
    {
        var activeInterfaces = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .OrderBy(networkInterface => networkInterface.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            "Net",
            "",
            $"Updated:            {DateTime.Now:T}",
            $"Host name:          {Environment.MachineName}",
            $"Active adapters:    {activeInterfaces.Count}",
            "",
            "Current IP addresses",
        };

        if (activeInterfaces.Count == 0)
        {
            lines.Add("  No active network adapters found.");
        }

        foreach (var networkInterface in activeInterfaces)
        {
            var properties = networkInterface.GetIPProperties();
            var addresses = properties.UnicastAddresses
                .Where(address => address.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Select(address => FormatIpAddress(address))
                .ToList();

            var gatewayAddresses = properties.GatewayAddresses
                .Select(gateway => gateway.Address)
                .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Select(address => address.ToString())
                .ToList();

            lines.Add("");
            lines.Add($"  {networkInterface.Name}");
            lines.Add($"    Type:           {networkInterface.NetworkInterfaceType}");
            lines.Add($"    Description:    {networkInterface.Description}");
            lines.Add($"    Speed:          {FormatInterfaceSpeed(networkInterface.Speed)}");
            lines.Add($"    IP:             {(addresses.Count == 0 ? "No IP address assigned" : string.Join(", ", addresses))}");
            lines.Add($"    Gateway:        {(gatewayAddresses.Count == 0 ? "None" : string.Join(", ", gatewayAddresses))}");
        }

        netText.Text = string.Join(Environment.NewLine, lines);
        statusLabel.Text = $"Net refreshed at {DateTime.Now:T}.";
    }

    private static string FormatIpAddress(UnicastIPAddressInformation address)
    {
        var scope = address.Address.AddressFamily == AddressFamily.InterNetworkV6
            ? "IPv6"
            : "IPv4";

        return $"{address.Address} ({scope})";
    }

    private static string FormatInterfaceSpeed(long bitsPerSecond) =>
        bitsPerSecond > 0
            ? FormatRate(bitsPerSecond / 8.0)
            : "Unknown";
}
