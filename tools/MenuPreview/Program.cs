// 菜单样式预览：用真实的 Win11MenuStyle 渲染一个菜单，截图保存后退出。
using System.Drawing;
using System.Windows.Forms;
using HotkeyManager.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开配置文件");
        menu.Items.Add("重新加载配置");
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出");
        Win11MenuStyle.Apply(menu);

        var form = new Form
        {
            Text = "MenuPreview",
            Width = 320,
            Height = 240,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(300, 200)
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
        var bounds = new Rectangle(280, 180, 400, 320);
        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "menu-preview.png");
        bmp.Save(path);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "menu-preview-path.txt"), path);

        // 调试：导出各条目布局信息
        var lines = new List<string> { $"menu.Bounds={menu.Bounds}" };
        foreach (ToolStripItem item in menu.Items)
            lines.Add($"{item.GetType().Name} text={item.Text} Bounds={item.Bounds} Content={item.ContentRectangle} Font={item.Font.Name},{item.Font.Size}");
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "menu-layout.txt"), lines);
    }
}
