using System;
using System.Collections.Generic;

namespace ArcadeHub
{
    public class Achievement
    {
        public string Id          { get; set; }
        public string Name        { get; set; }
        public string Description { get; set; }
        public string Icon        { get; set; }   // Короткий ASCII/текст — не emoji
        public string Game        { get; set; }
        public bool   IsUnlocked  { get; set; }
        public int    Progress    { get; set; }
        public int    MaxProgress { get; set; }
    }

    public static class AchievementManager
    {
        public static List<Achievement> Achievements { get; private set; } = new List<Achievement>();
        public static event Action<Achievement> OnAchievementUnlocked;

        static AchievementManager()
        {
            // ── ТЕТРИС ─────────────────────────────────────────────────────
            Add("tetris_first_line", "Первая линия",     "Убрать первую линию",                 "L1",    "Тетрис", 1);
            Add("tetris_tetris",     "Тетрис!",          "Убрать 4 линии за раз",               "T4",    "Тетрис", 1);
            Add("tetris_novice",     "Новичок",          "Набрать 1 000 очков",                 "1K",    "Тетрис", 1000);
            Add("tetris_pro",        "Профи",            "Набрать 10 000 очков",                "10K",   "Тетрис", 10000);
            Add("tetris_elite",      "Элита",            "Набрать 50 000 очков",                "50K",   "Тетрис", 50000);
            Add("tetris_speedrun",   "Спидраннер",       "Добраться до 10 уровня",              "LV10",  "Тетрис", 10);
            Add("tetris_level15",    "Ветеран скорости", "Добраться до 15 уровня",              "LV15",  "Тетрис", 15);
            Add("tetris_master",     "Мастер",           "Убрать 100 линий суммарно",           "100L",  "Тетрис", 100);
            Add("tetris_lines200",   "Легенда",          "Убрать 200 линий суммарно",           "200L",  "Тетрис", 200);
            Add("tetris_games5",     "Постоянный гость", "Сыграть 5 партий в Тетрис",           "5G",    "Тетрис", 5);

            // ── САПЁР ──────────────────────────────────────────────────────
            Add("ms_first_win",   "Первая победа",    "Выиграть первую игру",                   "WIN",   "Сапёр", 1);
            Add("ms_no_flags",    "Без флагов",       "Победить не поставив ни одного флага",   "0F",    "Сапёр", 1);
            Add("ms_lightning",   "Молния",           "Победить на среднем за < 60 секунд",     "<60",   "Сапёр", 1);
            Add("ms_expert",      "Эксперт",          "Победить на сложном уровне",             "EXP",   "Сапёр", 1);
            Add("ms_streak5",     "Серия 5",          "Выиграть 5 игр подряд",                  "5W",    "Сапёр", 5);
            Add("ms_streak10",    "Серия 10",         "Выиграть 10 игр подряд",                 "10W",   "Сапёр", 10);
            Add("ms_beginner30",  "Скоростной сапёр", "Победить на новичке за < 30 секунд",     "<30",   "Сапёр", 1);
            Add("ms_wins10",      "Опытный сапёр",    "Одержать 10 побед суммарно",             "10V",   "Сапёр", 10);
            Add("ms_wins50",      "Профессионал",     "Одержать 50 побед суммарно",             "50V",   "Сапёр", 50);

            // ── ГОНКИ ──────────────────────────────────────────────────────
            Add("race_100m",    "Первые 100м",   "Проехать 100 метров",                  "100",  "Гонки", 100);
            Add("race_500m",    "Неуязвимый",    "Проехать 500 метров",                  "500",  "Гонки", 500);
            Add("race_1000m",   "Гонщик",        "Проехать 1000 метров",                 "1KM",  "Гонки", 1000);
            Add("race_2000m",   "Профи гонщик",  "Проехать 2000 метров",                 "2KM",  "Гонки", 2000);
            Add("race_coins10", "Сборщик",       "Собрать 10 монет за игру",             "10C",  "Гонки", 10);
            Add("race_coins50", "Монетоман",     "Собрать 50 монет за игру",             "50C",  "Гонки", 50);
            Add("race_coins500","Богач",          "Собрать 500 монет суммарно",           "500C", "Гонки", 500);
            Add("race_coins1k", "Миллионер",     "Собрать 1000 монет суммарно",          "1KC",  "Гонки", 1000);
            Add("race_nodmg",   "Чистый заезд",  "Проехать 300м не столкнувшись",        "OK",   "Гонки", 300);

            // ── ЗМЕЙКА ─────────────────────────────────────────────────────
            Add("snake_first_apple", "Первое яблоко",  "Съесть первое яблоко",              "A1",   "Змейка", 1);
            Add("snake_length10",    "Длинная",        "Достичь длины 10",                  "L10",  "Змейка", 10);
            Add("snake_length25",    "Гигант",         "Достичь длины 25",                  "L25",  "Змейка", 25);
            Add("snake_length40",    "Монстр",         "Достичь длины 40",                  "L40",  "Змейка", 40);
            Add("snake_apples100",   "Обжора",         "Съесть 100 яблок суммарно",         "100A", "Змейка", 100);
            Add("snake_apples500",   "Ненасытный",     "Съесть 500 яблок суммарно",         "500A", "Змейка", 500);
            Add("snake_fast200",     "Быстрый старт",  "200 очков за < 30 секунд",          "SPD",  "Змейка", 1);
            Add("snake_score500",    "Очковик",        "Набрать 500 очков за игру",         "500P", "Змейка", 500);
            Add("snake_wallmode",    "Призрак",        "Сыграть в режиме без стен",         "GHO",  "Змейка", 1);
            Add("snake_games10",     "Завсегдатай",    "Сыграть 10 игр в Змейку",           "10G",  "Змейка", 10);
        }

        private static void Add(string id, string name, string desc, string icon, string game, int max)
        {
            Achievements.Add(new Achievement
            {
                Id          = id,
                Name        = name,
                Description = desc,
                Icon        = icon,
                Game        = game,
                MaxProgress = max
            });
        }

        public static Achievement GetById(string id) => Achievements.Find(a => a.Id == id);

        public static void Unlock(string id)
        {
            var ach = GetById(id);
            if (ach == null || ach.IsUnlocked) return;
            ach.IsUnlocked = true;
            ach.Progress   = ach.MaxProgress;
            OnAchievementUnlocked?.Invoke(ach);
            GameStats.Save();
        }

        public static void UpdateProgress(string id, int progress)
        {
            var ach = GetById(id);
            if (ach == null || ach.IsUnlocked) return;
            ach.Progress = Math.Min(progress, ach.MaxProgress);
            if (ach.Progress >= ach.MaxProgress) Unlock(id);
        }

        public static int GetUnlockedCount() => Achievements.FindAll(a => a.IsUnlocked).Count;
    }
}
