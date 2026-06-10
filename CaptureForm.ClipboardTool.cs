using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private const int WmClipboardUpdate = 0x031D;
    private static readonly string ClipboardDatabasePath = Path.Combine(GetRootLevelFolder(), "clipboard.db");
    private static readonly string ClipboardPreviewFolder = Path.Combine(Path.GetTempPath(), "cKitClipboardPreview");

    private void BuildClipboardWorkspace()
    {
        clipboardWorkspace.Dock = DockStyle.Fill;
        clipboardWorkspace.BackColor = FluentAppBackground;
        clipboardWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = FluentSurface,
            WrapContents = false,
        };

        ConfigureButton(refreshClipboardButton, "Refresh History", "\uE72C");
        ConfigureButton(openClipboardDbFolderButton, "Open DB Folder", "\uE8B7");
        refreshClipboardButton.Click += (_, _) => RefreshClipboardHistory();
        openClipboardDbFolderButton.Click += (_, _) => OpenClipboardDatabaseFolder();
        toolbar.Controls.Add(refreshClipboardButton);
        toolbar.Controls.Add(openClipboardDbFolderButton);

        clipboardStatusValue.AutoSize = false;
        clipboardStatusValue.Width = 520;
        clipboardStatusValue.Height = 34;
        clipboardStatusValue.Margin = new Padding(8, 0, 0, 0);
        clipboardStatusValue.TextAlign = ContentAlignment.MiddleLeft;
        clipboardStatusValue.ForeColor = FluentTextSecondary;
        clipboardStatusValue.Text = $"Database: {ClipboardDatabasePath}";
        toolbar.Controls.Add(clipboardStatusValue);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320,
            BackColor = FluentAppBackground,
            Padding = new Padding(18),
        };

        clipboardHistoryList.Dock = DockStyle.Fill;
        clipboardHistoryList.View = View.Details;
        clipboardHistoryList.FullRowSelect = true;
        clipboardHistoryList.MultiSelect = false;
        clipboardHistoryList.HideSelection = false;
        clipboardHistoryList.BorderStyle = BorderStyle.FixedSingle;
        clipboardHistoryList.Columns.Add("ID", 70);
        clipboardHistoryList.Columns.Add("Captured", 150);
        clipboardHistoryList.Columns.Add("Type", 100);
        clipboardHistoryList.Columns.Add("Size", 90);
        clipboardHistoryList.Columns.Add("Preview", 520);
        clipboardHistoryList.SelectedIndexChanged += (_, _) => ShowSelectedClipboardItem();

        clipboardPreviewText.Dock = DockStyle.Fill;
        clipboardPreviewText.ReadOnly = true;
        clipboardPreviewText.Multiline = true;
        clipboardPreviewText.ScrollBars = ScrollBars.Vertical;
        ConfigureTextSurface(clipboardPreviewText);

        clipboardHtmlPreviewBrowser.Dock = DockStyle.Fill;
        clipboardHtmlPreviewBrowser.AllowNavigation = true;
        clipboardHtmlPreviewBrowser.AllowWebBrowserDrop = false;
        clipboardHtmlPreviewBrowser.IsWebBrowserContextMenuEnabled = false;
        clipboardHtmlPreviewBrowser.ScriptErrorsSuppressed = true;
        NavigateHtmlPreview(CreateHtmlPreviewMessage("Select an HTML clipboard item."), "message-select");

        clipboardImagePreview.Dock = DockStyle.Fill;
        clipboardImagePreview.BackColor = FluentPreviewBackground;
        clipboardImagePreview.BorderStyle = BorderStyle.FixedSingle;
        clipboardImagePreview.SizeMode = PictureBoxSizeMode.Zoom;

        clipboardDetailsPreviewTab.Controls.Add(clipboardPreviewText);
        clipboardHtmlPreviewTab.Controls.Add(clipboardHtmlPreviewBrowser);
        clipboardImagePreviewTab.Controls.Add(clipboardImagePreview);

        clipboardPreviewTabs.Dock = DockStyle.Fill;
        clipboardPreviewTabs.TabPages.Add(clipboardDetailsPreviewTab);
        clipboardPreviewTabs.TabPages.Add(clipboardHtmlPreviewTab);
        clipboardPreviewTabs.TabPages.Add(clipboardImagePreviewTab);

        split.Panel1.Controls.Add(clipboardHistoryList);
        split.Panel2.Controls.Add(clipboardPreviewTabs);

        clipboardWorkspace.Controls.Add(split);
        clipboardWorkspace.Controls.Add(toolbar);
    }

    private void InitializeClipboardDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ClipboardDatabasePath)!);

        using var connection = OpenClipboardConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS clipboard_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                captured_at TEXT NOT NULL,
                content_type TEXT NOT NULL,
                format_summary TEXT NOT NULL,
                text_content TEXT NULL,
                binary_content BLOB NULL,
                char_count INTEGER NOT NULL DEFAULT 0,
                byte_count INTEGER NOT NULL DEFAULT 0,
                content_hash TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_clipboard_items_captured_at
                ON clipboard_items(captured_at DESC);
            """;
        command.ExecuteNonQuery();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyFluentWindowChrome();
        StartClipboardListener();
        CaptureClipboardSnapshot();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        StopClipboardListener();
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmClipboardUpdate)
        {
            CaptureClipboardSnapshot();
        }

        base.WndProc(ref message);
    }

    private void StartClipboardListener()
    {
        if (clipboardListenerRegistered || !IsHandleCreated)
        {
            return;
        }

        clipboardListenerRegistered = AddClipboardFormatListener(Handle);
    }

    private void StopClipboardListener()
    {
        if (!clipboardListenerRegistered || !IsHandleCreated)
        {
            return;
        }

        _ = RemoveClipboardFormatListener(Handle);
        clipboardListenerRegistered = false;
    }

    private void CaptureClipboardSnapshot()
    {
        ClipboardSnapshot? snapshot;
        try
        {
            snapshot = ReadClipboardSnapshot();
        }
        catch (ExternalException)
        {
            return;
        }
        catch (ThreadStateException)
        {
            return;
        }

        if (snapshot is null || snapshot.Signature == lastClipboardSignature)
        {
            return;
        }

        lastClipboardSignature = snapshot.Signature;
        SaveClipboardSnapshot(snapshot);

        if (currentTool == ToolKind.Clipboard)
        {
            RefreshClipboardHistory();
        }
    }

    private static ClipboardSnapshot? ReadClipboardSnapshot()
    {
        var dataObject = Clipboard.GetDataObject();
        if (dataObject is null)
        {
            return null;
        }

        var formats = dataObject.GetFormats(false);
        var formatSummary = formats.Length == 0
            ? "Unknown"
            : string.Join(", ", formats);

        if (Clipboard.ContainsImage())
        {
            using var image = Clipboard.GetImage();
            if (image is not null)
            {
                using var stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();
                return ClipboardSnapshot.Binary("Image", formatSummary, bytes, $"{image.Width} x {image.Height} PNG image");
            }
        }

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            var text = FormatFileDropList(files);
            return ClipboardSnapshot.Text("Files", formatSummary, text);
        }

        if (dataObject.GetDataPresent(DataFormats.Html))
        {
            return ClipboardSnapshot.Text("HTML", formatSummary, dataObject.GetData(DataFormats.Html)?.ToString() ?? string.Empty);
        }

        if (dataObject.GetDataPresent(DataFormats.Rtf))
        {
            return ClipboardSnapshot.Text("RTF", formatSummary, dataObject.GetData(DataFormats.Rtf)?.ToString() ?? string.Empty);
        }

        if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
        {
            return ClipboardSnapshot.Text("Text", formatSummary, Clipboard.GetText(TextDataFormat.UnicodeText));
        }

        return formats.Length == 0
            ? null
            : ClipboardSnapshot.Text("Metadata", formatSummary, $"Formats: {formatSummary}");
    }

    private void SaveClipboardSnapshot(ClipboardSnapshot snapshot)
    {
        using var connection = OpenClipboardConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO clipboard_items (
                captured_at,
                content_type,
                format_summary,
                text_content,
                binary_content,
                char_count,
                byte_count,
                content_hash
            )
            VALUES (
                $capturedAt,
                $contentType,
                $formatSummary,
                $textContent,
                $binaryContent,
                $charCount,
                $byteCount,
                $contentHash
            );
            """;

        command.Parameters.AddWithValue("$capturedAt", DateTimeOffset.Now.ToString("O"));
        command.Parameters.AddWithValue("$contentType", snapshot.ContentType);
        command.Parameters.AddWithValue("$formatSummary", snapshot.FormatSummary);
        command.Parameters.AddWithValue("$textContent", (object?)snapshot.TextContent ?? DBNull.Value);
        command.Parameters.Add("$binaryContent", SqliteType.Blob).Value = (object?)snapshot.BinaryContent ?? DBNull.Value;
        command.Parameters.AddWithValue("$charCount", snapshot.TextContent?.Length ?? 0);
        command.Parameters.AddWithValue("$byteCount", snapshot.BinaryContent?.Length ?? Encoding.UTF8.GetByteCount(snapshot.TextContent ?? string.Empty));
        command.Parameters.AddWithValue("$contentHash", snapshot.ContentHash);
        command.ExecuteNonQuery();
    }

    private void RefreshClipboardHistory()
    {
        var selectedId = clipboardHistoryList.SelectedItems.Count > 0 &&
            clipboardHistoryList.SelectedItems[0].Tag is long selectedItemId
                ? selectedItemId
                : (long?)null;

        clipboardHistoryList.BeginUpdate();
        clipboardHistoryList.Items.Clear();

        using var connection = OpenClipboardConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, captured_at, content_type, char_count, byte_count, text_content
            FROM clipboard_items
            ORDER BY captured_at DESC
            LIMIT 200;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var capturedAt = DateTimeOffset.Parse(reader.GetString(1)).LocalDateTime;
            var contentType = reader.GetString(2);
            var charCount = reader.GetInt64(3);
            var byteCount = reader.GetInt64(4);
            var previewText = reader.IsDBNull(5)
                ? $"{byteCount:n0} bytes"
                : CreateClipboardPreview(reader.GetString(5));

            var item = new ListViewItem(id.ToString())
            {
                Tag = id,
            };
            item.SubItems.Add(capturedAt.ToString("g"));
            item.SubItems.Add(contentType);
            item.SubItems.Add(charCount > 0 ? $"{charCount:n0} chars" : $"{byteCount:n0} bytes");
            item.SubItems.Add(previewText);
            clipboardHistoryList.Items.Add(item);
        }

        clipboardHistoryList.EndUpdate();
        SelectClipboardHistoryItem(selectedId);
        clipboardStatusValue.Text = $"Stored {clipboardHistoryList.Items.Count:n0} recent items in {ClipboardDatabasePath}";
    }

    private void ShowSelectedClipboardItem()
    {
        if (clipboardHistoryList.SelectedItems.Count == 0 ||
            clipboardHistoryList.SelectedItems[0].Tag is not long id)
        {
            clipboardPreviewText.Text = string.Empty;
            NavigateHtmlPreview(CreateHtmlPreviewMessage("Select an HTML clipboard item."), "message-select");
            ClearClipboardImagePreview();
            return;
        }

        using var connection = OpenClipboardConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT captured_at, content_type, format_summary, text_content, binary_content, byte_count, content_hash
            FROM clipboard_items
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            clipboardPreviewText.Text = string.Empty;
            NavigateHtmlPreview(CreateHtmlPreviewMessage("Clipboard item was not found."), "message-missing");
            ClearClipboardImagePreview();
            return;
        }

        var capturedAt = DateTimeOffset.Parse(reader.GetString(0)).LocalDateTime;
        var contentType = reader.GetString(1);
        var formatSummary = reader.GetString(2);
        var textContent = reader.IsDBNull(3) ? null : reader.GetString(3);
        var imageBytes = reader.IsDBNull(4) ? null : (byte[])reader["binary_content"];
        var byteCount = reader.GetInt64(5);
        var contentHash = reader.GetString(6);

        clipboardPreviewText.Text = string.Join(Environment.NewLine, new[]
        {
            $"ID:          {id}",
            $"Captured:    {capturedAt:F}",
            $"Type:        {contentType}",
            $"Size:        {byteCount:n0} bytes",
            $"Hash:        {contentHash}",
            $"Formats:     {formatSummary}",
            "",
            "Content",
            textContent ?? "Binary content stored in the database.",
        });

        if (IsImageClipboardItem(contentType, imageBytes))
        {
            ShowClipboardImagePreview(imageBytes!);
            NavigateHtmlPreview(CreateHtmlPreviewMessage("No HTML preview available for image clipboard items."), "message-image");
            clipboardPreviewTabs.SelectedTab = clipboardImagePreviewTab;
            return;
        }

        ClearClipboardImagePreview();

        if (IsHtmlClipboardItem(contentType, formatSummary, textContent))
        {
            NavigateHtmlPreview(ExtractClipboardHtml(textContent!), $"item-{id}");
            clipboardPreviewTabs.SelectedTab = clipboardHtmlPreviewTab;
            return;
        }

        NavigateHtmlPreview(CreateHtmlPreviewMessage("No HTML preview available for this clipboard item."), "message-unavailable");
        clipboardPreviewTabs.SelectedTab = clipboardDetailsPreviewTab;
    }

    private void SelectClipboardHistoryItem(long? preferredId)
    {
        if (clipboardHistoryList.Items.Count == 0)
        {
            clipboardPreviewText.Text = string.Empty;
            NavigateHtmlPreview(CreateHtmlPreviewMessage("Clipboard history is empty."), "message-empty");
            ClearClipboardImagePreview();
            return;
        }

        foreach (ListViewItem item in clipboardHistoryList.Items)
        {
            if (preferredId.HasValue && item.Tag is long id && id == preferredId.Value)
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                ShowSelectedClipboardItem();
                return;
            }
        }

        clipboardHistoryList.Items[0].Selected = true;
        clipboardHistoryList.Items[0].Focused = true;
        clipboardHistoryList.Items[0].EnsureVisible();
        ShowSelectedClipboardItem();
    }

    private static SqliteConnection OpenClipboardConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = ClipboardDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string FormatFileDropList(StringCollection files)
    {
        var lines = new List<string>();
        foreach (var file in files)
        {
            if (!string.IsNullOrEmpty(file))
            {
                lines.Add(file);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string CreateClipboardPreview(string text)
    {
        var normalized = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 160
            ? normalized
            : normalized[..160] + "...";
    }

    private static string ExtractClipboardHtml(string clipboardHtml)
    {
        var startHtml = ReadClipboardHtmlOffset(clipboardHtml, "StartHTML:");
        var endHtml = ReadClipboardHtmlOffset(clipboardHtml, "EndHTML:");

        if (startHtml >= 0 && endHtml > startHtml)
        {
            var bytes = Encoding.UTF8.GetBytes(clipboardHtml);
            if (endHtml <= bytes.Length)
            {
                return Encoding.UTF8.GetString(bytes, startHtml, endHtml - startHtml);
            }
        }

        var startFragment = clipboardHtml.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
        var endFragment = clipboardHtml.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
        if (startFragment >= 0 && endFragment > startFragment)
        {
            return clipboardHtml[startFragment..(endFragment + "<!--EndFragment-->".Length)];
        }

        return clipboardHtml;
    }

    private void NavigateHtmlPreview(string html, string key)
    {
        Directory.CreateDirectory(ClipboardPreviewFolder);
        var fileName = $"{SanitizePreviewFileName(key)}.html";
        var path = Path.Combine(ClipboardPreviewFolder, fileName);
        File.WriteAllText(path, html, Encoding.UTF8);
        clipboardHtmlPreviewBrowser.Navigate(path);
    }

    private static string SanitizePreviewFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '-' : character);
        }

        return builder.Length == 0 ? "preview" : builder.ToString();
    }

    private static bool IsHtmlClipboardItem(string contentType, string formatSummary, string? textContent) =>
        !string.IsNullOrWhiteSpace(textContent) &&
        (contentType.Equals("HTML", StringComparison.OrdinalIgnoreCase) ||
            formatSummary.Contains(DataFormats.Html, StringComparison.OrdinalIgnoreCase) ||
            textContent.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            textContent.Contains("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase));

    private static bool IsImageClipboardItem(string contentType, byte[]? imageBytes) =>
        imageBytes is { Length: > 0 } && contentType.Equals("Image", StringComparison.OrdinalIgnoreCase);

    private void ShowClipboardImagePreview(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        using var loadedImage = Image.FromStream(stream);
        var previewImage = new Bitmap(loadedImage);
        var oldImage = clipboardImagePreview.Image;
        clipboardImagePreview.Image = previewImage;
        oldImage?.Dispose();
    }

    private void ClearClipboardImagePreview()
    {
        var oldImage = clipboardImagePreview.Image;
        clipboardImagePreview.Image = null;
        oldImage?.Dispose();
    }

    private static int ReadClipboardHtmlOffset(string clipboardHtml, string label)
    {
        var labelIndex = clipboardHtml.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (labelIndex < 0)
        {
            return -1;
        }

        var valueStart = labelIndex + label.Length;
        var valueEnd = valueStart;
        while (valueEnd < clipboardHtml.Length && char.IsDigit(clipboardHtml[valueEnd]))
        {
            valueEnd++;
        }

        return int.TryParse(clipboardHtml[valueStart..valueEnd], out var offset)
            ? offset
            : -1;
    }

    private static string CreateHtmlPreviewMessage(string message) =>
        """
        <!doctype html>
        <html>
        <head>
            <meta charset="utf-8">
            <style>
                body {
                    margin: 0;
                    padding: 18px;
                    color: #414652;
                    background: #ffffff;
                    font-family: "Segoe UI", Arial, sans-serif;
                    font-size: 14px;
                }
            </style>
        </head>
        <body>
        """
        + WebUtility.HtmlEncode(message)
        + """
        </body>
        </html>
        """;

    private static void OpenClipboardDatabaseFolder()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ClipboardDatabasePath)!);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.GetDirectoryName(ClipboardDatabasePath)!,
            UseShellExecute = true,
        });
    }

    private static string GetRootLevelFolder()
    {
        var candidates = new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
        };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "cKit.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Environment.CurrentDirectory;
    }

    private sealed record ClipboardSnapshot(
        string ContentType,
        string FormatSummary,
        string? TextContent,
        byte[]? BinaryContent,
        string ContentHash,
        string Signature)
    {
        public static ClipboardSnapshot Text(string contentType, string formatSummary, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            return new ClipboardSnapshot(contentType, formatSummary, text, null, hash, $"{contentType}:{hash}");
        }

        public static ClipboardSnapshot Binary(string contentType, string formatSummary, byte[] bytes, string description)
        {
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            return new ClipboardSnapshot(contentType, formatSummary, description, bytes, hash, $"{contentType}:{hash}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
