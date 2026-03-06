using System;
using System.Windows.Forms;

namespace ArcadeHub
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            GameStats.Load();
            Application.Run(new MainMenuForm());
            GameStats.Save();
        }
    }
}