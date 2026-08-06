namespace HotkeyManager.Config;

// 配置模型（小驼峰 schema，与 mac 版一致：组合键字符串 + 进程信息，扁平结构）

public sealed class AppConfig
{
    public List<HotkeyEntry> Hotkeys { get; set; } = new();
}

/// <summary>单个热键配置。</summary>
public sealed class HotkeyEntry
{
    /// <summary>组合键字符串，如 "alt+1"、"ctrl+alt+a"，最后一段为按键，其余为修饰键。</summary>
    public string Key { get; set; } = "";

    /// <summary>进程名（不含 .exe），如 WeChat、notepad。</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>可执行文件路径，进程未运行时用于启动。</summary>
    public string ExePath { get; set; } = "";

    /// <summary>隐藏方式：minimize（最小化，默认）或 hide（彻底隐藏窗口）。</summary>
    public string HideMode { get; set; } = "minimize";

    /// <summary>窗口类名（可选，用 Spy++ 查看）。填写后优先按类名查找，可定位到托盘化的隐藏窗口。</summary>
    public string WindowClass { get; set; } = "";
}
