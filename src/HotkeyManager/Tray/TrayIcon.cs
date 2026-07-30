using System.Drawing;
using System.Windows.Forms;

namespace HotkeyManager.Tray;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;

    public TrayIcon(Action openConfig, Action reloadConfig, Action exit)
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("打开配置文件", null, (_, _) => openConfig());
        _menu.Items.Add("重新加载配置", null, (_, _) => reloadConfig());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("退出", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
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
