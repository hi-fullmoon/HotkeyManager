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

    // 启动中状态：目标程序启动到窗口出现需要时间，期间连按热键会反复走进 Launch 打开多个窗口。
    // 记录启动的进程对象用于自检——进程已退出说明启动失败/闪退，允许立即重试；
    // 窗口一旦出现 FindWindow 就能命中，状态随之解除。时间仅作兜底：进程卡死一直不出窗口时允许重试。
    // Toggle 始终在 UI 线程（WndProc 同步）调用，无需加锁。
    private sealed class LaunchState
    {
        public required Process? Process; // UseShellExecute 启动 UWP/协议时可能拿不到进程对象，为 null 时只能靠时间兜底
        public required DateTime FallbackDeadline;
    }

    private static readonly TimeSpan LaunchFallbackTimeout = TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, LaunchState> _launching = new();

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
        var launchKey = entry.ProcessName ?? entry.ExePath;
        var hwnd = FindWindow(entry);
        if (hwnd == IntPtr.Zero)
        {
            if (launchKey is not null && _launching.TryGetValue(launchKey, out var state))
            {
                var aliveOrUnknown = state.Process is null || !state.Process.HasExited;
                if (aliveOrUnknown && DateTime.UtcNow < state.FallbackDeadline)
                    return; // 仍在启动中：忽略本次触发，避免连按开出多个窗口

                // 进程已退出（启动失败/闪退）或超过兜底超时：解除状态，允许重试
                ClearLaunchState(launchKey);
            }

            // 只有真正启动了进程才记状态：启动失败时下次按键应再次尝试并提示错误
            if (TryLaunch(entry, out var started) && launchKey is not null)
                _launching[launchKey] = new LaunchState
                {
                    Process = started,
                    FallbackDeadline = DateTime.UtcNow + LaunchFallbackTimeout,
                };
            return;
        }

        // 窗口已出现，启动成功，解除启动中状态
        if (launchKey is not null)
            ClearLaunchState(launchKey);

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
            User32.ShowWindow(hwnd, User32.SW_MINIMIZE);
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
        // 在无 owner 的顶层窗口里打分选优：带标题 > 可见 > 非最小化。
        // 主窗口几乎总有标题，隐藏的辅助/消息窗口通常没有，打分优先标题可避开后者；
        // 不把"可见"设为硬性条件，否则 hide 模式藏掉的窗口找不回。
        var result = IntPtr.Zero;
        var bestScore = -1;
        User32.EnumWindows((hwnd, _) =>
        {
            User32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != processId || User32.GetWindow(hwnd, User32.GW_OWNER) != IntPtr.Zero)
                return true;

            var score = 0;
            if (User32.GetWindowTextLength(hwnd) > 0)
                score += 4;
            if (User32.IsWindowVisible(hwnd))
                score += 2;
            if (!User32.IsIconic(hwnd))
                score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                result = hwnd;
            }
            return true; // 继续枚举，取分数最高的候选
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

    private void ClearLaunchState(string launchKey)
    {
        if (_launching.Remove(launchKey, out var state))
            state.Process?.Dispose();
    }

    /// <summary>
    /// 启动目标进程。返回 false 表示启动失败（已通过 <see cref="ErrorOccurred"/> 提示）；
    /// 返回 true 时 <paramref name="started"/> 为启动的进程，壳启动拿不到进程对象时为 null。
    /// </summary>
    private bool TryLaunch(HotkeyEntry entry, out Process? started)
    {
        started = null;
        if (string.IsNullOrWhiteSpace(entry.ExePath) || !File.Exists(entry.ExePath))
        {
            ErrorOccurred?.Invoke($"未找到「{entry.ProcessName}」的窗口，且启动路径无效：{entry.ExePath}");
            return false;
        }

        try
        {
            // 进程句柄保留在 LaunchState 中用于 HasExited 自检，不能在这里 Dispose
            started = Process.Start(new ProcessStartInfo(entry.ExePath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ErrorOccurred?.Invoke($"启动「{entry.ProcessName}」失败：{ex.Message}");
            return false;
        }
    }
}
