using System.Runtime.InteropServices;

namespace HotkeyManager.Interop;

/// <summary>dwmapi.dll 的窗口属性设置，用于圆角窗口与沉浸式深色模式。</summary>
public static class DwmApi
{
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
