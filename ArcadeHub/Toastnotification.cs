using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArcadeHub
{
    /// <summary>
    /// Всплывающее уведомление о достижении — плавное, современное.
    /// </summary>
    public class ToastNotification : Panel
    {
        private Timer  _timer;
        private int    _phase;       // 0=slide-in, 1=hold, 2=slide-out
        private int    _holdTicks;
        private float  _currentY;
        private float  _targetY;
        private float  _startY;
        private string _text;
        private Form   _owner;
        private float  _progress;   // 0..1 для easing

        // Плавный прогресс заполнения полоски удержания
        private float _holdProgress;

        public ToastNotification(Form owner)
        {
            _owner    = owner;
            Size      = new Size(300, 64);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);

            _timer          = new Timer { Interval = 16 };   // ~60 FPS
            _timer.Tick    += TimerTick;
        }

        public void Show(string text)
        {
            _text        = text;
            _phase       = 0;
            _holdTicks   = 0;
            _holdProgress = 0f;
            _progress    = 0f;

            int x   = _owner.ClientSize.Width - Width - 14;
            _startY = _owner.ClientSize.Height + 10;
            _targetY = _owner.ClientSize.Height - Height - 14;
            _currentY = _startY;

            if (_owner.Controls.Contains(this))
                _owner.Controls.Remove(this);

            _owner.Controls.Add(this);
            BringToFront();
            Location = new Point(x, (int)_currentY);
            Visible  = true;
            _timer.Start();
        }

        // ── Easing ──────────────────────────────────────────────────────────
        private float EaseOutCubic(float t) => 1 - (float)Math.Pow(1 - t, 3);
        private float EaseInCubic (float t) => t * t * t;

        private void TimerTick(object sender, EventArgs e)
        {
            const int HOLD_FRAMES = 220;   // ~3.5 секунды при 60 fps

            switch (_phase)
            {
                case 0: // Slide in (eased)
                    _progress = Math.Min(1f, _progress + 0.06f);
                    _currentY = _startY + (_targetY - _startY) * EaseOutCubic(_progress);
                    Location  = new Point(Left, (int)_currentY);
                    Invalidate();
                    if (_progress >= 1f) { _phase = 1; _progress = 0f; }
                    break;

                case 1: // Hold
                    _holdTicks++;
                    _holdProgress = (float)_holdTicks / HOLD_FRAMES;
                    Invalidate();
                    if (_holdTicks >= HOLD_FRAMES) { _phase = 2; _progress = 0f; }
                    break;

                case 2: // Slide out (eased)
                    _progress  = Math.Min(1f, _progress + 0.06f);
                    _currentY  = _targetY + (_startY - _targetY) * EaseInCubic(_progress);
                    Location   = new Point(Left, (int)_currentY);
                    Invalidate();
                    if (_progress >= 1f)
                    {
                        _timer.Stop();
                        Visible = false;
                        if (_owner.Controls.Contains(this))
                            _owner.Controls.Remove(this);
                    }
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rc  = new Rectangle(0, 0, Width - 1, Height - 1);

            // ── Тень (смещённая копия) ──────────────────────────────────
            using (var shadowPath = RoundedRect(new Rectangle(3, 4, Width - 1, Height - 1), 12))
            using (var shadowB    = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillPath(shadowB, shadowPath);

            // ── Фон ─────────────────────────────────────────────────────
            using (var path = RoundedRect(rc, 12))
            {
                using (var bg = new LinearGradientBrush(rc,
                    Color.FromArgb(240, 22, 22, 42),
                    Color.FromArgb(240, 14, 14, 30),
                    LinearGradientMode.Vertical))
                    g.FillPath(bg, path);

                // Рамка с акцентом
                using (var pen = new Pen(Color.FromArgb(200, 233, 69, 96), 1.5f))
                    g.DrawPath(pen, path);

                // Светлая полоска сверху
                using (var shine = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
                    g.DrawLine(shine, 14, 1, Width - 14, 1);
            }

            // ── Цветная полоска слева ────────────────────────────────────
            using (var accent = new SolidBrush(Color.FromArgb(233, 69, 96)))
                g.FillRectangle(accent, 0, 0, 4, Height);

            // ── Иконка трофея (нарисованная, не эмодзи) ──────────────────
            DrawTrophy(g, 12, 10, 36);

            // ── Текст «ДОСТИЖЕНИЕ!» ───────────────────────────────────────
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(233, 69, 96)))
                g.DrawString("ДОСТИЖЕНИЕ!", font, brush, new PointF(58, 9));

            // ── Название достижения ───────────────────────────────────────
            using (var font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(_text ?? "", font, brush,
                    new RectangleF(58, 27, Width - 70, 24), sf);
            }

            // ── Полоска прогресса (таймер исчезания) ─────────────────────
            int barY = Height - 5;
            int barW = (int)((Width - 6) * (1f - _holdProgress));
            if (barW > 0)
            {
                using (var bgB = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillRectangle(bgB, 3, barY, Width - 6, 3);
                using (var fgB = new LinearGradientBrush(
                    new Rectangle(3, barY, Width - 6, 3),
                    Color.FromArgb(233, 69, 96),
                    Color.FromArgb(255, 140, 50),
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(fgB, 3, barY, barW, 3);
            }
        }

        // ── Нарисованный трофей ──────────────────────────────────────────────
        private void DrawTrophy(Graphics g, int x, int y, int size)
        {
            float s = size / 24f;
            // Чаша
            var cup = new[]
            {
                new PointF(x + 4*s,  y + 0),
                new PointF(x + 20*s, y + 0),
                new PointF(x + 18*s, y + 9*s),
                new PointF(x + 14*s, y + 13*s),
                new PointF(x + 10*s, y + 13*s),
                new PointF(x + 6*s,  y + 9*s),
            };
            using (var b = new SolidBrush(Color.FromArgb(255, 200, 0)))
                g.FillPolygon(b, cup);

            // Ручки
            using (var pen = new Pen(Color.FromArgb(220, 160, 0), 2.5f))
            {
                g.DrawArc(pen, x + 0, y + 1, 9*s, 9*s, 90, 180);
                g.DrawArc(pen, x + 15*s, y + 1, 9*s, 9*s, 270, 180);
            }

            // Ножка
            using (var b = new SolidBrush(Color.FromArgb(200, 160, 0)))
            {
                g.FillRectangle(b, x + 10*s, y + 13*s, 4*s, 5*s);
                g.FillRectangle(b, x + 6*s,  y + 18*s, 12*s, 3*s);
            }

            // Блик
            using (var b = new SolidBrush(Color.FromArgb(80, 255, 255, 200)))
                g.FillEllipse(b, x + 7*s, y + 2*s, 5*s, 6*s);
        }

        private GraphicsPath RoundedRect(Rectangle b, int r)
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
}
