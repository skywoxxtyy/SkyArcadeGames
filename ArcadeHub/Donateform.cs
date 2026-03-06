using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArcadeHub
{
    public class DonateForm : Form
    {
        public DonateForm()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Поддержать автора";
            Size = new Size(420, 480);
            BackColor = Color.FromArgb(20, 20, 30);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            // ── Аватар ────────────────────────────────────────────────────
            var avatar = new Panel { Location = new Point(155, 25), Size = new Size(90, 90), BackColor = Color.Transparent };
            avatar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Color.FromArgb(145, 70, 255)))
                    g.FillEllipse(b, 2, 2, 85, 85);
                using (var pen = new Pen(Color.FromArgb(180, 140, 255), 3))
                    g.DrawEllipse(pen, 2, 2, 85, 85);
                using (var f = new Font("Segoe UI", 36f, FontStyle.Bold))
                using (var tb = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("S", f, tb, new RectangleF(2, 2, 85, 85), sf);
                }
            };

            // ── Никнейм ───────────────────────────────────────────────────
            var lblName = new Label
            {
                Text = "skywoxxtyyy",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(380, 40),
                Location = new Point(10, 125)
            };

            // ── Разделитель ───────────────────────────────────────────────
            var divider = new Panel { Location = new Point(40, 172), Size = new Size(320, 2), BackColor = Color.FromArgb(60, 60, 80) };

            // ── Призыв к действию ─────────────────────────────────────────
            var lblCall = new Label
            {
                Text = "Нравится то, что я делаю?\nПоддержи развитие проекта донатом!",
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(200, 200, 220),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(360, 55),
                Location = new Point(20, 185)
            };

            // ── Описание ─────────────────────────────────────────────────
            var lblDesc = new Label
            {
                Text = "Каждый донат помогает создавать новые игры,\n" +
                       "улучшать существующие и продолжать стримы.\n" +
                       "Ваша поддержка значит очень много!",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(140, 140, 160),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(360, 65),
                Location = new Point(20, 248)
            };

            // ── Большая кнопка доната ─────────────────────────────────────
            var btnDonate = new Button
            {
                Text = "  Поддержать на DonationAlerts",
                Location = new Point(45, 330),
                Size = new Size(310, 52),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDonate.FlatAppearance.BorderSize = 0;
            btnDonate.Paint += (s, e) =>
            {
                // Скруглённые углы
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(new Rectangle(0, 0, btnDonate.Width, btnDonate.Height), 10))
                using (var b = new SolidBrush(Color.FromArgb(249, 115, 22)))
                    g.FillPath(b, path);
                using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (var tb = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Поддержать на DonationAlerts", f, tb,
                        new RectangleF(0, 0, btnDonate.Width, btnDonate.Height), sf);
                }
            };
            btnDonate.Click += (s, e) =>
            {
                try { Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://www.donationalerts.com/r/skywoxxtyyy", UseShellExecute = true }); }
                catch { }
            };

            // ── Twitch кнопка ─────────────────────────────────────────────
            var btnTw = new TwitchButton { Location = new Point(120, 395), Size = new Size(170, 40) };

            // ── Мелкий текст ──────────────────────────────────────────────
            var lblFooter = new Label
            {
                Text = "Платёж обрабатывается через DonationAlerts",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(80, 80, 100),
                AutoSize = false,
                Size = new Size(380, 18),
                Location = new Point(10, 445),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Controls.AddRange(new Control[] { avatar, lblName, divider, lblCall, lblDesc, btnDonate, btnTw, lblFooter });
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