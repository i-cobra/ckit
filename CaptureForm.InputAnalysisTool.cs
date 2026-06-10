using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace CKitScreenCapture;

internal sealed partial class CaptureForm
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;
    private static readonly string InputAnalysisDatabasePath = Path.Combine(GetRootLevelFolder(), "analysis.db");
    private static readonly KeyDefinition[][] KeyboardLayout =
    [
        [
            new("Esc", Keys.Escape), new("1", Keys.D1), new("2", Keys.D2), new("3", Keys.D3), new("4", Keys.D4),
            new("5", Keys.D5), new("6", Keys.D6), new("7", Keys.D7), new("8", Keys.D8), new("9", Keys.D9),
            new("0", Keys.D0), new("Back", Keys.Back, 70),
        ],
        [
            new("Tab", Keys.Tab, 56), new("Q", Keys.Q), new("W", Keys.W), new("E", Keys.E), new("R", Keys.R),
            new("T", Keys.T), new("Y", Keys.Y), new("U", Keys.U), new("I", Keys.I), new("O", Keys.O),
            new("P", Keys.P), new("Enter", Keys.Return, 70),
        ],
        [
            new("Caps", Keys.CapsLock, 66), new("A", Keys.A), new("S", Keys.S), new("D", Keys.D), new("F", Keys.F),
            new("G", Keys.G), new("H", Keys.H), new("J", Keys.J), new("K", Keys.K), new("L", Keys.L),
            new(";", Keys.OemSemicolon), new("'", Keys.OemQuotes),
        ],
        [
            new("Shift", Keys.ShiftKey, 78), new("Z", Keys.Z), new("X", Keys.X), new("C", Keys.C), new("V", Keys.V),
            new("B", Keys.B), new("N", Keys.N), new("M", Keys.M), new(",", Keys.Oemcomma), new(".", Keys.OemPeriod),
            new("/", Keys.OemQuestion), new("Shift", Keys.RShiftKey, 78),
        ],
        [
            new("Ctrl", Keys.ControlKey, 58), new("Alt", Keys.Menu, 58), new("Space", Keys.Space, 260),
            new("Left", Keys.Left, 58), new("Up", Keys.Up, 58), new("Down", Keys.Down, 58), new("Right", Keys.Right, 58),
        ],
    ];

    private void BuildInputAnalysisWorkspace()
    {
        inputAnalysisWorkspace.Dock = DockStyle.Fill;
        inputAnalysisWorkspace.BackColor = FluentAppBackground;
        inputAnalysisWorkspace.Visible = false;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = FluentSurface,
            WrapContents = false,
        };

        ConfigureButton(resetInputAnalysisButton, "Reset Counts", "\uE777");
        ConfigureButton(openAnalysisDbFolderButton, "Open DB Folder", "\uE8B7");
        resetInputAnalysisButton.Click += (_, _) => ResetInputAnalysisCounts();
        openAnalysisDbFolderButton.Click += (_, _) => OpenInputAnalysisDatabaseFolder();
        toolbar.Controls.Add(resetInputAnalysisButton);
        toolbar.Controls.Add(openAnalysisDbFolderButton);

        inputAnalysisStatusValue.AutoSize = false;
        inputAnalysisStatusValue.Width = 520;
        inputAnalysisStatusValue.Height = 34;
        inputAnalysisStatusValue.Margin = new Padding(8, 0, 0, 0);
        inputAnalysisStatusValue.TextAlign = ContentAlignment.MiddleLeft;
        inputAnalysisStatusValue.ForeColor = FluentTextSecondary;
        inputAnalysisStatusValue.Text = $"Database: {InputAnalysisDatabasePath}";
        toolbar.Controls.Add(inputAnalysisStatusValue);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            BackColor = FluentAppBackground,
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
        };

        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));

        stack.Controls.Add(CreateInputSummarySection(), 0, 0);
        stack.Controls.Add(CreateKeyboardSection(), 0, 1);
        stack.Controls.Add(CreateMouseSection(), 0, 2);
        content.Controls.Add(stack);

        LoadInputAnalysisCountsFromDatabase();

        inputAnalysisWorkspace.Controls.Add(content);
        inputAnalysisWorkspace.Controls.Add(toolbar);
    }

    private Panel CreateInputSummarySection()
    {
        var row = CreateFluentCard();
        row.Padding = new Padding(14, 10, 14, 10);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.Controls.Add(CreateSummaryMetric("Key Presses", keyPressCountValue), 0, 0);
        grid.Controls.Add(CreateSummaryMetric("Mouse Clicks", mouseClickCountValue), 1, 0);
        grid.Controls.Add(CreateSummaryMetric("Last Input", lastInputValue), 2, 0);

        row.Controls.Add(grid);
        return row;
    }

    private static Panel CreateSummaryMetric(string title, Label valueLabel)
    {
        var metric = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = Color.Transparent,
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = title,
            Font = FluentFont(9, FontStyle.Bold),
            ForeColor = FluentTextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "0";
        valueLabel.Font = FluentFont(15, FontStyle.Bold);
        valueLabel.ForeColor = FluentText;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;

        metric.Controls.Add(valueLabel);
        metric.Controls.Add(titleLabel);
        return metric;
    }

    private Panel CreateKeyboardSection()
    {
        var section = CreateVisualSection("Keyboard");
        var keyboard = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 245,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0),
        };

        foreach (var row in KeyboardLayout)
        {
            var keyRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 5),
            };

            foreach (var key in row)
            {
                keyRow.Controls.Add(CreateKeyTile(key));
            }

            keyboard.Controls.Add(keyRow);
        }

        section.Controls.Add(keyboard);
        return section;
    }

    private Panel CreateMouseSection()
    {
        var section = CreateVisualSection("Mouse");
        var mouseBody = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 112,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 0),
        };

        mouseBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mouseBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mouseBody.Controls.Add(CreateMouseButtonTile("Left Button", leftMouseClickCountValue), 0, 0);
        mouseBody.Controls.Add(CreateMouseButtonTile("Right Button", rightMouseClickCountValue), 1, 0);

        section.Controls.Add(mouseBody);
        return section;
    }

    private static Panel CreateVisualSection(string title)
    {
        var section = CreateFluentCard();
        section.Padding = new Padding(14, 10, 14, 10);

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = title,
            Font = FluentFont(11, FontStyle.Bold),
            ForeColor = FluentText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        section.Controls.Add(titleLabel);
        return section;
    }

    private Panel CreateKeyTile(KeyDefinition key)
    {
        var tile = new Panel
        {
            Width = key.Width,
            Height = 42,
            BackColor = FluentSurface,
            Margin = new Padding(0, 0, 5, 0),
            Padding = new Padding(4),
        };

        var keyNameLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            Text = key.Label,
            Font = FluentFont(8, FontStyle.Bold),
            ForeColor = FluentText,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var countLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "0",
            Font = FluentFont(9, FontStyle.Bold),
            ForeColor = FluentAccent,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        tile.Controls.Add(countLabel);
        tile.Controls.Add(keyNameLabel);

        if (key.Key.HasValue)
        {
            keyCounts[key.Key.Value] = 0;
            keyCountLabels[key.Key.Value] = countLabel;
        }

        return tile;
    }

    private static Panel CreateMouseButtonTile(string title, Label countLabel)
    {
        var tile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentSurface,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12),
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = title,
            Font = FluentFont(10, FontStyle.Bold),
            ForeColor = FluentText,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        countLabel.Dock = DockStyle.Fill;
        countLabel.Text = "0";
        countLabel.Font = FluentFont(18, FontStyle.Bold);
        countLabel.ForeColor = FluentAccent;
        countLabel.TextAlign = ContentAlignment.MiddleCenter;

        tile.Controls.Add(countLabel);
        tile.Controls.Add(titleLabel);
        return tile;
    }

    private void ResetInputAnalysisCounts()
    {
        keyPressCount = 0;
        mouseClickCount = 0;
        leftMouseClickCount = 0;
        rightMouseClickCount = 0;
        foreach (var key in keyCountLabels.Keys)
        {
            keyCounts[key] = 0;
            keyCountLabels[key].Text = "0";
        }

        keyPressCountValue.Text = "0";
        mouseClickCountValue.Text = "0";
        leftMouseClickCountValue.Text = "0";
        rightMouseClickCountValue.Text = "0";
        lastInputValue.Text = "Waiting...";
        statusLabel.Text = "Input analysis counts were reset.";
        _ = SaveInputAnalysisEventAsync("Reset", "Counts Reset", null, null);
    }

    private void LoadInputAnalysisCountsFromDatabase()
    {
        foreach (var key in keyCountLabels.Keys)
        {
            keyCounts[key] = 0;
            keyCountLabels[key].Text = "0";
        }

        keyPressCount = 0;
        mouseClickCount = 0;
        leftMouseClickCount = 0;
        rightMouseClickCount = 0;
        lastInputValue.Text = "Waiting...";

        try
        {
            using var connection = OpenInputAnalysisConnection();

            using (var totalsCommand = connection.CreateCommand())
            {
                totalsCommand.CommandText =
                    """
                    SELECT key_press_total, mouse_click_total, left_mouse_total, right_mouse_total, event_type, input_name
                    FROM input_events
                    ORDER BY id DESC
                    LIMIT 1;
                    """;

                using var reader = totalsCommand.ExecuteReader();
                if (reader.Read())
                {
                    keyPressCount = reader.GetInt32(0);
                    mouseClickCount = reader.GetInt32(1);
                    leftMouseClickCount = reader.GetInt32(2);
                    rightMouseClickCount = reader.GetInt32(3);
                    lastInputValue.Text = $"{reader.GetString(4)} {reader.GetString(5)}";
                }
            }

            using (var keyCommand = connection.CreateCommand())
            {
                keyCommand.CommandText =
                    """
                    SELECT key_code, COUNT(*)
                    FROM input_events
                    WHERE event_type = 'Key'
                        AND key_code IS NOT NULL
                        AND id > COALESCE((
                            SELECT MAX(id)
                            FROM input_events
                            WHERE event_type = 'Reset'
                        ), 0)
                    GROUP BY key_code;
                    """;

                using var reader = keyCommand.ExecuteReader();
                while (reader.Read())
                {
                    var key = (Keys)reader.GetInt32(0);
                    var count = reader.GetInt32(1);
                    if (!keyCountLabels.TryGetValue(key, out var countLabel))
                    {
                        continue;
                    }

                    keyCounts[key] = count;
                    countLabel.Text = count.ToString();
                }
            }

            keyPressCountValue.Text = keyPressCount.ToString();
            mouseClickCountValue.Text = mouseClickCount.ToString();
            leftMouseClickCountValue.Text = leftMouseClickCount.ToString();
            rightMouseClickCountValue.Text = rightMouseClickCount.ToString();
            statusLabel.Text = "Input analysis counts were loaded from the database.";
        }
        catch
        {
            keyPressCountValue.Text = "0";
            mouseClickCountValue.Text = "0";
            leftMouseClickCountValue.Text = "0";
            rightMouseClickCountValue.Text = "0";
            statusLabel.Text = "Input analysis database could not be loaded.";
        }
    }

    private void SetInputAnalysisEnabled(bool enabled)
    {
        if (enabled)
        {
            StartInputAnalysis();
            return;
        }

        StopInputAnalysis();
    }

    private void StartInputAnalysis()
    {
        if (keyboardHookHandle != IntPtr.Zero || mouseHookHandle != IntPtr.Zero)
        {
            return;
        }

        keyboardHookProc = KeyboardHookCallback;
        mouseHookProc = MouseHookCallback;
        var moduleHandle = GetModuleHandle(null);
        keyboardHookHandle = SetWindowsHookEx(WhKeyboardLl, keyboardHookProc, moduleHandle, 0);
        mouseHookHandle = SetWindowsHookEx(WhMouseLl, mouseHookProc, moduleHandle, 0);

        if (keyboardHookHandle == IntPtr.Zero || mouseHookHandle == IntPtr.Zero)
        {
            StopInputAnalysis();
            statusLabel.Text = "Input analysis hooks are not available.";
            return;
        }

        statusLabel.Text = "Input analysis is active.";
    }

    private void StopInputAnalysis()
    {
        if (keyboardHookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(keyboardHookHandle);
            keyboardHookHandle = IntPtr.Zero;
        }

        if (mouseHookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(mouseHookHandle);
            mouseHookHandle = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == WmKeyDown || wParam == WmSysKeyDown))
        {
            var key = (Keys)Marshal.ReadInt32(lParam);
            keyPressCount++;
            keyPressCountValue.Text = keyPressCount.ToString();
            IncrementKeyCount(key);
            var keyName = FormatKeyName(key);
            lastInputValue.Text = $"Key {keyName}";
            _ = SaveInputAnalysisEventAsync("Key", keyName, (int)NormalizeKey(key), null);
        }

        return CallNextHookEx(keyboardHookHandle, code, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            if (wParam == WmLButtonDown)
            {
                leftMouseClickCount++;
                mouseClickCount++;
                leftMouseClickCountValue.Text = leftMouseClickCount.ToString();
                mouseClickCountValue.Text = mouseClickCount.ToString();
                lastInputValue.Text = "Mouse Left";
                _ = SaveInputAnalysisEventAsync("Mouse", "Left", null, "Left");
            }
            else if (wParam == WmRButtonDown)
            {
                rightMouseClickCount++;
                mouseClickCount++;
                rightMouseClickCountValue.Text = rightMouseClickCount.ToString();
                mouseClickCountValue.Text = mouseClickCount.ToString();
                lastInputValue.Text = "Mouse Right";
                _ = SaveInputAnalysisEventAsync("Mouse", "Right", null, "Right");
            }
        }

        return CallNextHookEx(mouseHookHandle, code, wParam, lParam);
    }

    private void IncrementKeyCount(Keys key)
    {
        var normalizedKey = NormalizeKey(key);
        if (!keyCounts.ContainsKey(normalizedKey))
        {
            return;
        }

        keyCounts[normalizedKey]++;
        keyCountLabels[normalizedKey].Text = keyCounts[normalizedKey].ToString();
    }

    private static Keys NormalizeKey(Keys key) =>
        key switch
        {
            Keys.LShiftKey or Keys.RShiftKey => Keys.ShiftKey,
            Keys.LControlKey or Keys.RControlKey => Keys.ControlKey,
            Keys.LMenu or Keys.RMenu => Keys.Menu,
            _ => key,
        };

    private static string FormatKeyName(Keys key) =>
        NormalizeKey(key) switch
        {
            Keys.Return => "Enter",
            Keys.Space => "Space",
            Keys.ControlKey => "Ctrl",
            Keys.Menu => "Alt",
            Keys.ShiftKey => "Shift",
            _ => key.ToString(),
        };

    private static void InitializeInputAnalysisDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InputAnalysisDatabasePath)!);

        using var connection = OpenInputAnalysisConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS input_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                captured_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                input_name TEXT NOT NULL,
                key_code INTEGER NULL,
                mouse_button TEXT NULL,
                key_press_total INTEGER NOT NULL,
                mouse_click_total INTEGER NOT NULL,
                left_mouse_total INTEGER NOT NULL,
                right_mouse_total INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_input_events_captured_at
                ON input_events(captured_at DESC);
            """;
        command.ExecuteNonQuery();
    }

    private Task SaveInputAnalysisEventAsync(string eventType, string inputName, int? keyCode, string? mouseButton)
    {
        var snapshot = new InputAnalysisEventSnapshot(
            DateTimeOffset.Now.ToString("O"),
            eventType,
            inputName,
            keyCode,
            mouseButton,
            keyPressCount,
            mouseClickCount,
            leftMouseClickCount,
            rightMouseClickCount);

        return Task.Run(() => SaveInputAnalysisEvent(snapshot));
    }

    private static void SaveInputAnalysisEvent(InputAnalysisEventSnapshot snapshot)
    {
        using var connection = OpenInputAnalysisConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO input_events (
                captured_at,
                event_type,
                input_name,
                key_code,
                mouse_button,
                key_press_total,
                mouse_click_total,
                left_mouse_total,
                right_mouse_total
            )
            VALUES (
                $capturedAt,
                $eventType,
                $inputName,
                $keyCode,
                $mouseButton,
                $keyPressTotal,
                $mouseClickTotal,
                $leftMouseTotal,
                $rightMouseTotal
            );
            """;

        command.Parameters.AddWithValue("$capturedAt", snapshot.CapturedAt);
        command.Parameters.AddWithValue("$eventType", snapshot.EventType);
        command.Parameters.AddWithValue("$inputName", snapshot.InputName);
        command.Parameters.AddWithValue("$keyCode", (object?)snapshot.KeyCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$mouseButton", (object?)snapshot.MouseButton ?? DBNull.Value);
        command.Parameters.AddWithValue("$keyPressTotal", snapshot.KeyPressTotal);
        command.Parameters.AddWithValue("$mouseClickTotal", snapshot.MouseClickTotal);
        command.Parameters.AddWithValue("$leftMouseTotal", snapshot.LeftMouseTotal);
        command.Parameters.AddWithValue("$rightMouseTotal", snapshot.RightMouseTotal);
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenInputAnalysisConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = InputAnalysisDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void OpenInputAnalysisDatabaseFolder()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InputAnalysisDatabasePath)!);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.GetDirectoryName(InputAnalysisDatabasePath)!,
            UseShellExecute = true,
        });
    }

    private readonly record struct InputAnalysisEventSnapshot(
        string CapturedAt,
        string EventType,
        string InputName,
        int? KeyCode,
        string? MouseButton,
        int KeyPressTotal,
        int MouseClickTotal,
        int LeftMouseTotal,
        int RightMouseTotal);

    private readonly record struct KeyDefinition(string Label, Keys? Key, int Width = 46);

    private delegate IntPtr LowLevelHookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelHookProc callback, IntPtr instanceHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
