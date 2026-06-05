namespace CKitScreenCapture;

internal sealed record CaptureTarget(string Id, string Name, Rectangle Bounds)
{
    public static CaptureTarget Merged() =>
        new("merged", "Merged screens", SystemInformation.VirtualScreen);

    public static CaptureTarget ForScreen(int index, Screen screen)
    {
        var bounds = screen.Bounds;
        var primary = screen.Primary ? " primary" : string.Empty;
        var name = $"Screen {index + 1}{primary} ({bounds.Width} x {bounds.Height})";

        return new($"screen-{index}", name, bounds);
    }

    public override string ToString() => Name;
}
