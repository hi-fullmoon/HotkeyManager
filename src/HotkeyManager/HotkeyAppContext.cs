using System.Diagnostics;
using HotkeyManager.Config;
using HotkeyManager.Core;
using HotkeyManager.Settings;
using HotkeyManager.Tray;

namespace HotkeyManager;

internal sealed class HotkeyAppContext : ApplicationContext
{
    private readonly string _configPath;
    private readonly Control _marshaler = new();
    private readonly HotkeyService _hotkeyService = new();
    private readonly WindowService _windowService = new();
    private readonly ConfigManager _configManager;
    private readonly TrayIcon _tray;
    private HotkeyListForm? _settingsForm;

    public HotkeyAppContext()
    {
        // 强制在 UI 线程创建句柄，用于把配置文件的变更回调封送回 UI 线程
        _ = _marshaler.Handle;

        // 配置文件放在 exe 所在目录（安装目录），随程序一起走
        _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        _configManager = new ConfigManager(_configPath);
        _tray = new TrayIcon(OpenConfig, ShowSettings, TogglePause,
            AutostartService.IsEnabled, SetAutostart, Exit);
        _windowService.ErrorOccurred += msg => _tray.ShowBalloon("HotkeyManager", msg);

        // FileSystemWatcher/Timer 回调在线程池线程上，封送回 UI 线程再重注册热键；
        // 退出时 marshaler 可能已释放，BeginInvoke 抛异常属正常竞态，忽略
        _configManager.Changed += () =>
        {
            try
            {
                _marshaler.BeginInvoke(new Action(() =>
                {
                    ApplyConfig(showSummary: false);
                    _settingsForm?.ReloadFromDisk();
                }));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
            }
        };

        ApplyConfig(showSummary: true);
    }

    private bool _paused;
    private bool _recording;
    private bool _disposed;

    private void ApplyConfig(bool showSummary)
    {
        // 退出后防抖回调仍可能触发热重载，此时依赖项已释放，直接忽略
        if (_disposed)
            return;

        var config = _configManager.Load();
        if (config is null)
        {
            // 配置损坏（常见原因：编辑器保存到一半触发了热重载），保持现有热键不动
            _tray.ShowBalloon("配置解析失败", "config.json 格式有误，已保持现有热键不变");
            return;
        }

        // 先解析全部条目，再统一注销重注册——避免某条配置错误导致已有热键全部失效
        var parsed = new List<(uint Modifiers, uint VirtualKey, HotkeyEntry Entry)>();
        foreach (var entry in config.Hotkeys ?? new List<HotkeyEntry>())
        {
            if (entry is null)
            {
                _tray.ShowBalloon("配置格式错误", "存在空的热键条目，已跳过");
                continue;
            }

            // 图形界面添加应用后会先保存一个“未设置”条目，等用户随后录制快捷键。
            if (string.IsNullOrWhiteSpace(entry.Key))
                continue;

            try
            {
                var (modifiers, virtualKey) = HotkeyParser.Parse(entry.Key);
                parsed.Add((modifiers, virtualKey, entry));
            }
            catch (FormatException ex)
            {
                _tray.ShowBalloon("配置格式错误", ex.Message);
            }
        }

        _hotkeyService.UnregisterAll();

        // 暂停状态下不注册：热重载只校验配置，按键保持释放状态
        if (_paused || _recording)
            return;

        var registered = 0;
        foreach (var (modifiers, virtualKey, entry) in parsed)
        {
            if (_hotkeyService.Register(modifiers, virtualKey, () => _windowService.Toggle(entry), out var error))
            {
                registered++;
            }
            else
            {
                _tray.ShowBalloon("热键注册失败", $"{entry.Key}：{error}");
            }
        }

        if (showSummary)
            _tray.ShowBalloon("HotkeyManager", $"已注册 {registered}/{parsed.Count} 个热键");
    }

    /// <summary>暂停/恢复热键。暂停时注销全部热键，把按键真正释放给其他程序。返回是否处于暂停状态。</summary>
    private bool TogglePause()
    {
        _paused = !_paused;
        if (_paused)
        {
            _hotkeyService.UnregisterAll();
            _tray.ShowBalloon("HotkeyManager", "热键已暂停，所有快捷键已释放");
        }
        else
        {
            ApplyConfig(showSummary: true); // 重新注册，内部有气球提示
        }
        return _paused;
    }

    private void SetAutostart(bool enabled)
    {
        AutostartService.SetEnabled(enabled);
        _tray.ShowBalloon("HotkeyManager", enabled ? "已开启开机自启" : "已关闭开机自启");
    }

    private void OpenConfig()
    {
        // UseShellExecute 打开文件时可能返回 null（壳关联启动），可空 Dispose 即可
        Process.Start(new ProcessStartInfo(_configPath) { UseShellExecute = true })?.Dispose();
    }

    private void ShowSettings()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new HotkeyListForm(
                _configManager,
                SetRecording,
                () => ApplyConfig(showSummary: false));
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        }

        if (_settingsForm.WindowState == FormWindowState.Minimized)
            _settingsForm.WindowState = FormWindowState.Normal;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    /// <summary>录制期间注销全局热键；录制结束后按当前配置恢复（原本处于暂停状态时不恢复）。</summary>
    private void SetRecording(bool recording)
    {
        _recording = recording;
        if (recording)
        {
            _hotkeyService.UnregisterAll();
        }
        else if (!_paused)
        {
            ApplyConfig(showSummary: false);
        }
    }

    private void Exit()
    {
        _disposed = true; // 先置位，防抖回调触发的 ApplyConfig 会直接忽略
        _settingsForm?.Close();
        _tray.Dispose();
        _hotkeyService.Dispose();
        _configManager.Dispose();
        _marshaler.Dispose();
        Application.Exit();
    }
}
