using System.Text.Json;

namespace HotkeyManager.Config;

/// <summary>config.jsonc 的加载与热重载（FileSystemWatcher + 防抖）。</summary>
public sealed class ConfigManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Threading.Timer _debounceTimer;

    /// <summary>配置文件变化后触发（在线程池线程上，使用前需封送到 UI 线程）。</summary>
    public event Action? Changed;

    public ConfigManager(string path)
    {
        _path = path;
        CreateConfigIfNeeded();

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

    /// <summary>读取配置；支持 JSONC 注释和尾随逗号。文件缺失或损坏时返回 null。</summary>
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

    /// <summary>首次运行时写入带注释的引导模板。</summary>
    private void CreateConfigIfNeeded()
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

    /// <summary>默认配置模板（与仓库根目录 config.jsonc 保持一致）。</summary>
    private const string DefaultTemplate = """
    {
      // 在 hotkeys 数组中添加任意数量的「快捷键 → 应用」配置。
      // 支持 // 或 /* */ 注释，也允许末尾逗号；保存后会自动重新加载。
      "hotkeys": [
        // 示例：删除下面 6 行开头的 //，再按实际安装位置修改即可启用。
        // {
        //   "key": "ctrl+alt+1",
        //   "processName": "notepad",
        //   "exePath": "C:\\Windows\\System32\\notepad.exe",
        //   "windowClass": ""
        // }
      ],
    }

    // 字段说明：
    // key         快捷键，格式为 ctrl+alt+1；支持 Ctrl / Alt / Shift / Win，F1~F24 可单独使用。
    // processName 进程名，不包含 .exe。程序会用它查找已经运行的应用。
    // exePath     可执行文件完整路径。应用未运行时，程序会从这里启动它。
    // windowClass 可选的窗口类名。通常留空；需要精确匹配特殊窗口时再填写。
    //
    // Windows 路径中的反斜杠必须写成两个，例如 C:\\Program Files\\App\\App.exe。
    // 暂时停用某一项时，可在该对象的每一行前添加 //，或直接删除该对象。
    """;

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
