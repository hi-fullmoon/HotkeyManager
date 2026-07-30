using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using HotkeyManager.Interop;
using Microsoft.Win32;

namespace HotkeyManager.Tray;

/// <summary>
/// 把 ContextMenuStrip 装扮成 Windows 11 风格：
/// 圆角弹出窗口、跟随系统浅/深色主题、圆角选中高亮、Segoe UI 字体。
/// </summary>
public static class Win11MenuStyle
{
    public static void Apply(ContextMenuStrip menu)
    {
        var dark = IsDarkMode();
        menu.Renderer = new FluentRenderer(dark);
        menu.ForeColor = dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(26, 26, 26);
        menu.Font = ResolveFont();
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = false;
        menu.Padding = new Padding(4);
        foreach (ToolStripItem item in menu.Items)
            item.Padding = new Padding(12, 6, 12, 6);

        menu.Opened += (_, _) =>
        {
            if (!menu.IsHandleCreated)
                return;
            var hwnd = menu.Handle;
            var round = DwmApi.DWMWCP_ROUND;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            var darkMode = dark ? 1 : 0;
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        };
    }

    /// <summary>读注册表判断应用浅/深色主题（HKCU\...\Personalize\AppsUseLightTheme）。</summary>
    private static bool IsDarkMode()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int i && i == 0;
        }
        catch
        {
            return false; // 读不到就按浅色处理
        }
    }

    /// <summary>Win11 默认 UI 字体是 Segoe UI Variable Text，缺失时回退 Segoe UI。</summary>
    private static Font ResolveFont()
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        var family = Array.Exists(fonts.Families, f => f.Name == "Segoe UI Variable Text")
            ? "Segoe UI Variable Text"
            : "Segoe UI";
        return new Font(family, 9f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static Color Background(bool dark) => dark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);

    private static Color Hover(bool dark) =>
        dark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class FluentRenderer : ToolStripProfessionalRenderer
    {
        private readonly bool _dark;

        public FluentRenderer(bool dark) : base(new FluentColors(dark)) => _dark = dark;

        /// <summary>选中项画成圆角高亮（接近菜单全宽），替代默认的直角渐变。</summary>
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            var rect = new Rectangle(Point.Empty, e.Item.Bounds.Size);
            rect.Inflate(-2, -1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(rect, 4);
            using var brush = new SolidBrush(Hover(_dark));
            e.Graphics.FillPath(brush, path);
        }

        /// <summary>
        /// 默认文本绘制按字体行高对齐，换成 Segoe UI Variable 后视觉上偏上；
        /// 改为手动垂直居中。注意 e.Graphics 的原点已平移到条目左上角，
        /// 必须用条目相对坐标（不能用 Bounds 的父容器坐标）。
        /// </summary>
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var size = e.Item.Bounds.Size;
            var rect = new Rectangle(16, 0, Math.Max(0, size.Width - 20), size.Height);
            var color = e.Item.Enabled ? e.Item.ForeColor : SystemColors.GrayText;
            TextRenderer.DrawText(
                e.Graphics, e.Text, e.Item.Font, rect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    private sealed class FluentColors : ProfessionalColorTable
    {
        private readonly bool _dark;

        public FluentColors(bool dark) => _dark = dark;

        public override Color ToolStripDropDownBackground => Background(_dark);
        public override Color MenuBorder => _dark ? Color.FromArgb(69, 69, 69) : Color.FromArgb(229, 229, 229);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Hover(_dark);
        public override Color MenuItemSelectedGradientBegin => Hover(_dark);
        public override Color MenuItemSelectedGradientEnd => Hover(_dark);
        public override Color MenuItemPressedGradientBegin => Hover(_dark);
        public override Color MenuItemPressedGradientEnd => Hover(_dark);
        public override Color ImageMarginGradientBegin => Background(_dark);
        public override Color ImageMarginGradientMiddle => Background(_dark);
        public override Color ImageMarginGradientEnd => Background(_dark);
        public override Color SeparatorDark => _dark ? Color.FromArgb(69, 69, 69) : Color.FromArgb(229, 229, 229);
        public override Color SeparatorLight => Color.Transparent;
        public override Color ToolStripBorder => Background(_dark);
    }
}
