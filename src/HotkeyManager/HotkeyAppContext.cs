using System.ComponentModel;
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
    private readonly Action _applyChangedConfig;
    private readonly Action _configChangedHandler;
    private readonly Action<string> _windowErrorHandler;
    private int _exitRequested;
    private int _disposeStarted;

    public HotkeyAppContext()
    {
        // 强制在 UI 线程创建句柄，用于把配置文件的变更回调封送回 UI 线程
        _ = _marshaler.Handle;

        // 配置文件放在 exe 所在目录（安装目录），随程序一起走
        _configPath = Path.Combine(AppContext.BaseDirectory, "config.jsonc");
        _configManager = new ConfigManager(_configPath);
        _applyChangedConfig = () => ApplyConfig(showSummary: false);
        _tray = new TrayIcon(OpenConfig, TogglePause,
            GetAutostart, SetAutostart, Exit);
        _windowErrorHandler = msg => _tray.ShowBalloon("HotkeyManager", msg);
        _windowService.ErrorOccurred += _windowErrorHandler;

        // FileSystemWatcher/Timer 回调在线程池线程上，封送回 UI 线程再重注册热键；
        // 退出时 marshaler 可能已释放，BeginInvoke 抛异常属正常竞态，忽略
        _configChangedHandler = () =>
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return;

            try
            {
                _marshaler.BeginInvoke(_applyChangedConfig);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
            }
        };
        _configManager.Changed += _configChangedHandler;

        ApplyConfig(showSummary: true);
    }

    private bool _paused;

    private void ApplyConfig(bool showSummary)
    {
        // 退出后防抖回调仍可能触发热重载，此时依赖项已释放，直接忽略
        if (Volatile.Read(ref _disposeStarted) != 0 || Volatile.Read(ref _exitRequested) != 0)
            return;

        var config = _configManager.Load();
        if (config is null)
        {
            // 配置损坏（常见原因：编辑器保存到一半触发了热重载），保持现有热键不动
            _tray.ShowBalloon("配置解析失败", "config.jsonc 格式有误，已保持现有热键不变");
            return;
        }

        _windowService.ResetCaches();

        // 先解析全部条目，再统一注销重注册——避免某条配置错误导致已有热键全部失效
        var parsed = new List<(uint Modifiers, uint VirtualKey, HotkeyEntry Entry)>(config.Hotkeys?.Count ?? 0);
        foreach (var entry in config.Hotkeys ?? Enumerable.Empty<HotkeyEntry>())
        {
            if (entry is null)
            {
                _tray.ShowBalloon("配置格式错误", "存在空的热键条目，已跳过");
                continue;
            }

            // 允许用户暂时保留尚未设置快捷键的条目。
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
            _windowService.ResetCaches();
            _tray.ShowBalloon("HotkeyManager", "热键已暂停，所有快捷键已释放");
        }
        else
        {
            ApplyConfig(showSummary: true); // 重新注册，内部有气球提示
        }
        return _paused;
    }

    private static bool? GetAutostart()
    {
        return AutostartService.TryGetEnabled(out var enabled, out _) ? enabled : null;
    }

    /// <summary>写入后回读注册表，以真实状态更新菜单并向用户报告结果。</summary>
    private bool? SetAutostart(bool enabled)
    {
        var action = enabled ? "开启" : "关闭";
        if (!AutostartService.TrySetEnabled(enabled, out var writeError))
        {
            _tray.ShowBalloon("开机自启设置失败", $"{action}失败：{writeError}");
            return GetAutostart();
        }

        if (!AutostartService.TryGetEnabled(out var actualState, out var readError))
        {
            _tray.ShowBalloon("开机自启设置失败", $"设置已写入，但无法读取注册表确认：{readError}");
            return null;
        }

        if (actualState != enabled)
        {
            _tray.ShowBalloon("开机自启设置失败", $"重新读取后仍未{action}开机自启，请检查注册表权限。");
            return actualState;
        }

        _tray.ShowBalloon("HotkeyManager", enabled ? "已开启开机自启" : "已关闭开机自启");
        return actualState;
    }

    private Settings.SettingsForm? _settings;
    private bool _recording;

    private void OpenConfig()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.WindowState = FormWindowState.Normal;
            _settings.Activate();
            return;
        }
        _settings = new Settings.SettingsForm(_configPath, active =>
        {
            _recording = active;
            if (active) _hotkeyService.UnregisterAll();
            else ApplyConfig(showSummary: false);
        });
        _settings.Show();
    }

    private void Exit()
    {
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (Interlocked.Exchange(ref _exitRequested, 1) != 0)
            return;

        // 只结束当前应用上下文的消息循环；Program 中的 using 随后统一释放全部资源。
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposeStarted, 1) == 0)
        {
            _configManager.Changed -= _configChangedHandler;
            _windowService.ErrorOccurred -= _windowErrorHandler;

            _settings?.Dispose();
            DisposeSafely(_tray, "托盘");
            DisposeSafely(_configManager, "配置监听");
            DisposeSafely(_hotkeyService, "热键服务");
            DisposeSafely(_windowService, "窗口服务");
            DisposeSafely(_marshaler, "UI marshaler");
        }

        base.Dispose(disposing);
    }

    private static void DisposeSafely(IDisposable resource, string name)
    {
        try
        {
            resource.Dispose();
        }
        catch (Exception ex)
        {
            // 退出阶段继续释放剩余资源，避免一个组件的清理失败阻断整个生命周期。
            Debug.WriteLine($"[HotkeyManager] 释放{name}失败：{ex.Message}");
        }
    }
}
