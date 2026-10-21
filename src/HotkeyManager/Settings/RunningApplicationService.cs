using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HotkeyManager.Interop;
using Microsoft.Win32.SafeHandles;

namespace HotkeyManager.Settings;

internal sealed record RunningApplication(int id, string name, string title, string process, string path, string? error);

internal static class RunningApplicationService
{
    public static List<RunningApplication> List()
    {
        var result = new List<RunningApplication>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId) continue;
                    var app = Read(process);
                    if (app is not null) result.Add(app);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        return result.OrderBy(a => a.name, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.id).ToList();
    }

    public static RunningApplication Resolve(int id)
    {
        using var process = Process.GetProcessById(id);
        var app = Read(process) ?? throw new InvalidOperationException("此应用窗口已关闭，请刷新列表。");
        if (app.error is not null) throw new InvalidOperationException(app.error);
        return app;
    }

    private static RunningApplication? Read(Process process)
    {
        var window = process.MainWindowHandle;
        if (window == IntPtr.Zero || !User32.IsWindowVisible(window)) return null;
        var title = process.MainWindowTitle;
        if (string.IsNullOrWhiteSpace(title)) return null;
        var name = process.ProcessName;
        var path = "";
        string? error = null;
        using var handle = OpenProcess(0x1000, false, process.Id); // PROCESS_QUERY_LIMITED_INFORMATION
        var buffer = new StringBuilder(32768);
        var size = buffer.Capacity;
        if (handle.IsInvalid || !QueryFullProcessImageName(handle, 0, buffer, ref size))
            error = "无法读取应用路径，请通过文件夹按钮手动选择 exe。";
        else path = buffer.ToString();
        if (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            error = "此应用使用系统窗口宿主，请通过文件夹按钮选择实际应用。";
        var displayName = name;
        if (path.Length > 0)
        {
            try { displayName = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim() is { Length: > 0 } description ? description : name; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return new(process.Id, displayName, title, name, path, error);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder exeName, ref int size);
}
