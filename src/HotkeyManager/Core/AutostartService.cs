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
        return TryGetEnabled(out var enabled, out _) && enabled;
    }

    /// <summary>读取当前程序的自启状态；读取失败时返回 false 并提供错误信息。</summary>
    public static bool TryGetEnabled(out bool enabled, out string? error)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            enabled = IsCurrentExecutableCommand(key?.GetValue(ValueName) as string);
            error = null;
            return true;
        }
        catch (Exception ex) when (IsRegistryException(ex))
        {
            enabled = false;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>设置当前程序的自启状态；注册表操作失败时返回 false 并提供错误信息。</summary>
    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        try
        {
            if (enabled)
            {
                // CreateSubKey 同时覆盖 Run 键尚不存在的情况。
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                if (key is null)
                    throw new IOException("无法创建或打开当前用户的 Run 注册表项。");

                key.SetValue(ValueName, QuoteExecutablePath(Application.ExecutablePath), RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            error = null;
            return true;
        }
        catch (Exception ex) when (IsRegistryException(ex))
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsCurrentExecutableCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var trimmed = command.Trim();
        var executablePath = Application.ExecutablePath;

        // 新值与安装器都统一写成带引号的纯 exe 路径。严格要求结束引号位于命令末尾，
        // 避免把带额外参数的命令或名称相近的其他程序误判为当前实例。
        if (trimmed.Length >= 2 && trimmed[0] == '\"' && trimmed[^1] == '\"')
        {
            return string.Equals(
                trimmed[1..^1],
                executablePath,
                StringComparison.OrdinalIgnoreCase);
        }

        // 兼容旧版本在无空格路径中写入的裸路径；含空白的裸路径不是有效的 Run 命令。
        return !executablePath.Any(char.IsWhiteSpace)
            && string.Equals(trimmed, executablePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteExecutablePath(string path) => $"\"{path}\"";

    private static bool IsRegistryException(Exception ex) =>
        ex is UnauthorizedAccessException or System.Security.SecurityException or IOException;
}
