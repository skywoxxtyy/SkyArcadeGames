using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Windows.Forms;

namespace ArcadeHub
{
    public class MainMenuForm : Form
    {
        private class Particle { public float X, Y, VX, VY, Size, Alpha, Life; public Color Color; }
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Random _rng = new Random();
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private int _fpsCount, _fps; private long _fpsMs;
        private Label _lblFps, _lblTime, _lblScore, _lblAch;
        private Timer _statsTimer, _animTimer;
        private GameCard[] _gameCards;

        // 0=tetris 1=mine 2=racing 3=snake 4=trophy 5=hearts 6=twitch
        private static readonly Image[] _icons = new Image[7];
        private static bool _iconsLoading;

        public MainMenuForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BuildUI(); SpawnParticles(); RefreshStats();
            if (!_iconsLoading) LoadIcons();
        }

        private void LoadIcons()
        {
            _iconsLoading = true;
            var urls = new (string u, int i)[]
            {
                ("https://img.icons8.com/fluency/96/tetris.png",        0),
                ("https://img.icons8.com/fluency/96/mine-sweeper.png",  1),
                ("https://img.icons8.com/fluency/96/racing-car.png",    2),
                ("https://img.icons8.com/fluency/96/snake.png",         3),
                ("https://img.icons8.com/fluency/96/trophy.png",        4),
                ("https://img.icons8.com/fluency/96/like.png",          5),
                ("https://img.icons8.com/fluency/96/twitch.png",        6),
            };
            foreach (var (url, idx) in urls)
            {
                int ci = idx;
                var wc = new WebClient();
                wc.DownloadDataCompleted += (s, e) => {
                    if (e.Error != null || e.Cancelled) return;
                    try { using (var ms = new System.IO.MemoryStream(e.Result)) _icons[ci] = Image.FromStream(ms); } catch { }
                    if (IsHandleCreated) Invoke(new Action(() => { Invalidate(true); foreach (var gc in _gameCards) gc?.Invalidate(); }));
                };
                wc.DownloadDataAsync(new Uri(url));
            }
        }

        private void BuildUI()
        {
            Text = "Sky Arcade Games"; Size = new Size(680, 780); MinimumSize = new Size(680, 780);
            BackColor = Color.FromArgb(11, 11, 22); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;

            _gameCards = new GameCard[4];
            var gd = new (string n, string s, Color a, int ic, Func<Form> factory)[]
            {
                ("ТЕТРИС",  "Складывай фигуры",  Color.FromArgb(0,210,230),  0, ()=>new TetrisForm()),
                ("САПЁР",   "Найди все мины",    Color.FromArgb(233,60,90),  1, ()=>MinesweeperForm.TryCreate()),
                ("ГОНКИ",   "Собирай монеты",    Color.FromArgb(255,165,30), 2, ()=>RacingForm.TryCreate()),
                ("ЗМЕЙКА",  "Съешь все яблоки",  Color.FromArgb(80,210,100), 3, ()=>SnakeForm.TryCreate()),
            };
            int cW = 288, cH = 138;
            for (int i = 0; i < 4; i++)
            {
                int idx = i; var d = gd[i];
                var card = new GameCard(d.n, d.s, d.a, d.ic, _icons);
                card.Location = new Point(28 + (i % 2) * (cW + 16), 196 + (i / 2) * (cH + 14));
                card.Size = new Size(cW, cH); card.Click += (s, e) => OpenGame(d.factory());
                _gameCards[i] = card; Controls.Add(card);
            }

            var sp = new GlassPanel { Location = new Point(28, 510), Size = new Size(622, 96) };
            Controls.Add(sp);

            _lblTime = StatLbl(44, 526, "", Color.FromArgb(140, 180, 255));
            _lblScore = StatLbl(248, 526, "", Color.FromArgb(255, 200, 60));
            _lblAch = StatLbl(452, 526, "", Color.FromArgb(180, 130, 255));
            Controls.AddRange(new Control[] { _lblTime, _lblScore, _lblAch });

            var btnAch = new IconButton("ДОСТИЖЕНИЯ", Color.FromArgb(255, 210, 40), 4, _icons);
            btnAch.Location = new Point(28, 622); btnAch.Size = new Size(188, 58);
            btnAch.Click += (s, e) => new AchievementsForm("Все").ShowDialog(this);

            var btnDonate = new IconButton("ПОДДЕРЖАТЬ", Color.FromArgb(150, 80, 255), 5, _icons);
            btnDonate.Location = new Point(234, 622); btnDonate.Size = new Size(188, 58);
            btnDonate.Click += (s, e) => new DonateForm().ShowDialog(this);

            var btnTwitch = new IconButton("TWITCH", Color.FromArgb(145, 70, 255), 6, _icons);
            btnTwitch.Location = new Point(440, 622); btnTwitch.Size = new Size(210, 58);
            btnTwitch.Click += (s, e) => { try { Process.Start(new ProcessStartInfo { FileName = "https://www.twitch.tv/skywoxxtyyy", UseShellExecute = true }); } catch { } };

            Controls.AddRange(new Control[] { btnAch, btnDonate, btnTwitch });

            _lblFps = new Label
            {
                Font = new Font("Consolas", 7f),
                ForeColor = Color.FromArgb(30, 35, 55),
                AutoSize = true,
                Location = new Point(8, 8),
                BackColor = Color.Transparent,
                Text = "FPS: --"
            };
            Controls.Add(_lblFps);

            _statsTimer = new Timer { Interval = 3000 }; _statsTimer.Tick += (s, e) => RefreshStats(); _statsTimer.Start();
            _animTimer = new Timer { Interval = 16 }; _animTimer.Tick += AnimTick; _animTimer.Start();
        }

        private Label StatLbl(int x, int y, string t, Color c) => new Label
        {
            Text = t,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = c,
            Location = new Point(x, y),
            Size = new Size(200, 60),
            BackColor = Color.Transparent
        };

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(12, 10, 28), Color.FromArgb(8, 6, 18), 110f))
                g.FillRectangle(bg, ClientRectangle);
            using (var p = new Pen(Color.FromArgb(8, 60, 100, 200), 1))
            { for (int y = 0; y < Height; y += 44) g.DrawLine(p, 0, y, Width, y); for (int x = 0; x < Width; x += 44) g.DrawLine(p, x, 0, x, Height); }
            using (var p = new Pen(Color.FromArgb(5, 80, 140, 255), 1.5f))
                for (int i = -10; i < 30; i++) { int off = i * 70; g.DrawLine(p, off, 0, off + Height, Height); }
            using (var gl = new LinearGradientBrush(new Rectangle(0, 0, Width, 300), Color.FromArgb(25, 40, 80, 255), Color.Transparent, LinearGradientMode.Vertical))
                g.FillRectangle(gl, 0, 0, Width, 300);
            foreach (var p in _particles)
            {
                int a = (int)Math.Min(255, p.Alpha * p.Life); if (a < 5) continue;
                using (var b = new SolidBrush(Color.FromArgb(a, p.Color))) g.FillEllipse(b, p.X - p.Size, p.Y - p.Size, p.Size * 2, p.Size * 2);
            }
            DrawLogo(g);
            DrawDivider(g, 175, "ВЫБЕРИТЕ ИГРУ");
            DrawDivider(g, 494, "СТАТИСТИКА");
            DrawDivider(g, 606, "БЫСТРЫЙ ДОСТУП");
            using (var f = new Font("Segoe UI", 7.5f))
            using (var b = new SolidBrush(Color.FromArgb(38, 43, 62)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("© skywoxxtyyy  •  Sky Arcade Games  v2.0", f, b, new RectangleF(0, Height - 20, Width, 16), sf);
            }
        }

        private void DrawLogo(Graphics g)
        {
            var rc = new Rectangle(24, 18, 632, 140);
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(rc);
                using (var lg = new LinearGradientBrush(rc, Color.FromArgb(22, 28, 55), Color.FromArgb(14, 16, 38), LinearGradientMode.ForwardDiagonal))
                    g.FillPath(lg, path);
                using (var pen = new Pen(Color.FromArgb(50, 65, 130), 1.5f)) g.DrawPath(pen, path);
                // Левая цветная полоса
                using (var ab = new LinearGradientBrush(new Rectangle(24, 18, 6, 140), Color.FromArgb(80, 160, 255), Color.FromArgb(233, 69, 130), LinearGradientMode.Vertical))
                    g.FillRectangle(ab, 24, 18, 5, 140);
                // Правый угловой акцент
                var tri = new PointF[] { new PointF(656, 18), new PointF(580, 18), new PointF(656, 90) };
                using (var tb = new SolidBrush(Color.FromArgb(15, 80, 140, 255))) g.FillPolygon(tb, tri);
            }
            using (var f = new Font("Segoe UI Black", 52f, FontStyle.Bold))
            using (var br = new LinearGradientBrush(new RectangleF(46, 20, 360, 80), Color.FromArgb(80, 170, 255), Color.FromArgb(233, 80, 140), LinearGradientMode.Horizontal))
                g.DrawString("SKY", f, br, 46f, 22f);
            using (var f = new Font("Segoe UI", 28f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(200, 210, 240))) g.DrawString("ARCADE", f, b, 50f, 92f);
            using (var f = new Font("Segoe UI", 11f))
            using (var b = new SolidBrush(Color.FromArgb(65, 75, 115))) g.DrawString("G A M E S", f, b, 52f, 138f);
            // Декоративные полоски справа
            for (int i = 0; i < 5; i++)
            {
                int x = 578 + i * 14; int h = 28 + i * 18; int y = 88 - h / 2;
                using (var b = new SolidBrush(Color.FromArgb(18 + i * 9, 80, 140, 255))) g.FillRectangle(b, x, y, 8, h);
            }
            using (var lb = new LinearGradientBrush(new Rectangle(28, 162, 622, 2), Color.Transparent, Color.FromArgb(75, 80, 140, 255), LinearGradientMode.Horizontal))
            using (var lp = new Pen(lb, 1)) g.DrawLine(lp, 28, 163, 650, 163);
        }

        private void DrawDivider(Graphics g, int y, string title)
        {
            using (var b = new LinearGradientBrush(new Rectangle(28, y, 180, 1), Color.Transparent, Color.FromArgb(55, 80, 140, 220), LinearGradientMode.Horizontal))
            using (var p = new Pen(b)) g.DrawLine(p, 28, y, 208, y);
            using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var b2 = new SolidBrush(Color.FromArgb(65, 75, 115))) g.DrawString(title, f, b2, 212, y - 7);
            int tw = title.Length * 7;
            using (var b = new LinearGradientBrush(new Rectangle(218 + tw, y, 300, 1), Color.FromArgb(55, 80, 140, 220), Color.Transparent, LinearGradientMode.Horizontal))
            using (var p = new Pen(b)) g.DrawLine(p, 220 + tw, y, 650, y);
        }

        private static readonly Color[] _pc = { Color.FromArgb(80, 160, 255), Color.FromArgb(233, 69, 96), Color.FromArgb(145, 70, 255), Color.FromArgb(0, 200, 200), Color.FromArgb(255, 165, 30), Color.FromArgb(80, 210, 100) };
        private void SpawnParticles() { for (int i = 0; i < 52; i++) _particles.Add(NewP()); }
        private Particle NewP() => new Particle { X = _rng.Next(0, Width), Y = _rng.Next(0, Height), VX = (float)((_rng.NextDouble() - 0.5) * 0.5), VY = (float)((_rng.NextDouble() - 0.5) * 0.5), Size = (float)(_rng.NextDouble() * 2.5 + 0.8), Alpha = (float)(_rng.NextDouble() * 70 + 20), Life = 1f, Color = _pc[_rng.Next(_pc.Length)] };

        private void AnimTick(object s, EventArgs e)
        {
            double t = _sw.Elapsed.TotalSeconds;
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i]; p.X += p.VX; p.Y += p.VY;
                p.Alpha = 30 + (float)(Math.Sin(t * 0.7 + i) * 20 + 20);
                if (p.X < -10 || p.X > Width + 10 || p.Y < -10 || p.Y > Height + 10) { _particles[i] = NewP(); _particles[i].X = _rng.Next(0, Width); _particles[i].Y = _rng.Next(0, Height); }
                else _particles[i] = p;
            }
            Invalidate(new Rectangle(0, 0, Width, Height));
            _fpsCount++;
            long ms = _sw.ElapsedMilliseconds;
            if (ms - _fpsMs >= 1000) { _fps = _fpsCount; _fpsCount = 0; _fpsMs = ms; _lblFps.Text = $"FPS: {_fps}"; }
        }

        private void RefreshStats()
        {
            var ts = TimeSpan.FromSeconds(GameStats.TotalPlayTimeSeconds);
            _lblTime.Text = $"ВРЕМЯ В ИГРАХ\n{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            _lblScore.Text = $"ВСЕГО ОЧКОВ\n{GameStats.TotalScore:N0}";
            _lblAch.Text = $"ДОСТИЖЕНИЙ\n{AchievementManager.GetUnlockedCount()} / {AchievementManager.Achievements.Count}";
        }

        private void OpenGame(Form gf)
        {
            if (gf == null || gf.IsDisposed) return;
            gf.FormClosed += (s, e) => RefreshStats();
            gf.Show();
        }
        protected override void OnFormClosed(FormClosedEventArgs e) { _animTimer?.Stop(); _statsTimer?.Stop(); GameStats.Save(); base.OnFormClosed(e); }
    }

    // ── GameCard ──────────────────────────────────────────────────────────────
    public class GameCard : Control
    {
        private readonly string _name, _sub; private readonly Color _accent;
        private readonly int _iconIdx; private readonly Image[] _pool;
        private bool _hov, _press; private float _glow; private Timer _gt;

        public GameCard(string name, string sub, Color accent, int iconIdx, Image[] pool)
        {
            _name = name; _sub = sub; _accent = accent; _iconIdx = iconIdx; _pool = pool;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            _gt = new Timer { Interval = 16 };
            _gt.Tick += (s, e) => { float t = _hov ? 1f : 0f; _glow += (t - _glow) * 0.1f; if (Math.Abs(_glow - t) < 0.01f) { _glow = t; if (!_hov) _gt.Stop(); } Invalidate(); };
        }
        protected override void OnMouseEnter(EventArgs e) { _hov = true; _gt.Start(); }
        protected override void OnMouseLeave(EventArgs e) { _hov = false; _press = false; _gt.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RR(rc, 14))
            {
                using (var bg = new LinearGradientBrush(rc, Color.FromArgb(30, 32, 56), Color.FromArgb(18, 18, 36), LinearGradientMode.ForwardDiagonal))
                    g.FillPath(bg, path);
                if (_glow > 0.02f) using (var gl = new SolidBrush(Color.FromArgb((int)(50 * _glow), _accent))) g.FillPath(gl, path);
                if (_press) using (var pb = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillPath(pb, path);
                using (var pen = new Pen(Color.FromArgb(_hov ? 200 : 55, _accent), _hov ? 1.8f : 1f)) g.DrawPath(pen, path);
            }
            // Диагональный акцент
            if (_glow > 0.02f)
            {
                using (var ap = new GraphicsPath())
                {
                    ap.AddPolygon(new PointF[] { new PointF(Width - 50, 0), new PointF(Width - 1, 0), new PointF(Width - 1, 50) });
                    using (var ab = new SolidBrush(Color.FromArgb((int)(40 * _glow), _accent))) g.FillPath(ab, ap);
                }
            }
            // Левая полоса
            using (var al = new LinearGradientBrush(new RectangleF(0, 14, 4, Height - 28), Color.FromArgb(_hov ? 220 : 100, _accent), Color.Transparent, LinearGradientMode.Vertical))
                g.FillRectangle(al, 0, 14, 4, Height - 28);
            // Иконка
            int isz = 68, ix = 16, iy = (Height - isz) / 2;
            var icon = _pool[_iconIdx];
            if (icon != null)
            {
                using (var ib = new SolidBrush(Color.FromArgb(25, _accent))) g.FillEllipse(ib, ix - 4, iy - 4, isz + 8, isz + 8);
                g.DrawImage(icon, ix, iy, isz, isz);
            }
            else
            {
                using (var ib = new SolidBrush(Color.FromArgb(40, _accent))) g.FillEllipse(ib, ix, iy, isz, isz);
                using (var f = new Font("Segoe UI", 22f, FontStyle.Bold))
                using (var b = new SolidBrush(Color.FromArgb(150, _accent)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(_name.Substring(0, 1), f, b, new RectangleF(ix, iy, isz, isz), sf);
                }
            }
            int tx = ix + isz + 18, tw = Width - tx - 12;
            using (var f = new Font("Segoe UI Black", 15f, FontStyle.Bold))
            using (var b = new SolidBrush(_hov ? Color.White : Color.FromArgb(220, 226, 255))) g.DrawString(_name, f, b, new RectangleF(tx, 26, tw, 28));
            using (var f = new Font("Segoe UI", 9f))
            using (var b = new SolidBrush(Color.FromArgb(95, 100, 135))) g.DrawString(_sub, f, b, new RectangleF(tx, 58, tw, 20));
            if (_hov)
            {
                var br = new RectangleF(tx, 84, 96, 26);
                using (var pp = RRF(br, 7))
                using (var bb = new LinearGradientBrush(new Rectangle((int)br.X, (int)br.Y, (int)br.Width, (int)br.Height), _accent, Color.FromArgb(Math.Max(0, _accent.R - 40), _accent.G, _accent.B), LinearGradientMode.Horizontal))
                    g.FillPath(bb, pp);
                using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("ИГРАТЬ", f, Brushes.White, br, sf);
                }
            }
        }

        private GraphicsPath RR(Rectangle b, int r) { var p = new GraphicsPath(); p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90); p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); return p; }
        private GraphicsPath RRF(RectangleF b, int r) { var p = new GraphicsPath(); p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90); p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); return p; }
    }

    // ── GlassPanel ────────────────────────────────────────────────────────────
    public class GlassPanel : Panel
    {
        public GlassPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RR(rc, 12))
            {
                using (var bg = new LinearGradientBrush(rc, Color.FromArgb(30, 40, 80, 160), Color.FromArgb(16, 22, 50, 120), LinearGradientMode.Vertical)) g.FillPath(bg, path);
                using (var p = new Pen(Color.FromArgb(48, 78, 140, 255), 1)) g.DrawPath(p, path);
                using (var p = new Pen(Color.FromArgb(18, 200, 220, 255), 1)) g.DrawLine(p, 16, 2, Width - 16, 2);
            }
            using (var p = new Pen(Color.FromArgb(28, 55, 95), 1)) { g.DrawLine(p, Width / 3, 14, Width / 3, Height - 14); g.DrawLine(p, Width * 2 / 3, 14, Width * 2 / 3, Height - 14); }
        }
        private GraphicsPath RR(Rectangle b, int r) { var p = new GraphicsPath(); p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90); p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); return p; }
    }

    // ── IconButton ────────────────────────────────────────────────────────────
    public class IconButton : Control
    {
        private readonly string _label; private readonly Color _accent;
        private readonly int _iconIdx; private readonly Image[] _pool;
        private bool _hov, _press; private float _glow; private Timer _gt;

        public IconButton(string label, Color accent, int iconIdx, Image[] pool)
        {
            _label = label; _accent = accent; _iconIdx = iconIdx; _pool = pool;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            _gt = new Timer { Interval = 16 };
            _gt.Tick += (s, e) => { float t = _hov ? 1f : 0f; _glow += (t - _glow) * 0.12f; if (Math.Abs(_glow - t) < 0.01f) { _glow = t; if (!_hov) _gt.Stop(); } Invalidate(); };
        }
        protected override void OnMouseEnter(EventArgs e) { _hov = true; _gt.Start(); }
        protected override void OnMouseLeave(EventArgs e) { _hov = false; _press = false; _gt.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RR(rc, 12))
            {
                using (var bg = new LinearGradientBrush(rc, Color.FromArgb(28, 30, 52), Color.FromArgb(18, 18, 34), LinearGradientMode.Vertical)) g.FillPath(bg, path);
                if (_glow > 0.02f) using (var gl = new SolidBrush(Color.FromArgb((int)(55 * _glow), _accent))) g.FillPath(gl, path);
                if (_press) using (var pb = new SolidBrush(Color.FromArgb(35, 0, 0, 0))) g.FillPath(pb, path);
                using (var pen = new Pen(Color.FromArgb(_hov ? 200 : 50, _accent), _hov ? 1.8f : 1f)) g.DrawPath(pen, path);
            }
            int isz = 28, ix = 14, iy = (Height - isz) / 2;
            var icon = _pool[_iconIdx];
            if (icon != null) g.DrawImage(icon, ix, iy, isz, isz);
            else { using (var f = new Font("Segoe UI", 11f, FontStyle.Bold)) using (var b = new SolidBrush(Color.FromArgb(150, _accent))) { var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }; g.DrawString("?", f, b, new RectangleF(ix, iy, isz, isz), sf); } }
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var b = new SolidBrush(_hov ? Color.White : Color.FromArgb(185, 192, 225)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_label, f, b, new RectangleF(ix + isz + 4, 0, Width - ix - isz - 18, Height), sf);
            }
        }
        private GraphicsPath RR(Rectangle b, int r) { var p = new GraphicsPath(); p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90); p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); return p; }
    }

    // ── StatsPanel (совместимость) ─────────────────────────────────────────
    public class StatsPanel : Panel
    {
        public StatsPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RR(rc, 12)) { using (var bg = new SolidBrush(Color.FromArgb(28, 60, 100, 180))) g.FillPath(bg, path); using (var pen = new Pen(Color.FromArgb(60, 80, 160, 255), 1.5f)) g.DrawPath(pen, path); }
        }
        private GraphicsPath RR(Rectangle b, int r) { var p = new GraphicsPath(); p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90); p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); return p; }
    }

    // ── ArcadeButton (совместимость с другими формами) ─────────────────────
    public class ArcadeButton : Control
    {
        private string _icon, _label; private Color _accent;
        private bool _hov, _press; private float _glow; private Timer _gt;
        public ArcadeButton(string icon, string label, Color accent)
        {
            _icon = icon; _label = label; _accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            _gt = new Timer { Interval = 16 };
            _gt.Tick += (s, e) => { float t = _hov ? 1f : 0f; _glow += (t - _glow) * 0.12f; if (Math.Abs(_glow - t) < 0.01f) { _glow = t; if (!_hov) _gt.Stop(); } Invalidate(); };
        }
        protected override void OnMouseEnter(EventArgs e) { _hov = true; _gt.Start(); }
        protected override void OnMouseLeave(EventArgs e) { _hov = false; _press = false; _gt.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RR(rc, 12))
            {
                using (var bg = new SolidBrush(Color.FromArgb(32, 32, 55))) g.FillPath(bg, path);
                if (_glow > 0.01f) using (var gl = new SolidBrush(Color.FromArgb((int)(55 * _glow), _accent))) g.FillPath(gl, path);
                if (_press) using (var pb = new SolidBrush(Color.FromArgb(35, 0, 0, 0))) g.FillPath(pb, path);
                using (var pen = new Pen(Color.FromArgb(_hov ? 200 : 55, _accent), _hov ? 1.8f : 1f)) g.DrawPath(pen, path);
            }
            using (var f = new Font("Segoe UI", Height < 70 ? 14f : 20f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(180, _accent)))
            { var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }; g.DrawString(_icon, f, b, new RectangleF(0, 0, 44, Height), sf); }
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(_press ? 190 : 235, Color.White)))
            { var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }; g.DrawString(_label, f, b, new RectangleF(40, 0, Width - 44, Height), sf); }
        }
        private GraphicsPath RR(Rectangle b, int r) { var p = new GraphicsPath(); p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90); p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90); p.CloseFigure(); return p; }
    }
}