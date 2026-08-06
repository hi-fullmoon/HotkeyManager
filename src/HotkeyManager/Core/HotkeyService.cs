using System.Runtime.InteropServices;
using System.Windows.Forms;
using HotkeyManager.Interop;

namespace HotkeyManager.Core;

/// <summary>全局热键注册与分发。基于 RegisterHotKey + 隐藏窗口接收 WM_HOTKEY。</summary>
public sealed class HotkeyService : IDisposable
{
    private sealed class HotkeyWindow : NativeWindow
    {
        public event Action<int>? HotkeyReceived;

        public HotkeyWindow() => CreateHandle(new CreateParams());

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == User32.WM_HOTKEY)
                HotkeyReceived?.Invoke(m.WParam.ToInt32());

            base.WndProc(ref m);
        }
    }

    private readonly HotkeyWindow _window = new();
    private readonly Dictionary<int, Action> _callbacks = new();
    private int _lastId;

    public HotkeyService()
    {
        _window.HotkeyReceived += id =>
        {
            if (_callbacks.TryGetValue(id, out var callback))
                callback();
        };
    }

    /// <summary>注册一个全局热键。失败时通过 <paramref name="error"/> 返回原因（如被占用）。</summary>
    public bool Register(uint modifiers, uint virtualKey, Action callback, out string? error)
    {
        var id = ++_lastId;
        if (!User32.RegisterHotKey(_window.Handle, id, modifiers | User32.MOD_NOREPEAT, virtualKey))
        {
            var code = Marshal.GetLastWin32Error();
            error = code == 1409 ? "热键已被占用（可能与其他程序或本配置中的重复条目冲突）" : $"系统错误 {code}";
            return false;
        }

        _callbacks[id] = callback;
        error = null;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys)
            User32.UnregisterHotKey(_window.Handle, id);
        _callbacks.Clear();
        // id 只在全部注销后才会重新分配，归零避免多次热重载后无限增长
        _lastId = 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.DestroyHandle();
    }
}
