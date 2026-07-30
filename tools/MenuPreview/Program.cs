// 菜单样式预览：用真实的 TrayIcon 构造菜单，截图保存后退出。
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using HotkeyManager.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 真实 TrayIcon + 空回调；通过反射取出内部菜单来展示
        using var tray = new TrayIcon(
            openConfig: () => { },
            reloadConfig: () => { },
            togglePause: () => true,
            isAutostart: () => true,
            setAutostart: _ => { },
            exit: () => { });
        var menu = (ContextMenuStrip)typeof(TrayIcon)
            .GetField("_menu", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(tray)!;

        var form = new Form
        {
            Text = "MenuPreview",
            Width = 320,
            Height = 280,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(300, 200),
            TopMost = true // 保证不被其他窗口遮挡，截图才可靠
        };
        form.Shown += (_, _) =>
        {
            menu.Show(form, new Point(16, 16));
            var timer = new System.Windows.Forms.Timer { Interval = 800 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Capture(menu);
                Application.Exit();
            };
            timer.Start();
        };
        Application.Run(form);
    }

    private static void Capture(ContextMenuStrip menu)
    {
        // 固定截取窗体所在区域，避免菜单变大后被裁剪
        var bounds = new Rectangle(280, 180, 400, 360);
        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "menu-preview.png");
        bmp.Save(path);

        // 调试：导出各条目布局信息
        var lines = new List<string> { $"menu.Bounds={menu.Bounds}" };
        foreach (ToolStripItem item in menu.Items)
            lines.Add($"{item.GetType().Name} text={item.Text} Bounds={item.Bounds}");
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "menu-layout.txt"), lines);
    }
}
