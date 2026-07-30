using System.Windows.Forms;
using Microsoft.Win32;

namespace HotkeyManager.Core;

/// <summary>通过 HKCU\...\Run 键管理开机自启，指向当前运行的 exe。</summary>
public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HotkeyManager";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string path
            && string.Equals(path, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (enabled)
            key.SetValue(ValueName, Application.ExecutablePath);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
