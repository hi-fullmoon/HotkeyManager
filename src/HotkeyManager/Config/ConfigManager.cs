using System.Text.Json;

namespace HotkeyManager.Config;

/// <summary>config.json 的加载与热重载（FileSystemWatcher + 防抖）。</summary>
public sealed class ConfigManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Threading.Timer _debounceTimer;

    /// <summary>配置文件变化后触发（在线程池线程上，使用前需封送到 UI 线程）。</summary>
    public event Action? Changed;

    public ConfigManager(string path)
    {
        _path = path;
        SaveDefaultIfNeeded();

        _debounceTimer = new System.Threading.Timer(_ => Changed?.Invoke(), null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Renamed += OnFileChanged;
        _watcher.Created += OnFileChanged; // 有些编辑器"删除再新建"式保存只触发 Created
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) =>
        _debounceTimer.Change(300, Timeout.Infinite);

    /// <summary>读取配置；文件缺失/损坏（如保存到一半的 JSON）时返回 null，调用方应保持现状。</summary>
    public AppConfig? Load()
    {
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// 保存配置。先写入同目录临时文件再替换，避免文件监听器读到只写了一半的 JSON。
    /// </summary>
    public bool Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_path)!;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions) + Environment.NewLine;
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine($"[HotkeyManager] 保存配置失败：{ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[HotkeyManager] 清理临时配置失败：{ex.Message}");
            }
        }
    }

    /// <summary>首次运行时写入默认模板。</summary>
    private void SaveDefaultIfNeeded()
    {
        if (File.Exists(_path))
            return;
        try
        {
            File.WriteAllText(_path, DefaultTemplate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 写入失败不致命：Load 会返回 null，用户可从托盘菜单修复配置
            System.Diagnostics.Debug.WriteLine($"[HotkeyManager] 写入默认配置失败：{ex.Message}");
        }
    }

    /// <summary>默认配置模板（与仓库根目录 config.json 保持一致）。</summary>
    private const string DefaultTemplate = """
    {
      "hotkeys": [
        { "key": "alt+1", "processName": "WeChat", "exePath": "C:\\Program Files\\Tencent\\WeChat\\WeChat.exe" },
        { "key": "alt+2", "processName": "chrome", "exePath": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe" },
        { "key": "alt+3", "processName": "WindowsTerminal", "exePath": "C:\\Users\\<用户名>\\AppData\\Local\\Microsoft\\WindowsApps\\wt.exe" },
        { "key": "alt+4", "processName": "Code", "exePath": "C:\\Program Files\\Microsoft VS Code\\Code.exe" },
        { "key": "alt+5", "processName": "Obsidian", "exePath": "C:\\Program Files\\Obsidian\\Obsidian.exe" }
      ]
    }
    """;

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
