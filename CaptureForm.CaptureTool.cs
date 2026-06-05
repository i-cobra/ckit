using System.Drawing.Imaging;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private void BuildCaptureWorkspace()
    {
        captureWorkspace.Dock = DockStyle.Fill;
        captureWorkspace.BackColor = Color.FromArgb(246, 247, 250);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            WrapContents = false,
        };

        ConfigureScreenSelector();
        ConfigureButton(captureButton, "Capture");
        ConfigureButton(refreshScreensButton, "Refresh Screens");
        ConfigureButton(saveButton, "Save PNG");
        ConfigureButton(copyButton, "Copy");
        ConfigureButton(openFolderButton, "Open Folder");

        saveButton.Enabled = false;
        copyButton.Enabled = false;

        captureButton.Click += (_, _) => CaptureScreen();
        refreshScreensButton.Click += (_, _) => LoadScreens();
        saveButton.Click += (_, _) => SaveCapture();
        copyButton.Click += (_, _) => CopyCapture();
        openFolderButton.Click += (_, _) => OpenCaptureFolder();

        toolbar.Controls.Add(screenSelector);
        toolbar.Controls.Add(captureButton);
        toolbar.Controls.Add(refreshScreensButton);
        toolbar.Controls.Add(saveButton);
        toolbar.Controls.Add(copyButton);
        toolbar.Controls.Add(openFolderButton);

        preview.Dock = DockStyle.Fill;
        preview.BackColor = Color.FromArgb(31, 35, 43);
        preview.SizeMode = PictureBoxSizeMode.Zoom;
        preview.BorderStyle = BorderStyle.FixedSingle;

        captureWorkspace.Controls.Add(preview);
        captureWorkspace.Controls.Add(toolbar);
    }

    private void ConfigureScreenSelector()
    {
        screenSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        screenSelector.Width = 280;
        screenSelector.Height = 34;
        screenSelector.Margin = new Padding(0, 0, 8, 0);
        screenSelector.IntegralHeight = false;
        screenSelector.MaxDropDownItems = 8;
    }

    private void LoadScreens()
    {
        var previousSelection = (screenSelector.SelectedItem as CaptureTarget)?.Id;

        screenSelector.BeginUpdate();
        screenSelector.Items.Clear();
        screenSelector.Items.Add(CaptureTarget.Merged());

        var screens = Screen.AllScreens;
        for (var i = 0; i < screens.Length; i++)
        {
            screenSelector.Items.Add(CaptureTarget.ForScreen(i, screens[i]));
        }

        screenSelector.EndUpdate();

        SelectTarget(previousSelection);
        statusLabel.Text = $"Found {screens.Length} screen{(screens.Length == 1 ? string.Empty : "s")}.";
    }

    private void SelectTarget(string? preferredId)
    {
        if (preferredId is not null)
        {
            for (var i = 0; i < screenSelector.Items.Count; i++)
            {
                if ((screenSelector.Items[i] as CaptureTarget)?.Id == preferredId)
                {
                    screenSelector.SelectedIndex = i;
                    return;
                }
            }
        }

        screenSelector.SelectedIndex = 0;
    }

    private void CaptureScreen()
    {
        if (screenSelector.SelectedItem is not CaptureTarget target)
        {
            LoadScreens();
            target = (CaptureTarget)screenSelector.SelectedItem!;
        }

        var bounds = target.Bounds;
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        var oldCapture = currentCapture;
        currentCapture = bitmap;
        preview.Image = currentCapture;
        oldCapture?.Dispose();

        saveButton.Enabled = true;
        copyButton.Enabled = true;
        statusLabel.Text = $"Captured {target.Name} ({bounds.Width} x {bounds.Height}) at {DateTime.Now:T}.";
    }

    private void SaveCapture()
    {
        if (currentCapture is null)
        {
            return;
        }

        Directory.CreateDirectory(DefaultCaptureFolder);
        var fileName = $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        var path = Path.Combine(DefaultCaptureFolder, fileName);

        currentCapture.Save(path, ImageFormat.Png);
        statusLabel.Text = $"Saved {path}";
    }

    private void CopyCapture()
    {
        if (currentCapture is null)
        {
            return;
        }

        Clipboard.SetImage(currentCapture);
        statusLabel.Text = "Copied capture to clipboard.";
    }
}
