using System.Windows.Forms;
using HotkeyManager.Interop;

namespace HotkeyManager.Core;

/// <summary>把配置里的组合键字符串（如 "ctrl+alt+1"）解析成 user32 需要的修饰键标志和虚拟键码。</summary>
public static class HotkeyParser
{
    /// <summary>最后一段为按键，其余为修饰键；Key 在 JSON 里可能是显式 null，按空串处理（解析失败）。</summary>
    public static (uint Modifiers, uint VirtualKey) Parse(string? combo)
    {
        var parts = (combo ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new FormatException("组合键为空（格式如 \"ctrl+alt+1\"，最后一段为按键，其余为修饰键）");

        var mods = 0u;
        foreach (var part in parts.Take(parts.Length - 1))
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

        var key = parts[^1];
        // 单个数字按主键盘数字键处理（"1" → D1），与 mac 版 "alt+1" 写法一致
        if (key.Length == 1 && char.IsDigit(key[0]))
            key = "D" + key;

        // Enum.TryParse 会把纯数字字符串按底层数值解析（如 "12" → Keys.Clear、"13" → Keys.Enter），
        // 多位数数字串必须显式拒绝，否则会静默注册错误的按键
        if (key.Length > 1 && key.All(char.IsDigit))
            throw new FormatException($"未知的按键：{key}（参考 WinForms Keys 枚举，如 1、F5、A、NumPad0）");

        if (!Enum.TryParse<Keys>(key, ignoreCase: true, out var keys) || !Enum.IsDefined(keys))
            throw new FormatException($"未知的按键：{key}（参考 WinForms Keys 枚举，如 1、F5、A、NumPad0）");

        return (mods, (uint)(keys & Keys.KeyCode));
    }
}
