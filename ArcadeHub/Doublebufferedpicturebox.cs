using System.Windows.Forms;

namespace ArcadeHub
{
    /// <summary>
    /// PictureBox with double buffering enabled via SetStyle.
    /// </summary>
    public class DoubleBufferedPictureBox : PictureBox
    {
        public DoubleBufferedPictureBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}