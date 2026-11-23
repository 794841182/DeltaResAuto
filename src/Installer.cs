using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeltaResSetup
{
    static class Info
    {
        public const string AppName = "ResPilot";
        public const string Version = "1.4.0";
        public const string Publisher = "冷渡-Van1sh";
        public const string ExeName = "ResPilot.exe";
        public const string UninstallName = "uninstall.exe";
        public const string RegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ResPilot";
        public const string RunValue = "ResPilot";
        public const string LogPath = @"%TEMP%\ResPilot_setup.log";
    }

    static class Res
    {
        static Assembly Asm { get { return Assembly.GetExecutingAssembly(); } }

        public static byte[] Bytes(string name)
        {
            using (Stream s = Asm.GetManifestResourceStream(name))
            {
                if (s == null) throw new Exception("安装包缺少资源：" + name);
                byte[] buf = new byte[s.Length];
                int off = 0;
                while (off < buf.Length)
                {
                    int n = s.Read(buf, off, buf.Length - off);
                    if (n <= 0) break;
                    off += n;
                }
                return buf;
            }
        }

        public static string Text(string name)
        {
            return Encoding.UTF8.GetString(Bytes(name));
        }
    }

    static class Payload
    {
        // 资源名 -> 安装后的文件名
        public static readonly List<string[]> Files = new List<string[]>();
        public static readonly Dictionary<string, string> Hash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static Payload()
        {
            Files.Add(new string[] { "app.exe", Info.ExeName });
            Files.Add(new string[] { "uninstall.exe", Info.UninstallName });
            Files.Add(new string[] { "README.txt", "README.txt" });
            Files.Add(new string[] { "LICENSE.txt", "LICENSE.txt" });
            Files.Add(new string[] { "settings.example.ini", "settings.example.ini" });

            string txt = Res.Text("manifest.txt");
            string[] lines = txt.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = lines[i].Split('|');
                if (p.Length >= 2) Hash[p[0].Trim()] = p[1].Trim().ToLowerInvariant();
            }
        }

        public static string TargetOf(string resName)
        {
            for (int i = 0; i < Files.Count; i++)
                if (string.Equals(Files[i][0], resName, StringComparison.OrdinalIgnoreCase)) return Files[i][1];
            return resName;
        }
    }

    static class Sha
    {
        public static string Of(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string OfFile(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }

    static class Core
    {
        public static string DefaultDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\ResPilot");
        }

        public static string CfgDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResPilot");
        }

        public static void Log(string text)
        {
            try
            {
                string p = Environment.ExpandEnvironmentVariables(Info.LogPath);
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        public static void KillApp()
        {
            try
            {
                string[] names = new string[] { "ResPilot", "自动检测分辨率" };
                for (int n = 0; n < names.Length; n++)
                {
                    Process[] ps = Process.GetProcessesByName(names[n]);
                    for (int i = 0; i < ps.Length; i++)
                    {
                        try { ps[i].Kill(); ps[i].WaitForExit(3000); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void BackupConfig()
        {
            try
            {
                string ini = Path.Combine(CfgDir(), "settings.ini");
                if (!File.Exists(ini)) return;
                Directory.CreateDirectory(Path.Combine(CfgDir(), "backup"));
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(ini, Path.Combine(CfgDir(), "backup", "install-" + stamp + ".ini"), true);
                File.Copy(ini, ini + ".bak", true);
            }
            catch { }
        }

        public static string VerifyPayload()
        {
            for (int i = 0; i < Payload.Files.Count; i++)
            {
                string name = Payload.Files[i][0];
                byte[] data = Res.Bytes(name);
                string want = Payload.Hash.ContainsKey(name) ? Payload.Hash[name] : "";
                if (want.Length == 0) return "清单缺少 " + name + " 的校验值";
                string got = Sha.Of(data);
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                    return "安装包文件校验失败：" + name + "\r\n期望 " + want + "\r\n实际 " + got;
            }
            return null;
        }

        public static string WriteFiles(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                for (int i = 0; i < Payload.Files.Count; i++)
                {
                    string target = Path.Combine(dir, Payload.Files[i][1]);
                    File.WriteAllBytes(target, Res.Bytes(Payload.Files[i][0]));
                }
                return null;
            }
            catch (Exception ex)
            {
                return "写入文件失败：" + ex.Message;
            }
        }

        public static string WriteManifest(string dir)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Payload.Files.Count; i++)
                {
                    string target = Path.Combine(dir, Payload.Files[i][1]);
                    sb.Append(Sha.OfFile(target)).Append(" *").Append(Payload.Files[i][1]).Append("\r\n");
                }
                File.WriteAllText(Path.Combine(dir, "manifest.sha256"), sb.ToString(), Encoding.UTF8);
                return null;
            }
            catch (Exception ex) { return "写入校验清单失败：" + ex.Message; }
        }

        public static string VerifyInstalled(string dir)
        {
            for (int i = 0; i < Payload.Files.Count; i++)
            {
                string name = Payload.Files[i][0];
                string target = Path.Combine(dir, Payload.Files[i][1]);
                if (!File.Exists(target)) return "缺少文件：" + Payload.Files[i][1];
                string want = Payload.Hash[name];
                string got = Sha.OfFile(target);
                if (!string.Equals(want, got, StringComparison.OrdinalIgnoreCase))
                    return "文件校验不一致：" + Payload.Files[i][1];
            }
            return null;
        }

        public static string DeskLink()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Info.AppName + ".lnk");
        }

        public static string StartLink()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ResPilot", Info.AppName + ".lnk");
        }

        public static void MakeLink(string lnk, string target, string desc)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lnk));
                object wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                object sc = wsh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, wsh, new object[] { lnk });
                Type t = sc.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch (Exception ex) { Log("创建快捷方式失败：" + ex.Message); }
        }

        public static void Shortcuts(string dir, bool on)
        {
            string exe = Path.Combine(dir, Info.ExeName);
            if (on)
            {
                MakeLink(DeskLink(), exe, "桌面分辨率自动切换");
                MakeLink(StartLink(), exe, "桌面分辨率自动切换");
            }
            else
            {
                try { File.Delete(DeskLink()); } catch { }
                try
                {
                    string d = Path.GetDirectoryName(StartLink());
                    if (Directory.Exists(d)) Directory.Delete(d, true);
                }
                catch { }
            }
        }

        public static void Autostart(string dir, bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(Info.RunValue, "\"" + Path.Combine(dir, Info.ExeName) + "\" --bg");
                    else k.DeleteValue(Info.RunValue, false);
                    k.DeleteValue("Delta" + "ResAuto", false);
                }
            }
            catch { }
        }

        public static void RegistryAdd(string dir)
        {
            try
            {
                long size = 0;
                for (int i = 0; i < Payload.Files.Count; i++)
                {
                    string f = Path.Combine(dir, Payload.Files[i][1]);
                    if (File.Exists(f)) size += new FileInfo(f).Length;
                }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Delta" + "ResAuto", false); } catch { }
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Info.RegKey))
                {
                    k.SetValue("DisplayName", Info.AppName);
                    k.SetValue("DisplayVersion", Info.Version);
                    k.SetValue("Publisher", Info.Publisher);
                    k.SetValue("InstallLocation", dir);
                    k.SetValue("DisplayIcon", Path.Combine(dir, Info.ExeName));
                    k.SetValue("UninstallString", "\"" + Path.Combine(dir, Info.UninstallName) + "\"");
                    k.SetValue("QuietUninstallString", "\"" + Path.Combine(dir, Info.UninstallName) + "\" /S");
                    k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { Log("写入卸载信息失败：" + ex.Message); }
        }

        public static string Install(string dir, bool shortcutsOn, bool autostartOn,
            Action<int, string> step)
        {
            try
            {
                if (step != null) step(4, "停止正在运行的程序…");
                KillApp();
                System.Threading.Thread.Sleep(300);

                if (step != null) step(12, "备份现有配置…");
                BackupConfig();

                if (step != null) step(22, "校验安装包文件完整性…");
                string err = VerifyPayload();
                if (err != null) return err;

                if (step != null) step(42, "释放程序文件…");
                err = WriteFiles(dir);
                if (err != null) return err;

                if (step != null) step(60, "写入校验清单…");
                err = WriteManifest(dir);
                if (err != null) return err;

                if (step != null) step(70, "创建快捷方式…");
                Shortcuts(dir, shortcutsOn);

                if (step != null) step(78, "写入开机自启与卸载信息…");
                Autostart(dir, autostartOn);
                RegistryAdd(dir);

                if (step != null) step(92, "复核已安装文件…");
                err = VerifyInstalled(dir);
                if (err != null) return err;

                if (step != null) step(100, "安装完成");
                Log("安装成功：" + dir);
                return null;
            }
            catch (Exception ex)
            {
                Log("安装异常：" + ex);
                return "安装失败：" + ex.Message;
            }
        }
    }

    static class Ui
    {
        public static float K = 1f;
        public static readonly Color Bg = Color.FromArgb(13, 14, 17);
        public static readonly Color Card = Color.FromArgb(24, 26, 31);
        public static readonly Color Stroke = Color.FromArgb(48, 54, 64);
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

        public static Font F(float size, FontStyle st)
        {
            return new Font("Microsoft YaHei UI", size, st);
        }

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
        public int Kind = 0; // 0 主按钮 1 幽灵按钮 2 迷你
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
            else if (Kind == 1)
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
            else
            {
                if (Hover)
                {
                    using (GraphicsPath p = Ui.Round(new Rectangle(0, 0, Width, Height), Ui.Z(8)))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 200, 214, 232)))
                        g.FillPath(b, p);
                }
                TextRenderer.DrawText(g, Caption, Font, r, Ui.Dim,
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
            BackColor = Ui.Card;
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

    class Bar : Control
    {
        public double Value;
        public Bar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int h = Height;
            Rectangle r = new Rectangle(0, 0, Width, h);
            using (GraphicsPath p = Ui.Round(r, h / 2))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 38, 45)))
                g.FillPath(b, p);
            int w = (int)Math.Round(Width * Value / 100.0);
            if (w > h)
            {
                Rectangle rr = new Rectangle(0, 0, w, h);
                using (GraphicsPath p = Ui.Round(rr, h / 2))
                using (LinearGradientBrush lb = new LinearGradientBrush(rr, Color.FromArgb(96, 200, 232), Color.FromArgb(64, 150, 190), LinearGradientMode.Horizontal))
                    g.FillPath(lb, p);
            }
        }
    }

    class PathPanel : Control
    {
        public string Value = "";
        public bool Hover;
        public event EventHandler Browse;

        public PathPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Ui.Card;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Browse != null) Browse(this, EventArgs.Empty);
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Ui.Round(r, Ui.Z(10)))
            {
                using (SolidBrush b = new SolidBrush(Ui.Field)) g.FillPath(b, p);
                using (Pen pen = new Pen(Hover ? Color.FromArgb(150, 96, 200, 232) : Color.FromArgb(56, 64, 76), 1f))
                    g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Value, Font, new Rectangle(Ui.Z(12), 0, Width - Ui.Z(38), Height), Ui.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 148, 155, 166)))
                g.FillRectangle(b, Width - Ui.Z(24), Height / 2 - Ui.Z(1), Ui.Z(10), Ui.Z(2));
        }
    }

    class SetupForm : Form
    {
        PathPanel pathPanel;
        Btn browse, close, main;
        Opt optDesk, optAuto, optRun;
        Bar bar;
        string phase = "ready";   // ready / running / done / error
        string status = "";
        string detail = "";
        string doneDir = "";
        bool doneSc, doneAu;

        public SetupForm()
        {
            Text = Info.AppName + " 安装程序";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Ui.Z(540), Ui.Z(470));
            DoubleBuffered = true;
            BackColor = Ui.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            try { Icon = new Icon(new MemoryStream(Res.Bytes("app.ico"))); } catch { }
            try
            {
                Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Ui.Z(18), Ui.Z(18)));
            }
            catch { }

            pathPanel = new PathPanel();
            pathPanel.Font = Ui.F(9.5f, FontStyle.Regular);
            pathPanel.SetBounds(Ui.Z(44), Ui.Z(134), Ui.Z(380), Ui.Z(38));
            pathPanel.Value = Args.Dir(Core.DefaultDir());
            pathPanel.Browse += delegate { PickDir(); };
            Controls.Add(pathPanel);

            browse = new Btn();
            browse.Kind = 1; browse.Caption = "浏览…";
            browse.BackColor = Ui.Card;
            browse.Font = Ui.F(9f, FontStyle.Regular);
            browse.SetBounds(Ui.Z(432), Ui.Z(134), Ui.Z(64), Ui.Z(38));
            browse.Click += delegate { PickDir(); };
            Controls.Add(browse);

            optDesk = new Opt("创建桌面快捷方式", !Args.Has("/NOSHORTCUT"));
            optDesk.Font = Ui.F(9f, FontStyle.Regular);
            optDesk.SetBounds(Ui.Z(44), Ui.Z(184), Ui.Z(260), Ui.Z(22));
            Controls.Add(optDesk);

            optAuto = new Opt("开机自动启动（后台监听）", !Args.Has("/NOAUTOSTART"));
            optAuto.Font = Ui.F(9f, FontStyle.Regular);
            optAuto.SetBounds(Ui.Z(44), Ui.Z(210), Ui.Z(320), Ui.Z(22));
            Controls.Add(optAuto);

            optRun = new Opt("安装完成后立即运行", !Args.Has("/NORUN"));
            optRun.Font = Ui.F(9f, FontStyle.Regular);
            optRun.SetBounds(Ui.Z(44), Ui.Z(236), Ui.Z(320), Ui.Z(22));
            Controls.Add(optRun);

            bar = new Bar();
            bar.BackColor = Ui.Bg;
            bar.SetBounds(Ui.Z(28), Ui.Z(350), Ui.Z(484), Ui.Z(5));
            bar.Visible = false;
            Controls.Add(bar);

            main = new Btn();
            main.Kind = 0; main.Caption = "开始安装";
            main.Font = Ui.F(10.5f, FontStyle.Bold);
            main.SetBounds(Ui.Z(28), Ui.Z(400), Ui.Z(484), Ui.Z(42));
            main.Click += delegate { OnMain(); };
            Controls.Add(main);

            close = new Btn();
            close.Kind = 2; close.Caption = "×";
            close.BackColor = Ui.Bg;
            close.Font = Ui.F(12f, FontStyle.Regular);
            close.SetBounds(Ui.Z(496), Ui.Z(18), Ui.Z(24), Ui.Z(24));
            close.Click += delegate
            {
                if (phase == "running") return;
                Close();
            };
            Controls.Add(close);

            MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && e.Y < Ui.Z(72)) DragMove(this);
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && phase != "running") { OnMain(); return true; }
            if (keyData == Keys.Escape && phase != "running") { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
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
            try
            {
                ReleaseCapture();
                SendMessage(f.Handle, 0xA1, 0x2, 0);
            }
            catch { }
        }

        void OnMain()
        {
            if (phase == "done") { Close(); return; }
            if (phase == "error") { phase = "ready"; status = ""; detail = ""; ShowInputs(true); SetRunningLook(false); Invalidate(); return; }

            string dir = pathPanel.Value.Trim();
            if (dir.Length == 0) { status = "请填写安装位置"; Invalidate(); return; }
            if (!Path.IsPathRooted(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), dir);
            dir = Path.GetFullPath(dir);
            pathPanel.Value = dir;

            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write_test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
            }
            catch
            {
                status = "无法写入该目录，请换一个位置（建议使用默认的用户目录）";
                Invalidate();
                return;
            }

            phase = "running";
            SetRunningLook(true);
            bar.Visible = true; bar.Value = 0;
            detail = ""; status = "准备安装…";
            Invalidate();
            Application.DoEvents();

            bool sc = optDesk.Checked;
            bool au = optAuto.Checked;
            bool run = optRun.Checked;
            string err = Core.Install(dir, sc, au, delegate(int pct, string text)
            {
                bar.Value = pct;
                status = text;
                Invalidate();
                Application.DoEvents();
            });

            if (err != null)
            {
                phase = "error";
                status = "安装未完成";
                detail = err;
                ShowInputs(false);
                SetRunningLook(false);
                bar.Visible = false;
                Invalidate();
                return;
            }

            phase = "done";
            doneDir = dir;
            doneSc = sc; doneAu = au;
            status = "";
            ShowInputs(false);
            bar.Visible = false;
            SetRunningLook(false);
            main.Caption = "完成";
            Invalidate();
            Application.DoEvents();

            if (run)
            {
                try { Process.Start(Path.Combine(dir, Info.ExeName)); } catch { }
            }
        }

        void PickDir()
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.Description = "选择安装位置";
                if (Directory.Exists(pathPanel.Value)) fb.SelectedPath = pathPanel.Value;
                if (fb.ShowDialog(this) == DialogResult.OK) pathPanel.Value = fb.SelectedPath;
            }
        }

        void ShowInputs(bool on)
        {
            pathPanel.Visible = on;
            browse.Visible = on;
            optDesk.Visible = on;
            optAuto.Visible = on;
            optRun.Visible = on;
        }

        void SetRunningLook(bool running)
        {
            pathPanel.Enabled = !running;
            browse.Enabled = !running;
            optDesk.Enabled = !running;
            optAuto.Enabled = !running;
            optRun.Enabled = !running;
            main.Caption = running ? "正在安装…" : (phase == "error" ? "返回" : "开始安装");
            main.Enabled = !running;
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

            // 顶部青色短线
            using (SolidBrush b = new SolidBrush(Ui.Accent))
                g.FillRectangle(b, Ui.Z(28), Ui.Z(26), Ui.Z(13), Ui.Z(3));

            TextRenderer.DrawText(g, phase == "done" ? "安装完成" : (phase == "error" ? "安装未完成" : Info.AppName),
                Ui.F(17f, FontStyle.Bold), new Rectangle(Ui.Z(28), Ui.Z(34), Ui.Z(380), Ui.Z(36)), Ui.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, "v" + Info.Version + " · 安装程序",
                Ui.F(8.5f, FontStyle.Regular), new Rectangle(Ui.Z(350), Ui.Z(42), Ui.Z(140), Ui.Z(20)), Ui.Dim,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (phase != "done" && phase != "error")
            {
                TextRenderer.DrawText(g, "桌面分辨率自动切换 · 安装前校验文件完整性，安装中自动备份配置",
                    Ui.F(9f, FontStyle.Regular), new Rectangle(Ui.Z(28), Ui.Z(70), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                Rectangle card = new Rectangle(Ui.Z(24), Ui.Z(100), Ui.Z(492), Ui.Z(206));
                using (GraphicsPath p = Ui.Round(card, Ui.Z(14)))
                {
                    using (SolidBrush b = new SolidBrush(Ui.Card)) g.FillPath(b, p);
                    using (Pen pen2 = new Pen(Color.FromArgb(30, 200, 214, 232), 1f)) g.DrawPath(pen2, p);
                }

                TextRenderer.DrawText(g, "安装位置", Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(44), Ui.Z(110), Ui.Z(200), Ui.Z(20)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else if (phase == "done")
            {
                // 大对勾
                int s = Ui.Z(42);
                Rectangle circle = new Rectangle(Ui.Z(28), Ui.Z(104), s, s);
                using (GraphicsPath p = Ui.Round(circle, s / 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 96, 200, 232)))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Ui.Accent, Ui.Z(2)))
                    g.DrawPath(pen, Ui.Round(circle, s / 2));
                PointF[] pts = new PointF[3];
                pts[0] = new PointF(circle.X + s * 0.24f, circle.Y + s * 0.52f);
                pts[1] = new PointF(circle.X + s * 0.43f, circle.Y + s * 0.72f);
                pts[2] = new PointF(circle.X + s * 0.77f, circle.Y + s * 0.30f);
                using (Pen pen = new Pen(Ui.Accent, 2.4f * Ui.K))
                    g.DrawLines(pen, pts);

                TextRenderer.DrawText(g, "已安装到", Ui.F(9f, FontStyle.Regular),
                    new Rectangle(Ui.Z(86), Ui.Z(104), Ui.Z(120), Ui.Z(20)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, doneDir, Ui.F(9.5f, FontStyle.Bold),
                    new Rectangle(Ui.Z(86), Ui.Z(124), Ui.Z(420), Ui.Z(24)), Ui.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis);

                TextRenderer.DrawText(g, "5 个文件已写入并通过 SHA256 复核", Ui.F(9f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(170), Ui.Z(480), Ui.Z(22)), Color.FromArgb(150, 206, 218, 228),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, "配置目录：" + Core.CfgDir(), Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(198), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.PathEllipsis);
                TextRenderer.DrawText(g, "旧配置已备份到配置目录下的 backup 文件夹", Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(220), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, "快捷方式　" + (doneSc ? "已创建（桌面 / 开始菜单）" : "未创建"), Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(252), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, "开机自启　" + (doneAu ? "已开启（登录后后台监听）" : "未开启"), Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(278), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, "卸载　设置 → 应用，或运行安装目录下的 uninstall.exe", Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(304), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else
            {
                TextRenderer.DrawText(g, "没有写入任何文件。安装包或目标目录有问题。", Ui.F(9f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(104), Ui.Z(480), Ui.Z(22)), Ui.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, detail, Ui.F(9f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(136), Ui.Z(480), Ui.Z(120)), Ui.Bad,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }

            if (phase == "ready")
            {
                if (status.Length > 0)
                    TextRenderer.DrawText(g, status, Ui.F(8.5f, FontStyle.Bold),
                        new Rectangle(Ui.Z(28), Ui.Z(320), Ui.Z(480), Ui.Z(20)), Ui.Bad,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                else
                    TextRenderer.DrawText(g, "版本 " + Info.Version + " · 5 个文件 · 约 4.7 MB · 安装后可在系统“应用”里卸载",
                        Ui.F(8.5f, FontStyle.Regular), new Rectangle(Ui.Z(28), Ui.Z(320), Ui.Z(480), Ui.Z(20)), Ui.Dim,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            if (phase == "running")
            {
                int w = Ui.Z(460);
                TextRenderer.DrawText(g, status, Ui.F(8.5f, FontStyle.Regular),
                    new Rectangle(Ui.Z(28), Ui.Z(320), Ui.Z(480), Ui.Z(20)), Color.FromArgb(170, 206, 218, 228),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            if (phase == "error")
                TextRenderer.DrawText(g, status, Ui.F(9f, FontStyle.Bold),
                    new Rectangle(Ui.Z(28), Ui.Z(76), Ui.Z(480), Ui.Z(22)), Ui.Bad,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, "作者 " + Info.Publisher + "   ·   开源 MIT License",
                Ui.F(8f, FontStyle.Regular), new Rectangle(Ui.Z(28), Ui.Z(446), Ui.Z(300), Ui.Z(16)),
                Color.FromArgb(118, 124, 136), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "SHA256 清单随包提供",
                Ui.F(8f, FontStyle.Regular), new Rectangle(Ui.Z(280), Ui.Z(446), Ui.Z(232), Ui.Z(16)),
                Color.FromArgb(118, 124, 136), TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    static class Args
    {
        public static bool Has(string flag)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 1; i < a.Length; i++)
                if (string.Equals(a[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string Dir(string def)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 1; i < a.Length; i++)
                if (a[i].StartsWith("/D=", StringComparison.OrdinalIgnoreCase))
                    return a[i].Substring(3).Trim('"');
            return def;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Ui.Init();
            bool silent = Args.Has("/S") || Args.Has("/SILENT");
            string dir = Args.Dir(Core.DefaultDir());

            bool sc = !Args.Has("/NOSHORTCUT");
            bool au = !Args.Has("/NOAUTOSTART");
            bool run = Args.Has("/RUN");

            if (silent)
            {
                Core.Log("静默安装开始：" + dir);
                string err = Core.Install(dir, sc, au, null);
                Core.Log(err == null ? "静默安装成功" : "静默安装失败：" + err);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
