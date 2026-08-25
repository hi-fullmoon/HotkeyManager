using HotkeyManager.Core;

namespace HotkeyManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        SingleInstanceGuard? instanceGuard;
        try
        {
            instanceGuard = SingleInstanceGuard.TryAcquire();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法建立单实例保护，HotkeyManager 未启动。\n\n{ex.Message}",
                "HotkeyManager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // 同一 Windows 用户已有实例时直接退出，不创建托盘或其他服务。
        if (instanceGuard is null)
            return;

        using (instanceGuard)
        using (var context = new HotkeyAppContext())
            Application.Run(context);
    }
}
