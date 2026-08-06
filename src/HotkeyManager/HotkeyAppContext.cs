using System.Diagnostics;
using HotkeyManager.Config;
using HotkeyManager.Core;
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

    public HotkeyAppContext()
    {
        // 强制在 UI 线程创建句柄，用于把配置文件的变更回调封送回 UI 线程
        _ = _marshaler.Handle;

        // 与 mac 版一致：个人目录下的隐藏文件 %USERPROFILE%\.hotkeymanager.json
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hotkeymanager.json");
        _configManager = new ConfigManager(_configPath);
        _tray = new TrayIcon(OpenConfig, ApplyConfig, TogglePause,
            AutostartService.IsEnabled, SetAutostart, Exit);
        _windowService.ErrorOccurred += msg => _tray.ShowBalloon("HotkeyManager", msg);

        // FileSystemWatcher/Timer 回调在线程池线程上，封送回 UI 线程再重注册热键；
        // 退出时 marshaler 可能已释放，BeginInvoke 抛异常属正常竞态，忽略
        _configManager.Changed += () =>
        {
            try
            {
                _marshaler.BeginInvoke(new Action(ApplyConfig));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
            }
        };

        ApplyConfig();
    }

    private void ApplyConfig()
    {
        var config = _configManager.Load();
        if (config is null)
        {
            // 配置损坏（常见原因：编辑器保存到一半触发了热重载），保持现有热键不动
            _tray.ShowBalloon("配置解析失败", ".hotkeymanager.json 格式有误，已保持现有热键不变");
            return;
        }

        // 先解析全部条目，再统一注销重注册——避免某条配置错误导致已有热键全部失效
        var parsed = new List<(uint Modifiers, uint VirtualKey, HotkeyEntry Entry)>();
        foreach (var entry in config.Hotkeys)
        {
            if (entry is null)
            {
                _tray.ShowBalloon("配置格式错误", "存在空的热键条目，已跳过");
                continue;
            }

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
        if (_paused)
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

        _tray.ShowBalloon("HotkeyManager", $"已注册 {registered}/{parsed.Count} 个热键");
    }

    private bool _paused;

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
            ApplyConfig(); // 重新注册，内部有气球提示
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

    private void Exit()
    {
        _tray.Dispose();
        _hotkeyService.Dispose();
        _configManager.Dispose();
        _marshaler.Dispose();
        Application.Exit();
    }
}
