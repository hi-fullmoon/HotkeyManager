using System.ComponentModel;
using System.Diagnostics;
using HotkeyManager.Config;
using HotkeyManager.Interop;

namespace HotkeyManager.Core;

/// <summary>窗口查找与"显示 ↔ 隐藏"切换。</summary>
public sealed class WindowService
{
    /// <summary>操作失败时触发（参数为用户可读的提示信息），由上层决定如何展示。</summary>
    public event Action<string>? ErrorOccurred;

    /// <summary>
    /// 切换目标应用窗口状态：最小化/隐藏 → 还原并置前；显示中 → 按配置最小化或隐藏；
    /// 进程未运行 → 按配置路径启动。
    /// </summary>
    public void Toggle(HotkeyEntry entry)
    {
        // 从 WndProc 同步调用，任何异常逃出都会导致进程崩溃，必须兜底
        try
        {
            ToggleCore(entry);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"切换「{entry.ProcessName}」失败：{ex.Message}");
        }
    }

    private void ToggleCore(HotkeyEntry entry)
    {
        var hwnd = FindWindow(entry);
        if (hwnd == IntPtr.Zero)
        {
            Launch(entry);
            return;
        }

        if (!User32.IsWindowVisible(hwnd) || User32.IsIconic(hwnd))
        {
            User32.ShowWindow(hwnd, User32.SW_RESTORE);
            ForceForeground(hwnd);
        }
        else if (User32.GetForegroundWindow() != hwnd)
        {
            // 窗口可见但不在前台（刚被其他快捷键切走）：先置前而不是直接隐藏
            ForceForeground(hwnd);
        }
        else
        {
            // HideMode 在 JSON 里可能被显式写成 null，用 string.Equals 避免空引用
            var command = string.Equals(entry.HideMode, "hide", StringComparison.OrdinalIgnoreCase)
                ? User32.SW_HIDE
                : User32.SW_MINIMIZE;
            User32.ShowWindow(hwnd, command);
        }
    }

    private static IntPtr FindWindow(HotkeyEntry entry)
    {
        // 优先按窗口类名查找：FindWindow 能找到被隐藏（最小化到托盘）的窗口
        if (!string.IsNullOrWhiteSpace(entry.WindowClass))
        {
            var hwnd = User32.FindWindow(entry.WindowClass, null);
            if (hwnd != IntPtr.Zero)
                return hwnd;
        }

        if (!string.IsNullOrWhiteSpace(entry.ProcessName))
        {
            foreach (var process in Process.GetProcessesByName(entry.ProcessName))
            {
                using (process)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        return process.MainWindowHandle;

                    var hwnd = FindWindowByProcessId((uint)process.Id);
                    if (hwnd != IntPtr.Zero)
                        return hwnd;
                }
            }
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindWindowByProcessId(uint processId)
    {
        var result = IntPtr.Zero;
        User32.EnumWindows((hwnd, _) =>
        {
            User32.GetWindowThreadProcessId(hwnd, out var pid);
            // 顶层且无 owner 即认为是主窗口；不要求可见，否则 hide 模式藏掉的窗口找不回
            if (pid == processId && User32.GetWindow(hwnd, User32.GW_OWNER) == IntPtr.Zero)
            {
                result = hwnd;
                return false; // 停止枚举
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// SetForegroundWindow 在非前台进程调用时会被系统拒绝，
    /// 先用 AttachThreadInput 挂靠到当前前台线程再置前。
    /// </summary>
    private static void ForceForeground(IntPtr hwnd)
    {
        var foreground = User32.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero
            ? 0u
            : User32.GetWindowThreadProcessId(foreground, out _);
        var currentThread = User32.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread;

        if (attached)
            User32.AttachThreadInput(currentThread, foregroundThread, true);

        User32.BringWindowToTop(hwnd);
        User32.SetForegroundWindow(hwnd);

        if (attached)
            User32.AttachThreadInput(currentThread, foregroundThread, false);
    }

    private void Launch(HotkeyEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ExePath) || !File.Exists(entry.ExePath))
        {
            ErrorOccurred?.Invoke($"未找到「{entry.ProcessName}」的窗口，且启动路径无效：{entry.ExePath}");
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(entry.ExePath) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ErrorOccurred?.Invoke($"启动「{entry.ProcessName}」失败：{ex.Message}");
        }
    }
}
