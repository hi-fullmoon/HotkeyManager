using System.Windows.Forms;
using HotkeyManager.Interop;

namespace HotkeyManager.Core;

/// <summary>把配置里的字符串（如 "Ctrl+Alt" / "D1"）解析成 user32 需要的修饰键标志和虚拟键码。</summary>
public static class HotkeyParser
{
    public static (uint Modifiers, uint VirtualKey) Parse(string modifiers, string key)
    {
        // Modifiers 在 JSON 里可能是显式 null，按"无修饰键"处理
        var mods = 0u;
        foreach (var part in (modifiers ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            mods |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => User32.MOD_CONTROL,
                "alt" => User32.MOD_ALT,
                "shift" => User32.MOD_SHIFT,
                "win" or "windows" => User32.MOD_WIN,
                _ => throw new FormatException($"未知的修饰键：{part}（支持 Ctrl / Alt / Shift / Win）")
            };
        }

        if (string.IsNullOrWhiteSpace(key) || !Enum.TryParse<Keys>(key, ignoreCase: true, out var keys))
            throw new FormatException($"未知的按键：{key}（参考 WinForms Keys 枚举，如 D1、F5、A、NumPad0）");

        return (mods, (uint)(keys & Keys.KeyCode));
    }
}
