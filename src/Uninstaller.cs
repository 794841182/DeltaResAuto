using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeltaResUninstall
{
    static class Info
    {
        public const string AppName = "自动检测分辨率";
        public const string ExeName = "自动检测分辨率.exe";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DeltaResAuto";
        public const string RunValue = "DeltaResAuto";
        public const string LogPath = @"%TEMP%\DeltaResAuto_uninstall.log";
    }

    static class Core
    {
        public static string CfgDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeltaResAuto");
        }

        // 只删安装清单里的文件，避免用户把程序装进已有目录时误删其他文件
        static List<string> InstalledFileNames(string dir)
        {
            List<string> names = new List<string>();
            string man = Path.Combine(dir, "manifest.sha256");
            try
            {
                if (File.Exists(man))
                {
                    string[] lines = File.ReadAllLines(man, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int star = lines[i].IndexOf('*');
                        if (star < 0) continue;
                        string n = lines[i].Substring(star + 1).Trim();
                        if (n.Length == 0) continue;
                        if (!string.Equals(Path.GetFileName(n), n, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!names.Contains(n)) names.Add(n);
                    }
                }
            }
            catch { }

            string[] known = new string[]
            {
                Info.ExeName, "uninstall.exe", "README.txt", "LICENSE.txt",
                "settings.example.ini", "manifest.sha256"
            };
            for (int i = 0; i < known.Length; i++)
                if (!names.Contains(known[i])) names.Add(known[i]);
            return names;
        }

        public static void Log(string text)
        {
            try
            {
                File.AppendAllText(Environment.ExpandEnvironmentVariables(Info.LogPath),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        public static void KillApp()
        {
            try
            {
                Process[] ps = Process.GetProcessesByName("自动检测分辨率");
                for (int i = 0; i < ps.Length; i++)
                {
                    try { ps[i].Kill(); ps[i].WaitForExit(3000); } catch { }
                }
            }
            catch { }
        }

        public static void DropShortcuts()
        {
            try
            {
                File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Info.AppName + ".lnk"));
            }
            catch { }
            try
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "DeltaResAuto");
                if (Directory.Exists(d)) Directory.Delete(d, true);
            }
            catch { }
        }

        public static void DropRegistry()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    if (k != null) k.DeleteValue(Info.RunValue, false);
            }
            catch { }
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(Info.RegKey, false);
            }
            catch { }
        }

        public static string RemoveFiles(string dir, bool delConfig, Action<string> step)
        {
            try
            {
                if (step != null) step("停止正在运行的程序…");
                KillApp();
                System.Threading.Thread.Sleep(300);

                if (!File.Exists(Path.Combine(dir, Info.ExeName)))
                    return "这个目录里没有 " + Info.ExeName + "，已停止卸载。";

                if (step != null) step("删除快捷方式与注册表项…");
                DropShortcuts();
                DropRegistry();

                if (step != null) step("删除程序文件…");
                string self = Application.ExecutablePath;
                List<string> names = InstalledFileNames(dir);
                for (int i = 0; i < names.Count; i++)
                {
                    string p = Path.Combine(dir, names[i]);
                    if (string.Equals(p, self, StringComparison.OrdinalIgnoreCase)) continue;
                    try { if (File.Exists(p)) File.Delete(p); } catch { }
                }
                string[] dirs = Directory.GetDirectories(dir);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(dirs[i]).Length == 0)
                            Directory.Delete(dirs[i]);
                    }
                    catch { }
                }

                if (delConfig)
                {
                    if (step != null) step("删除配置与备份…");
                    string cfg = CfgDir();
                    try { if (Directory.Exists(cfg)) Directory.Delete(cfg, true); } catch { }
                }

                if (step != null) step("清理安装目录…");
                // 只删掉空的安装目录；不用 /s，避免误删用户放在同一目录里的其他文件
                string cmd = "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + self + "\" & rmdir \"" + dir + "\"";
                Process.Start(new ProcessStartInfo("cmd.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                Log("卸载完成：" + dir);
                return null;
            }
            catch (Exception ex)
            {
                Log("卸载异常：" + ex);
                return "卸载失败：" + ex.Message;
            }
        }
    }

    static class Ui
    {
        public static float K = 1f;
        public static readonly Color Bg = Color.FromArgb(13, 14, 17);
        public static readonly Color Card = Color.FromArgb(24, 26, 31);
        public static readonly Color Ink = Color.FromArgb(238, 241, 246);
        public static readonly Color Dim = Color.FromArgb(148, 155, 166);
        public static readonly Color Accent = Color.FromArgb(96, 200, 232);
        public static readonly Color AccentDown = Color.FromArgb(66, 146, 190);
        public static readonly Color Bad = Color.FromArgb(226, 138, 128);
        public static readonly Color Field = Color.FromArgb(18, 20, 24);

        public static void Init()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) K = g.DpiX / 96f;
            if (K < 1f) K = 1f;
        }

        public static int Z(int v) { return (int)Math.Round(v * K); }
        public static Font F(float size, FontStyle st) { return new Font("Microsoft YaHei UI", size, st); }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    class Btn : Control
    {
        public string Caption = "";
        public int Kind = 0;
        public bool Hover, Down;
        public Btn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Kind == 0)
            {
                Color c1 = Down ? Ui.AccentDown : (Hover ? Color.FromArgb(114, 200, 238) : Ui.Accent);
                Color c2 = Down ? Color.FromArgb(44, 114, 154) : (Hover ? Color.FromArgb(70, 156, 198) : Color.FromArgb(58, 138, 178));
                using (LinearGradientBrush lb = new LinearGradientBrush(r, c1, c2, LinearGradientMode.Vertical))
                using (GraphicsPath p = Ui.Round(r, Ui.Z(12)))
                    g.FillPath(lb, p);
                TextRenderer.DrawText(g, Caption, Font, r, Color.FromArgb(10, 28, 38),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else
            {
                using (GraphicsPath p = Ui.Round(r, Ui.Z(10)))
                {
                    using (SolidBrush b = new SolidBrush(Hover ? Color.FromArgb(36, 41, 49) : Color.FromArgb(26, 29, 35)))
                        g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(90, 98, 112), 1f)) g.DrawPath(pen, p);
                }
                TextRenderer.DrawText(g, Caption, Font, r, Ui.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    class Opt : Control
    {
        public bool Checked;
        string label;
        public Opt(string text, bool chk)
        {
            label = text; Checked = chk;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseDown(MouseEventArgs e) { Checked = !Checked; Invalidate(); base.OnMouseDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int s = Ui.Z(18);
            Rectangle box = new Rectangle(0, (Height - s) / 2, s, s);
            using (GraphicsPath p = Ui.Round(box, Ui.Z(5)))
            {
                if (Checked)
                {
                    using (SolidBrush b = new SolidBrush(Ui.Accent)) g.FillPath(b, p);
                    PointF[] pts = new PointF[3];
                    pts[0] = new PointF(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f);
                    pts[1] = new PointF(box.X + box.Width * 0.44f, box.Y + box.Height * 0.72f);
                    pts[2] = new PointF(box.X + box.Width * 0.78f, box.Y + box.Height * 0.30f);
                    using (Pen pen = new Pen(Color.FromArgb(10, 28, 38), Ui.Z(2)))
                        g.DrawLines(pen, pts);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(32, 36, 43))) g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(80, 90, 104), 1f)) g.DrawPath(pen, p);
                }
            }
            Rectangle tr = new Rectangle(s + Ui.Z(10), 0, Width - s - Ui.Z(10), Height);
            TextRenderer.DrawText(g, label, Font, tr, Checked ? Ui.Ink : Color.FromArgb(198, 204, 214),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    class UninstallForm : Form
    {
        string dir;
        Opt optCfg;
        Btn ok, cancel;
        string status = "";
        bool running;

        public UninstallForm()
        {
            dir = Path.GetDirectoryName(Application.ExecutablePath);
            Text = "卸载 " + Info.AppName;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Ui.Z(460), Ui.Z(252));
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            try
            {
                Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Ui.Z(18), Ui.Z(18)));
            }
            catch { }

            optCfg = new Opt("同时删除配置与备份（settings.ini 与 backup）", false);
            optCfg.Font = Ui.F(9f, FontStyle.Regular);
            optCfg.SetBounds(Ui.Z(28), Ui.Z(140), Ui.Z(400), Ui.Z(22));
            Controls.Add(optCfg);

            ok = new Btn();
            ok.Kind = 0; ok.Caption = "卸载";
            ok.Font = Ui.F(10f, FontStyle.Bold);
            ok.SetBounds(Ui.Z(250), Ui.Z(190), Ui.Z(180), Ui.Z(38));
            ok.Click += delegate { StartUninstall(); };
            Controls.Add(ok);

            cancel = new Btn();
            cancel.Kind = 1; cancel.Caption = "取消";
            cancel.Font = Ui.F(9.5f, FontStyle.Regular);
            cancel.SetBounds(Ui.Z(28), Ui.Z(190), Ui.Z(120), Ui.Z(38));
            cancel.Click += delegate { if (!running) Close(); };
            Controls.Add(cancel);

            MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && e.Y < Ui.Z(60) && !running) DragMove(this);
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;
                return cp;
            }
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        static void DragMove(Form f)
        {
            try { ReleaseCapture(); SendMessage(f.Handle, 0xA1, 0x2, 0); } catch { }
        }

        void StartUninstall()
        {
            if (running) return;
            if (MessageBox.Show(this, "确定要卸载 " + Info.AppName + " 吗？", "卸载确认",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            running = true;
            ok.Enabled = false; cancel.Enabled = false;
            string err = Core.RemoveFiles(dir, optCfg.Checked, delegate(string s)
            {
                status = s;
                Invalidate();
                Application.DoEvents();
            });

            if (err != null)
            {
                status = err;
                running = false;
                ok.Enabled = true; cancel.Enabled = true; cancel.Caption = "关闭";
                Invalidate();
                return;
            }

            MessageBox.Show(this, "卸载完成，配置" + (optCfg.Checked ? "已一并删除。" : "已保留。"), Info.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (LinearGradientBrush bg = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height), Color.FromArgb(16, 17, 21), Ui.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(bg, 0, 0, Width, Height);
            using (Pen pen = new Pen(Color.FromArgb(40, 170, 186, 210), 1f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(Ui.Accent))
                g.FillRectangle(b, Ui.Z(28), Ui.Z(24), Ui.Z(13), Ui.Z(3));

            TextRenderer.DrawText(g, "卸载 " + Info.AppName, Ui.F(16f, FontStyle.Bold),
                new Rectangle(Ui.Z(28), Ui.Z(32), Ui.Z(380), Ui.Z(32)), Ui.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, "将删除程序文件、快捷方式和开机自启项。",
                Ui.F(9f, FontStyle.Regular), new Rectangle(Ui.Z(28), Ui.Z(72), Ui.Z(400), Ui.Z(22)), Ui.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "安装目录：" + dir, Ui.F(8.5f, FontStyle.Regular),
                new Rectangle(Ui.Z(28), Ui.Z(96), Ui.Z(404), Ui.Z(20)), Color.FromArgb(122, 129, 141),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis);
            TextRenderer.DrawText(g, "配置目录：" + Core.CfgDir(), Ui.F(8.5f, FontStyle.Regular),
                new Rectangle(Ui.Z(28), Ui.Z(116), Ui.Z(404), Ui.Z(20)), Color.FromArgb(122, 129, 141),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis);

            if (status.Length > 0)
                TextRenderer.DrawText(g, status, Ui.F(9f, FontStyle.Bold),
                    new Rectangle(Ui.Z(28), Ui.Z(164), Ui.Z(404), Ui.Z(20)), Ui.Accent,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Ui.Init();
            string[] a = Environment.GetCommandLineArgs();
            bool silent = false, delCfg = false;
            for (int i = 1; i < a.Length; i++)
            {
                if (string.Equals(a[i], "/S", StringComparison.OrdinalIgnoreCase)) silent = true;
                if (string.Equals(a[i], "/DELCONFIG", StringComparison.OrdinalIgnoreCase)) delCfg = true;
            }
            string dir = Path.GetDirectoryName(Application.ExecutablePath);

            if (silent)
            {
                Core.Log("静默卸载开始：" + dir);
                string err = Core.RemoveFiles(dir, delCfg, null);
                Core.Log(err == null ? "静默卸载完成" : "静默卸载失败：" + err);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UninstallForm());
        }
    }
}
