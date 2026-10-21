using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HotkeyManager.Settings;

internal sealed class SettingsForm : Form
{
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly Action<bool> _recording;
    private bool _allowClose;
    private bool _pageReady;
    private readonly SettingsConfigStore _store;
    private Dictionary<int, RunningApplication> _runningApps = new();

    public SettingsForm(string path, Action<bool> recording)
    {
        _store = new SettingsConfigStore(path, recording);
        _recording = recording;
        Text = "快捷键设置 · Hotkey Manager";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(760, 540);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Controls.Add(_view);
        Shown += async (_, _) => await InitializeAsync();
        Deactivate += (_, _) => StopRecording();
        FormClosed += (_, _) => _recording(false);
        FormClosing += (_, e) =>
        {
            if (!_allowClose && _pageReady && _view.CoreWebView2 is not null && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Send(new { type = "closing" });
            }
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SetClientSizeForDpi(DeviceDpi);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        SetClientSizeForDpi(e.DeviceDpiNew);
    }

    private void SetClientSizeForDpi(int dpi) => ClientSize = new Size(
        (int)Math.Round(760 * dpi / 96d), (int)Math.Round(540 * dpi / 96d));

    private async Task InitializeAsync()
    {
        try
        {
            var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HotkeyManager", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, dataFolder);
            if (IsDisposed) return;
            await _view.EnsureCoreWebView2Async(environment);
            if (IsDisposed) return;
            var core = _view.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            core.NewWindowRequested += (_, e) => e.Handled = true;

            core.WebMessageReceived += OnMessage;
            using var stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("HotkeyManager.Settings.settings.html")!;
            using var reader = new StreamReader(stream);
            using var scriptStream = typeof(SettingsForm).Assembly.GetManifestResourceStream("HotkeyManager.Settings.settings.js")!;
            using var scriptReader = new StreamReader(scriptStream);
            using var styleStream = typeof(SettingsForm).Assembly.GetManifestResourceStream("HotkeyManager.Settings.settings.css")!;
            using var styleReader = new StreamReader(styleStream);
            var css = await styleReader.ReadToEndAsync();
            var html = (await reader.ReadToEndAsync()).Replace("/* SETTINGS_STYLE */", css);
            var script = await scriptReader.ReadToEndAsync();
            // 部分 Runtime 会将 NavigateToString 转换成 data URI。
            // 显式生成并仅允许这一份内嵌文档，避免误拦截或放行任意外部页面。
            var documentUri = "data:text/html;charset=utf-8;base64," +
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html.Replace("/* SETTINGS_SCRIPT */", script)));
            core.NavigationStarting += (_, e) => e.Cancel = e.Uri != documentUri;
            core.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess && !IsDisposed)
                    MessageBox.Show(this, $"设置页面加载失败：{e.WebErrorStatus}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            if (!IsDisposed) core.Navigate(documentUri);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) MessageBox.Show(this, "无法加载设置窗口。请确认已安装 Microsoft Edge WebView2 Runtime。\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopRecording()
    {
        _recording(false);
        if (_view.CoreWebView2 is not null)
            Send(new { type = "stopRecording" });
    }

    private void Send(object message) => _view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));

    private void LoadConfig() => Send(new { type = "loaded", entries = _store.Load() });

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            _pageReady = true;
            switch (root.GetProperty("type").GetString())
            {
                case "close": _allowClose = true; Close(); break;
                case "load": LoadConfig(); break;
                case "record": _recording(root.GetProperty("active").GetBoolean()); break;
                case "browse":
                    using (var dialog = new OpenFileDialog { Filter = "应用程序 (*.exe)|*.exe", Title = "选择应用程序" })
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                            Send(new { type = "picked", path = dialog.FileName, process = Path.GetFileNameWithoutExtension(dialog.FileName) });
                    }
                    break;
                case "runningApps":
                    try
                    {
                        var apps = await Task.Run(RunningApplicationService.List);
                        if (IsDisposed) return;
                        _runningApps = apps.ToDictionary(app => app.id);
                        Send(new { type = "runningApps", apps });
                    }
                    catch (Exception ex)
                    {
                        if (!IsDisposed) Send(new { type = "runningAppError", message = "读取应用列表失败：" + ex.Message });
                    }
                    break;
                case "pickRunningApp":
                    try
                    {
                        var id = root.GetProperty("id").GetInt32();
                        if (!_runningApps.TryGetValue(id, out var previous)) throw new InvalidOperationException("应用列表已变化，请刷新后重新选择。");
                        var app = RunningApplicationService.Resolve(id);
                        if (!app.path.Equals(previous.path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("应用进程已变化，请刷新后重新选择。");
                        Send(new { type = "picked", app.path, app.process, app.name });
                    }
                    catch (Exception ex) { Send(new { type = "runningAppError", message = ex.Message }); }
                    break;
                case "save":
                    var entries = JsonNode.Parse(root.GetProperty("entries").GetRawText()) as JsonArray
                        ?? throw new InvalidDataException("应用列表格式有误。");
                    _store.Save(entries);
                    Send(new { type = "saved" });
                    break;
            }
        }
        catch (Exception ex)
        {
            Send(new { type = "error", message = ex.Message });
        }
    }

}
