using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArcadeHub
{
    public class TetrisForm : Form
    {
        private const int COLS = 10, ROWS = 20, CELL = 30;

        private static readonly Color[] PieceColors = {
            Color.Cyan, Color.Yellow, Color.Purple,
            Color.Green, Color.Red, Color.Blue, Color.Orange
        };

        private static readonly int[][][][] Pieces = BuildPieces();

        private int[,] _board = new int[ROWS, COLS];
        private int _curType, _curRot, _curX, _curY, _nextType;
        private int _score, _lines, _level;
        private bool _gameOver, _started;

        private Timer _gameTimer;
        private DoubleBufferedPictureBox _gameBox, _nextBox;
        private Label _lblScore, _lblLines, _lblLevel, _lblHiScore, _lblStatAll;
        private ToastNotification _toast;
        private DateTime _gameStart;
        private readonly Random _rng = new Random();

        public TetrisForm()
        {
            BuildUI();
            AchievementManager.OnAchievementUnlocked += OnAchUnlocked;
            PickNext(); Spawn(); BeginGame();
        }

        private void BuildUI()
        {
            Text = "Тетрис — ArcadeHub";
            Size = new Size(COLS * CELL + 180, ROWS * CELL + 80);
            BackColor = Color.FromArgb(26, 26, 46);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            _gameBox = new DoubleBufferedPictureBox { Location = new Point(10, 40), Size = new Size(COLS * CELL, ROWS * CELL), BackColor = Color.FromArgb(10, 10, 30) };
            _gameBox.Paint += PaintBoard;

            int sx = COLS * CELL + 20;
            _nextBox = new DoubleBufferedPictureBox { Location = new Point(sx, 40), Size = new Size(120, 100), BackColor = Color.FromArgb(10, 10, 30) };
            _nextBox.Paint += PaintNext;

            Label Lbl(int y, string t, Color c, Font f = null) { var l = new Label { Location = new Point(sx, y), AutoSize = true, Text = t, ForeColor = c }; l.Font = f ?? new Font("Consolas", 10f); return l; }

            var capScore = Lbl(155, "СЧЁТ", Color.FromArgb(160, 160, 180));
            var capLines = Lbl(210, "ЛИНИЙ", Color.FromArgb(160, 160, 180));
            var capLevel = Lbl(265, "УРОВЕНЬ", Color.FromArgb(160, 160, 180));
            var capHi = Lbl(320, "РЕКОРД", Color.FromArgb(160, 160, 180));
            var capNext = Lbl(15, "СЛЕДУЮЩАЯ", Color.FromArgb(160, 160, 180));

            _lblScore = Lbl(170, "0", Color.White, new Font("Consolas", 13f, FontStyle.Bold));
            _lblLines = Lbl(225, "0", Color.White);
            _lblLevel = Lbl(280, "1", Color.White);
            _lblHiScore = Lbl(335, GameStats.TetrisHighScore.ToString(), Color.FromArgb(233, 69, 96));

            var btnAch = new ArcadeButton("", "Достижения", Color.FromArgb(255, 167, 38)) { Location = new Point(sx, 390), Size = new Size(130, 50) };
            btnAch.Click += (s, e) => new AchievementsForm("Тетрис").ShowDialog(this);
            var btnTw = new TwitchButton { Location = new Point(sx, 455) };

            // ── Статистика ────────────────────────────────────────────────
            var capStat = Lbl(520, "─ СТАТИСТИКА ─", Color.FromArgb(55, 65, 100));
            _lblStatAll = new Label
            {
                Location = new Point(sx, 538),
                Size = new Size(150, 120),
                Font = new Font("Consolas", 7.5f),
                ForeColor = Color.FromArgb(95, 105, 145),
                BackColor = Color.Transparent
            };
            RefreshStatLabel();

            Size = new Size(COLS * CELL + 180, Math.Max(ROWS * CELL + 80, 720));
            Controls.AddRange(new Control[] { _gameBox, _nextBox, capScore, capLines, capLevel, capHi, capNext,
                _lblScore, _lblLines, _lblLevel, _lblHiScore, btnAch, btnTw, capStat, _lblStatAll });

            _gameTimer = new Timer { Interval = 500 };
            _gameTimer.Tick += Tick;
            _toast = new ToastNotification(this);
        }

        // ── ИСПРАВЛЕНИЕ УПРАВЛЕНИЯ: ProcessCmdKey перехватывает стрелки ──
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_gameOver)
            {
                switch (keyData)
                {
                    case Keys.Left: if (Fits(_curType, _curRot, _curY, _curX - 1)) _curX--; _gameBox.Invalidate(); return true;
                    case Keys.Right: if (Fits(_curType, _curRot, _curY, _curX + 1)) _curX++; _gameBox.Invalidate(); return true;
                    case Keys.Down: if (Fits(_curType, _curRot, _curY + 1, _curX)) _curY++; _gameBox.Invalidate(); return true;
                    case Keys.Up:
                        int nr = (_curRot + 1) % 4;
                        if (Fits(_curType, nr, _curY, _curX)) _curRot = nr;
                        _gameBox.Invalidate(); return true;
                    case Keys.Space:
                        _curY = GhostY(); Lock();
                        int n = ClearLines(); if (n > 0) ProcessLines(n);
                        Spawn(); _gameBox.Invalidate(); return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BeginGame() { _gameStart = DateTime.Now; _gameTimer.Start(); _started = true; GameStats.TetrisTotalGames++; }
        private void PickNext() { _nextType = _rng.Next(7); }

        private void Spawn()
        {
            _curType = _nextType; _curRot = 0; _curX = 3; _curY = 0;
            PickNext(); _nextBox.Invalidate();
            if (!Fits(_curType, _curRot, _curY, _curX)) { _gameOver = true; _gameTimer.Stop(); GameEnded(); }
        }

        private bool Fits(int type, int rot, int y, int x)
        {
            foreach (var cell in Pieces[type][rot])
            {
                int r = y + cell[0], c = x + cell[1];
                if (r < 0 || r >= ROWS || c < 0 || c >= COLS || _board[r, c] != 0) return false;
            }
            return true;
        }

        private void Lock() { foreach (var cell in Pieces[_curType][_curRot]) _board[_curY + cell[0], _curX + cell[1]] = _curType + 1; }

        private int ClearLines()
        {
            int cleared = 0;
            for (int r = ROWS - 1; r >= 0; r--)
            {
                bool full = true;
                for (int c = 0; c < COLS; c++) if (_board[r, c] == 0) { full = false; break; }
                if (!full) continue;
                cleared++;
                for (int rr = r; rr > 0; rr--) for (int c = 0; c < COLS; c++) _board[rr, c] = _board[rr - 1, c];
                for (int c = 0; c < COLS; c++) _board[0, c] = 0;
                r++;
            }
            return cleared;
        }

        private int GhostY() { int gy = _curY; while (Fits(_curType, _curRot, gy + 1, _curX)) gy++; return gy; }

        private void Tick(object s, EventArgs e)
        {
            if (_gameOver) return;
            if (Fits(_curType, _curRot, _curY + 1, _curX)) _curY++;
            else { Lock(); int n = ClearLines(); if (n > 0) ProcessLines(n); Spawn(); }
            _gameBox.Invalidate();
        }

        private void ProcessLines(int n)
        {
            int[] pts = { 0, 100, 300, 500, 800 };
            _score += pts[Math.Min(n, 4)] * (_level + 1);
            _lines += n; _level = _lines / 10;
            _gameTimer.Interval = Math.Max(100, 500 - _level * 40);
            RefreshLabels(); CheckAch(n);
        }

        private void CheckAch(int cleared)
        {
            if (_lines >= 1) AchievementManager.Unlock("tetris_first_line");
            if (cleared == 2) AchievementManager.Unlock("tetris_double");
            if (cleared == 3) AchievementManager.Unlock("tetris_triple");
            if (cleared == 4) AchievementManager.Unlock("tetris_tetris");
            AchievementManager.UpdateProgress("tetris_novice", _score);
            AchievementManager.UpdateProgress("tetris_skilled", _score);
            AchievementManager.UpdateProgress("tetris_pro", _score);
            AchievementManager.UpdateProgress("tetris_legend", _score);
            AchievementManager.UpdateProgress("tetris_level5", _level + 1);
            AchievementManager.UpdateProgress("tetris_speedrun", _level + 1);
            AchievementManager.UpdateProgress("tetris_level15", _level + 1);
            AchievementManager.UpdateProgress("tetris_master", GameStats.TetrisTotalLines + _lines);
            AchievementManager.UpdateProgress("tetris_lines500", GameStats.TetrisTotalLines + _lines);
            AchievementManager.UpdateProgress("tetris_10games", GameStats.TetrisTotalGames);
        }

        private void RefreshLabels()
        {
            _lblScore.Text = _score.ToString();
            _lblLines.Text = _lines.ToString();
            _lblLevel.Text = (_level + 1).ToString();
            if (_score > GameStats.TetrisHighScore) { GameStats.TetrisHighScore = _score; _lblHiScore.Text = _score.ToString(); }
            RefreshStatLabel();
        }

        private void RefreshStatLabel()
        {
            if (_lblStatAll == null) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Рекорд:  {GameStats.TetrisHighScore}");
            sb.AppendLine($"Линий:   {GameStats.TetrisTotalLines}");
            sb.AppendLine($"Игр:     {GameStats.TetrisTotalGames}");
            sb.AppendLine($"Макс ур: {GameStats.TetrisMaxLevel}");
            var t = TimeSpan.FromSeconds(GameStats.TotalPlayTimeSeconds);
            sb.Append($"Время:   {t:hh\\:mm\\:ss}");
            _lblStatAll.Text = sb.ToString();
        }

        private void PaintBoard(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            for (int r = 0; r < ROWS; r++)
                for (int c = 0; c < COLS; c++)
                {
                    var rc = new Rectangle(c * CELL, r * CELL, CELL - 1, CELL - 1);
                    if (_board[r, c] != 0)
                    {
                        using (var b = new SolidBrush(PieceColors[_board[r, c] - 1])) g.FillRectangle(b, rc);
                        using (var p = new Pen(Color.FromArgb(200, Color.White))) g.DrawRectangle(p, rc);
                    }
                    else { using (var p = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawRectangle(p, rc); }
                }
            if (_gameOver) return;
            int gy = GhostY();
            foreach (var cell in Pieces[_curType][_curRot])
            {
                int r = gy + cell[0], c = _curX + cell[1];
                if (r >= 0 && r < ROWS && c >= 0 && c < COLS)
                {
                    var rc2 = new Rectangle(c * CELL, r * CELL, CELL - 1, CELL - 1);
                    using (var b = new SolidBrush(Color.FromArgb(60, PieceColors[_curType]))) g.FillRectangle(b, rc2);
                    using (var p = new Pen(Color.FromArgb(120, PieceColors[_curType]))) g.DrawRectangle(p, rc2);
                }
            }
            foreach (var cell in Pieces[_curType][_curRot])
            {
                int r = _curY + cell[0], c = _curX + cell[1];
                if (r >= 0 && r < ROWS && c >= 0 && c < COLS)
                {
                    var rc2 = new Rectangle(c * CELL, r * CELL, CELL - 1, CELL - 1);
                    using (var b = new SolidBrush(PieceColors[_curType])) g.FillRectangle(b, rc2);
                    using (var p = new Pen(Color.White)) g.DrawRectangle(p, rc2);
                }
            }
        }

        private void PaintNext(object s, PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Color.FromArgb(10, 10, 30));
            int cs = 22;
            foreach (var cell in Pieces[_nextType][0])
            {
                var rc = new Rectangle(10 + cell[1] * cs, 10 + cell[0] * cs, cs - 2, cs - 2);
                using (var b = new SolidBrush(PieceColors[_nextType])) g.FillRectangle(b, rc);
                using (var p = new Pen(Color.White)) g.DrawRectangle(p, rc);
            }
        }

        private void GameEnded()
        {
            int el = (int)(DateTime.Now - _gameStart).TotalSeconds;
            GameStats.TotalPlayTimeSeconds += el; GameStats.TotalScore += _score;
            if (_score > GameStats.TetrisHighScore) GameStats.TetrisHighScore = _score;
            GameStats.TetrisTotalLines += _lines;
            if (_level + 1 > GameStats.TetrisMaxLevel) GameStats.TetrisMaxLevel = _level + 1;
            AchievementManager.UpdateProgress("tetris_master", GameStats.TetrisTotalLines);
            AchievementManager.UpdateProgress("tetris_lines500", GameStats.TetrisTotalLines);
            GameStats.Save();
            MessageBox.Show($"Игра окончена!\nСчёт: {_score}\nЛиний: {_lines}\nУровень: {_level + 1}", "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private void OnAchUnlocked(Achievement ach) { if (InvokeRequired) { Invoke(new Action<Achievement>(OnAchUnlocked), ach); return; } _toast.Show(ach.Name); }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AchievementManager.OnAchievementUnlocked -= OnAchUnlocked;
            if (!_gameOver && _started) { int el = (int)(DateTime.Now - _gameStart).TotalSeconds; GameStats.TotalPlayTimeSeconds += el; GameStats.TotalScore += _score; GameStats.Save(); }
            base.OnFormClosed(e);
        }

        private static int[][][][] BuildPieces()
        {
            var p = new int[7][][][];
            p[0] = new[] { new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 } }, new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 } }, new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 } } };
            p[1] = new[] { new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 } } };
            p[2] = new[] { new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 1 } }, new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 1, 1 } }, new[] { new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 }, new[] { 0, 1 } }, new[] { new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 1 }, new[] { 1, 0 } } };
            p[3] = new[] { new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 0 }, new[] { 1, 1 } }, new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 2, 1 } }, new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 0 }, new[] { 1, 1 } }, new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 2, 1 } } };
            p[4] = new[] { new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, 2 } }, new[] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 2, 0 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, 2 } }, new[] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 2, 0 } } };
            p[5] = new[] { new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 2, 0 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 2 } }, new[] { new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 0 }, new[] { 2, 1 } } };
            p[6] = new[] { new[] { new[] { 0, 2 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } }, new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 2, 1 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 0 } }, new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 1 } } };
            return p;
        }
    }
}