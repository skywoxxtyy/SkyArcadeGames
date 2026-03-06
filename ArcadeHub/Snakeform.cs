using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArcadeHub
{
    // ── Карты Змейки ─────────────────────────────────────────────────────────
    public enum SnakeMap { Classic, Neon, Forest, Space }
    public enum SnakeMode { Classic, NoWalls, DoubleApple, Obstacles }

    public class SnakeForm : Form
    {
        private const int COLS = 28, ROWS = 28, CELL = 20;

        private enum Dir { Up, Down, Left, Right }

        // ── Состояние ─────────────────────────────────────────────────────
        private LinkedList<Point> _snake = new LinkedList<Point>();
        private Point _food;
        private Point _food2;      // для режима двойного яблока
        private List<Point> _obstacles = new List<Point>();
        private Dir _dir = Dir.Right, _nextDir = Dir.Right;
        private int _score, _apples, _level, _combo, _maxCombo;
        private bool _gameOver, _paused;
        private bool _wallKill = true;
        private bool _doubleApple = false;
        private Timer _timer;
        private DateTime _gameStart;
        private ToastNotification _toast;

        // ── Карта и режим ─────────────────────────────────────────────────
        private SnakeMap _map = SnakeMap.Classic;
        private SnakeMode _mode = SnakeMode.Classic;

        private readonly Random _rng = new Random();
        private int _tick = 0;
        private float _particleTime = 0;
        private List<FoodParticle> _particles = new List<FoodParticle>();

        // ── UI ────────────────────────────────────────────────────────────
        private DoubleBufferedPictureBox _canvas;
        private Label _lblScore, _lblLen, _lblLevel, _lblCombo, _lblRecord, _lblMode, _lblStatAll;
        private Panel _hud;

        // ── Тема ──────────────────────────────────────────────────────────
        private SnakeTheme Theme => _themes[(int)_map];
        private static readonly SnakeTheme[] _themes = new SnakeTheme[]
        {
            // Classic
            new SnakeTheme("Классика", Color.FromArgb(12,24,12), Color.FromArgb(20,40,20),
                Color.FromArgb(80,200,80), Color.FromArgb(200,80,80),
                Color.FromArgb(20,40,20), Color.FromArgb(26,48,26)),
            // Neon
            new SnakeTheme("Неон", Color.FromArgb(8,0,20), Color.FromArgb(14,0,30),
                Color.FromArgb(0,255,180), Color.FromArgb(255,60,180),
                Color.FromArgb(14,0,30), Color.FromArgb(20,0,40)),
            // Forest
            new SnakeTheme("Лес", Color.FromArgb(20,38,10), Color.FromArgb(28,50,14),
                Color.FromArgb(120,220,60), Color.FromArgb(255,140,30),
                Color.FromArgb(28,50,14), Color.FromArgb(36,60,18)),
            // Space
            new SnakeTheme("Космос", Color.FromArgb(5,5,18), Color.FromArgb(8,8,25),
                Color.FromArgb(60,140,255), Color.FromArgb(255,200,60),
                Color.FromArgb(8,8,25), Color.FromArgb(12,12,35)),
        };

        public static SnakeForm TryCreate()
        {
            var dlg = new SnakeSelectDialog();
            if (dlg.ShowDialog() != DialogResult.OK) return null;
            return new SnakeForm(dlg.SelectedMap, dlg.SelectedMode);
        }

        public SnakeForm(SnakeMap map = SnakeMap.Classic, SnakeMode mode = SnakeMode.Classic)
        {
            _map = map;
            _mode = mode;
            BuildUI();
            AchievementManager.OnAchievementUnlocked += OnAchUnlocked;
            ApplyMode();
            ResetGame();
        }

        // ── Смена карты во время игры ────────────────────────────────────
        private void ShowMapSelect()
        {
            var dlg = new SnakeSelectDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _map = dlg.SelectedMap;
            _mode = dlg.SelectedMode;
            ApplyMode();
            ResetGame();
        }

        private void ApplyMode()
        {
            _wallKill = _mode != SnakeMode.NoWalls;
            _doubleApple = _mode == SnakeMode.DoubleApple;
        }

        // ── UI ───────────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text = "Змейка — ArcadeHub";
            Size = new Size(COLS * CELL + 230, ROWS * CELL + 80);
            BackColor = Color.FromArgb(10, 10, 20);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            _canvas = new DoubleBufferedPictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(COLS * CELL, ROWS * CELL),
                BackColor = Color.Black
            };
            _canvas.Paint += PaintGame;

            int sx = COLS * CELL + 22;

            _hud = new Panel
            {
                Location = new Point(sx, 10),
                Size = new Size(190, ROWS * CELL),
                BackColor = Color.FromArgb(16, 16, 28)
            };
            _hud.Paint += HudPaint;

            _lblScore = HudLabel(sx, 35, "0", Color.White, 18f, FontStyle.Bold);
            _lblLen = HudLabel(sx, 90, "Длина: 3", Color.FromArgb(180, 200, 255), 11f);
            _lblLevel = HudLabel(sx, 112, "Уровень: 1", Color.FromArgb(80, 200, 255), 11f);
            _lblCombo = HudLabel(sx, 134, "Комбо: ×1", Color.FromArgb(255, 200, 40), 11f);
            _lblRecord = HudLabel(sx, 160, $"Рек: {GameStats.SnakeBestScore}", Color.FromArgb(110, 115, 150), 9.5f);
            _lblMode = HudLabel(sx, 178, "Классика", Color.FromArgb(100, 110, 140), 9f, FontStyle.Italic);

            var btnRestart = new SnakeHudBtn("Заново [Space]", Color.FromArgb(40, 80, 40));
            btnRestart.Location = new Point(sx, 210);
            btnRestart.Size = new Size(185, 38);
            btnRestart.Click += (s, e) => ResetGame();

            var btnMap = new SnakeHudBtn("Карта / Режим", Color.FromArgb(40, 40, 80));
            btnMap.Location = new Point(sx, 256);
            btnMap.Size = new Size(185, 38);
            btnMap.Click += (s, e) => { _timer.Stop(); ShowMapSelect(); };

            var btnAch = new SnakeHudBtn("Достижения", Color.FromArgb(60, 50, 10));
            btnAch.Location = new Point(sx, 302);
            btnAch.Size = new Size(185, 38);
            btnAch.Click += (s, e) => new AchievementsForm("Змейка").ShowDialog(this);

            var btnPause = new SnakeHudBtn("Пауза [P]", Color.FromArgb(50, 50, 70));
            btnPause.Location = new Point(sx, 348);
            btnPause.Size = new Size(185, 38);
            btnPause.Click += (s, e) => TogglePause();

            var btnTw = new TwitchButton { Location = new Point(sx, 394), Size = new Size(185, 38) };

            // ── Панель статистики ─────────────────────────────────────────
            var lblStatTitle = new Label
            {
                Text = "── СТАТИСТИКА ──",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 65, 100),
                Location = new Point(sx + 5, 442),
                Size = new Size(185, 18),
                BackColor = Color.Transparent
            };
            _lblStatAll = HudLabel(sx, 462, BuildSnakeStatText(), Color.FromArgb(95, 105, 145), 8f);
            _lblStatAll.Size = new Size(192, 110);

            // Управление
            var lblCtrl = new Label
            {
                Text = "Стрелки/WASD  |  P-пауза",
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(45, 50, 75),
                Location = new Point(sx, ROWS * CELL - 22),
                Size = new Size(185, 18),
                BackColor = Color.Transparent
            };

            Controls.AddRange(new Control[]
            {
                _canvas, _hud,
                _lblScore, _lblLen, _lblLevel, _lblCombo, _lblRecord, _lblMode,
                btnRestart, btnMap, btnAch, btnPause, btnTw,
                lblStatTitle, _lblStatAll, lblCtrl
            });

            _timer = new Timer { Interval = 160 };
            _timer.Tick += GameTick;
            _toast = new ToastNotification(this);
        }

        private void HudPaint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, _hud.Width, _hud.Height),
                Color.FromArgb(20, 20, 36), Color.FromArgb(13, 13, 24), LinearGradientMode.Vertical))
                g.FillRectangle(bg, 0, 0, _hud.Width, _hud.Height);
            using (var p = new Pen(Color.FromArgb(30, 40, 70)))
                g.DrawRectangle(p, 0, 0, _hud.Width - 1, _hud.Height - 1);

            // Заголовок секции
            using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(50, 60, 90)))
                g.DrawString("СТАТИСТИКА", f, b, 8, 20);
            using (var p = new Pen(Color.FromArgb(25, 35, 60)))
                g.DrawLine(p, 4, 34, _hud.Width - 4, 34);
        }

        private Label HudLabel(int x, int y, string t, Color c, float fs, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = t,
                Font = new Font("Segoe UI", fs, style),
                ForeColor = c,
                Location = new Point(x + 6, y),
                Size = new Size(185, 22),
                BackColor = Color.Transparent
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up: case Keys.W: if (_dir != Dir.Down) _nextDir = Dir.Up; return true;
                case Keys.Down: case Keys.S: if (_dir != Dir.Up) _nextDir = Dir.Down; return true;
                case Keys.Left: case Keys.A: if (_dir != Dir.Right) _nextDir = Dir.Left; return true;
                case Keys.Right: case Keys.D: if (_dir != Dir.Left) _nextDir = Dir.Right; return true;
                case Keys.P: TogglePause(); return true;
                case Keys.Space: if (_gameOver) ResetGame(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TogglePause()
        {
            _paused = !_paused;
            if (_paused) _timer.Stop(); else _timer.Start();
            _canvas.Invalidate();
        }

        // ── Сброс ────────────────────────────────────────────────────────
        private void ResetGame()
        {
            _snake.Clear();
            for (int i = 3; i >= 0; i--)
                _snake.AddLast(new Point(COLS / 2 + i, ROWS / 2));
            _dir = Dir.Right; _nextDir = Dir.Right;
            _score = 0; _apples = 0; _level = 1;
            _combo = 0; _maxCombo = 0;
            _gameOver = false; _paused = false;
            _timer.Interval = 160;
            _obstacles.Clear(); _particles.Clear();
            _tick = 0;

            if (_mode == SnakeMode.Obstacles) GenerateObstacles();

            SpawnFood();
            if (_doubleApple) SpawnFood2();

            _gameStart = DateTime.Now;
            GameStats.SnakeTotalGames++;
            AchievementManager.UpdateProgress("snake_10games", GameStats.SnakeTotalGames);
            _timer.Start();
            UpdateHud();
            _canvas.Invalidate();
        }

        private void GenerateObstacles()
        {
            _obstacles.Clear();
            int count = 10 + _level * 2;
            for (int i = 0; i < count; i++)
            {
                Point p;
                do { p = new Point(_rng.Next(2, COLS - 2), _rng.Next(2, ROWS - 2)); }
                while (_snake.Contains(p) || (p.X == COLS / 2 && p.Y == ROWS / 2));
                _obstacles.Add(p);
            }
        }

        private void SpawnFood()
        {
            do { _food = new Point(_rng.Next(COLS), _rng.Next(ROWS)); }
            while (_snake.Contains(_food) || _obstacles.Contains(_food));
        }

        private void SpawnFood2()
        {
            do { _food2 = new Point(_rng.Next(COLS), _rng.Next(ROWS)); }
            while (_snake.Contains(_food2) || _food2 == _food || _obstacles.Contains(_food2));
        }

        // ── Тик ──────────────────────────────────────────────────────────
        private void GameTick(object sender, EventArgs e)
        {
            if (_gameOver || _paused) return;
            _dir = _nextDir;
            _tick++;
            _particleTime += 0.1f;

            var head = _snake.First.Value;
            Point nh;
            switch (_dir)
            {
                case Dir.Up: nh = new Point(head.X, head.Y - 1); break;
                case Dir.Down: nh = new Point(head.X, head.Y + 1); break;
                case Dir.Left: nh = new Point(head.X - 1, head.Y); break;
                default: nh = new Point(head.X + 1, head.Y); break;
            }

            // Стены
            if (_wallKill)
            {
                if (nh.X < 0 || nh.X >= COLS || nh.Y < 0 || nh.Y >= ROWS)
                { EndGame(); return; }
            }
            else
            {
                bool wrapped = (nh.X < 0 || nh.X >= COLS || nh.Y < 0 || nh.Y >= ROWS);
                nh = new Point((nh.X + COLS) % COLS, (nh.Y + ROWS) % ROWS);
                if (wrapped) AchievementManager.Unlock("snake_nowall");
            }

            // Препятствия
            if (_obstacles.Contains(nh)) { EndGame(); return; }

            // Сам себя
            if (_snake.Contains(nh)) { EndGame(); return; }

            _snake.AddFirst(nh);
            bool ate = false;

            if (nh == _food)
            {
                ate = true; _apples++;
                _combo++; if (_combo > _maxCombo) _maxCombo = _combo;
                int bonus = 10 * _level * Math.Max(1, _combo / 2);
                _score += bonus;
                GameStats.SnakeTotalApples++;
                SpawnParticles(nh.X * CELL, nh.Y * CELL, Theme.Food);
                SpawnFood();
                if (_apples % 5 == 0) LevelUp();
                CheckAch();
            }
            else if (_doubleApple && nh == _food2)
            {
                ate = true; _apples++;
                _combo++; if (_combo > _maxCombo) _maxCombo = _combo;
                int bonus = 15 * _level * Math.Max(1, _combo / 2);
                _score += bonus;
                GameStats.SnakeTotalApples++;
                SpawnParticles(nh.X * CELL, nh.Y * CELL, Color.FromArgb(255, 160, 40));
                SpawnFood2();
                CheckAch();
            }

            if (!ate)
            {
                _snake.RemoveLast();
                _combo = 0;
            }

            // Обновить частицы
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.X += p.VX; p.Y += p.VY; p.Life -= 0.04f; p.VY += 0.15f;
                _particles[i] = p;
                if (p.Life <= 0) _particles.RemoveAt(i);
            }

            if (_snake.Count > GameStats.SnakeBestLength) GameStats.SnakeBestLength = _snake.Count;
            if (_score > GameStats.SnakeBestScore) GameStats.SnakeBestScore = _score;

            UpdateHud();
            _canvas.Invalidate();
        }

        private void LevelUp()
        {
            _level++;
            _timer.Interval = Math.Max(60, 160 - (_level - 1) * 14);
            _toast?.Show($"Уровень {_level}! Скорость растёт!");
            if (_mode == SnakeMode.Obstacles) GenerateObstacles();
            CheckAch();
        }

        private void SpawnParticles(int px, int py, Color c)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                float speed = (float)(_rng.NextDouble() * 3 + 1);
                _particles.Add(new FoodParticle
                {
                    X = px + CELL / 2f,
                    Y = py + CELL / 2f,
                    VX = (float)Math.Cos(angle) * speed,
                    VY = (float)Math.Sin(angle) * speed - 2f,
                    Life = 1f,
                    Color = c
                });
            }
        }

        private void CheckAch()
        {
            if (_apples >= 1) AchievementManager.Unlock("snake_first_apple");
            AchievementManager.UpdateProgress("snake_length10", _snake.Count);
            AchievementManager.UpdateProgress("snake_length25", _snake.Count);
            AchievementManager.UpdateProgress("snake_length40", _snake.Count);
            AchievementManager.UpdateProgress("snake_apples100", GameStats.SnakeTotalApples);
            AchievementManager.UpdateProgress("snake_apples500", GameStats.SnakeTotalApples);
            AchievementManager.UpdateProgress("snake_score500", _score);
            AchievementManager.UpdateProgress("snake_games10", GameStats.SnakeTotalGames);
            var el = (DateTime.Now - _gameStart).TotalSeconds;
            if (_score >= 200 && el < 30) AchievementManager.Unlock("snake_fast200");
        }

        private void UpdateHud()
        {
            _lblScore.Text = $"{_score:N0}";
            _lblLen.Text = $"Длина:   {_snake.Count}";
            _lblLevel.Text = $"Уровень: {_level}";
            _lblCombo.Text = _combo > 1 ? $"Комбо:  x{_combo} !" : "Комбо:  x1";
            _lblRecord.Text = $"Рек: {GameStats.SnakeBestScore}";
            _lblMode.Text = GetModeName();
            if (_lblStatAll != null) _lblStatAll.Text = BuildSnakeStatText();
        }

        private string BuildSnakeStatText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Рекорд очков:  {GameStats.SnakeBestScore}");
            sb.AppendLine($"Макс длина:    {GameStats.SnakeBestLength}");
            sb.AppendLine($"Яблок всего:   {GameStats.SnakeTotalApples}");
            sb.AppendLine($"Игр сыграно:   {GameStats.SnakeTotalGames}");
            var t = TimeSpan.FromSeconds(GameStats.TotalPlayTimeSeconds);
            sb.Append($"Время в играх: {t:hh\\:mm\\:ss}");
            return sb.ToString();
        }

        private string GetModeName()
        {
            switch (_mode)
            {
                case SnakeMode.NoWalls: return "Без стен";
                case SnakeMode.DoubleApple: return "Двойное яблоко";
                case SnakeMode.Obstacles: return "Препятствия";
                default: return "Классика";
            }
        }

        private void EndGame()
        {
            _gameOver = true; _timer.Stop();
            int el = (int)(DateTime.Now - _gameStart).TotalSeconds;
            GameStats.TotalPlayTimeSeconds += el;
            GameStats.TotalScore += _score;
            GameStats.Save();
            _canvas.Invalidate();

            var res = MessageBox.Show(
                $"КОНЕЦ ИГРЫ\n\nКарта: {Theme.Name}  |  {GetModeName()}\n" +
                $"Счёт:     {_score}\nДлина:    {_snake.Count}\n" +
                $"Яблок:    {_apples}\nМакс комбо: {_maxCombo}x\nУровень:  {_level}\n\nСыграть ещё?",
                "Game Over", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes) ResetGame();
        }

        // ── ОТРИСОВКА ────────────────────────────────────────────────────
        private void PaintGame(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Фон
            using (var bg = new SolidBrush(Theme.BgMain))
                g.FillRectangle(bg, 0, 0, COLS * CELL, ROWS * CELL);

            // Сетка
            DrawGrid(g);

            // Препятствия
            foreach (var obs in _obstacles)
                DrawObstacle(g, obs.X, obs.Y);

            // Еда (основная)
            DrawFood(g, _food, Theme.Food, _tick);

            // Еда-2 (двойной режим)
            if (_doubleApple)
                DrawFood(g, _food2, Color.FromArgb(255, 160, 40), _tick + 15);

            // Частицы
            foreach (var p in _particles)
            {
                int alpha = (int)(p.Life * 240);
                int sz = (int)(p.Life * 8) + 2;
                using (var pb = new SolidBrush(Color.FromArgb(Math.Max(0, alpha), p.Color)))
                    g.FillEllipse(pb, p.X - sz / 2, p.Y - sz / 2, sz, sz);
            }

            // Змейка
            DrawSnake(g);

            // HUD поверх
            DrawInGameHUD(g);

            // Пауза / Game Over
            if (_paused && !_gameOver)
            {
                using (var pb = new SolidBrush(Color.FromArgb(180, 5, 5, 15)))
                    g.FillRectangle(pb, 0, 0, COLS * CELL, ROWS * CELL);
                DrawCenteredText(g, "⏸  ПАУЗА", 26f, Color.White, -24);
                DrawCenteredText(g, "Нажми P чтобы продолжить", 10f, Color.FromArgb(150, 155, 180), 18);
            }

            if (_gameOver)
            {
                using (var ob = new SolidBrush(Color.FromArgb(200, 5, 5, 15)))
                    g.FillRectangle(ob, 0, 0, COLS * CELL, ROWS * CELL);
                DrawCenteredText(g, "GAME OVER", 28f, Color.FromArgb(233, 69, 96), -36);
                DrawCenteredText(g, $"Счёт: {_score}  •  Длина: {_snake.Count}", 11f, Color.White, 8);
                DrawCenteredText(g, "Space — новая игра", 9f, Color.FromArgb(120, 120, 160), 36);
            }
        }

        private void DrawGrid(Graphics g)
        {
            // Чередующиеся клетки
            for (int r = 0; r < ROWS; r++)
                for (int c = 0; c < COLS; c++)
                {
                    Color cell = ((r + c) % 2 == 0) ? Theme.Cell1 : Theme.Cell2;
                    using (var b = new SolidBrush(cell))
                        g.FillRectangle(b, c * CELL, r * CELL, CELL, CELL);
                }

            // Для неон-карты — светящаяся сетка
            if (_map == SnakeMap.Neon)
            {
                using (var p = new Pen(Color.FromArgb(15, 0, 255, 180), 1))
                {
                    for (int x = 0; x <= COLS; x++) g.DrawLine(p, x * CELL, 0, x * CELL, ROWS * CELL);
                    for (int y = 0; y <= ROWS; y++) g.DrawLine(p, 0, y * CELL, COLS * CELL, y * CELL);
                }
            }

            // Для космоса — звёзды
            if (_map == SnakeMap.Space)
            {
                var starRng = new Random(42);
                for (int i = 0; i < 60; i++)
                {
                    int sx = starRng.Next(COLS * CELL);
                    int sy = starRng.Next(ROWS * CELL);
                    float bright = (float)(starRng.NextDouble() * 0.5 + 0.3);
                    using (var sb = new SolidBrush(Color.FromArgb((int)(bright * 200), 200, 220, 255)))
                        g.FillEllipse(sb, sx, sy, 2, 2);
                }
            }

            // Рамка
            using (var p = new Pen(Color.FromArgb(40, Theme.Food), 2))
                g.DrawRectangle(p, 1, 1, COLS * CELL - 2, ROWS * CELL - 2);
        }

        private void DrawFood(Graphics g, Point food, Color foodColor, int tick)
        {
            float pulse = (float)Math.Sin(tick * 0.15) * 2.5f;
            float cx = food.X * CELL + CELL / 2f;
            float cy = food.Y * CELL + CELL / 2f;
            float r = CELL / 2f - 2 + pulse;

            // Свечение
            for (int glow = 3; glow >= 1; glow--)
            {
                using (var gb = new SolidBrush(Color.FromArgb(20 * glow, foodColor)))
                    g.FillEllipse(gb, cx - r - glow * 3, cy - r - glow * 3, (r + glow * 3) * 2, (r + glow * 3) * 2);
            }

            // Тело
            using (var fb = new SolidBrush(foodColor))
                g.FillEllipse(fb, cx - r, cy - r, r * 2, r * 2);

            // Блик
            using (var hb = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                g.FillEllipse(hb, cx - r + 3, cy - r + 3, r * 0.5f, r * 0.5f);

            // Стебель/листик для яблока
            if (_map == SnakeMap.Classic || _map == SnakeMap.Forest)
            {
                using (var sp = new Pen(Color.FromArgb(100, 60, 20), 2))
                    g.DrawLine(sp, cx, cy - r - 1, cx + 3, cy - r - 5);
                using (var lf = new SolidBrush(Color.FromArgb(80, 180, 40)))
                    g.FillEllipse(lf, cx + 1, cy - r - 7, 6, 4);
            }
        }

        private void DrawObstacle(Graphics g, int col, int row)
        {
            float x = col * CELL + 1, y = row * CELL + 1;
            float w = CELL - 2, h = CELL - 2;

            using (var ob = new SolidBrush(Color.FromArgb(100, 70, 50)))
                g.FillRectangle(ob, x, y, w, h);
            using (var op = new Pen(Color.FromArgb(150, 100, 60), 1))
                g.DrawRectangle(op, x, y, w, h);

            // Крест
            using (var cp = new Pen(Color.FromArgb(180, 80, 50), 2))
            {
                g.DrawLine(cp, x + 3, y + 3, x + w - 3, y + h - 3);
                g.DrawLine(cp, x + w - 3, y + 3, x + 3, y + h - 3);
            }
        }

        private void DrawSnake(Graphics g)
        {
            int idx = 0, total = _snake.Count;
            foreach (var seg in _snake)
            {
                float t = 1f - (float)idx / Math.Max(total, 1);
                Color segColor = InterpolateColor(Theme.SnakeTail, Theme.SnakeHead, t);

                float sx = seg.X * CELL + 1, sy = seg.Y * CELL + 1;
                float sw = CELL - 2, sh = CELL - 2;

                // Свечение у головы
                if (idx == 0 && _map == SnakeMap.Neon)
                {
                    using (var glow = new SolidBrush(Color.FromArgb(40, segColor)))
                        g.FillEllipse(glow, sx - 4, sy - 4, sw + 8, sh + 8);
                }

                // Тело
                using (var sb = new SolidBrush(segColor))
                {
                    if (idx == 0)
                    {
                        // Голова — скруглённый прямоугольник
                        using (var path = RoundRect(new Rectangle((int)sx, (int)sy, (int)sw, (int)sh), 6))
                            g.FillPath(sb, path);
                    }
                    else
                    {
                        using (var path = RoundRect(new Rectangle((int)sx + 1, (int)sy + 1, (int)sw - 2, (int)sh - 2), 4))
                            g.FillPath(sb, path);
                    }
                }

                // Глаза на голове
                if (idx == 0)
                {
                    DrawEyes(g, seg, _dir);
                }
                // Блик на теле
                else if (idx < total - 1)
                {
                    using (var shine = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                        g.FillEllipse(shine, sx + 3, sy + 2, sw * 0.4f, sh * 0.3f);
                }

                idx++;
            }
        }

        private void DrawEyes(Graphics g, Point head, Dir dir)
        {
            float hx = head.X * CELL + 1, hy = head.Y * CELL + 1;
            float sz = CELL - 2;

            PointF eye1, eye2;
            switch (dir)
            {
                case Dir.Right: eye1 = new PointF(hx + sz - 6, hy + 3); eye2 = new PointF(hx + sz - 6, hy + sz - 7); break;
                case Dir.Left: eye1 = new PointF(hx + 2, hy + 3); eye2 = new PointF(hx + 2, hy + sz - 7); break;
                case Dir.Up: eye1 = new PointF(hx + 3, hy + 2); eye2 = new PointF(hx + sz - 7, hy + 2); break;
                default: eye1 = new PointF(hx + 3, hy + sz - 6); eye2 = new PointF(hx + sz - 7, hy + sz - 6); break;
            }

            // Белок
            g.FillEllipse(Brushes.White, eye1.X, eye1.Y, 5, 5);
            g.FillEllipse(Brushes.White, eye2.X, eye2.Y, 5, 5);
            // Зрачок
            g.FillEllipse(Brushes.Black, eye1.X + 1.5f, eye1.Y + 1.5f, 2.5f, 2.5f);
            g.FillEllipse(Brushes.Black, eye2.X + 1.5f, eye2.Y + 1.5f, 2.5f, 2.5f);
            // Блик
            g.FillEllipse(Brushes.White, eye1.X + 1, eye1.Y + 1, 1.5f, 1.5f);
            g.FillEllipse(Brushes.White, eye2.X + 1, eye2.Y + 1, 1.5f, 1.5f);
        }

        private void DrawInGameHUD(Graphics g)
        {
            // Верхняя полоска
            using (var hb = new SolidBrush(Color.FromArgb(150, 5, 5, 15)))
                g.FillRectangle(hb, 0, 0, COLS * CELL, 34);

            using (var sf = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                g.DrawString($"Счёт: {_score}", sf, new SolidBrush(Color.White), 8, 9);
                g.DrawString($"×{Math.Max(1, _combo)} комбо", sf,
                    new SolidBrush(_combo > 2 ? Color.FromArgb(255, 200, 40) : Color.FromArgb(120, 125, 160)),
                    COLS * CELL / 2f - 30, 9);
                g.DrawString($"Ур.{_level}  •  {_snake.Count} сег.", sf,
                    new SolidBrush(Color.FromArgb(100, 180, 255)), COLS * CELL - 130, 9);
            }
        }

        private void DrawCenteredText(Graphics g, string text, float size, Color color, int offsetY)
        {
            int cw = COLS * CELL, ch = ROWS * CELL;
            using (var f = new Font("Segoe UI", size, FontStyle.Bold))
            using (var b = new SolidBrush(color))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, f, b, new RectangleF(0, ch / 2f + offsetY - size / 2f, cw, size * 2f), sf);
            }
        }

        private Color InterpolateColor(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        private GraphicsPath RoundRect(Rectangle rc, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void OnAchUnlocked(Achievement ach)
        {
            if (InvokeRequired) { Invoke(new Action<Achievement>(OnAchUnlocked), ach); return; }
            _toast?.Show(ach.Name);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AchievementManager.OnAchievementUnlocked -= OnAchUnlocked;
            base.OnFormClosed(e);
        }
    }

    // ── Частица ───────────────────────────────────────────────────────────────
    public struct FoodParticle
    {
        public float X, Y, VX, VY, Life;
        public Color Color;
    }

    // ── Тема карты ───────────────────────────────────────────────────────────
    public class SnakeTheme
    {
        public string Name;
        public Color BgMain, BgSecond, SnakeHead, Food, Cell1, Cell2;
        public Color SnakeTail => Color.FromArgb(SnakeHead.R / 2 + 20, SnakeHead.G / 2 + 20, SnakeHead.B / 2 + 20);

        public SnakeTheme(string name, Color bg, Color bg2, Color head, Color food, Color c1, Color c2)
        { Name = name; BgMain = bg; BgSecond = bg2; SnakeHead = head; Food = food; Cell1 = c1; Cell2 = c2; }
    }

    // ── Диалог выбора карты/режима ────────────────────────────────────────────
    public class SnakeSelectDialog : Form
    {
        public SnakeMap SelectedMap { get; private set; } = SnakeMap.Classic;
        public SnakeMode SelectedMode { get; private set; } = SnakeMode.Classic;

        private int _selMap = 0, _selMode = 0;
        private Panel[] _mapCards = new Panel[4];
        private Panel[] _modeCards = new Panel[4];

        private static readonly string[] MapNames = { "Классика", "Неон", "Лес", "Космос" };
        private static readonly string[] MapIcons = { "🟩", "🌟", "🌲", "🚀" };
        private static readonly Color[] MapColors =
        {
            Color.FromArgb(80,200,80), Color.FromArgb(0,255,180),
            Color.FromArgb(120,220,60), Color.FromArgb(60,140,255)
        };

        private static readonly string[] ModeNames = { "Классика", "Без стен", "2 яблока", "Препятствия" };
        private static readonly string[] ModeIcons = { "🐍", "👻", "🍎🍊", "🧱" };
        private static readonly string[] ModeDescs =
        {
            "Классические правила","Проходи сквозь стены","Два яблока одновременно","Избегай препятствий"
        };
        private static readonly Color[] ModeColors =
        {
            Color.FromArgb(80,200,80), Color.FromArgb(80,180,255),
            Color.FromArgb(255,160,40), Color.FromArgb(200,80,80)
        };

        public SnakeSelectDialog()
        {
            Text = "Выбор карты и режима";
            Size = new Size(520, 430);
            BackColor = Color.FromArgb(14, 14, 24);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var title = new Label
            {
                Text = "ЗМЕЙКА",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 12),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(title);

            AddSectionLabel("Карта:", 52);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new SnakeCard(MapIcons[i], MapNames[i], MapColors[i], i == 0);
                card.Location = new Point(18 + i * 116, 72);
                card.Size = new Size(108, 86);
                card.Click += (s, e) =>
                {
                    _selMap = idx;
                    for (int j = 0; j < 4; j++) ((SnakeCard)_mapCards[j]).SetActive(j == idx);
                };
                _mapCards[i] = card;
                Controls.Add(card);
            }

            AddSectionLabel("Режим игры:", 172);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new SnakeModeCard(ModeIcons[i], ModeNames[i], ModeDescs[i], ModeColors[i], i == 0);
                card.Location = new Point(18 + i * 116, 192);
                card.Size = new Size(108, 86);
                card.Click += (s, e) =>
                {
                    _selMode = idx;
                    for (int j = 0; j < 4; j++) ((SnakeModeCard)_modeCards[j]).SetActive(j == idx);
                };
                _modeCards[i] = card;
                Controls.Add(card);
            }

            var btnStart = new Button
            {
                Text = "ИГРАТЬ",
                Location = new Point(120, 320),
                Size = new Size(160, 48),
                BackColor = Color.FromArgb(60, 180, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) =>
            {
                SelectedMap = (SnakeMap)_selMap;
                SelectedMode = (SnakeMode)_selMode;
                DialogResult = DialogResult.OK;
            };

            var btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(292, 320),
                Size = new Size(110, 48),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.FromArgb(160, 160, 200),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };

            Controls.AddRange(new Control[] { btnStart, btnCancel });
        }

        private void AddSectionLabel(string text, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 120, 160),
                Location = new Point(18, y),
                BackColor = Color.Transparent
            });
        }
    }

    // ── Карточки выбора ──────────────────────────────────────────────────────
    public class SnakeCard : Panel
    {
        protected string _icon, _name; protected Color _color; protected bool _active, _hovered;
        public SnakeCard(string icon, string name, Color color, bool active)
        {
            _icon = icon; _name = name; _color = color; _active = active;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        public void SetActive(bool a) { _active = a; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RR(rc, 10))
            {
                using (var b = new SolidBrush(_active ? Color.FromArgb(50, _color) :
                    (_hovered ? Color.FromArgb(25, _color) : Color.FromArgb(20, 20, 36))))
                    g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(_active ? 220 : (_hovered ? 100 : 40), _color), _active ? 2f : 1f))
                    g.DrawPath(p, path);
            }
            using (var f = new Font("Segoe UI", 20f)) g.DrawString(_icon, f, Brushes.White, Width / 2f - 14, 10);
            using (var f = new Font("Segoe UI", 8.5f, _active ? FontStyle.Bold : FontStyle.Regular))
            using (var b2 = new SolidBrush(_active ? _color : Color.FromArgb(150, 155, 180)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(_name, f, b2, new RectangleF(0, 56, Width, 22), sf);
            }
        }
        protected GraphicsPath RR(Rectangle b, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    public class SnakeModeCard : SnakeCard
    {
        private string _desc;
        public SnakeModeCard(string icon, string name, string desc, Color color, bool active)
            : base(icon, name, color, active) { _desc = desc; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var f = new Font("Segoe UI", 6.5f))
            using (var b = new SolidBrush(Color.FromArgb(90, 95, 120)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                e.Graphics.DrawString(_desc, f, b, new RectangleF(2, 70, Width - 4, 18), sf);
            }
        }
    }

    // ── Кнопка HUD змейки ────────────────────────────────────────────────────
    public class SnakeHudBtn : Button
    {
        public SnakeHudBtn(string text, Color bg)
        {
            Text = text; BackColor = bg; ForeColor = Color.FromArgb(190, 195, 220);
            FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9f);
            FlatAppearance.BorderColor = Color.FromArgb(40, 50, 80);
            FlatAppearance.BorderSize = 1;
        }
    }
}