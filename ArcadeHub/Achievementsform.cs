using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArcadeHub
{
    public class AchievementsForm : Form
    {
        private string          _filterGame;
        private FlowLayoutPanel _panel;
        private Panel[]         _tabButtons;
        private Label           _lblCount;

        private static readonly string[] Games = { "Все", "Тетрис", "Сапёр", "Гонки", "Змейка" };

        private static readonly Color[] GameColors =
        {
            Color.FromArgb(120, 120, 160),   // Все
            Color.FromArgb(0,   188, 212),   // Тетрис
            Color.FromArgb(233, 69,  96),    // Сапёр
            Color.FromArgb(255, 167, 38),    // Гонки
            Color.FromArgb(102, 187, 106),   // Змейка
        };

        public AchievementsForm(string filterGame = "Все")
        {
            _filterGame = filterGame;
            BuildUI();
            LoadAchievements();
        }

        private void BuildUI()
        {
            Text            = "Достижения — Sky Arcade Games";
            Size            = new Size(720, 640);
            BackColor       = Color.FromArgb(20, 20, 36);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            // ── Шапка ─────────────────────────────────────────────────────
            var header = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(720, 52),
                BackColor = Color.FromArgb(16, 16, 30)
            };
            header.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(40, 233, 69, 96), 1))
                    e.Graphics.DrawLine(pen, 0, 51, 720, 51);
            };

            var lblTitle = new Label
            {
                Text      = "ДОСТИЖЕНИЯ",
                Font      = new Font("Segoe UI", 17f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 12),
                BackColor = Color.Transparent
            };

            _lblCount = new Label
            {
                Location  = new Point(520, 17),
                Size      = new Size(180, 20),
                ForeColor = Color.FromArgb(100, 120, 160),
                Font      = new Font("Segoe UI", 9f),
                BackColor = Color.Transparent
            };

            header.Controls.AddRange(new Control[] { lblTitle, _lblCount });
            Controls.Add(header);

            // ── Вкладки фильтра ───────────────────────────────────────────
            _tabButtons = new Panel[Games.Length];
            int tabX = 10;
            for (int i = 0; i < Games.Length; i++)
            {
                int idx = i;
                var tab = new TabButton(Games[i], GameColors[i], Games[i] == _filterGame);
                tab.Location = new Point(tabX, 58);
                tab.Size     = new Size(96, 32);
                tab.Click   += (s, e) =>
                {
                    _filterGame = Games[idx];
                    for (int j = 0; j < _tabButtons.Length; j++)
                        ((TabButton)_tabButtons[j]).SetActive(j == idx);
                    LoadAchievements();
                };
                _tabButtons[i] = tab;
                Controls.Add(tab);
                tabX += 100;
            }

            // ── Прокручиваемый список ─────────────────────────────────────
            _panel = new FlowLayoutPanel
            {
                Location      = new Point(8, 96),
                Size          = new Size(696, 532),
                AutoScroll    = true,
                BackColor     = Color.Transparent,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false
            };
            Controls.Add(_panel);
        }

        private void LoadAchievements()
        {
            _panel.Controls.Clear();

            var list = AchievementManager.Achievements;
            if (_filterGame != "Все")
                list = list.FindAll(a => a.Game == _filterGame);

            int unlocked = list.FindAll(a => a.IsUnlocked).Count;
            _lblCount.Text = $"Получено: {unlocked} / {list.Count}";

            foreach (var ach in list)
                _panel.Controls.Add(MakeCard(ach));
        }

        // ── Карточка достижения ────────────────────────────────────────────
        private Panel MakeCard(Achievement ach)
        {
            bool done = ach.IsUnlocked;

            Color accent = done
                ? Color.FromArgb(255, 200, 0)
                : Color.FromArgb(70, 70, 100);

            var card = new DoubleBufferedCard
            {
                Size      = new Size(668, 62),
                BackColor = Color.Transparent,
                Margin    = new Padding(2, 2, 2, 2)
            };

            card.Paint += (s, e) =>
            {
                var g  = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rc = new Rectangle(0, 0, card.Width - 1, card.Height - 1);

                // Фон карточки
                using (var path = RoundRect(rc, 10))
                {
                    Color bg = done
                        ? Color.FromArgb(30, 60, 30)
                        : Color.FromArgb(24, 24, 44);
                    using (var b = new SolidBrush(bg))
                        g.FillPath(b, path);

                    using (var pen = new Pen(Color.FromArgb(done ? 180 : 50, accent), done ? 1.5f : 1f))
                        g.DrawPath(pen, path);
                }

                // Цветная полоска слева
                g.FillRectangle(new SolidBrush(Color.FromArgb(done ? 230 : 60, accent)), 0, 0, 4, card.Height);

                // ── Иконка-плашка ─────────────────────────────────────────
                var iconBg = done ? Color.FromArgb(255, 200, 0) : Color.FromArgb(50, 50, 75);
                using (var iconPath = RoundRect(new Rectangle(8, 8, 44, 44), 8))
                using (var ib = new SolidBrush(iconBg))
                    g.FillPath(ib, iconPath);

                using (var font = new Font("Consolas", 7f, FontStyle.Bold))
                using (var fb   = new SolidBrush(done ? Color.FromArgb(30, 20, 0) : Color.FromArgb(120, 130, 160)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(ach.Icon, font, fb, new RectangleF(8, 8, 44, 44), sf);
                }

                // ── Название ──────────────────────────────────────────────
                using (var font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (var b    = new SolidBrush(done ? Color.FromArgb(255, 200, 0) : Color.FromArgb(200, 205, 225)))
                    g.DrawString(ach.Name, font, b, new PointF(62, 7));

                // ── Описание ──────────────────────────────────────────────
                using (var font = new Font("Segoe UI", 8f))
                using (var b    = new SolidBrush(Color.FromArgb(110, 115, 140)))
                    g.DrawString(ach.Description, font, b, new PointF(62, 26));

                // ── Тег игры ──────────────────────────────────────────────
                int gameIdx = Array.IndexOf(new[]{"Тетрис","Сапёр","Гонки","Змейка"}, ach.Game);
                var gameColor = gameIdx >= 0 ? GameColors[gameIdx + 1] : Color.Gray;
                using (var font = new Font("Segoe UI", 7f, FontStyle.Bold))
                using (var b    = new SolidBrush(Color.FromArgb(180, gameColor)))
                    g.DrawString(ach.Game, font, b, new PointF(440, 7));

                // ── Статус ────────────────────────────────────────────────
                string status     = done ? "ПОЛУЧЕНО" : "заперто";
                var    statusColor = done ? Color.FromArgb(80, 200, 80) : Color.FromArgb(80, 85, 105);
                using (var font = new Font("Segoe UI", 7.5f, done ? FontStyle.Bold : FontStyle.Regular))
                using (var b    = new SolidBrush(statusColor))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Far };
                    g.DrawString(status, font, b, new RectangleF(440, 22, 218, 14), sf);
                }

                // ── Прогресс-бар ──────────────────────────────────────────
                int bx = 62, by = 48, bw = 596, bh = 6;
                using (var bgPath = RoundRect(new Rectangle(bx, by, bw, bh), 3))
                using (var bgB    = new SolidBrush(Color.FromArgb(35, 36, 58)))
                    g.FillPath(bgB, bgPath);

                int fill = ach.MaxProgress > 0
                    ? (int)((float)ach.Progress / ach.MaxProgress * bw)
                    : 0;
                if (fill > 0)
                {
                    var c1 = done ? Color.FromArgb(255, 200, 0) : Color.FromArgb(80, 110, 200);
                    var c2 = done ? Color.FromArgb(255, 140, 20) : Color.FromArgb(120, 160, 255);
                    using (var fillPath = RoundRect(new Rectangle(bx, by, fill, bh), 3))
                    using (var fb2 = new LinearGradientBrush(
                        new Rectangle(bx, by, Math.Max(fill, 1), bh), c1, c2,
                        LinearGradientMode.Horizontal))
                        g.FillPath(fb2, fillPath);
                }

                // Прогресс текст
                using (var font = new Font("Consolas", 6.5f))
                using (var b    = new SolidBrush(Color.FromArgb(80, 85, 110)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Far };
                    g.DrawString($"{ach.Progress}/{ach.MaxProgress}", font, b,
                        new RectangleF(bx, by - 12, bw, 12), sf);
                }
            };

            return card;
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90);
            p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ── Вкладка-кнопка фильтра ─────────────────────────────────────────────
    public class TabButton : Panel
    {
        private string _text;
        private Color  _color;
        private bool   _active;
        private bool   _hovered;

        public TabButton(string text, Color color, bool active)
        {
            _text = text; _color = color; _active = active;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetActive(bool active) { _active = active; Invalidate(); }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rc   = new Rectangle(0, 0, Width - 1, Height - 1);
            var path = RoundRect(rc, 8);

            if (_active)
            {
                using (var b = new SolidBrush(Color.FromArgb(50, _color))) g.FillPath(b, path);
                using (var p = new Pen(_color, 1.5f)) g.DrawPath(p, path);
            }
            else if (_hovered)
            {
                using (var b = new SolidBrush(Color.FromArgb(20, _color))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(80, _color), 1)) g.DrawPath(p, path);
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(20, 20, 40))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(40, 40, 65), 1)) g.DrawPath(p, path);
            }

            using (var f = new Font("Segoe UI", 8.5f, _active ? FontStyle.Bold : FontStyle.Regular))
            using (var b2 = new SolidBrush(_active ? _color : Color.FromArgb(130, 135, 160)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_text, f, b2, new RectangleF(0, 0, Width, Height), sf);
            }

            path.Dispose();
        }

        private GraphicsPath RoundRect(Rectangle b, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90);
            p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ── Двойная буферизация для карточек ───────────────────────────────────
    public class DoubleBufferedCard : Panel
    {
        public DoubleBufferedCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }
}
