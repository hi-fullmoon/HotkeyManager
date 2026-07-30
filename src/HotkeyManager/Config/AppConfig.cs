namespace HotkeyManager.Config;

public sealed class AppConfig
{
    public List<HotkeyEntry> Hotkeys { get; set; } = new();
}

public sealed class HotkeyEntry
{
    /// <summary>修饰键，用 + 分隔：Ctrl / Alt / Shift / Win，如 "Ctrl+Alt"。</summary>
    public string Modifiers { get; set; } = "Ctrl";

    /// <summary>按键名，对应 WinForms Keys 枚举：D1、F5、A、NumPad0 等。</summary>
    public string Key { get; set; } = "D1";

    /// <summary>隐藏方式：minimize（最小化，默认）或 hide（彻底隐藏窗口）。</summary>
    public string HideMode { get; set; } = "minimize";

    public TargetApp Target { get; set; } = new();
}

public sealed class TargetApp
{
    /// <summary>显示名，仅用于日志和提示。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>进程名（不含 .exe），如 WeChat、notepad。</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>可执行文件路径，进程未运行时用于启动。</summary>
    public string ExePath { get; set; } = "";

    /// <summary>窗口类名（可选，用 Spy++ 查看）。填写后优先按类名查找，可定位到托盘化的隐藏窗口。</summary>
    public string WindowClass { get; set; } = "";
}
