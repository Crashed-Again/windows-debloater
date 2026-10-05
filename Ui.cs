using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Cub
{
    // ------------------------------------------------------------------ theme
    internal static class Theme
    {
        public static float Scale = 1f;
        public static int S(int v) { return (int)Math.Round(v * Scale); }

        public static readonly Color Bg         = Color.FromArgb(38, 38, 38);
        public static readonly Color Side       = Color.FromArgb(0, 0, 0);
        public static readonly Color Card       = Color.FromArgb(20, 20, 20);
        public static readonly Color Hover      = Color.FromArgb(32, 32, 32);     // nav hover
        public static readonly Color Btn        = Color.FromArgb(58, 58, 58);     // flat grey button
        public static readonly Color BtnHover   = Color.FromArgb(78, 78, 78);
        public static readonly Color Input      = Color.FromArgb(52, 52, 52);
        public static readonly Color Border     = Color.FromArgb(90, 90, 90);
        public static readonly Color Text       = Color.FromArgb(232, 232, 232);
        public static readonly Color Sub        = Color.FromArgb(150, 150, 150);
        public static readonly Color Accent     = Color.FromArgb(59, 130, 246);
        public static readonly Color AccentDim  = Color.FromArgb(30, 78, 160);
        public static readonly Color AccentText = Color.FromArgb(255, 255, 255);
        public static readonly Color Warn       = Color.FromArgb(255, 181, 71);
        public static readonly Color Good       = Color.FromArgb(80, 220, 140);
        public static readonly Color Off        = Color.FromArgb(120, 120, 120);

        // Poppins (what Upscayl uses) if it is installed, otherwise Segoe UI
        public static readonly string Family = PickFamily();
        private static string PickFamily()
        {
            try
            {
                using (var fonts = new InstalledFontCollection())
                    foreach (FontFamily f in fonts.Families)
                        if (string.Equals(f.Name, "Poppins", StringComparison.OrdinalIgnoreCase)) return "Poppins";
            }
            catch { }
            return "Segoe UI";
        }

        public static Font Body  = new Font(Family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Small = new Font(Family, 9f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Bold  = new Font(Family, 10f, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Title = new Font(Family, 20f, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Mono  = new Font("Consolas", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 5x7 dot-matrix letters for the NEONBEAR logo
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'N', new[] { "10001", "11001", "10101", "10101", "10011", "10001", "10001" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'B', new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" } },
            { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } }
        };

        public static void DrawDots(Graphics g, string text, int x, int y, int step, int dia, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(c))
            {
                int cx = x;
                foreach (char ch in text)
                {
                    string[] rows;
                    if (Glyphs.TryGetValue(ch, out rows))
                    {
                        for (int r = 0; r < rows.Length; r++)
                            for (int col = 0; col < rows[r].Length; col++)
                                if (rows[r][col] == '1')
                                    g.FillEllipse(b, cx + col * step, y + r * step, dia, dia);
                    }
                    cx += 6 * step;
                }
            }
        }
    }

    // ------------------------------------------------------------------ dark title bar / scrollbars
    internal static class DarkMode
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subApp, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

        public static void TitleBar(IntPtr handle)
        {
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
            DwmSetWindowAttribute(handle, 19, ref v, sizeof(int));
        }

        public static void Scrollbars(Control c)
        {
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            else c.HandleCreated += (s, e) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        }
    }

    // ------------------------------------------------------------------ rounded card
    internal class Card : Panel
    {
        public Card()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Theme.Round(r, Theme.S(2)))
            using (var b = new SolidBrush(Theme.Card))
                e.Graphics.FillPath(b, p);
        }
    }

    // ------------------------------------------------------------------ toggle switch
    internal sealed class ToggleSwitch : Control
    {
        private bool _on;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.S(46), Theme.S(24));
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
        }

        public bool Checked
        {
            get { return _on; }
            set
            {
                if (_on == value) return;
                _on = value;
                Invalidate();
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(Color.FromArgb(10, 10, 10))) g.FillRectangle(b, r);
            using (var pen = new Pen(_on ? Theme.Accent : Theme.Border, 1.5f)) g.DrawRectangle(pen, r);
            int pad = Theme.S(3);
            int d = Height - pad * 2 - 1;
            int x = _on ? Width - d - pad - 1 : pad;
            using (var b = new SolidBrush(_on ? Theme.Accent : Theme.Off))
                g.FillRectangle(b, x, pad, d, d);
        }
    }

    // ------------------------------------------------------------------ button
    internal sealed class NButton : Control
    {
        public bool Primary;
        private bool _hover;

        public NButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.Bold;
            Size = new Size(Theme.S(110), Theme.S(40));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, fore;
            if (Primary)
            {
                fill = !Enabled ? Color.FromArgb(40, 52, 80) : (_hover ? Color.FromArgb(88, 150, 255) : Theme.Accent);
                fore = Enabled ? Theme.AccentText : Theme.Sub;
            }
            else
            {
                fill = !Enabled ? Color.FromArgb(42, 42, 42) : (_hover ? Theme.BtnHover : Theme.Btn);
                fore = Enabled ? Theme.Text : Theme.Sub;
            }
            using (var p = Theme.Round(r, Theme.S(2)))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, p);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(fore))
                g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // ------------------------------------------------------------------ sidebar nav item
    internal sealed class NavButton : Control
    {
        private bool _hover;
        private bool _selected;

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.Body;
        }

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Side);
            var inner = new Rectangle(Theme.S(10), Theme.S(3), Width - Theme.S(20), Height - Theme.S(6));
            if (_selected || _hover)
            {
                using (var p = Theme.Round(inner, Theme.S(2)))
                using (var b = new SolidBrush(_selected ? Color.FromArgb(34, 34, 34) : Theme.Hover))
                    g.FillPath(b, p);
            }
            if (_selected)
            {
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, inner.X + Theme.S(2), inner.Y + Theme.S(9), Theme.S(3), inner.Height - Theme.S(18));
            }
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(_selected ? Theme.Text : Theme.Sub))
                g.DrawString(Text, Font, b, new RectangleF(inner.X + Theme.S(18), inner.Y, inner.Width - Theme.S(18), inner.Height), sf);
        }
    }

    // ------------------------------------------------------------------ progress bar
    internal sealed class ThinProgress : Control
    {
        private double _value;

        public ThinProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(8);
        }

        public double Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(1, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            var track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Theme.Round(track, 1))
            using (var b = new SolidBrush(Theme.Card))
                g.FillPath(b, p);
            int w = (int)((Width - 1) * _value);
            if (w > Height)
            {
                using (var p = Theme.Round(new Rectangle(0, 0, w, Height - 1), 1))
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, w), Height), Theme.AccentDim, Theme.Accent, 0f))
                    g.FillPath(b, p);
            }
        }
    }
}
