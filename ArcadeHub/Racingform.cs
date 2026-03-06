using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArcadeHub
{
    // ── Карты (треки) ────────────────────────────────────────────────────────
    public enum RaceMap { City, Desert, Night, Ice }

    public class RacingForm : Form
    {
        private const int W = 440, H = 620;
        private const int RoadLeft = 55, RoadRight = 385, RoadW = 330;
        private const int LaneW = 110;

        private struct Car { public float X, Y; public Color Body, Roof; public float Speed; public int Lane; }
        private struct Coin { public float X, Y; public bool Active; public float Pulse; }
        private struct Boost { public float X, Y; public bool Active; public float Anim; }

        // ── Состояние ─────────────────────────────────────────────────────
        private Car _player;
        private List<Car> _enemies = new List<Car>();
        private List<Coin> _coins = new List<Coin>();
        private List<Boost> _boosts = new List<Boost>();
        private float _roadOffset, _roadOffset2;
        private float _speed;
        private float _boostMult = 1f, _boostTimer = 0f;
        private int _score, _distance, _coinCount, _combo, _maxCombo;
        private bool _gameOver, _paused;
        private bool _leftKey, _rightKey, _upKey, _downKey;
        private Timer _gameTimer;
        private int _tickCount;
        private int _lives = 3;
        private int _shieldTicks = 0;
        private Random _rng = new Random();
        private DateTime _gameStart;
        private ToastNotification _toast;

        // ── Карта и уровень ───────────────────────────────────────────────
        private RaceMap _map = RaceMap.City;
        private int _level = 1;         // 1..5
        private float _flashAlpha = 0f;   // при столкновении (со щитом)

        // ── UI ────────────────────────────────────────────────────────────
        private DoubleBufferedPictureBox _canvas;
        private Panel _hud;
        private Label _lblDist, _lblCoins, _lblScore, _lblRecord, _lblLevel, _lblLives, _lblStatAll;
        private Panel _mapBar;

        // ── Цвета карты ───────────────────────────────────────────────────
        private MapTheme Theme => _themes[(int)_map];
        private static readonly MapTheme[] _themes = new MapTheme[]
        {
            // City
            new MapTheme("Город",   Color.FromArgb(60,60,70),   Color.FromArgb(40,40,50),
                                    Color.FromArgb(30,120,30),  Color.FromArgb(255,220,50), Color.FromArgb(255,120,20)),
            // Desert
            new MapTheme("Пустыня", Color.FromArgb(180,140,70), Color.FromArgb(160,110,50),
                                    Color.FromArgb(200,170,80), Color.FromArgb(255,200,60), Color.FromArgb(220,80,20)),
            // Night
            new MapTheme("Ночь",    Color.FromArgb(25,25,60),   Color.FromArgb(15,15,45),
                                    Color.FromArgb(10,30,80),   Color.FromArgb(80,200,255), Color.FromArgb(200,50,255)),
            // Ice
            new MapTheme("Лёд",     Color.FromArgb(180,210,240),Color.FromArgb(160,190,220),
                                    Color.FromArgb(200,230,255),Color.FromArgb(140,220,255),Color.FromArgb(60,140,255)),
        };

        // Вызывается из меню вместо new RacingForm()
        public static RacingForm TryCreate()
        {
            var dlg = new MapSelectDialog();
            if (dlg.ShowDialog() != DialogResult.OK) return null;
            return new RacingForm(dlg.SelectedMap, dlg.SelectedLevel);
        }

        public RacingForm(RaceMap map = RaceMap.City, int level = 1)
        {
            _map = map;
            _level = level;
            InitUI();
            AchievementManager.OnAchievementUnlocked += ShowAchievement;
            ResetGame();
        }

        // ── Смена карты во время игры ────────────────────────────────────
        private void ShowMapSelect()
        {
            var dlg = new MapSelectDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _map = dlg.SelectedMap;
            _level = dlg.SelectedLevel;
            ResetGame();
        }

        // ── UI ───────────────────────────────────────────────────────────
        private void InitUI()
        {
            Text = "Гонки — ArcadeHub";
            Size = new Size(W + 220, H + 50);
            BackColor = Color.FromArgb(14, 14, 26);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            _canvas = new DoubleBufferedPictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(W, H),
                BackColor = Color.Black
            };
            _canvas.Paint += Canvas_Paint;

            int sx = W + 22;

            // ── Боковая панель ─────────────────────────────────────────
            _hud = new Panel
            {
                Location = new Point(sx, 10),
                Size = new Size(200, H),
                BackColor = Color.FromArgb(20, 20, 36)
            };
            _hud.Paint += HudPaint;

            // Лейблы
            _lblScore = MakeLabel(sx, 30, "0", Color.White, 18f, FontStyle.Bold);
            _lblDist = MakeLabel(sx, 90, "0м", Color.FromArgb(180, 200, 255), 13f);
            _lblCoins = MakeLabel(sx, 120, "0", Color.FromArgb(255, 200, 50), 13f);
            _lblRecord = MakeLabel(sx, 150, $"Рек: {GameStats.RacingBestDistance}м", Color.FromArgb(120, 120, 160), 10f);
            _lblLevel = MakeLabel(sx, 175, "Ур. 1", Color.FromArgb(80, 200, 255), 10f, FontStyle.Bold);
            _lblLives = MakeLabel(sx, 198, "III", Color.FromArgb(233, 69, 96), 13f, FontStyle.Bold);

            var btnPause = new HudButton("Пауза [P]", Color.FromArgb(60, 60, 90));
            btnPause.Location = new Point(sx, 240);
            btnPause.Size = new Size(190, 38);
            btnPause.Click += (s, e) => TogglePause();

            var btnMap = new HudButton("Сменить карту", Color.FromArgb(40, 70, 110));
            btnMap.Location = new Point(sx, 286);
            btnMap.Size = new Size(190, 38);
            btnMap.Click += (s, e) => { _gameTimer.Stop(); ShowMapSelect(); };

            var btnAch = new HudButton("Достижения", Color.FromArgb(80, 60, 20));
            btnAch.Location = new Point(sx, 332);
            btnAch.Size = new Size(190, 38);
            btnAch.Click += (s, e) => new AchievementsForm("Гонки").ShowDialog(this);

            var btnTw = new TwitchButton { Location = new Point(sx, 380), Size = new Size(190, 38) };

            // ── Панель статистики ─────────────────────────────────────────
            var lblStatTitle = new Label
            {
                Text = "── СТАТИСТИКА ──",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 65, 100),
                Location = new Point(sx + 5, 430),
                Size = new Size(185, 18),
                BackColor = Color.Transparent
            };
            _lblStatAll = MakeLabel(sx, 450, BuildStatText(), Color.FromArgb(100, 110, 150), 8f);
            _lblStatAll.Size = new Size(195, 130);

            // Управление
            var lblCtrl = new Label
            {
                Text = "Стрелки - руль/газ  |  Space - нитро",
                Font = new Font("Segoe UI", 7f),
                ForeColor = Color.FromArgb(50, 55, 80),
                Location = new Point(sx, H - 30),
                Size = new Size(195, 24),
                BackColor = Color.Transparent
            };

            Controls.AddRange(new Control[]
            {
                _canvas, _hud,
                _lblScore, _lblDist, _lblCoins, _lblRecord, _lblLevel, _lblLives,
                btnPause, btnMap, btnAch, btnTw, lblStatTitle, _lblStatAll, lblCtrl
            });

            _gameTimer = new Timer { Interval = 16 };
            _gameTimer.Tick += GameTick;
            _toast = new ToastNotification(this);

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }

        private Label MakeLabel(int x, int y, string t, Color c, float fs, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = t,
                Font = new Font("Segoe UI", fs, style),
                ForeColor = c,
                Location = new Point(x + 5, y),
                Size = new Size(195, 28),
                BackColor = Color.Transparent
            };
        }

        private void HudPaint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = new Rectangle(0, 0, _hud.Width - 1, _hud.Height - 1);
            using (var bg = new LinearGradientBrush(rc,
                Color.FromArgb(24, 24, 42), Color.FromArgb(16, 16, 30), LinearGradientMode.Vertical))
                g.FillRectangle(bg, rc);
            using (var pen = new Pen(Color.FromArgb(40, 60, 100), 1))
                g.DrawRectangle(pen, rc);

            // Секции
            DrawSection(g, 18, "ОЧКИ", 0, 60);
            DrawSection(g, 82, "СТАТИСТИКА", 60, 110);
        }

        private void DrawSection(Graphics g, int y, string title, int divY1, int divY2)
        {
            using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(60, 70, 100)))
                g.DrawString(title, f, b, new PointF(6, y));
            using (var p = new Pen(Color.FromArgb(30, 40, 70)))
                g.DrawLine(p, 4, y + 14, _hud.Width - 4, y + 14);
        }

        // ── Сброс игры ──────────────────────────────────────────────────
        private void ResetGame()
        {
            float baseSpeed = 3f + (_level - 1) * 0.8f;
            _player = new Car { X = W / 2f - 15, Y = H - 120, Body = Color.Cyan, Roof = Color.FromArgb(0, 180, 200), Speed = 0, Lane = 1 };
            _enemies.Clear(); _coins.Clear(); _boosts.Clear();
            _score = 0; _distance = 0; _coinCount = 0;
            _combo = 0; _maxCombo = 0;
            _speed = baseSpeed;
            _boostMult = 1f; _boostTimer = 0f;
            _tickCount = 0;
            _lives = 3 + (_level > 3 ? 0 : 1); // 4 на лёгких, 3 на сложных
            _shieldTicks = 60; // стартовый щит
            _gameOver = false; _paused = false;
            _gameStart = DateTime.Now;
            UpdateLabels();
            _gameTimer.Start();
        }

        // ── Управление ──────────────────────────────────────────────────
        private void OnKeyDown(object s, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.A: _leftKey = true; break;
                case Keys.Right: case Keys.D: _rightKey = true; break;
                case Keys.Up: case Keys.W: _upKey = true; break;
                case Keys.Down: case Keys.Z: _downKey = true; break;
                case Keys.Space: ActivateBoost(); break;
                case Keys.P: TogglePause(); break;
            }
        }
        private void OnKeyUp(object s, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.A: _leftKey = false; break;
                case Keys.Right: case Keys.D: _rightKey = false; break;
                case Keys.Up: case Keys.W: _upKey = false; break;
                case Keys.Down: case Keys.Z: _downKey = false; break;
            }
        }

        private void TogglePause()
        {
            _paused = !_paused;
            if (_paused) _gameTimer.Stop(); else _gameTimer.Start();
            _canvas.Invalidate();
        }

        private void ActivateBoost()
        {
            if (_boostTimer <= 0 && _coinCount >= 5)
            {
                _coinCount -= 5;
                _boostMult = 2.5f;
                _boostTimer = 180; // ~3 секунды
                UpdateLabels();
            }
        }

        // ── Игровой тик ─────────────────────────────────────────────────
        private void GameTick(object sender, EventArgs e)
        {
            if (_gameOver || _paused) return;
            _tickCount++;

            // Нарастание скорости с уровнем
            float levelMult = 1f + (_level - 1) * 0.15f;
            _speed = (3f + _tickCount / 500f) * levelMult * _boostMult;

            if (_boostTimer > 0) { _boostTimer--; if (_boostTimer == 0) _boostMult = 1f; }
            if (_shieldTicks > 0) _shieldTicks--;
            if (_flashAlpha > 0) _flashAlpha -= 0.05f;

            // Прокрутка дороги (два слоя для параллакса)
            _roadOffset = (_roadOffset + _speed) % 80f;
            _roadOffset2 = (_roadOffset2 + _speed * 0.4f) % 200f;

            // Движение игрока
            float moveSpeed = 4.5f + (_upKey ? 1.5f : 0);
            if (_leftKey && _player.X > RoadLeft + 5) _player.X -= moveSpeed;
            if (_rightKey && _player.X < RoadRight - 36) _player.X += moveSpeed;
            if (_downKey && _speed > 2f) { /* тормоз — эффект только визуальный */ }

            // Спавн врагов (чаще на высоких уровнях)
            int spawnInterval = Math.Max(40, 100 - _level * 12 - _tickCount / 300);
            if (_tickCount % spawnInterval == 0)
            {
                int lane = _rng.Next(3);
                Color[] carPalette = { Color.FromArgb(220,50,50), Color.FromArgb(50,180,50),
                    Color.FromArgb(220,120,30), Color.FromArgb(180,50,220), Color.FromArgb(200,200,50) };
                Color body = carPalette[_rng.Next(carPalette.Length)];
                _enemies.Add(new Car
                {
                    X = RoadLeft + lane * LaneW + 15,
                    Y = -70,
                    Body = body,
                    Roof = Color.FromArgb(body.R / 2, body.G / 2, body.B / 2),
                    Speed = _speed + _rng.Next(0, 2 + _level),
                    Lane = lane
                });
            }

            // Спавн монет
            if (_tickCount % 90 == 0)
            {
                int lane = _rng.Next(3);
                _coins.Add(new Coin { X = RoadLeft + lane * LaneW + 55, Y = -20, Active = true });
            }

            // Спавн бустеров каждые ~10 секунд
            if (_tickCount % 600 == 0)
            {
                int lane = _rng.Next(3);
                _boosts.Add(new Boost { X = RoadLeft + lane * LaneW + 40, Y = -30, Active = true });
            }

            // Обновить пульс монет
            for (int i = 0; i < _coins.Count; i++)
            {
                var c = _coins[i]; c.Pulse = (_tickCount * 0.08f) % (float)(Math.PI * 2); _coins[i] = c;
            }
            for (int i = 0; i < _boosts.Count; i++)
            {
                var b = _boosts[i]; b.Anim = (_tickCount * 0.05f); _boosts[i] = b;
            }

            // Движение врагов + коллизия
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var car = _enemies[i];
                car.Y += car.Speed;
                _enemies[i] = car;
                if (car.Y > H + 100) { _enemies.RemoveAt(i); continue; }

                var pr = new RectangleF(_player.X + 4, _player.Y + 4, 28, 46);
                var er = new RectangleF(car.X + 4, car.Y + 4, 28, 46);
                if (pr.IntersectsWith(er))
                {
                    if (_shieldTicks > 0) { _flashAlpha = 1f; continue; }
                    _lives--;
                    _shieldTicks = 90; _flashAlpha = 1f; _combo = 0;
                    if (_lives <= 0) { EndGame(); return; }
                    _enemies.RemoveAt(i);
                    UpdateLabels();
                }
            }

            // Монеты
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var coin = _coins[i];
                coin.Y += _speed;
                _coins[i] = coin;
                if (coin.Y > H) { _coins.RemoveAt(i); continue; }
                if (!coin.Active) continue;
                var pr = new RectangleF(_player.X, _player.Y, 36, 54);
                var cr = new RectangleF(coin.X - 12, coin.Y - 12, 24, 24);
                if (pr.IntersectsWith(cr))
                {
                    _coinCount++; _combo++; if (_combo > _maxCombo) _maxCombo = _combo;
                    _score += 10 * Math.Max(1, _combo / 3);
                    coin.Active = false; _coins[i] = coin;
                    GameStats.RacingTotalCoins++;
                    CheckAchievements();
                }
            }

            // Бустеры
            for (int i = _boosts.Count - 1; i >= 0; i--)
            {
                var b = _boosts[i];
                b.Y += _speed; _boosts[i] = b;
                if (b.Y > H) { _boosts.RemoveAt(i); continue; }
                if (!b.Active) continue;
                var pr = new RectangleF(_player.X, _player.Y, 36, 54);
                var br = new RectangleF(b.X - 14, b.Y - 14, 28, 28);
                if (pr.IntersectsWith(br))
                {
                    _boostMult = 2.5f; _boostTimer = 180;
                    b.Active = false; _boosts[i] = b;
                    _toast?.Show("Нитро активировано!");
                }
            }

            _distance = (int)(_tickCount * 0.5f * levelMult);
            _score = _distance + _coinCount * 10 * _level;

            // Переход на следующий уровень
            if (_distance > 0 && _distance % 1000 == 0 && _level < 5)
            {
                _level++;
                _toast?.Show($"Уровень {_level}!");
                _shieldTicks = 120;
            }

            UpdateLabels();
            _canvas.Invalidate();
        }

        private void UpdateLabels()
        {
            _lblScore.Text = $"{_score:N0}";
            _lblDist.Text = $"Дист: {_distance}м";
            _lblCoins.Text = $"Монет: {_coinCount}  x{Math.Max(1, _combo / 3)} комбо";
            _lblRecord.Text = $"Рек: {GameStats.RacingBestDistance}м";
            _lblLevel.Text = $"Ур.{_level}  {Theme.Name}";
            string h = "";
            for (int i = 0; i < 4; i++) h += i < _lives ? "[+]" : "[ ]";
            _lblLives.Text = h;
            if (_lblStatAll != null) _lblStatAll.Text = BuildStatText();
        }

        private string BuildStatText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Лучший заезд:   {GameStats.RacingBestDistance}м");
            sb.AppendLine($"Монет рекорд:   {GameStats.RacingBestCoins}");
            sb.AppendLine($"Монет всего:    {GameStats.RacingTotalCoins}");
            sb.AppendLine($"Дистанция всего:{GameStats.RacingTotalDistance}м");
            var t = TimeSpan.FromSeconds(GameStats.TotalPlayTimeSeconds);
            sb.AppendLine($"Время в играх:  {t:hh\\:mm\\:ss}");
            sb.Append($"Очков всего:    {GameStats.TotalScore:N0}");
            return sb.ToString();
        }

        private void CheckAchievements()
        {
            if (_distance >= 100) AchievementManager.Unlock("race_100m");
            if (_distance >= 500) AchievementManager.Unlock("race_500m");
            if (_distance >= 1000) AchievementManager.Unlock("race_1000m");
            if (_distance >= 2000) AchievementManager.Unlock("race_2000m");
            if (_coinCount >= 10) AchievementManager.Unlock("race_coins10");
            if (_coinCount >= 50) AchievementManager.Unlock("race_coins50");
            AchievementManager.UpdateProgress("race_coins500", GameStats.RacingTotalCoins);
            AchievementManager.UpdateProgress("race_coins1k", GameStats.RacingTotalCoins);
        }

        private void EndGame()
        {
            _gameOver = true;
            _gameTimer.Stop();
            _canvas.Invalidate();

            int elapsed = (int)(DateTime.Now - _gameStart).TotalSeconds;
            GameStats.TotalPlayTimeSeconds += elapsed;
            GameStats.TotalScore += _score;
            if (_distance > GameStats.RacingBestDistance) GameStats.RacingBestDistance = _distance;
            if (_coinCount > GameStats.RacingBestCoins) GameStats.RacingBestCoins = _coinCount;
            GameStats.RacingTotalDistance += _distance;
            GameStats.Save();
            CheckAchievements();

            var res = MessageBox.Show(
                $"GAME OVER\n\nКарта: {Theme.Name}  |  Уровень: {_level}\n" +
                $"Дистанция: {_distance}м\nМонет:    {_coinCount}\nОчки:     {_score}\n" +
                $"Макс комбо: {_maxCombo}x\n\nСыграть ещё?",
                "Game Over", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes) ShowMapSelect();
            else Close();
        }

        // ── РИСОВАНИЕ ────────────────────────────────────────────────────
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Фон (трава / обочина)
            using (var gb = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                Theme.Grass, Theme.GrassDark, LinearGradientMode.Vertical))
                g.FillRectangle(gb, 0, 0, W, H);

            // Параллакс-полосы на обочине
            DrawSideDetails(g);

            // Дорога
            using (var rb = new LinearGradientBrush(new Rectangle(RoadLeft, 0, RoadW, H),
                Theme.Road, Theme.RoadDark, LinearGradientMode.Vertical))
                g.FillRectangle(rb, RoadLeft, 0, RoadW, H);

            // Бордюр дороги
            g.FillRectangle(new SolidBrush(Color.FromArgb(200, 200, 200)), RoadLeft - 4, 0, 4, H);
            g.FillRectangle(new SolidBrush(Color.FromArgb(200, 200, 200)), RoadRight, 0, 4, H);
            g.FillRectangle(new SolidBrush(Color.FromArgb(240, 240, 240)), RoadLeft - 8, 0, 4, H);
            g.FillRectangle(new SolidBrush(Color.FromArgb(240, 240, 240)), RoadRight + 4, 0, 4, H);

            // Разметка — пунктир (анимированный)
            using (var pen = new Pen(Color.FromArgb(200, Theme.Line), 3))
            {
                pen.DashStyle = DashStyle.Custom;
                pen.DashPattern = new float[] { 2.5f, 1.5f };
                for (int lane = 1; lane < 3; lane++)
                {
                    int lx = RoadLeft + lane * LaneW;
                    for (float y = -(80 - _roadOffset % 80); y < H; y += 80)
                        g.DrawLine(pen, lx, y, lx, y + 48);
                }
            }

            // Горизонтальные полосы скорости (перспектива)
            if (_map == RaceMap.Night)
            {
                for (float y = -(200 - _roadOffset2 % 200); y < H; y += 200)
                {
                    using (var sp = new Pen(Color.FromArgb(20, 100, 150, 255), 1))
                        g.DrawLine(sp, RoadLeft, y, RoadRight, y);
                }
            }

            // Боковые объекты (деревья/кактусы/фонари)
            DrawSideObjects(g);

            // Бустеры
            foreach (var b in _boosts)
            {
                if (!b.Active) continue;
                float pulse = (float)Math.Sin(b.Anim) * 4f;
                int sz = 28 + (int)pulse;
                DrawBoostIcon(g, (int)b.X - sz / 2, (int)b.Y - sz / 2, sz);
            }

            // Монеты
            foreach (var coin in _coins)
            {
                if (!coin.Active) continue;
                float pulse = (float)Math.Sin(coin.Pulse) * 2f;
                float cx = coin.X, cy = coin.Y;
                int r = 12 + (int)pulse;
                using (var cb = new SolidBrush(Color.FromArgb(255, 210, 30)))
                    g.FillEllipse(cb, cx - r, cy - r, r * 2, r * 2);
                using (var cs = new SolidBrush(Color.FromArgb(255, 240, 140)))
                    g.FillEllipse(cs, cx - r + 3, cy - r + 3, r - 4, r - 4);
                using (var cf = new Font("Segoe UI", 6f, FontStyle.Bold))
                    g.DrawString("₽", cf, Brushes.DarkGoldenrod, cx - 5, cy - 6);
                // Свечение монеты
                using (var glow = new SolidBrush(Color.FromArgb(40, 255, 220, 30)))
                    g.FillEllipse(glow, cx - r - 4, cy - r - 4, (r + 4) * 2, (r + 4) * 2);
            }

            // Машины врагов
            foreach (var car in _enemies)
                DrawCar(g, car.X, car.Y, car.Body, car.Roof, false);

            // Игрок
            bool shielded = _shieldTicks > 0;
            bool boosting = _boostTimer > 0;
            if (shielded)
            {
                // Щит-ореол
                using (var sb = new SolidBrush(Color.FromArgb(40, 80, 180, 255)))
                    g.FillEllipse(sb, _player.X - 10, _player.Y - 8, 56, 70);
                using (var sp = new Pen(Color.FromArgb(120, 80, 180, 255), 2))
                    g.DrawEllipse(sp, _player.X - 10, _player.Y - 8, 56, 70);
            }
            if (boosting)
            {
                // Нитро-пламя сзади
                DrawNitroFlame(g, _player.X, _player.Y + 52);
            }
            DrawCar(g, _player.X, _player.Y, Color.Cyan, Color.FromArgb(0, 180, 200), true);

            // Вспышка при ударе
            if (_flashAlpha > 0)
            {
                using (var fb = new SolidBrush(Color.FromArgb((int)(_flashAlpha * 80), 255, 60, 60)))
                    g.FillRectangle(fb, 0, 0, W, H);
            }

            // Скорость-эффект при бусте
            if (boosting)
            {
                for (int i = 0; i < 8; i++)
                {
                    int lx = _rng.Next(RoadLeft, RoadRight);
                    int from = _rng.Next(0, H / 2);
                    using (var sp = new Pen(Color.FromArgb(30, Theme.Line), 1))
                        g.DrawLine(sp, lx, from, lx, from + 60);
                }
            }

            // Boost-полоска
            if (_boostTimer > 0 || _coinCount >= 5)
            {
                int bw = 200; int bh = 8; int bx = W / 2 - bw / 2; int by = H - 30;
                using (var bbg = new SolidBrush(Color.FromArgb(40, 40, 60)))
                    g.FillRectangle(bbg, bx, by, bw, bh);
                int fill = _boostTimer > 0
                    ? (int)((float)_boostTimer / 180 * bw)
                    : (int)(Math.Min(_coinCount, 20) / 20f * bw);
                Color bc = _boostTimer > 0 ? Color.FromArgb(100, 220, 255) : Color.FromArgb(255, 200, 40);
                using (var bfg = new SolidBrush(bc))
                    g.FillRectangle(bfg, bx, by, fill, bh);
                using (var bp = new Pen(Color.FromArgb(80, bc)))
                    g.DrawRectangle(bp, bx, by, bw, bh);
                using (var bf = new Font("Segoe UI", 7f))
                    g.DrawString(_boostTimer > 0 ? "НИТРО" : "НИТРО ГОТОВ (Space)", bf,
                        new SolidBrush(Color.FromArgb(150, bc)), bx, by - 14);
            }

            // HUD - дистанция + уровень
            DrawTopHUD(g);

            // Мини-карта
            DrawMinimap(g);

            // Пауза
            if (_paused)
            {
                using (var pb = new SolidBrush(Color.FromArgb(180, 10, 10, 25)))
                    g.FillRectangle(pb, 0, 0, W, H);
                using (var pf = new Font("Segoe UI", 28f, FontStyle.Bold))
                using (var pb2 = new SolidBrush(Color.White))
                    g.DrawString("ПАУЗА", pf, pb2, W / 2f - 70, H / 2f - 30);
                using (var pf2 = new Font("Segoe UI", 11f))
                    g.DrawString("Нажми P чтобы продолжить", pf2,
                        new SolidBrush(Color.FromArgb(160, 160, 200)), W / 2f - 110, H / 2f + 18);
            }

            // Game Over overlay
            if (_gameOver)
            {
                using (var ob = new SolidBrush(Color.FromArgb(200, 10, 10, 25)))
                    g.FillRectangle(ob, 0, 0, W, H);
                using (var of1 = new Font("Segoe UI", 34f, FontStyle.Bold))
                    g.DrawString("АВАРИЯ!", of1, Brushes.Red, W / 2f - 100, H / 2f - 60);
                using (var of2 = new Font("Segoe UI", 13f))
                    g.DrawString($"{_distance}м  •  {_coinCount} монет  •  {_score} очков",
                        of2, new SolidBrush(Color.White), W / 2f - 130, H / 2f + 10);
            }
        }

        private void DrawTopHUD(Graphics g)
        {
            // Полупрозрачная шапка
            using (var hb = new SolidBrush(Color.FromArgb(160, 10, 10, 25)))
                g.FillRectangle(hb, 0, 0, W, 40);

            // Карта
            using (var mf = new Font("Segoe UI", 8f, FontStyle.Bold))
                g.DrawString(Theme.Name.ToUpper(), mf,
                    new SolidBrush(Color.FromArgb(200, Theme.Line)), 10, 12);

            // Скорость
            int kmh = (int)(_speed * 30);
            using (var sf = new Font("Segoe UI", 8f))
                g.DrawString($"{kmh} км/ч", sf, new SolidBrush(Color.FromArgb(160, 200, 255)), W / 2f - 25, 12);

            // Жизни
            string hearts = "";
            for (int i = 0; i < 4; i++) hearts += i < _lives ? "♥" : "♡";
            using (var hf = new Font("Segoe UI", 11f))
                g.DrawString(hearts, hf, new SolidBrush(Color.FromArgb(233, 69, 96)), W - 80, 9);
        }

        private void DrawMinimap(Graphics g)
        {
            // Мини-карта в правом нижнем углу
            int mx = W - 85, my = H - 120, mw = 75, mh = 110;
            using (var mb = new SolidBrush(Color.FromArgb(140, 10, 10, 25)))
                g.FillRectangle(mb, mx, my, mw, mh);
            using (var mp = new Pen(Color.FromArgb(60, 80, 120)))
                g.DrawRectangle(mp, mx, my, mw, mh);

            // Игрок
            float px = mx + ((_player.X - RoadLeft) / RoadW) * mw;
            float py = my + (_player.Y / H) * mh;
            g.FillEllipse(Brushes.Cyan, px - 3, py - 3, 6, 6);

            // Враги
            foreach (var car in _enemies)
            {
                float ex = mx + ((car.X - RoadLeft) / RoadW) * mw;
                float ey = my + (car.Y / H) * mh;
                if (ey >= my && ey <= my + mh)
                    g.FillEllipse(Brushes.Red, ex - 2, ey - 2, 4, 4);
            }
        }

        private void DrawSideDetails(Graphics g)
        {
            // Полосы обочины
            using (var sb = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
            {
                for (float y = -(_roadOffset2 % 40); y < H; y += 40)
                {
                    g.FillRectangle(sb, 0, y, RoadLeft - 8, 2);
                    g.FillRectangle(sb, RoadRight + 8, y, W - RoadRight - 8, 2);
                }
            }
        }

        private void DrawSideObjects(Graphics g)
        {
            // Рисуем объекты по краям (деревья/кактусы/фонари) по фиксированному паттерну
            var positions = new[] { 0.05f, 0.2f, 0.4f, 0.55f, 0.75f, 0.9f };
            foreach (var t in positions)
            {
                float y = ((t * H) - _roadOffset2 * 0.5f + H * 2) % H;
                DrawSideObject(g, 18, (int)y, false);
                DrawSideObject(g, W - 22, (int)y, true);
            }
        }

        private void DrawSideObject(Graphics g, int x, int y, bool right)
        {
            switch (_map)
            {
                case RaceMap.City:
                    // Фонарный столб
                    using (var p = new Pen(Color.FromArgb(100, 100, 120), 3))
                        g.DrawLine(p, x, y, x, y + 30);
                    g.FillEllipse(new SolidBrush(Color.FromArgb(255, 240, 180)), x - 4, y - 5, 8, 8);
                    using (var glow = new SolidBrush(Color.FromArgb(30, 255, 240, 140)))
                        g.FillEllipse(glow, x - 12, y - 13, 24, 24);
                    break;
                case RaceMap.Desert:
                    // Кактус
                    using (var cb = new SolidBrush(Color.FromArgb(50, 140, 40)))
                    {
                        g.FillRectangle(cb, x - 3, y - 10, 6, 25);
                        g.FillRectangle(cb, x - 10, y - 2, 8, 5);
                        g.FillRectangle(cb, x + 3, y, 8, 5);
                    }
                    break;
                case RaceMap.Night:
                    // Неоновый знак
                    using (var np = new Pen(Color.FromArgb(200, 80, 0, 255), 2))
                        g.DrawRectangle(np, x - 6, y - 6, 12, 12);
                    using (var ng = new SolidBrush(Color.FromArgb(40, 80, 0, 255)))
                        g.FillRectangle(ng, x - 6, y - 6, 12, 12);
                    break;
                case RaceMap.Ice:
                    // Ёлка
                    var treePoints = new PointF[]
                    {
                        new PointF(x, y - 18),
                        new PointF(x - 10, y + 8),
                        new PointF(x + 10, y + 8)
                    };
                    using (var tb = new SolidBrush(Color.FromArgb(30, 160, 60)))
                        g.FillPolygon(tb, treePoints);
                    using (var sb = new SolidBrush(Color.White))
                        g.FillEllipse(sb, x - 2, y - 2, 4, 4);
                    break;
            }
        }

        private void DrawBoostIcon(Graphics g, int x, int y, int sz)
        {
            using (var gb = new SolidBrush(Color.FromArgb(60, 80, 200, 255)))
                g.FillEllipse(gb, x - 6, y - 6, sz + 12, sz + 12);
            using (var gp = new Pen(Color.FromArgb(200, 80, 200, 255), 2))
                g.DrawEllipse(gp, x, y, sz, sz);
            using (var bf = new Font("Segoe UI", 9f, FontStyle.Bold))
                g.DrawString("⚡", bf, new SolidBrush(Color.FromArgb(255, 200, 100)), x + 4, y + 4);
        }

        private void DrawNitroFlame(Graphics g, float x, float y)
        {
            float t = _tickCount * 0.15f;
            var flamePts = new PointF[]
            {
                new PointF(x + 8,  y),
                new PointF(x + 4,  y + 12 + (float)Math.Sin(t)*4),
                new PointF(x + 14, y + 8),
                new PointF(x + 18, y + 18 + (float)Math.Cos(t*1.3f)*4),
                new PointF(x + 28, y),
            };
            using (var fb = new SolidBrush(Color.FromArgb(180, 0, 180, 255)))
                g.FillPolygon(fb, flamePts);
            using (var fb2 = new SolidBrush(Color.FromArgb(100, 200, 255, 255)))
            {
                var inner = new PointF[]
                {
                    new PointF(x + 11, y + 2),
                    new PointF(x + 14, y + 10),
                    new PointF(x + 25, y + 2),
                };
                g.FillPolygon(fb2, inner);
            }
        }

        private void DrawCar(Graphics g, float x, float y, Color body, Color roof, bool isPlayer)
        {
            // Тень
            using (var sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillEllipse(sh, x + 2, y + 48, 32, 10);

            // Кузов
            using (var bb = new SolidBrush(body))
                g.FillRoundedRect(x, y + 8, 36, 46, bb, 4);

            // Бамперы
            using (var bum = new SolidBrush(Color.FromArgb(80, 80, 80)))
            {
                g.FillRectangle(bum, x + 4, y + 6, 28, 4);
                g.FillRectangle(bum, x + 4, y + 48, 28, 6);
            }

            // Кабина
            using (var cb = new SolidBrush(roof))
                g.FillRoundedRect(x + 6, y, 24, 24, cb, 4);

            // Стёкла
            using (var wb = new SolidBrush(Color.FromArgb(160, 180, 220, 255)))
            {
                g.FillRectangle(wb, x + 8, y + 2, 20, 10);
                g.FillRectangle(wb, x + 8, y + 14, 20, 8);
            }

            // Колёса
            DrawWheel(g, x - 5, y + 10);
            DrawWheel(g, x + 28, y + 10);
            DrawWheel(g, x - 5, y + 32);
            DrawWheel(g, x + 28, y + 32);

            // Фары (игрок — спереди, враги — сзади)
            if (isPlayer)
            {
                // Задние огни
                using (var rl = new SolidBrush(Color.FromArgb(220, 255, 30, 30)))
                {
                    g.FillRectangle(rl, x + 2, y + 50, 8, 4);
                    g.FillRectangle(rl, x + 26, y + 50, 8, 4);
                }
            }
            else
            {
                // Фары
                using (var hl = new SolidBrush(Color.FromArgb(220, 255, 250, 200)))
                {
                    g.FillEllipse(hl, x + 2, y + 6, 8, 4);
                    g.FillEllipse(hl, x + 26, y + 6, 8, 4);
                }
            }

            // Полоса блика
            using (var shine = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                g.FillRectangle(shine, x + 4, y + 10, 6, 36);
        }

        private void DrawWheel(Graphics g, float x, float y)
        {
            using (var wb = new SolidBrush(Color.FromArgb(30, 30, 30)))
                g.FillEllipse(wb, x, y, 12, 14);
            using (var wc = new SolidBrush(Color.FromArgb(100, 100, 120)))
                g.FillEllipse(wc, x + 3, y + 3, 6, 8);
        }

        private void ShowAchievement(Achievement ach)
        {
            if (InvokeRequired) { Invoke(new Action<Achievement>(ShowAchievement), ach); return; }
            _toast?.Show(ach.Name);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AchievementManager.OnAchievementUnlocked -= ShowAchievement;
            base.OnFormClosed(e);
        }
    }

    // ── Тема карты ───────────────────────────────────────────────────────────
    public class MapTheme
    {
        public string Name;
        public Color Road, RoadDark, Grass, GrassDark, Line;
        public MapTheme(string name, Color road, Color roadDark, Color grass, Color line, Color grassDark)
        { Name = name; Road = road; RoadDark = roadDark; Grass = grass; GrassDark = grassDark; Line = line; }
    }

    // ── Диалог выбора карты/уровня ───────────────────────────────────────────
    public class MapSelectDialog : Form
    {
        public RaceMap SelectedMap { get; private set; } = RaceMap.City;
        public int SelectedLevel { get; private set; } = 1;

        private Panel[] _mapCards = new Panel[4];
        private Panel[] _lvlCards = new Panel[5];
        private int _selMap = 0;
        private int _selLevel = 0;

        private static readonly string[] MapNames = { "Город", "Пустыня", "Ночь", "Лёд" };
        private static readonly string[] MapIcons = { "🏙", "🏜", "🌃", "❄" };
        private static readonly Color[] MapColors =
        {
            Color.FromArgb(0,188,212),
            Color.FromArgb(255,167,38),
            Color.FromArgb(145,70,255),
            Color.FromArgb(100,180,255)
        };

        private static readonly string[] LvlNames = { "Новичок", "Средний", "Хардкор", "Эксперт", "Ад" };
        private static readonly Color[] LvlColors =
        {
            Color.FromArgb(80,200,80),
            Color.FromArgb(255,200,40),
            Color.FromArgb(255,130,30),
            Color.FromArgb(233,69,96),
            Color.FromArgb(160,0,255)
        };

        public MapSelectDialog()
        {
            Text = "Выбор карты и уровня";
            Size = new Size(520, 440);
            BackColor = Color.FromArgb(16, 16, 28);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var title = new Label
            {
                Text = "ВЫБОР КАРТЫ",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(title);

            var lMap = new Label
            {
                Text = "Карта:",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(120, 130, 160),
                Location = new Point(20, 56),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(lMap);

            // Карты
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new MapCard(MapIcons[i], MapNames[i], MapColors[i], i == 0);
                card.Location = new Point(20 + i * 116, 76);
                card.Size = new Size(108, 90);
                card.Click += (s, e) =>
                {
                    _selMap = idx;
                    for (int j = 0; j < 4; j++) ((MapCard)_mapCards[j]).SetActive(j == idx);
                };
                _mapCards[i] = card;
                Controls.Add(card);
            }

            var lLvl = new Label
            {
                Text = "Сложность:",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(120, 130, 160),
                Location = new Point(20, 182),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(lLvl);

            // Уровни
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var card = new LevelCard(LvlNames[i], LvlColors[i], i + 1, i == 0);
                card.Location = new Point(20 + i * 94, 202);
                card.Size = new Size(86, 80);
                card.Click += (s, e) =>
                {
                    _selLevel = idx;
                    for (int j = 0; j < 5; j++) ((LevelCard)_lvlCards[j]).SetActive(j == idx);
                };
                _lvlCards[i] = card;
                Controls.Add(card);
            }

            // Описание уровней
            var lblDesc = new Label
            {
                Text = "Новичок: медленно, 4 жизни  •  Средний: стандарт  •  Хардкор: быстрее, больше врагов\n" +
                       "Эксперт: очень быстро  •  Ад: максимальный хаос, без пощады",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(80, 85, 110),
                Location = new Point(20, 294),
                Size = new Size(474, 40),
                BackColor = Color.Transparent
            };
            Controls.Add(lblDesc);

            var btnStart = new Button
            {
                Text = "▶  СТАРТ",
                Location = new Point(180, 348),
                Size = new Size(150, 46),
                BackColor = Color.FromArgb(0, 188, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) =>
            {
                SelectedMap = (RaceMap)_selMap;
                SelectedLevel = _selLevel + 1;
                DialogResult = DialogResult.OK;
            };
            Controls.Add(btnStart);

            var btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(340, 348),
                Size = new Size(100, 46),
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.FromArgb(140, 140, 180),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            Controls.Add(btnCancel);
        }
    }

    // ── Карточка карты ───────────────────────────────────────────────────────
    public class MapCard : Panel
    {
        private string _icon, _name;
        private Color _color;
        private bool _active, _hovered;
        public MapCard(string icon, string name, Color color, bool active)
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
            using (var p2 = RR(rc, 10))
            {
                var bg = _active ? Color.FromArgb(50, _color) : (_hovered ? Color.FromArgb(25, _color) : Color.FromArgb(22, 22, 40));
                using (var b = new SolidBrush(bg)) g.FillPath(b, p2);
                float pw = _active ? 2f : 1f; int pa = _active ? 220 : (_hovered ? 100 : 40);
                using (var pen = new Pen(Color.FromArgb(pa, _color), pw)) g.DrawPath(pen, p2);
            }
            using (var f = new Font("Segoe UI", 22f)) g.DrawString(_icon, f, Brushes.White, Width / 2f - 14, 12);
            using (var f = new Font("Segoe UI", 8.5f, _active ? FontStyle.Bold : FontStyle.Regular))
            using (var b = new SolidBrush(_active ? _color : Color.FromArgb(160, 165, 190)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(_name, f, b, new RectangleF(0, 60, Width, 22), sf);
            }
        }
        private GraphicsPath RR(Rectangle b, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    // ── Карточка уровня ──────────────────────────────────────────────────────
    public class LevelCard : Panel
    {
        private string _name; private Color _color; private int _num; private bool _active, _hovered;
        public LevelCard(string name, Color color, int num, bool active)
        {
            _name = name; _color = color; _num = num; _active = active;
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
            using (var p2 = RR(rc, 10))
            {
                var bg = _active ? Color.FromArgb(50, _color) : (_hovered ? Color.FromArgb(25, _color) : Color.FromArgb(22, 22, 40));
                using (var b = new SolidBrush(bg)) g.FillPath(b, p2);
                float pw = _active ? 2f : 1f; int pa = _active ? 220 : (_hovered ? 100 : 40);
                using (var pen = new Pen(Color.FromArgb(pa, _color), pw)) g.DrawPath(pen, p2);
            }
            using (var f = new Font("Segoe UI", 18f, FontStyle.Bold))
            using (var b = new SolidBrush(_active ? _color : Color.FromArgb(100, _color)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(_num.ToString(), f, b, new RectangleF(0, 8, Width, 32), sf);
            }
            using (var f = new Font("Segoe UI", 7.5f, _active ? FontStyle.Bold : FontStyle.Regular))
            using (var b2 = new SolidBrush(_active ? _color : Color.FromArgb(100, 110, 140)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(_name, f, b2, new RectangleF(0, 44, Width, 22), sf);
            }
        }
        private GraphicsPath RR(Rectangle b, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90); p.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
            p.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90); p.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    // ── Кнопка HUD ───────────────────────────────────────────────────────────
    public class HudButton : Button
    {
        public HudButton(string text, Color bg)
        {
            Text = text; BackColor = bg; ForeColor = Color.FromArgb(200, 205, 230);
            FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9f);
            FlatAppearance.BorderColor = Color.FromArgb(50, 60, 90);
            FlatAppearance.BorderSize = 1;
        }
    }

    // ── Extension для Graphics ────────────────────────────────────────────────
    public static class GraphicsExtensions
    {
        public static void FillRoundedRect(this Graphics g, float x, float y, float w, float h, Brush b, int r)
        {
            var rc = new RectangleF(x, y, w, h);
            using (var path = new GraphicsPath())
            {
                path.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
                path.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
                path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
                path.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                g.FillPath(b, path);
            }
        }
    }
}