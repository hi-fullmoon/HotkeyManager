using System.Drawing;
using System.Windows.Forms;

namespace HotkeyManager.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;

    public TrayIcon(Action openConfig, Action reloadConfig, Func<bool> togglePause,
        Func<bool> isAutostart, Action<bool> setAutostart, Action exit)
    {
        var pauseItem = new ToolStripMenuItem("暂停热键");
        pauseItem.Click += (_, _) =>
            pauseItem.Text = togglePause() ? "恢复热键" : "暂停热键";

        var autostartItem = new ToolStripMenuItem();
        var autostart = isAutostart();
        autostartItem.Text = autostart ? "关闭开机自启" : "开启开机自启";
        autostartItem.Click += (_, _) =>
        {
            autostart = !autostart;
            setAutostart(autostart);
            autostartItem.Text = autostart ? "关闭开机自启" : "开启开机自启";
        };

        _menu = new ContextMenuStrip();
        _menu.Items.Add("打开配置文件", null, (_, _) => openConfig());
        _menu.Items.Add("重新加载配置", null, (_, _) => reloadConfig());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(pauseItem);
        _menu.Items.Add(autostartItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => exit());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem($"HotkeyManager v{Application.ProductVersion}")
        {
            Enabled = false
        });
        Win11MenuStyle.Apply(_menu);

        _icon = new NotifyIcon
        {
            // 复用嵌入 exe 的应用图标，避免再分发一份 .ico 文件
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)!,
            Text = "HotkeyManager 全局热键",
            Visible = true,
            ContextMenuStrip = _menu
        };
    }

    public void ShowBalloon(string title, string message) =>
        _icon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
