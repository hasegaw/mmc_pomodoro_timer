using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace PomodoroTimer;

internal sealed class MainForm : Form
{
    private const string WindowTitle = "Pomodoro Timer";
    private const string AppIconResourceName = "PomodoroTimer.AppIcon.ico";
    private const int NormalClientLength = 500;
    private const int CompactClientLength = 250;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;
    private static readonly Color ChromaGreen = Color.FromArgb(0, 255, 0);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

    private readonly Label _timeLabel;
    private readonly System.Windows.Forms.Integration.ElementHost _timeEditorHost;
    private readonly System.Windows.Controls.TextBox _timeEditor;
    private readonly Button _resetButton;
    private readonly Button _startStopButton;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly System.Windows.Forms.Timer _hoverTimer;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _playAlarmMenuItem;
    private readonly ToolStripMenuItem _alwaysOnTopMenuItem;
    private readonly ToolStripMenuItem _compactModeMenuItem;
    private readonly AlertMediaPlayer _player = new();
    private readonly string _iniPath = Path.Combine(AppContext.BaseDirectory, "pomodoro.ini");
    private readonly string? _alertPath = FindAlertPath();
    private readonly IniSettings _settings;
    private readonly Icon? _windowIcon;

    private Image? _backgroundImage;
    private Font _displayFont;
    private Font? _fittedDisplayFont;
    private int _lastDisplayTextLength = -1;
    private TimeSpan _startDuration;
    private TimeSpan _remaining;
    private DateTime _deadlineUtc;
    private bool _running;

    public MainForm()
    {
        _settings = IniSettings.Load(_iniPath);
        _startDuration = _settings.StartDuration;
        _remaining = _startDuration;
        _displayFont = CreateFont(_settings.FontFamily, _settings.FontSize, _settings.FontStyle);
        _windowIcon = LoadWindowIcon();

        Text = WindowTitle;
        Name = "PomodoroTimerWindow";
        ShowIcon = true;
        ShowInTaskbar = true;
        if (_windowIcon is not null)
        {
            Icon = _windowIcon;
        }
        ClientSize = new Size(500, 500);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = ChromaGreen;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = _settings.AlwaysOnTop;
        DoubleBuffered = true;

        _timeLabel = new Label
        {
            Name = "TimerDisplay",
            Bounds = new Rectangle(20, 155, 460, 190),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.IBeam,
            ContextMenuStrip = null
        };
        _timeLabel.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                BeginTimeEdit();
            }
        };

        _timeEditor = new System.Windows.Controls.TextBox
        {
            Name = "TimerEditor",
            TextAlignment = System.Windows.TextAlignment.Center,
            VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
            BorderThickness = new System.Windows.Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            CaretBrush = System.Windows.Media.Brushes.White,
            MaxLength = 12,
            Padding = new System.Windows.Thickness(0)
        };
        _timeEditor.KeyDown += TimeEditorOnKeyDown;

        _timeEditorHost = new System.Windows.Forms.Integration.ElementHost
        {
            Name = "TimerEditorHost",
            Bounds = new Rectangle(20, 155, 460, 190),
            BackColorTransparent = true,
            Child = _timeEditor,
            Visible = false
        };

        _resetButton = CreateOverlayButton("リセット", new Point(105, 400));
        _resetButton.Click += (_, _) => ResetTimer();
        _startStopButton = CreateOverlayButton("スタート", new Point(265, 400));
        _startStopButton.Click += (_, _) => ToggleTimer();

        Controls.AddRange([_timeLabel, _timeEditorHost, _resetButton, _startStopButton]);

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("フォント...", null, (_, _) => ChooseFont());
        _playAlarmMenuItem = new ToolStripMenuItem("アラーム音を再生")
        {
            CheckOnClick = true,
            Checked = _settings.PlayAlarm
        };
        _contextMenu.Items.Add(_playAlarmMenuItem);
        _alwaysOnTopMenuItem = new ToolStripMenuItem("最前面に表示")
        {
            CheckOnClick = true,
            Checked = _settings.AlwaysOnTop
        };
        _alwaysOnTopMenuItem.CheckedChanged += (_, _) => TopMost = _alwaysOnTopMenuItem.Checked;
        _contextMenu.Items.Add(_alwaysOnTopMenuItem);
        _compactModeMenuItem = new ToolStripMenuItem("縮小表示")
        {
            CheckOnClick = true,
            Checked = _settings.CompactMode
        };
        _compactModeMenuItem.CheckedChanged += (_, _) => ApplyCompactMode(_compactModeMenuItem.Checked);
        _contextMenu.Items.Add(_compactModeMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("終了", null, (_, _) => Close());
        ContextMenuStrip = _contextMenu;
        _timeLabel.ContextMenuStrip = _contextMenu;
        _timeEditorHost.ContextMenuStrip = _contextMenu;

        _uiTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _uiTimer.Tick += (_, _) => UpdateCountdown();
        _uiTimer.Start();

        _hoverTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _hoverTimer.Tick += (_, _) => UpdateOverlayVisibility();
        _hoverTimer.Start();

        Load += (_, _) => RestoreWindowLocation();
        FormClosing += (_, _) => SaveSettings();
        FormClosed += (_, _) => DisposeResources();
        Resize += (_, _) =>
        {
            ApplyResponsiveLayout();
            Invalidate();
        };

        LoadBackgroundImage();
        ApplyCompactMode(_settings.CompactMode);
        UpdateDisplay();
        UpdateOverlayVisibility();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(ChromaGreen);
        if (_backgroundImage is null)
        {
            return;
        }

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float scale = Math.Max((float)ClientSize.Width / _backgroundImage.Width,
            (float)ClientSize.Height / _backgroundImage.Height);
        float width = _backgroundImage.Width * scale;
        float height = _backgroundImage.Height * scale;
        float x = (ClientSize.Width - width) / 2f;
        float y = (ClientSize.Height - height) / 2f;
        e.Graphics.DrawImage(_backgroundImage, x, y, width, height);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }

    private static Button CreateOverlayButton(string text, Point location) => new()
    {
        Text = text,
        Location = location,
        Size = new Size(130, 46),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(210, 20, 20, 20),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 12, FontStyle.Bold),
        UseVisualStyleBackColor = false,
        Visible = false
    };

    private void ToggleTimer()
    {
        if (_running)
        {
            StopTimer();
        }
        else
        {
            StartTimer();
        }
    }

    private void StartTimer()
    {
        if (_running)
        {
            return;
        }

        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = _startDuration;
        }

        _deadlineUtc = DateTime.UtcNow + _remaining;
        _running = true;
        UpdateStartStopButton();
        UpdateDisplay();
    }

    private void StopTimer()
    {
        if (!_running)
        {
            return;
        }

        UpdateCountdown();
        _running = false;
        UpdateStartStopButton();
        UpdateDisplay();
    }

    private void ResetTimer()
    {
        _running = false;
        _remaining = _startDuration;
        UpdateStartStopButton();
        UpdateDisplay();
    }

    private void UpdateCountdown()
    {
        if (!_running)
        {
            return;
        }

        _remaining = _deadlineUtc - DateTime.UtcNow;
        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            _running = false;
            UpdateStartStopButton();
            if (_playAlarmMenuItem.Checked)
            {
                _player.Play(_alertPath);
            }
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        string text = TimerText.Format(_remaining);
        _timeLabel.Text = text;
        if (_lastDisplayTextLength != text.Length)
        {
            Font nextFont = FitFont(text, _timeLabel.ClientSize, _displayFont);
            _timeLabel.Font = nextFont;
            _fittedDisplayFont?.Dispose();
            _fittedDisplayFont = nextFont;
            _lastDisplayTextLength = text.Length;
        }
    }

    private static Font FitFont(string text, Size bounds, Font source, float minimumSize = 8f)
    {
        float size = source.Size;
        using var probe = new Font(source.FontFamily, size, source.Style);
        Size measured = TextRenderer.MeasureText(text, probe, bounds, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        if (measured.Width <= bounds.Width - 10 && measured.Height <= bounds.Height - 10)
        {
            return new Font(source.FontFamily, size, source.Style);
        }

        float scale = Math.Min((bounds.Width - 10f) / Math.Max(1, measured.Width),
            (bounds.Height - 10f) / Math.Max(1, measured.Height));
        return new Font(source.FontFamily, Math.Max(minimumSize, size * scale), source.Style);
    }

    private void ApplyEditorFont(Font font)
    {
        _timeEditor.FontFamily = new System.Windows.Media.FontFamily(font.FontFamily.Name);
        _timeEditor.FontSize = font.SizeInPoints * 96.0 / 72.0;
        _timeEditor.FontWeight = font.Bold
            ? System.Windows.FontWeights.Bold
            : System.Windows.FontWeights.Normal;
        _timeEditor.FontStyle = font.Italic
            ? System.Windows.FontStyles.Italic
            : System.Windows.FontStyles.Normal;

        var decorations = new System.Windows.TextDecorationCollection();
        if (font.Underline)
        {
            decorations.Add(System.Windows.TextDecorations.Underline[0]);
        }
        if (font.Strikeout)
        {
            decorations.Add(System.Windows.TextDecorations.Strikethrough[0]);
        }
        _timeEditor.TextDecorations = decorations;
    }

    private void BeginTimeEdit()
    {
        if (_running || _timeEditorHost.Visible)
        {
            return;
        }

        _timeEditor.Text = TimerText.Format(_startDuration);
        using Font nextFont = FitFont(_timeEditor.Text, _timeEditorHost.ClientSize, _displayFont);
        ApplyEditorFont(nextFont);
        _timeEditorHost.Visible = true;
        _timeEditorHost.BringToFront();
        _timeEditorHost.Focus();
        _timeEditor.Focus();
        _timeEditor.SelectAll();
    }

    private void TimeEditorOnKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            CancelTimeEdit();
            return;
        }

        if (e.Key != System.Windows.Input.Key.Enter && e.Key != System.Windows.Input.Key.Return)
        {
            return;
        }

        e.Handled = true;
        if (!TimerText.TryParse(_timeEditor.Text, out TimeSpan value))
        {
            MessageBox.Show(this, "時刻は MM:SS、HH:MM:SS、MMSS、HHMMSS のいずれかで、1秒以上を入力してください。",
                WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _timeEditor.Focus();
            _timeEditor.SelectAll();
            return;
        }

        SetTimer(value);
    }

    private void CancelTimeEdit()
    {
        if (!_timeEditorHost.Visible)
        {
            return;
        }

        _timeEditorHost.Visible = false;
        UpdateDisplay();
    }

    internal void ExecuteCommand(AppCommand command)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ExecuteCommand(command));
            return;
        }

        if (command.Duration is TimeSpan duration)
        {
            SetTimer(duration);
        }

        switch (command.Kind)
        {
            case AppCommandKind.Start:
                StartTimer();
                break;
            case AppCommandKind.Stop:
                StopTimer();
                break;
            case AppCommandKind.Click:
                ToggleTimer();
                break;
            case AppCommandKind.Reset:
                ResetTimer();
                break;
            case AppCommandKind.Set:
                break;
        }
    }

    private void SetTimer(TimeSpan duration)
    {
        _running = false;
        _startDuration = duration;
        _remaining = duration;
        _timeEditorHost.Visible = false;
        UpdateStartStopButton();
        UpdateDisplay();
    }

    private void ChooseFont()
    {
        using var dialog = new FontDialog
        {
            Font = _displayFont,
            FontMustExist = true,
            ShowColor = false,
            ShowEffects = true,
            MinSize = 8,
            MaxSize = 240
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _displayFont.Dispose();
        _displayFont = new Font(dialog.Font.FontFamily, dialog.Font.Size, dialog.Font.Style);
        _lastDisplayTextLength = -1;
        UpdateDisplay();
    }

    private void UpdateOverlayVisibility()
    {
        Point clientPoint = PointToClient(Cursor.Position);
        bool visible = ClientRectangle.Contains(clientPoint) && !_timeEditorHost.Visible;
        _resetButton.Visible = visible;
        _startStopButton.Visible = visible;
    }

    private void UpdateStartStopButton()
    {
        _startStopButton.Text = _running ? "ストップ" : "スタート";
        FitOverlayButtonFont(_startStopButton);
    }

    private void ApplyCompactMode(bool compact)
    {
        int length = compact ? CompactClientLength : NormalClientLength;
        ClientSize = new Size(length, length);
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (_timeLabel is null)
        {
            return;
        }

        float scale = Math.Min((float)ClientSize.Width / NormalClientLength,
            (float)ClientSize.Height / NormalClientLength);
        _timeLabel.Bounds = ScaleBounds(new Rectangle(20, 155, 460, 190), scale);
        _timeEditorHost.Bounds = _timeLabel.Bounds;
        _resetButton.Bounds = ScaleBounds(new Rectangle(105, 400, 130, 46), scale);
        _startStopButton.Bounds = ScaleBounds(new Rectangle(265, 400, 130, 46), scale);
        FitOverlayButtonFont(_resetButton);
        FitOverlayButtonFont(_startStopButton);
        _lastDisplayTextLength = -1;
        UpdateDisplay();
    }

    private void FitOverlayButtonFont(Button button)
    {
        float scale = Math.Min((float)ClientSize.Width / NormalClientLength,
            (float)ClientSize.Height / NormalClientLength);
        using var source = new Font("Segoe UI", Math.Max(6f, 12f * scale), FontStyle.Bold);
        Font fitted = FitFont(button.Text, button.ClientSize, source, 5f);
        Font previous = button.Font;
        button.Font = fitted;
        previous.Dispose();
    }

    private static Rectangle ScaleBounds(Rectangle bounds, float scale) => new(
        (int)Math.Round(bounds.X * scale),
        (int)Math.Round(bounds.Y * scale),
        Math.Max(1, (int)Math.Round(bounds.Width * scale)),
        Math.Max(1, (int)Math.Round(bounds.Height * scale)));

    private void LoadBackgroundImage()
    {
        string directory = AppContext.BaseDirectory;
        string? path = new[] { "background.png", "background.jpeg", "background.jpg" }
            .Select(name => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);

        if (path is null)
        {
            return;
        }

        try
        {
            using var source = Image.FromFile(path);
            _backgroundImage = new Bitmap(source);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException)
        {
            _backgroundImage = null;
        }
    }

    private static string? FindAlertPath()
    {
        string directory = AppContext.BaseDirectory;
        return new[] { "timer.mp4", "timer.mp3", "timer.wav" }
            .Select(name => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    private static Icon? LoadWindowIcon()
    {
        try
        {
            using Stream? stream = typeof(MainForm).Assembly.GetManifestResourceStream(AppIconResourceName);
            if (stream is null)
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }

            using var source = new Icon(stream);
            return (Icon)source.Clone();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
    }

    private void RestoreWindowLocation()
    {
        if (_settings.WindowLocation is not Point location)
        {
            return;
        }

        var proposed = new Rectangle(location, Size);
        if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(proposed)))
        {
            StartPosition = FormStartPosition.Manual;
            Location = location;
        }
    }

    private void SaveSettings()
    {
        if (WindowState == FormWindowState.Normal)
        {
            _settings.WindowLocation = Location;
        }

        _settings.StartDuration = _startDuration;
        _settings.FontFamily = _displayFont.FontFamily.Name;
        _settings.FontSize = _displayFont.Size;
        _settings.FontStyle = _displayFont.Style;
        _settings.PlayAlarm = _playAlarmMenuItem.Checked;
        _settings.AlwaysOnTop = _alwaysOnTopMenuItem.Checked;
        _settings.CompactMode = _compactModeMenuItem.Checked;

        try
        {
            _settings.Save(_iniPath);
        }
        catch (IOException)
        {
            // The application remains usable even if its directory is read-only.
        }
        catch (UnauthorizedAccessException)
        {
            // The application remains usable even if its directory is read-only.
        }
    }

    private void DisposeResources()
    {
        _uiTimer.Dispose();
        _hoverTimer.Dispose();
        _backgroundImage?.Dispose();
        _fittedDisplayFont?.Dispose();
        _displayFont.Dispose();
        Icon = null;
        _windowIcon?.Dispose();
        _player.Dispose();
        _contextMenu.Dispose();
    }

    private static Font CreateFont(string family, float size, FontStyle style)
    {
        try
        {
            return new Font(family, size, style);
        }
        catch (ArgumentException)
        {
            return new Font("Segoe UI", 72, FontStyle.Bold);
        }
    }
}
