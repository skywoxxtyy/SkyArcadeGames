using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Windows.Forms;

namespace ArcadeHub
{
    public class TwitchButton : UserControl
    {
        private bool _hovered = false;
        private bool _pressed = false;
        private readonly Color _normalColor  = Color.FromArgb(145, 70, 255);
        private readonly Color _hoverColor   = Color.FromArgb(170, 100, 255);
        private readonly Color _pressedColor = Color.FromArgb(120, 50, 220);

        // Настоящая SVG-иконка Twitch, перегнанная в PNG через публичный URL
        private static Image _twitchIcon;
        private static bool  _iconLoaded;

        public TwitchButton()
        {
            Size   = new Size(160, 36);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            if (!_iconLoaded)
                LoadIconAsync();
        }

        // ── Загрузить иконку Twitch с CDN ──────────────────────────────────
        private static void LoadIconAsync()
        {
            _iconLoaded = true;                       // не пытаться повторно
            var wc = new WebClient();
            wc.DownloadDataCompleted += (s, e) =>
            {
                if (e.Error != null || e.Cancelled) return;
                try
                {
                    using (var ms = new System.IO.MemoryStream(e.Result))
                        _twitchIcon = Image.FromStream(ms);
                }
                catch { }
            };
            // Официальная PNG-иконка Twitch (32×32) с публичного CDN
            wc.DownloadDataAsync(new Uri(
                "https://static.twitchscdn.net/assets/favicon-32-e29e246c157142c1a8f8.png"));
        }

        // ── Hover / Press ───────────────────────────────────────────────────
        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp  (MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e);   }

        protected override void OnClick(EventArgs e)
        {
            try { Process.Start(new ProcessStartInfo { FileName = "https://www.twitch.tv/skywoxxtyyy", UseShellExecute = true }); } catch { }
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // ── Фон с градиентом ─────────────────────────────────────────
            var c1 = _pressed ? _pressedColor : _hovered ? _hoverColor : _normalColor;
            var c2 = Color.FromArgb(Math.Max(0, c1.R - 40), Math.Max(0, c1.G - 20), Math.Max(0, c1.B - 20));
            using (var path = RoundedRect(ClientRectangle, 8))
            using (var gb = new LinearGradientBrush(ClientRectangle, c1, c2, LinearGradientMode.Vertical))
                g.FillPath(gb, path);

            // Тонкая светлая рамка сверху (glassmorphism-штрих)
            using (var path2 = RoundedRect(new Rectangle(1, 1, Width - 3, Height - 3), 7))
            using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
                g.DrawPath(pen, path2);

            // ── Иконка Twitch ────────────────────────────────────────────
            int iconSize = Height - 10;
            int iconX    = 8;
            int iconY    = (Height - iconSize) / 2;

            if (_twitchIcon != null)
            {
                g.DrawImage(_twitchIcon, iconX, iconY, iconSize, iconSize);
            }
            else
            {
                // Fallback: рисуем упрощённый логотип Twitch вручную
                DrawTwitchLogo(g, iconX, iconY, iconSize);
            }

            // ── Текст ────────────────────────────────────────────────────
            using (var sf   = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var tb   = new SolidBrush(Color.White))
            {
                g.DrawString("  Twitch", font, tb,
                    new RectangleF(iconX + iconSize + 4, 0, Width - iconX - iconSize - 8, Height), sf);
            }
        }

        // ── Нарисованный логотип Twitch (запасной вариант) ─────────────────
        private void DrawTwitchLogo(Graphics g, int x, int y, int size)
        {
            float s = size / 20f;
            // Тело
            using (var wb = new SolidBrush(Color.White))
            {
                // Прямоугольник логотипа
                var pts = new PointF[]
                {
                    new PointF(x + 2*s,  y + 0),
                    new PointF(x + 18*s, y + 0),
                    new PointF(x + 18*s, y + 13*s),
                    new PointF(x + 13*s, y + 13*s),
                    new PointF(x + 11*s, y + 16*s),
                    new PointF(x + 9*s,  y + 13*s),
                    new PointF(x + 2*s,  y + 13*s),
                };
                g.FillPolygon(wb, pts);
            }
            // Полосы (чат-иконка)
            using (var pb = new SolidBrush(Color.FromArgb(145, 70, 255)))
            {
                g.FillRectangle(pb, x + 6*s, y + 4*s, 2.5f*s, 5*s);
                g.FillRectangle(pb, x + 11*s, y + 4*s, 2.5f*s, 5*s);
            }
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
