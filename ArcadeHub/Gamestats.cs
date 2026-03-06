using System;
using System.IO;

namespace ArcadeHub
{
    public static class GameStats
    {
        private static readonly string SaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcadeHub");
        private static readonly string SaveFile = Path.Combine(SaveDir, "stats.dat");

        // Global
        public static long TotalPlayTimeSeconds { get; set; } = 0;
        public static long TotalScore { get; set; } = 0;

        // Tetris
        public static int TetrisHighScore { get; set; } = 0;
        public static int TetrisTotalLines { get; set; } = 0;
        public static int TetrisTotalGames { get; set; } = 0;
        public static int TetrisMaxLevel { get; set; } = 0;

        // Minesweeper
        public static int MinesweeperBestTimeBeginner { get; set; } = int.MaxValue;
        public static int MinesweeperBestTimeMedium { get; set; } = int.MaxValue;
        public static int MinesweeperBestTimeExpert { get; set; } = int.MaxValue;
        public static int MinesweeperTotalGames { get; set; } = 0;
        public static int MinesweeperWins { get; set; } = 0;
        public static int MinesweeperWinStreak { get; set; } = 0;
        public static int MinesweeperCurrentStreak { get; set; } = 0;

        // Racing
        public static int RacingBestDistance { get; set; } = 0;
        public static int RacingBestCoins { get; set; } = 0;
        public static int RacingTotalCoins { get; set; } = 0;
        public static int RacingTotalDistance { get; set; } = 0;

        // Snake
        public static int SnakeBestLength { get; set; } = 0;
        public static int SnakeBestScore { get; set; } = 0;
        public static int SnakeTotalApples { get; set; } = 0;
        public static int SnakeTotalGames { get; set; } = 0;

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(SaveDir))
                    Directory.CreateDirectory(SaveDir);

                using (var sw = new StreamWriter(SaveFile, false))
                {
                    sw.WriteLine($"TotalPlayTimeSeconds={TotalPlayTimeSeconds}");
                    sw.WriteLine($"TotalScore={TotalScore}");
                    sw.WriteLine($"TetrisHighScore={TetrisHighScore}");
                    sw.WriteLine($"TetrisTotalLines={TetrisTotalLines}");
                    sw.WriteLine($"TetrisTotalGames={TetrisTotalGames}");
                    sw.WriteLine($"TetrisMaxLevel={TetrisMaxLevel}");
                    sw.WriteLine($"MinesweeperBestTimeBeginner={MinesweeperBestTimeBeginner}");
                    sw.WriteLine($"MinesweeperBestTimeMedium={MinesweeperBestTimeMedium}");
                    sw.WriteLine($"MinesweeperBestTimeExpert={MinesweeperBestTimeExpert}");
                    sw.WriteLine($"MinesweeperTotalGames={MinesweeperTotalGames}");
                    sw.WriteLine($"MinesweeperWins={MinesweeperWins}");
                    sw.WriteLine($"MinesweeperWinStreak={MinesweeperWinStreak}");
                    sw.WriteLine($"MinesweeperCurrentStreak={MinesweeperCurrentStreak}");
                    sw.WriteLine($"RacingBestDistance={RacingBestDistance}");
                    sw.WriteLine($"RacingBestCoins={RacingBestCoins}");
                    sw.WriteLine($"RacingTotalCoins={RacingTotalCoins}");
                    sw.WriteLine($"RacingTotalDistance={RacingTotalDistance}");
                    sw.WriteLine($"SnakeBestLength={SnakeBestLength}");
                    sw.WriteLine($"SnakeBestScore={SnakeBestScore}");
                    sw.WriteLine($"SnakeTotalApples={SnakeTotalApples}");
                    sw.WriteLine($"SnakeTotalGames={SnakeTotalGames}");

                    // Save achievements
                    foreach (var ach in AchievementManager.Achievements)
                    {
                        sw.WriteLine($"ACH_{ach.Id}_Unlocked={ach.IsUnlocked}");
                        sw.WriteLine($"ACH_{ach.Id}_Progress={ach.Progress}");
                    }
                }
            }
            catch { }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(SaveFile)) return;
                using (var sr = new StreamReader(SaveFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();

                        switch (key)
                        {
                            case "TotalPlayTimeSeconds": TotalPlayTimeSeconds = long.Parse(val); break;
                            case "TotalScore": TotalScore = long.Parse(val); break;
                            case "TetrisHighScore": TetrisHighScore = int.Parse(val); break;
                            case "TetrisTotalLines": TetrisTotalLines = int.Parse(val); break;
                            case "TetrisTotalGames": TetrisTotalGames = int.Parse(val); break;
                            case "TetrisMaxLevel": TetrisMaxLevel = int.Parse(val); break;
                            case "MinesweeperBestTimeBeginner": MinesweeperBestTimeBeginner = int.Parse(val); break;
                            case "MinesweeperBestTimeMedium": MinesweeperBestTimeMedium = int.Parse(val); break;
                            case "MinesweeperBestTimeExpert": MinesweeperBestTimeExpert = int.Parse(val); break;
                            case "MinesweeperTotalGames": MinesweeperTotalGames = int.Parse(val); break;
                            case "MinesweeperWins": MinesweeperWins = int.Parse(val); break;
                            case "MinesweeperWinStreak": MinesweeperWinStreak = int.Parse(val); break;
                            case "MinesweeperCurrentStreak": MinesweeperCurrentStreak = int.Parse(val); break;
                            case "RacingBestDistance": RacingBestDistance = int.Parse(val); break;
                            case "RacingBestCoins": RacingBestCoins = int.Parse(val); break;
                            case "RacingTotalCoins": RacingTotalCoins = int.Parse(val); break;
                            case "RacingTotalDistance": RacingTotalDistance = int.Parse(val); break;
                            case "SnakeBestLength": SnakeBestLength = int.Parse(val); break;
                            case "SnakeBestScore": SnakeBestScore = int.Parse(val); break;
                            case "SnakeTotalApples": SnakeTotalApples = int.Parse(val); break;
                            case "SnakeTotalGames": SnakeTotalGames = int.Parse(val); break;
                            default:
                                if (key.StartsWith("ACH_") && key.EndsWith("_Unlocked"))
                                {
                                    var id = key.Replace("ACH_", "").Replace("_Unlocked", "");
                                    var ach = AchievementManager.GetById(id);
                                    if (ach != null) ach.IsUnlocked = bool.Parse(val);
                                }
                                else if (key.StartsWith("ACH_") && key.EndsWith("_Progress"))
                                {
                                    var id = key.Replace("ACH_", "").Replace("_Progress", "");
                                    var ach = AchievementManager.GetById(id);
                                    if (ach != null) ach.Progress = int.Parse(val);
                                }
                                break;
                        }
                    }
                }
            }
            catch { }
        }
    }
}