using System.Diagnostics;
using HotkeyManager.Config;
using HotkeyManager.Core;

namespace HotkeyManager.Settings;

/// <summary>
/// 图形化快捷键设置：选择 EXE 添加应用、点击录制、移除、拖拽排序；每次操作立即保存。
/// </summary>
internal sealed class HotkeyListForm : Form
{
    private const int IconColumnIndex = 0;
    private const int AppColumnIndex = 1;
    private const int HotkeyColumnIndex = 2;

    private readonly ConfigManager _configManager;
    private readonly Action<bool> _setRecording;
    private readonly Action _onConfigSaved;
    private readonly HotkeyRecorder _recorder = new();
    private readonly EmptyMessageGrid _grid = new();
    private readonly Button _removeButton = new();
    private readonly Label _statusLabel = new();
    private readonly ToolTip _buttonTips = new();
    private readonly List<Image> _rowIcons = new();

    private AppConfig _config = new();
    private int? _recordingIndex;
    private int _dragRowIndex = -1;
    private int _dropRowIndex = -1;
    private Point _dragStart;
    private bool _closing;

    public HotkeyListForm(
        ConfigManager configManager,
        Action<bool> setRecording,
        Action onConfigSaved)
    {
        _configManager = configManager;
        _setRecording = setRecording;
        _onConfigSaved = onConfigSaved;

        Text = "设置快捷键";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(430, 360);
        MinimumSize = new Size(380, 300);
        ShowIcon = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(243, 243, 243);

        BuildUi();

        _recorder.Completed += RecorderCompleted;
        _recorder.InputRejected += message =>
        {
            System.Media.SystemSounds.Beep.Play();
            SetStatus(message, isError: true);
        };

        Shown += (_, _) => ReloadFromDisk();
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            DisposeRowIcons();
            _buttonTips.Dispose();
        };
    }

    private void BuildUi()
    {
        ConfigureGrid();

        var addButton = new Button
        {
            Text = "+",
            AccessibleName = "添加应用",
            Size = new Size(32, 28),
            Font = new Font(Font.FontFamily, 11F),
            Margin = new Padding(0),
            UseVisualStyleBackColor = true
        };
        addButton.Click += (_, _) => AddApplication();
        _buttonTips.SetToolTip(addButton, "添加应用");

        _removeButton.Text = "−";
        _removeButton.AccessibleName = "移除选中的快捷键";
        _removeButton.Size = new Size(32, 28);
        _removeButton.Font = new Font(Font.FontFamily, 11F);
        _removeButton.Enabled = false;
        _removeButton.Margin = new Padding(0);
        _removeButton.UseVisualStyleBackColor = true;
        _removeButton.Click += (_, _) => RemoveSelected();
        _buttonTips.SetToolTip(_removeButton, "移除选中的快捷键");

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            AutoSize = true
        };
        actions.Controls.Add(addButton);
        actions.Controls.Add(_removeButton);

        _statusLabel.Text = " ";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.ForeColor = Color.FromArgb(96, 96, 96);
        _statusLabel.Margin = new Padding(8, 0, 0, 0);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            ColumnCount = 2,
            Padding = new Padding(12, 6, 12, 8),
            BackColor = BackColor
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.Controls.Add(actions, 0, 0);
        footer.Controls.Add(_statusLabel, 1, 0);

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 12, 12, 0),
            BackColor = BackColor
        };
        gridHost.Controls.Add(_grid);

        Controls.Add(gridHost);
        Controls.Add(footer);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToOrderColumns = false;
        _grid.AutoGenerateColumns = false;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowTemplate.Height = 32;
        _grid.ColumnHeadersHeight = 30;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(64, 64, 64);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 250, 250);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Regular);
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.ForeColor = Color.FromArgb(32, 32, 32);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 239, 255);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 20, 20);
        _grid.DefaultCellStyle.Padding = new Padding(3, 0, 3, 0);
        _grid.GridColor = Color.FromArgb(234, 234, 234);
        _grid.AllowDrop = true;

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            Name = "Icon",
            HeaderText = "",
            Width = 34,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Application",
            HeaderText = "应用",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 140,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Hotkey",
            HeaderText = "快捷键",
            Width = 125,
            FlatStyle = FlatStyle.Standard,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.False,
            UseColumnTextForButtonValue = false
        });

        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == HotkeyColumnIndex)
                ToggleRecording(e.RowIndex);
        };
        _grid.SelectionChanged += (_, _) =>
            _removeButton.Enabled = _grid.SelectedRows.Count > 0 && !_recorder.IsRecording;
        _grid.CellToolTipTextNeeded += GridCellToolTipTextNeeded;

        _grid.MouseDown += GridMouseDown;
        _grid.MouseMove += GridMouseMove;
        _grid.DragOver += GridDragOver;
        _grid.DragLeave += (_, _) => ClearDropIndicator();
        _grid.DragDrop += GridDragDrop;
        _grid.RowPostPaint += GridRowPostPaint;
        _grid.Paint += GridPaint;
    }

    public void ReloadFromDisk()
    {
        if (IsDisposed || _closing)
            return;

        var loaded = _configManager.Load();
        if (loaded is null)
        {
            SetStatus("配置文件格式有误，已保留当前列表", isError: true);
            return;
        }

        if (loaded.Hotkeys is null || loaded.Hotkeys.Any(entry => entry is null))
        {
            SetStatus("配置中存在无效条目，已保留当前列表", isError: true);
            return;
        }

        // 与 mac 版一致：本窗口保存触发的文件事件不应取消录制或重置当前选择。
        if (ConfigsEqual(loaded, _config))
            return;

        if (_recorder.IsRecording)
            _recorder.Cancel();

        _config = loaded;
        PopulateGrid();
    }

    private static bool ConfigsEqual(AppConfig left, AppConfig right)
    {
        if (left.Hotkeys.Count != right.Hotkeys.Count)
            return false;

        return left.Hotkeys.Zip(right.Hotkeys).All(pair =>
            string.Equals(pair.First.Key, pair.Second.Key, StringComparison.Ordinal) &&
            string.Equals(pair.First.ProcessName, pair.Second.ProcessName, StringComparison.Ordinal) &&
            string.Equals(pair.First.ExePath, pair.Second.ExePath, StringComparison.Ordinal) &&
            string.Equals(pair.First.WindowClass, pair.Second.WindowClass, StringComparison.Ordinal));
    }

    private void PopulateGrid(int selectIndex = -1)
    {
        _grid.Rows.Clear();
        DisposeRowIcons();

        foreach (var (entry, index) in _config.Hotkeys.Select((entry, index) => (entry, index)))
        {
            var (name, icon) = ResolveApplication(entry);
            if (icon is not null)
                _rowIcons.Add(icon);

            var rowIndex = _grid.Rows.Add(icon, name, HotkeyRecorder.ToDisplayString(entry.Key));
            var row = _grid.Rows[rowIndex];
            row.Tag = index;
            row.Cells[AppColumnIndex].ToolTipText = entry.ExePath;
            row.Cells[HotkeyColumnIndex].ToolTipText = "点击录制新的快捷键";
        }

        if (_grid.Rows.Count > 0)
        {
            var target = selectIndex >= 0 ? Math.Min(selectIndex, _grid.Rows.Count - 1) : 0;
            _grid.ClearSelection();
            _grid.Rows[target].Selected = true;
            _grid.CurrentCell = _grid.Rows[target].Cells[AppColumnIndex];
        }
        else
        {
            _removeButton.Enabled = false;
        }
    }

    private static (string Name, Image? Icon) ResolveApplication(HotkeyEntry entry)
    {
        var fallback = !string.IsNullOrWhiteSpace(entry.ProcessName)
            ? entry.ProcessName
            : Path.GetFileNameWithoutExtension(entry.ExePath);
        if (string.IsNullOrWhiteSpace(fallback))
            fallback = "未知应用";

        if (string.IsNullOrWhiteSpace(entry.ExePath) || !File.Exists(entry.ExePath))
            return (fallback, null);

        try
        {
            var version = FileVersionInfo.GetVersionInfo(entry.ExePath);
            var name = FirstNonEmpty(version.FileDescription, version.ProductName, fallback);
            using var extracted = Icon.ExtractAssociatedIcon(entry.ExePath);
            return (name, extracted?.ToBitmap());
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return (fallback, null);
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;

    private void AddApplication()
    {
        if (_recorder.IsRecording)
            _recorder.Cancel();

        using var dialog = new OpenFileDialog
        {
            Title = "选择应用",
            Filter = "应用程序 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var existing = _config.Hotkeys.FindIndex(entry =>
            string.Equals(entry.ExePath, dialog.FileName, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            SelectRow(existing);
            SetStatus("该应用已在列表中");
            return;
        }

        var entry = new HotkeyEntry
        {
            Key = "",
            ProcessName = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExePath = dialog.FileName
        };
        _config.Hotkeys.Add(entry);
        if (!SaveConfig("应用已添加，请点击“未设置”录制快捷键"))
        {
            _config.Hotkeys.RemoveAt(_config.Hotkeys.Count - 1);
            return;
        }

        PopulateGrid(_config.Hotkeys.Count - 1);
    }

    private void RemoveSelected()
    {
        if (_grid.SelectedRows.Count == 0)
            return;

        var index = _grid.SelectedRows[0].Index;
        if (index < 0 || index >= _config.Hotkeys.Count)
            return;

        var name = _grid.Rows[index].Cells[AppColumnIndex].Value?.ToString() ?? "该应用";
        var result = MessageBox.Show(
            this,
            $"将移除“{name}”的快捷键。",
            "移除快捷键？",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.OK)
            return;

        var removed = _config.Hotkeys[index];
        _config.Hotkeys.RemoveAt(index);
        if (!SaveConfig($"已移除“{name}”"))
        {
            _config.Hotkeys.Insert(index, removed);
            return;
        }

        PopulateGrid(index);
    }

    private void ToggleRecording(int rowIndex)
    {
        if (_recorder.IsRecording)
        {
            var wasSameRow = _recordingIndex == rowIndex;
            _recorder.Cancel();
            if (wasSameRow)
                return;
        }

        _recordingIndex = rowIndex;
        _setRecording(true);
        if (!_recorder.Start(out var error))
        {
            _recordingIndex = null;
            _setRecording(false);
            SetStatus(error ?? "无法开始录制", isError: true);
            return;
        }

        _grid.Rows[rowIndex].Cells[HotkeyColumnIndex].Value = "请按快捷键…";
        _grid.Rows[rowIndex].Selected = true;
        _removeButton.Enabled = false;
        SetStatus("按 Esc 或再次点击可取消");
    }

    private void RecorderCompleted(string? combo)
    {
        var index = _recordingIndex;
        _recordingIndex = null;

        if (_closing)
        {
            _setRecording(false);
            return;
        }

        if (index is null || index < 0 || index >= _config.Hotkeys.Count)
        {
            _setRecording(false);
            return;
        }

        var oldCombo = _config.Hotkeys[index.Value].Key;
        if (combo is not null)
        {
            if (HasConflict(combo, index.Value))
            {
                MessageBox.Show(
                    this,
                    $"{HotkeyRecorder.ToDisplayString(combo)} 已被其他条目占用，请换一个。",
                    "快捷键冲突",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                SetStatus("快捷键未更改", isError: true);
            }
            else
            {
                _config.Hotkeys[index.Value].Key = combo;
                if (!SaveConfig($"快捷键已更新为 {HotkeyRecorder.ToDisplayString(combo)}"))
                    _config.Hotkeys[index.Value].Key = oldCombo;
            }
        }
        else
        {
            SetStatus("已取消录制");
        }

        _grid.Rows[index.Value].Cells[HotkeyColumnIndex].Value =
            HotkeyRecorder.ToDisplayString(_config.Hotkeys[index.Value].Key);
        _removeButton.Enabled = _grid.SelectedRows.Count > 0;
        _setRecording(false);
    }

    private bool HasConflict(string combo, int excluding)
    {
        try
        {
            var target = HotkeyParser.Parse(combo);
            return _config.Hotkeys.Select((entry, index) => (entry, index)).Any(item =>
            {
                if (item.index == excluding || string.IsNullOrWhiteSpace(item.entry.Key))
                    return false;
                try
                {
                    var existing = HotkeyParser.Parse(item.entry.Key);
                    return existing.Modifiers == target.Modifiers && existing.VirtualKey == target.VirtualKey;
                }
                catch (FormatException)
                {
                    return false;
                }
            });
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private bool SaveConfig(string successMessage)
    {
        if (!_configManager.Save(_config))
        {
            MessageBox.Show(
                this,
                "无法写入 config.json，请检查文件权限后重试。",
                "保存失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            SetStatus("保存失败", isError: true);
            return false;
        }

        _onConfigSaved();
        SetStatus(successMessage);
        return true;
    }

    private void GridCellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _config.Hotkeys.Count)
            return;
        e.ToolTipText = e.ColumnIndex == HotkeyColumnIndex
            ? "点击录制新的快捷键"
            : _config.Hotkeys[e.RowIndex].ExePath;
    }

    private void GridMouseDown(object? sender, MouseEventArgs e)
    {
        _dragStart = e.Location;
        _dragRowIndex = e.Button == MouseButtons.Left ? _grid.HitTest(e.X, e.Y).RowIndex : -1;
    }

    private void GridMouseMove(object? sender, MouseEventArgs e)
    {
        if (_recorder.IsRecording || e.Button != MouseButtons.Left || _dragRowIndex < 0)
            return;

        var dragSize = SystemInformation.DragSize;
        var dragBounds = new Rectangle(
            _dragStart.X - dragSize.Width / 2,
            _dragStart.Y - dragSize.Height / 2,
            dragSize.Width,
            dragSize.Height);
        if (!dragBounds.Contains(e.Location))
            _grid.DoDragDrop(_dragRowIndex, DragDropEffects.Move);
    }

    private void GridDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data!.GetDataPresent(typeof(int)))
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        e.Effect = DragDropEffects.Move;
        var point = _grid.PointToClient(new Point(e.X, e.Y));
        var row = _grid.HitTest(point.X, point.Y).RowIndex;
        var nextDrop = row >= 0 ? row : point.Y <= _grid.ColumnHeadersHeight ? 0 : _grid.Rows.Count;
        if (_dropRowIndex != nextDrop)
        {
            _dropRowIndex = nextDrop;
            _grid.Invalidate();
        }
    }

    private void GridDragDrop(object? sender, DragEventArgs e)
    {
        var from = e.Data?.GetData(typeof(int)) as int? ?? -1;
        var to = _dropRowIndex < 0 ? _grid.Rows.Count : _dropRowIndex;
        ClearDropIndicator();

        if (from < 0 || from >= _config.Hotkeys.Count || to == from || to == from + 1)
            return;

        var entry = _config.Hotkeys[from];
        _config.Hotkeys.RemoveAt(from);
        if (to > from)
            to--;
        to = Math.Clamp(to, 0, _config.Hotkeys.Count);
        _config.Hotkeys.Insert(to, entry);

        if (!SaveConfig("顺序已更新"))
        {
            _config.Hotkeys.RemoveAt(to);
            _config.Hotkeys.Insert(from, entry);
            return;
        }

        PopulateGrid(to);
    }

    private void GridRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (_dropRowIndex != e.RowIndex)
            return;
        using var pen = new Pen(Color.FromArgb(0, 95, 184), 2);
        e.Graphics.DrawLine(pen, 0, e.RowBounds.Top, _grid.ClientSize.Width, e.RowBounds.Top);
    }

    private void GridPaint(object? sender, PaintEventArgs e)
    {
        if (_dropRowIndex != _grid.Rows.Count || _grid.Rows.Count == 0)
            return;
        var bottom = _grid.GetRowDisplayRectangle(_grid.Rows.Count - 1, cutOverflow: true).Bottom;
        using var pen = new Pen(Color.FromArgb(0, 95, 184), 2);
        e.Graphics.DrawLine(pen, 0, bottom, _grid.ClientSize.Width, bottom);
    }

    private void ClearDropIndicator()
    {
        _dropRowIndex = -1;
        _dragRowIndex = -1;
        _grid.Invalidate();
    }

    private void SelectRow(int index)
    {
        if (index < 0 || index >= _grid.Rows.Count)
            return;
        _grid.ClearSelection();
        _grid.Rows[index].Selected = true;
        _grid.CurrentCell = _grid.Rows[index].Cells[AppColumnIndex];
        _grid.FirstDisplayedScrollingRowIndex = index;
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.FromArgb(196, 43, 28) : Color.FromArgb(96, 96, 96);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _closing = true;
        if (_recorder.IsRecording)
            _recorder.Cancel();
        _recorder.Dispose();
    }

    private void DisposeRowIcons()
    {
        foreach (var icon in _rowIcons)
            icon.Dispose();
        _rowIcons.Clear();
    }

    private sealed class EmptyMessageGrid : DataGridView
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rows.Count != 0)
                return;

            var bounds = new Rectangle(0, ColumnHeadersHeight, ClientSize.Width, ClientSize.Height - ColumnHeadersHeight);
            TextRenderer.DrawText(
                e.Graphics,
                "还没有应用。点击“+”开始设置。",
                Font,
                bounds,
                Color.FromArgb(112, 112, 112),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
}
