using System.Runtime.InteropServices;
using HotkeyManager.Interop;

namespace HotkeyManager.Settings;

/// <summary>
/// 使用低级键盘钩子录制一次组合键。录制期间吞掉按键，避免 Win 等系统快捷键被触发。
/// </summary>
internal sealed class HotkeyRecorder : IDisposable
{
    private readonly User32.LowLevelKeyboardProc _hookProc;
    private SynchronizationContext? _uiContext;
    private IntPtr _hook;

    public event Action<string?>? Completed;
    public event Action<string>? InputRejected;

    public bool IsRecording => _hook != IntPtr.Zero;

    public HotkeyRecorder()
    {
        _hookProc = HookCallback;
    }

    public bool Start(out string? error)
    {
        _uiContext ??= SynchronizationContext.Current;
        if (IsRecording)
        {
            error = null;
            return true;
        }

        _hook = User32.SetWindowsHookEx(
            User32.WH_KEYBOARD_LL,
            _hookProc,
            User32.GetModuleHandle(null),
            0);
        if (_hook == IntPtr.Zero)
        {
            error = $"无法开始录制（系统错误 {Marshal.GetLastWin32Error()}）";
            return false;
        }

        error = null;
        return true;
    }

    public void Cancel()
    {
        if (IsRecording)
            Finish(null, deferCompletion: false);
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || !IsRecording)
            return User32.CallNextHookEx(_hook, code, message, data);

        var kind = message.ToInt32();
        if (kind is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN)
        {
            var info = Marshal.PtrToStructure<User32.KbdLlHookStruct>(data);
            var key = (Keys)(info.VirtualKey & (uint)Keys.KeyCode);

            if (key == Keys.Escape)
            {
                Finish(null, deferCompletion: true);
                return new IntPtr(1);
            }

            if (!IsModifierKey(key))
            {
                if (TryCreateCombo(key, out var combo))
                    Finish(combo, deferCompletion: true);
                else
                    InputRejected?.Invoke("普通按键需配合 Ctrl、Alt、Shift 或 Win；F1–F24 可单独使用");
            }
        }

        // 录制期间吞掉键盘消息，避免组合键同时触发系统或其他应用动作。
        return new IntPtr(1);
    }

    private static bool TryCreateCombo(Keys key, out string combo)
    {
        var tokens = new List<string>();
        if (IsDown(Keys.LControlKey) || IsDown(Keys.RControlKey))
            tokens.Add("ctrl");
        if (IsDown(Keys.LMenu) || IsDown(Keys.RMenu))
            tokens.Add("alt");
        if (IsDown(Keys.LShiftKey) || IsDown(Keys.RShiftKey))
            tokens.Add("shift");
        if (IsDown(Keys.LWin) || IsDown(Keys.RWin))
            tokens.Add("win");

        var functionKey = key >= Keys.F1 && key <= Keys.F24;
        if (tokens.Count == 0 && !functionKey)
        {
            combo = "";
            return false;
        }

        var keyToken = ToKeyToken(key);
        if (keyToken is null)
        {
            combo = "";
            return false;
        }

        tokens.Add(keyToken);
        combo = string.Join('+', tokens);
        return true;
    }

    private static bool IsDown(Keys key) => (User32.GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static bool IsModifierKey(Keys key) => key is
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.LWin or Keys.RWin;

    private static string? ToKeyToken(Keys key)
    {
        if (key >= Keys.D0 && key <= Keys.D9)
            return ((int)key - (int)Keys.D0).ToString();
        if (key >= Keys.A && key <= Keys.Z)
            return key.ToString().ToLowerInvariant();
        if (key == Keys.None || !Enum.IsDefined(key))
            return null;
        return key.ToString().ToLowerInvariant();
    }

    public static string ToDisplayString(string? combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return "未设置";

        var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "未设置";

        return string.Join(" + ", parts.Select((part, index) =>
        {
            if (index < parts.Length - 1)
            {
                return part.ToLowerInvariant() switch
                {
                    "ctrl" or "control" => "Ctrl",
                    "alt" => "Alt",
                    "shift" => "Shift",
                    "win" or "windows" => "Win",
                    _ => part
                };
            }

            if (part.Length == 2 && (part[0] is 'd' or 'D') && char.IsDigit(part[1]))
                return part[1].ToString();

            return part.ToLowerInvariant() switch
            {
                "return" or "enter" => "Enter",
                "escape" => "Esc",
                "prior" => "Page Up",
                "next" => "Page Down",
                "space" => "Space",
                _ when part.Length == 1 => part.ToUpperInvariant(),
                _ => char.ToUpperInvariant(part[0]) + part[1..]
            };
        }));
    }

    private void Finish(string? combo, bool deferCompletion)
    {
        StopHook();
        if (deferCompletion && _uiContext is not null)
            _uiContext.Post(_ => Completed?.Invoke(combo), null);
        else
            Completed?.Invoke(combo);
    }

    private void StopHook()
    {
        if (_hook == IntPtr.Zero)
            return;
        User32.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose() => StopHook();
}
