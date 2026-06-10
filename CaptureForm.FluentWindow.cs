using System.Runtime.InteropServices;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropTypeMica = 2;

    private void ApplyFluentWindowChrome()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var cornerPreference = DwmWindowCornerPreferenceRound;
        _ = DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

        var backdropType = DwmSystemBackdropTypeMica;
        _ = DwmSetWindowAttribute(Handle, DwmwaSystemBackdropType, ref backdropType, sizeof(int));

        var captionColor = ColorTranslator.ToWin32(FluentNavBackground);
        _ = DwmSetWindowAttribute(Handle, DwmwaCaptionColor, ref captionColor, sizeof(int));

        var textColor = ColorTranslator.ToWin32(FluentText);
        _ = DwmSetWindowAttribute(Handle, DwmwaTextColor, ref textColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
