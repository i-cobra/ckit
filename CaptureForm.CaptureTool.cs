using System.Drawing.Imaging;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private void BuildCaptureWorkspace()
    {
        captureWorkspace.Dock = DockStyle.Fill;
        captureWorkspace.BackColor = FluentAppBackground;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 118,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = FluentSurface,
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Capture",
            Font = FluentFont(16, FontStyle.Bold),
            ForeColor = FluentText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var subtitleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "Choose a display target, capture it, then copy or save the result.",
            Font = FluentFont(9),
            ForeColor = FluentTextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            Padding = new Padding(0, 8, 0, 0),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = FluentSurface,
            WrapContents = false,
        };

        ConfigureScreenSelector();
        ConfigurePrimaryButton(captureButton, "Capture", "\uE722");
        ConfigureButton(refreshScreensButton, "Refresh Screens", "\uE72C");
        ConfigureButton(saveButton, "Save PNG", "\uE74E");
        ConfigureButton(copyButton, "Copy", "\uE8C8");
        ConfigureButton(openFolderButton, "Open Folder", "\uE8B7");

        saveButton.Enabled = false;
        copyButton.Enabled = false;

        captureButton.Click += async (_, _) => await CaptureScreenAsync();
        refreshScreensButton.Click += (_, _) => LoadScreens();
        saveButton.Click += (_, _) => SaveCapture();
        copyButton.Click += (_, _) => CopyCapture();
        openFolderButton.Click += (_, _) => OpenCaptureFolder();

        actionRow.Controls.Add(screenSelector);
        actionRow.Controls.Add(captureButton);
        actionRow.Controls.Add(refreshScreensButton);
        actionRow.Controls.Add(saveButton);
        actionRow.Controls.Add(copyButton);
        actionRow.Controls.Add(openFolderButton);

        captureDetailsLabel.Dock = DockStyle.Right;
        captureDetailsLabel.Width = 260;
        captureDetailsLabel.Text = "No capture yet";
        captureDetailsLabel.Font = FluentFont(9);
        captureDetailsLabel.ForeColor = FluentTextSecondary;
        captureDetailsLabel.TextAlign = ContentAlignment.BottomRight;

        header.Controls.Add(captureDetailsLabel);
        header.Controls.Add(actionRow);
        header.Controls.Add(subtitleLabel);
        header.Controls.Add(titleLabel);

        var previewOuter = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = FluentAppBackground,
        };

        var previewStage = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = FluentPreviewBackground,
        };

        captureEmptyStateLabel.Dock = DockStyle.Fill;
        captureEmptyStateLabel.Text = "No capture yet\r\nSelect a target and click Capture.";
        captureEmptyStateLabel.Font = FluentFont(13, FontStyle.Bold);
        captureEmptyStateLabel.ForeColor = Color.FromArgb(210, 210, 210);
        captureEmptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;

        preview.Dock = DockStyle.Fill;
        preview.BackColor = Color.FromArgb(20, 20, 20);
        preview.SizeMode = PictureBoxSizeMode.Zoom;
        preview.BorderStyle = BorderStyle.None;
        preview.Visible = false;

        previewStage.Controls.Add(preview);
        previewStage.Controls.Add(captureEmptyStateLabel);
        previewOuter.Controls.Add(previewStage);

        captureWorkspace.Controls.Add(previewOuter);
        captureWorkspace.Controls.Add(header);
    }

    private void ConfigureScreenSelector()
    {
        screenSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        screenSelector.Width = 280;
        screenSelector.Height = 34;
        screenSelector.Margin = new Padding(0, 0, 8, 0);
        screenSelector.IntegralHeight = false;
        screenSelector.MaxDropDownItems = 8;
        screenSelector.SelectedIndexChanged += (_, _) => UpdateCaptureTargetDetails();
    }

    private static void ConfigurePrimaryButton(Button button, string text, string? iconGlyph = null)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Height = 34;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Padding = new Padding(18, 0, 18, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = FluentAccent;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderSize = 0;
        button.Font = FluentFont(9, FontStyle.Bold);
        button.FlatAppearance.MouseOverBackColor = FluentAccentHover;
        button.FlatAppearance.MouseDownBackColor = FluentAccentPressed;
        button.UseVisualStyleBackColor = false;

        if (iconGlyph is not null)
        {
            button.Image = CreateFluentIcon(iconGlyph, Color.White);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }
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
        UpdateCaptureTargetDetails();
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
                    UpdateCaptureTargetDetails();
                    return;
                }
            }
        }

        screenSelector.SelectedIndex = 0;
        UpdateCaptureTargetDetails();
    }

    private async Task CaptureScreenAsync()
    {
        if (screenSelector.SelectedItem is not CaptureTarget target)
        {
            LoadScreens();
            target = (CaptureTarget)screenSelector.SelectedItem!;
        }

        var bounds = target.Bounds;
        var wasVisible = Visible;
        var previousWindowState = WindowState;
        captureButton.Enabled = false;

        try
        {
            if (wasVisible)
            {
                Hide();
                await Task.Delay(200);
            }

            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            var oldCapture = currentCapture;
            currentCapture = bitmap;
            preview.Image = currentCapture;
            preview.Visible = true;
            captureEmptyStateLabel.Visible = false;
            oldCapture?.Dispose();

            saveButton.Enabled = true;
            copyButton.Enabled = true;
            captureDetailsLabel.Text = $"{target.Name}\r\n{bounds.Width} x {bounds.Height} captured at {DateTime.Now:T}";
            statusLabel.Text = $"Captured {target.Name} ({bounds.Width} x {bounds.Height}) at {DateTime.Now:T}.";
        }
        finally
        {
            if (wasVisible)
            {
                Show();
                WindowState = previousWindowState;
                Activate();
            }

            captureButton.Enabled = true;
        }
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

    private void UpdateCaptureTargetDetails()
    {
        if (currentCapture is not null)
        {
            return;
        }

        if (screenSelector.SelectedItem is not CaptureTarget target)
        {
            captureDetailsLabel.Text = "No target selected";
            return;
        }

        var bounds = target.Bounds;
        captureDetailsLabel.Text = $"{target.Name}\r\n{bounds.Width} x {bounds.Height}";
    }
}
