using System.Text.Json;

namespace HotkeyManager.Config;

/// <summary>config.json 的加载与热重载（FileSystemWatcher + 防抖）。</summary>
public sealed class ConfigManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Threading.Timer _debounceTimer;

    /// <summary>配置文件变化后触发（在线程池线程上，使用前需封送到 UI 线程）。</summary>
    public event Action? Changed;

    public ConfigManager(string path)
    {
        _path = path;
        if (!File.Exists(path))
            SaveDefault();

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
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SaveDefault()
    {
        var sample = new AppConfig();
        sample.Hotkeys.Add(new HotkeyEntry
        {
            Modifiers = "Ctrl+Alt",
            Key = "D1",
            Target = new TargetApp
            {
                DisplayName = "微信",
                ProcessName = "WeChat",
                ExePath = @"C:\Program Files\Tencent\WeChat\WeChat.exe",
                WindowClass = "WeChatMainWndForPC"
            }
        });
        File.WriteAllText(_path, JsonSerializer.Serialize(sample, JsonOptions));
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
