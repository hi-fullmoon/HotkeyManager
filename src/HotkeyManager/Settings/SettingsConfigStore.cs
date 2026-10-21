using System.Text.Json;
using System.Text.Json.Nodes;
using HotkeyManager.Core;

namespace HotkeyManager.Settings;

/// <summary>保存时检测外部修改，保留未知字段，并以原子替换生成上一版备份。</summary>
internal sealed class SettingsConfigStore(string path, Action<bool> recording)
{
    private string? _snapshot;

    private JsonObject Read(out string raw)
    {
        raw = File.ReadAllText(path);
        return JsonNode.Parse(raw, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new InvalidDataException("配置根节点必须是对象。");
    }

    public JsonArray Load()
    {
        var config = Read(out var raw);
        var entries = config["hotkeys"] as JsonArray ?? throw new InvalidDataException("hotkeys 必须是数组。");
        if (entries.Any(e => e is not JsonObject)) throw new InvalidDataException("配置中存在无效条目，请先修复配置文件。");
        _snapshot = raw;
        return entries;
    }

    public void Save(JsonArray entries)
    {
        Validate(entries);
        var config = Read(out var raw);
        if (_snapshot is null || raw != _snapshot)
            throw new InvalidDataException("配置已被其他程序修改，请重新加载后再保存。");
        config["hotkeys"] = entries.DeepClone();
        var output = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, output);
            File.Replace(temporary, path, path + ".bak");
            _snapshot = output;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        recording(false); // 立即应用已写入的新配置；文件监听仍处理外部编辑。
    }

    private void Validate(JsonArray entries)
    {
        var seen = new HashSet<(uint, uint)>();
        recording(true);
        try
        {
            using var probe = new HotkeyService();
            foreach (var node in entries)
            {
                if (node is not JsonObject item) throw new InvalidDataException("无效的应用条目。");
                var key = item["key"]?.GetValue<string>() ?? "";
                var process = item["processName"]?.GetValue<string>() ?? "";
                var exePath = item["exePath"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(process) || string.IsNullOrWhiteSpace(exePath))
                    throw new InvalidDataException("请填写应用路径和进程名称。");
                if (string.IsNullOrWhiteSpace(key)) continue;
                var parsed = HotkeyParser.Parse(key);
                if (parsed.Modifiers == 0 && (parsed.VirtualKey < (uint)Keys.F1 || parsed.VirtualKey > (uint)Keys.F24))
                    throw new InvalidDataException("普通按键需要搭配 Ctrl、Alt、Shift 或 Win。");
                if (!seen.Add(parsed)) throw new InvalidDataException($"快捷键 {key} 重复。");
                if (!probe.Register(parsed.Modifiers, parsed.VirtualKey, () => { }, out var error))
                    throw new InvalidDataException($"{key}：{error}");
            }
        }
        finally { recording(false); }
    }
}
