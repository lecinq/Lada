using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Lada.Services;

public sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
{
    public ThemedToolStripRenderer((Color Background, Color Border, Color Text, Color Hover, Color Accent, int CornerRadius, int BorderThickness) colors)
        : base(new ThemedColorTable(colors))
    {
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        try
        {
            var table = (ThemedColorTable)ColorTable;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            var isHighlighted = e.Item.Selected && e.Item.Enabled;
            using var brush = new SolidBrush(isHighlighted ? table.HoverColor : table.MenuBackgroundColor);
            g.FillRectangle(brush, bounds);
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(OnRenderMenuItemBackground), ex);
        }
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        try
        {
            var table = (ThemedColorTable)ColorTable;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // A filled dot in the theme's accent color, matching the WPF
            // context menus' own checkable-item glyph (see Styles/MenuStyles.xaml)
            // instead of WinForms' default light-themed checkbox square, which
            // was barely legible against a dark custom background.
            const int size = 8;
            var rect = new Rectangle(
                e.ImageRectangle.X + (e.ImageRectangle.Width - size) / 2,
                e.ImageRectangle.Y + (e.ImageRectangle.Height - size) / 2,
                size, size);
            using var brush = new SolidBrush(table.AccentColor);
            g.FillEllipse(brush, rect);
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(OnRenderItemCheck), ex);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        try
        {
            var table = (ThemedColorTable)ColorTable;
            using var pen = new Pen(table.BorderColor, table.BorderThicknessPx);
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(OnRenderSeparator), ex);
        }
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        try
        {
            var table = (ThemedColorTable)ColorTable;
            ApplyRoundedRegion(e.ToolStrip, table.CornerRadiusPx);

            using var pen = new Pen(table.BorderColor, table.BorderThicknessPx);
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);

            if (table.CornerRadiusPx > 0)
            {
                using var path = RoundedRectanglePath(rect, table.CornerRadiusPx);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
            else
            {
                e.Graphics.DrawRectangle(pen, rect);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(OnRenderToolStripBorder), ex);
        }
    }

    // Clips the dropdown's own window region to a rounded rectangle so the
    // corners are actually rounded (not just painted that way over square
    // corners) -- WinForms has no CornerRadius-style property for this, a
    // Region is the standard way to shape a control's outline.
    private static void ApplyRoundedRegion(ToolStrip toolStrip, int radius)
    {
        if (radius <= 0)
        {
            if (toolStrip.Region is not null)
            {
                toolStrip.Region = null;
            }
            return;
        }

        using var path = RoundedRectanglePath(new Rectangle(0, 0, toolStrip.Width, toolStrip.Height), radius);
        toolStrip.Region = new Region(path);
    }

    private static GraphicsPath RoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly (Color Background, Color Border, Color Text, Color Hover, Color Accent, int CornerRadius, int BorderThickness) _colors;

        public ThemedColorTable((Color Background, Color Border, Color Text, Color Hover, Color Accent, int CornerRadius, int BorderThickness) colors)
        {
            _colors = colors;
        }

        public Color MenuBackgroundColor => _colors.Background;
        public Color BorderColor => _colors.Border;
        public Color HoverColor => _colors.Hover;
        public Color AccentColor => _colors.Accent;
        public int CornerRadiusPx => _colors.CornerRadius;
        public int BorderThicknessPx => _colors.BorderThickness;

        public override Color ToolStripDropDownBackground => _colors.Background;
        public override Color ImageMarginGradientBegin => _colors.Background;
        public override Color ImageMarginGradientMiddle => _colors.Background;
        public override Color ImageMarginGradientEnd => _colors.Background;
        public override Color MenuBorder => _colors.Border;
        public override Color MenuItemBorder => _colors.Border;
        public override Color MenuItemSelected => _colors.Hover;
        public override Color MenuItemSelectedGradientBegin => _colors.Hover;
        public override Color MenuItemSelectedGradientEnd => _colors.Hover;
        public override Color SeparatorDark => _colors.Border;
        public override Color SeparatorLight => _colors.Border;
        public override Color CheckBackground => _colors.Background;
        public override Color CheckSelectedBackground => _colors.Hover;
        public override Color CheckPressedBackground => _colors.Hover;
    }
}
