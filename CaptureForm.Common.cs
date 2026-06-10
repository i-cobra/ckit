namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private static readonly FontFamily FluentFontFamily = new("Segoe UI");
    private static readonly Color FluentAppBackground = Color.FromArgb(243, 243, 243);
    private static readonly Color FluentSurface = Color.FromArgb(255, 255, 255);
    private static readonly Color FluentCard = Color.FromArgb(252, 252, 252);
    private static readonly Color FluentCardHover = Color.FromArgb(247, 247, 247);
    private static readonly Color FluentStroke = Color.FromArgb(225, 225, 225);
    private static readonly Color FluentText = Color.FromArgb(32, 32, 32);
    private static readonly Color FluentTextSecondary = Color.FromArgb(96, 96, 96);
    private static readonly Color FluentAccent = Color.FromArgb(0, 95, 184);
    private static readonly Color FluentAccentHover = Color.FromArgb(0, 85, 166);
    private static readonly Color FluentAccentPressed = Color.FromArgb(0, 72, 140);
    private static readonly Color FluentNavBackground = Color.FromArgb(249, 249, 249);
    private static readonly Color FluentNavSelected = Color.FromArgb(235, 243, 252);
    private static readonly Color FluentPreviewBackground = Color.FromArgb(32, 32, 32);

    private static string DefaultCaptureFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "cKitCaptures");

    private static void ConfigureButton(Button button, string text, string? iconGlyph = null)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Height = 34;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Padding = new Padding(12, 0, 12, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = FluentCard;
        button.ForeColor = FluentText;
        button.Font = FluentFont(9);
        button.FlatAppearance.BorderColor = FluentStroke;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = FluentCardHover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 238, 238);
        button.UseVisualStyleBackColor = false;

        if (iconGlyph is not null)
        {
            button.Image = CreateFluentIcon(iconGlyph, FluentText);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }
    }

    private static Font FluentFont(float size, FontStyle style = FontStyle.Regular) =>
        new(FluentFontFamily, size, style);

    private static Panel CreateFluentCard(int padding = 14) =>
        new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentCard,
            BorderColor = FluentStroke,
            CornerRadius = 8,
            Padding = new Padding(padding),
            Margin = new Padding(0, 0, 0, 10),
        };

    private static void ConfigureTextSurface(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = FluentSurface;
        textBox.ForeColor = FluentText;
        textBox.Font = new Font("Cascadia Mono", 10);
    }

    private static Bitmap CreateFluentIcon(string glyph, Color color, int size = 18)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(color);
        using var font = new Font("Segoe MDL2 Assets", size - 3, FontStyle.Regular, GraphicsUnit.Pixel);
        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size));
        return bitmap;
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

    private sealed class RoundedPanel : Panel
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; init; } = 8;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; init; } = FluentStroke;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundedRectangle(bounds, CornerRadius);
            using var brush = new SolidBrush(BackColor);
            using var pen = new Pen(BorderColor);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
