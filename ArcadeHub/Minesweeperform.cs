using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArcadeHub
{
    public class MinesweeperForm : Form
    {
        // ── Типы ──────────────────────────────────────────────────────────
        public enum Difficulty { Beginner, Medium, Expert }
        private enum CellState { Hidden, Revealed, Flagged }

        // ── Параметры поля ────────────────────────────────────────────────
        private int _rows, _cols, _mines;

        // ── Данные поля ───────────────────────────────────────────────────
        private int[,] _board;   // -1=мина, 0-8=цифра
        private CellState[,] _state;
        private Button[,] _buttons;

        // ── Состояние игры ────────────────────────────────────────────────
        private bool _firstClick = true;
        private bool _gameOver = false;
        private bool _noFlags = true;
        private int _flagsPlaced = 0;
        private int _seconds = 0;

        // ── UI ────────────────────────────────────────────────────────────
        private Timer _timer;
        private Label _lblMines, _lblTime, _lblStatInfo;
        private Button _btnFace;
        private Panel _gridPanel;
        private ToastNotification _toast;

        // ── Прочее ────────────────────────────────────────────────────────
        private Difficulty _difficulty;
        private DateTime _gameStart;

        private static readonly Color[] NumColors = {
            Color.Transparent, Color.Blue, Color.DarkGreen, Color.Red,
            Color.DarkBlue, Color.DarkRed, Color.Teal, Color.Black, Color.Gray
        };

        // ══════════════════════════════════════════════════════════════════
        public static MinesweeperForm TryCreate()
        {
            var diff = AskDifficultyStatic();
            if (diff == null) return null;
            return new MinesweeperForm(diff.Value);
        }

        private static Difficulty? AskDifficultyStatic()
        {
            Difficulty? chosen = null;

            var dlg = new Form
            {
                Text = "Выбор сложности",
                Size = new Size(360, 280),
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(22, 22, 40),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };

            var lbl = new Label
            {
                Text = "Выберите уровень сложности:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(20, 18),
                AutoSize = true
            };

            string begStr = GameStats.MinesweeperBestTimeBeginner == int.MaxValue ? "--" : $"{GameStats.MinesweeperBestTimeBeginner}с";
            string medStr = GameStats.MinesweeperBestTimeMedium == int.MaxValue ? "--" : $"{GameStats.MinesweeperBestTimeMedium}с";
            string expStr = GameStats.MinesweeperBestTimeExpert == int.MaxValue ? "--" : $"{GameStats.MinesweeperBestTimeExpert}с";

            var lblStats = new Label
            {
                Text = $"Рекорды:  Новичок {begStr}  |  Средний {medStr}  |  Эксперт {expStr}\n" +
                       $"Побед: {GameStats.MinesweeperWins}  |  Серия: {GameStats.MinesweeperWinStreak}  |  Игр: {GameStats.MinesweeperTotalGames}",
                ForeColor = Color.FromArgb(100, 120, 180),
                Font = new Font("Segoe UI", 8f),
                Location = new Point(20, 46),
                Size = new Size(310, 36),
                BackColor = Color.Transparent
            };

            Button MkBtn(string text, int y, Difficulty d)
            {
                var b = new Button
                {
                    Text = text,
                    Location = new Point(20, y),
                    Size = new Size(310, 32),
                    BackColor = Color.FromArgb(15, 52, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 9.5f)
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (s, e) => { chosen = d; dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                return b;
            }

            var btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(20, 218),
                Size = new Size(120, 28),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.FromArgb(150, 150, 190),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };

            dlg.Controls.AddRange(new Control[] {
                lbl, lblStats,
                MkBtn("Новичок   (9x9,   10 мин)",   94, Difficulty.Beginner),
                MkBtn("Средний   (16x16, 40 мин)",  132, Difficulty.Medium),
                MkBtn("Эксперт   (30x16, 99 мин)",  170, Difficulty.Expert),
                btnCancel
            });

            return dlg.ShowDialog() == DialogResult.OK ? chosen : null;
        }

        public MinesweeperForm(Difficulty difficulty)
        {
            _difficulty = difficulty;
            AchievementManager.OnAchievementUnlocked += OnAchievementUnlocked;
            SetupGame();
        }

        // ── Инициализация игры ────────────────────────────────────────────
        private void SetupGame()
        {
            switch (_difficulty)
            {
                case Difficulty.Beginner: _rows = 9; _cols = 9; _mines = 10; break;
                case Difficulty.Medium: _rows = 16; _cols = 16; _mines = 40; break;
                case Difficulty.Expert: _rows = 16; _cols = 30; _mines = 99; break;
            }

            _firstClick = true;
            _gameOver = false;
            _noFlags = true;
            _flagsPlaced = 0;
            _seconds = 0;

            _board = new int[_rows, _cols];
            _state = new CellState[_rows, _cols];

            Text = "Sapyor — ArcadeHub";
            BackColor = Color.FromArgb(192, 192, 192);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Controls.Clear();

            int cellSize = 22;
            int topH = 44;

            // ── Верхняя панель ────────────────────────────────────────────
            var topPanel = new Panel
            {
                Location = new Point(5, 5),
                Size = new Size(_cols * cellSize + 4, topH),
                BackColor = Color.FromArgb(192, 192, 192)
            };
            topPanel.Paint += (s, ev) =>
                ev.Graphics.DrawRectangle(Pens.Gray, 0, 0, topPanel.Width - 1, topPanel.Height - 1);

            _lblMines = new Label
            {
                Text = _mines.ToString("D3"),
                Font = new Font("Consolas", 16f, FontStyle.Bold),
                ForeColor = Color.Red,
                BackColor = Color.Black,
                Location = new Point(5, 5),
                Size = new Size(60, 32)
            };

            _lblTime = new Label
            {
                Text = "000",
                Font = new Font("Consolas", 16f, FontStyle.Bold),
                ForeColor = Color.Red,
                BackColor = Color.Black,
                Location = new Point(topPanel.Width - 65, 5),
                Size = new Size(60, 32)
            };

            _btnFace = new Button
            {
                Text = ":)",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(topPanel.Width / 2 - 18, 3),
                Size = new Size(36, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.LightGray,
                Cursor = Cursors.Hand
            };
            _btnFace.Click += (s, e) => RestartGame();

            topPanel.Controls.AddRange(new Control[] { _lblMines, _lblTime, _btnFace });

            // ── Сетка ─────────────────────────────────────────────────────
            _gridPanel = new Panel
            {
                Location = new Point(5, 54),
                Size = new Size(_cols * cellSize + 4, _rows * cellSize + 4)
            };
            BuildGrid(cellSize);

            // ── Кнопка Twitch ─────────────────────────────────────────────
            var btnTw = new TwitchButton
            {
                Location = new Point(5, 58 + _rows * cellSize + 5)
            };

            // ── Кнопка достижений ────────────────────────────────────────
            var btnAch = new Button
            {
                Text = "Достижения",
                Location = new Point(170, 58 + _rows * cellSize + 5),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(50, 50, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f)
            };
            btnAch.FlatAppearance.BorderSize = 0;
            btnAch.Click += (s, e) => new AchievementsForm("Сапёр").ShowDialog(this);

            // ── Панель статистики ─────────────────────────────────────────
            var statPanel = new Panel
            {
                Location = new Point(5, 58 + _rows * cellSize + 46),
                Size = new Size(_cols * cellSize + 4, 72),
                BackColor = Color.FromArgb(180, 180, 180)
            };
            statPanel.Paint += (s, ev) =>
                ev.Graphics.DrawRectangle(Pens.Gray, 0, 0, statPanel.Width - 1, statPanel.Height - 1);

            _lblStatInfo = new Label
            {
                Font = new Font("Consolas", 8f),
                ForeColor = Color.FromArgb(40, 40, 80),
                Location = new Point(6, 4),
                Size = new Size(statPanel.Width - 12, 64),
                BackColor = Color.Transparent
            };
            UpdateStatLabel();
            statPanel.Controls.Add(_lblStatInfo);

            Size = new Size(_cols * cellSize + 30, _rows * cellSize + 200);
            Controls.AddRange(new Control[] { topPanel, _gridPanel, btnTw, btnAch, statPanel });

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) =>
            {
                _seconds = Math.Min(_seconds + 1, 999);
                _lblTime.Text = _seconds.ToString("D3");
            };

            _toast = new ToastNotification(this);
            _gameStart = DateTime.Now;
            GameStats.MinesweeperTotalGames++;
        }

        // ── Построить сетку кнопок ────────────────────────────────────────
        private void BuildGrid(int cs)
        {
            _gridPanel.Controls.Clear();
            _buttons = new Button[_rows, _cols];

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                {
                    var btn = new Button
                    {
                        Location = new Point(2 + c * cs, 2 + r * cs),
                        Size = new Size(cs, cs),
                        FlatStyle = FlatStyle.System,
                        Font = new Font("Consolas", 8f, FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    int br = r, bc = c;
                    btn.MouseDown += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Left) LeftClick(br, bc);
                        else if (e.Button == MouseButtons.Right) RightClick(br, bc);
                    };
                    _buttons[r, c] = btn;
                    _gridPanel.Controls.Add(btn);
                }
        }

        // ── Расстановка мин (после первого клика) ─────────────────────────
        private void PlaceMines(int safeR, int safeC)
        {
            var rng = new Random();
            int placed = 0;
            while (placed < _mines)
            {
                int r = rng.Next(_rows), c = rng.Next(_cols);
                if (Math.Abs(r - safeR) <= 1 && Math.Abs(c - safeC) <= 1) continue;
                if (_board[r, c] == -1) continue;
                _board[r, c] = -1;
                placed++;
            }

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                {
                    if (_board[r, c] == -1) continue;
                    int count = 0;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int nr = r + dr, nc = c + dc;
                            if (nr >= 0 && nr < _rows && nc >= 0 && nc < _cols && _board[nr, nc] == -1)
                                count++;
                        }
                    _board[r, c] = count;
                }
        }

        // ── Левый клик ────────────────────────────────────────────────────
        private void LeftClick(int r, int c)
        {
            if (_gameOver) return;
            if (_state[r, c] == CellState.Flagged) return;
            if (_state[r, c] == CellState.Revealed) return;

            if (_firstClick)
            {
                _firstClick = false;
                PlaceMines(r, c);
                _timer.Start();
            }

            _btnFace.Text = ":O";

            if (_board[r, c] == -1)
            {
                ShowMines(r, c);
                return;
            }

            Reveal(r, c);
            CheckWin();
            _btnFace.Text = ":)";
        }

        // ── Правый клик ───────────────────────────────────────────────────
        private void RightClick(int r, int c)
        {
            if (_gameOver) return;
            if (_state[r, c] == CellState.Revealed) return;

            if (_state[r, c] == CellState.Hidden)
            {
                _state[r, c] = CellState.Flagged;
                _flagsPlaced++;
                _noFlags = false;
                _buttons[r, c].Text = "[F]";
                _buttons[r, c].ForeColor = Color.Red;
            }
            else
            {
                _state[r, c] = CellState.Hidden;
                _flagsPlaced--;
                _buttons[r, c].Text = "";
            }

            _lblMines.Text = (_mines - _flagsPlaced).ToString("D3");
        }

        // ── Открыть клетку (flood-fill) ───────────────────────────────────
        private void Reveal(int r, int c)
        {
            if (r < 0 || r >= _rows || c < 0 || c >= _cols) return;
            if (_state[r, c] != CellState.Hidden) return;

            _state[r, c] = CellState.Revealed;
            var btn = _buttons[r, c];
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.LightGray;
            btn.Enabled = false;

            if (_board[r, c] > 0)
            {
                btn.Text = _board[r, c].ToString();
                btn.ForeColor = NumColors[_board[r, c]];
            }
            else
            {
                btn.Text = "";
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                        Reveal(r + dr, c + dc);
            }
        }

        // ── Показать все мины (проигрыш) ──────────────────────────────────
        private void ShowMines(int hitR, int hitC)
        {
            _gameOver = true;
            _timer.Stop();

            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_board[r, c] == -1)
                    {
                        bool hit = (r == hitR && c == hitC);
                        _buttons[r, c].Text = hit ? "[!]" : "[*]";
                        _buttons[r, c].BackColor = hit ? Color.Red : Color.LightGray;
                    }

            _btnFace.Text = "X(";
            GameStats.MinesweeperCurrentStreak = 0;
            GameStats.MinesweeperTotalGames++;
            GameStats.Save();
            UpdateStatLabel();

            MessageBox.Show("Вы подорвались! Игра окончена.", "BOOM!",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        // ── Проверить победу ──────────────────────────────────────────────
        private void CheckWin()
        {
            int revealed = 0;
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_state[r, c] == CellState.Revealed) revealed++;

            if (revealed != _rows * _cols - _mines) return;

            _gameOver = true;
            _timer.Stop();
            _btnFace.Text = ":D";

            GameStats.MinesweeperWins++;
            GameStats.MinesweeperTotalGames++;
            GameStats.MinesweeperCurrentStreak++;
            if (GameStats.MinesweeperCurrentStreak > GameStats.MinesweeperWinStreak)
                GameStats.MinesweeperWinStreak = GameStats.MinesweeperCurrentStreak;

            if (_difficulty == Difficulty.Beginner && _seconds < GameStats.MinesweeperBestTimeBeginner)
                GameStats.MinesweeperBestTimeBeginner = _seconds;
            if (_difficulty == Difficulty.Medium && _seconds < GameStats.MinesweeperBestTimeMedium)
                GameStats.MinesweeperBestTimeMedium = _seconds;
            if (_difficulty == Difficulty.Expert && _seconds < GameStats.MinesweeperBestTimeExpert)
                GameStats.MinesweeperBestTimeExpert = _seconds;

            GameStats.TotalScore += _mines * 100;
            GameStats.Save();
            UpdateStatLabel();
            CheckAchievements();

            MessageBox.Show($"Победа! Время: {_seconds} сек.", "Вы победили!",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Обновить панель статистики ────────────────────────────────────
        private void UpdateStatLabel()
        {
            if (_lblStatInfo == null) return;
            string bBest = GameStats.MinesweeperBestTimeBeginner == int.MaxValue ? "--" : $"{GameStats.MinesweeperBestTimeBeginner}с";
            string mBest = GameStats.MinesweeperBestTimeMedium == int.MaxValue ? "--" : $"{GameStats.MinesweeperBestTimeMedium}с";
            string eBest = GameStats.MinesweeperBestTimeExpert == int.MaxValue ? "--" : $"{GameStats.MinesweeperBestTimeExpert}с";
            _lblStatInfo.Text =
                $"Побед: {GameStats.MinesweeperWins}  |  Игр: {GameStats.MinesweeperTotalGames}  |  Серия: {GameStats.MinesweeperCurrentStreak} (макс {GameStats.MinesweeperWinStreak})\n" +
                $"Рекорд Новичок: {bBest}  |  Средний: {mBest}  |  Эксперт: {eBest}";
        }

        // ── Достижения ────────────────────────────────────────────────────
        private void CheckAchievements()
        {
            AchievementManager.Unlock("ms_first_win");
            if (_noFlags) AchievementManager.Unlock("ms_no_flags");
            if (_difficulty == Difficulty.Medium && _seconds < 60) AchievementManager.Unlock("ms_lightning");
            if (_difficulty == Difficulty.Expert) AchievementManager.Unlock("ms_expert");
            AchievementManager.UpdateProgress("ms_streak5", GameStats.MinesweeperCurrentStreak);
        }

        // ── Рестарт ───────────────────────────────────────────────────────
        private void RestartGame()
        {
            _timer?.Stop();
            SetupGame();
        }

        // ── Тост достижения ───────────────────────────────────────────────
        private void OnAchievementUnlocked(Achievement ach)
        {
            if (InvokeRequired) { Invoke(new Action<Achievement>(OnAchievementUnlocked), ach); return; }
            _toast?.Show(ach.Name);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AchievementManager.OnAchievementUnlocked -= OnAchievementUnlocked;
            base.OnFormClosed(e);
        }
    }
}