namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private static string DefaultCaptureFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "cKitCaptures");

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

    private static void OpenCaptureFolder()
    {
        Directory.CreateDirectory(DefaultCaptureFolder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DefaultCaptureFolder,
            UseShellExecute = true,
        });
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

    private static string FormatRate(double bytesPerSecond) =>
        $"{FormatBytes((long)bytesPerSecond)}/s";
}
