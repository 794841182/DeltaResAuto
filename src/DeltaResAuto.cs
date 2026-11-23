using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeltaResAuto
{
    // ================= 系统接口 =================
    public static class Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern int ChangeDisplaySettingsEx(string dev, ref DEVMODE dm, IntPtr hwnd, int flags, IntPtr lp);
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        public static extern bool EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        static extern bool EnumDisplayDevices(string dev, uint num, ref DISPLAY_DEVICE dd, uint flags);
        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr h, int min, int max);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 pe);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 pe);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public const int DM_PELSWIDTH = 0x80000, DM_PELSHEIGHT = 0x100000, DM_BITSPERPEL = 0x40000, DM_DISPLAYFREQUENCY = 0x400000;
        public const int CDS_UPDATEREGISTRY = 0x01, CDS_TEST = 0x02, ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public int AccentState; public int AccentFlags; public int GradientColor; public int AnimationId;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        public class ProcEntry { public string Exe; public int Pid; }

        public static List<ProcEntry> Processes()
        {
            List<ProcEntry> list = new List<ProcEntry>();
            // 快照偶发失败（进程频繁创建/退出时），重试几次，避免误判成“游戏没开”
            for (int attempt = 0; attempt < 3; attempt++)
            {
                list.Clear();
                IntPtr snap = CreateToolhelp32Snapshot(0x00000002, 0);
                if (snap == IntPtr.Zero || snap == new IntPtr(-1))
                {
                    Thread.Sleep(80);
                    continue;
                }
                try
                {
                    PROCESSENTRY32 pe = new PROCESSENTRY32();
                    pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                    if (Process32First(snap, ref pe))
                    {
                        do
                        {
                            if (!string.IsNullOrEmpty(pe.szExeFile))
                            {
                                ProcEntry e = new ProcEntry();
                                e.Exe = pe.szExeFile;
                                e.Pid = (int)pe.th32ProcessID;
                                list.Add(e);
                            }
                        } while (Process32Next(snap, ref pe));
                    }
                }
                finally { CloseHandle(snap); }
                if (list.Count > 0) return list;
                Thread.Sleep(80);
            }
            return list;
        }

        public static DEVMODE Fresh()
        {
            DEVMODE d = new DEVMODE();
            d.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return d;
        }

        public static string PrimaryDevice()
        {
            DISPLAY_DEVICE dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            uint i = 0;
            while (EnumDisplayDevices(null, i, ref dd, 0))
            {
                if ((dd.StateFlags & 0x1) != 0 && (dd.StateFlags & 0x4) != 0) return dd.DeviceName;
                i++;
                dd = new DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            }
            return null;
        }

        // 枚举已接入桌面的显示器：[设备名, 显示标签]
        public static List<string[]> Devices()
        {
            List<string[]> list = new List<string[]>();
            DISPLAY_DEVICE dd = new DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            uint i = 0;
            while (EnumDisplayDevices(null, i, ref dd, 0))
            {
                if ((dd.StateFlags & 0x1) != 0)
                {
                    int w, h, hz;
                    GetCurrent(dd.DeviceName, out w, out h, out hz);
                    string tag = (dd.StateFlags & 0x4) != 0 ? "主屏" : "副屏" + (list.Count + 1);
                    string text = tag;
                    if (string.IsNullOrEmpty(dd.DeviceString) == false) text += " · " + dd.DeviceString.Trim();
                    text += "  " + w + "×" + h;
                    list.Add(new string[] { dd.DeviceName, text });
                }
                i++;
                dd = new DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            }
            return list;
        }

        public static bool GetCurrent(string dev, out int w, out int h, out int hz)
        {
            DEVMODE dm = Fresh();
            bool ok = EnumDisplaySettings(dev, ENUM_CURRENT_SETTINGS, ref dm);
            w = dm.dmPelsWidth; h = dm.dmPelsHeight; hz = dm.dmDisplayFrequency;
            return ok;
        }

        public static List<DEVMODE> FindModes(string dev, int w, int h)
        {
            List<DEVMODE> r = new List<DEVMODE>();
            DEVMODE dm = Fresh();
            int i = 0;
            while (EnumDisplaySettings(dev, i, ref dm))
            {
                if (dm.dmPelsWidth == w && dm.dmPelsHeight == h && dm.dmBitsPerPel >= 32) r.Add(dm);
                i++; dm = Fresh();
            }
            r.Sort(delegate (DEVMODE a, DEVMODE b) { return b.dmDisplayFrequency.CompareTo(a.dmDisplayFrequency); });
            return r;
        }

        public static List<string> Resolutions(string dev)
        {
            List<string> keys = new List<string>();
            List<int> pixels = new List<int>();
            DEVMODE dm = Fresh();
            int i = 0;
            while (EnumDisplaySettings(dev, i, ref dm))
            {
                if (dm.dmBitsPerPel >= 32)
                {
                    string k = dm.dmPelsWidth + " × " + dm.dmPelsHeight;
                    if (!keys.Contains(k)) { keys.Add(k); pixels.Add(dm.dmPelsWidth * dm.dmPelsHeight); }
                }
                i++; dm = Fresh();
            }
            for (int a = 0; a < keys.Count; a++)
                for (int b = a + 1; b < keys.Count; b++)
                    if (pixels[b] > pixels[a])
                    {
                        int tp = pixels[a]; pixels[a] = pixels[b]; pixels[b] = tp;
                        string tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
                    }
            return keys;
        }

        // 指定分辨率下驱动真正支持的刷新率，从高到低
        public static List<int> RefreshRates(string dev, int w, int h)
        {
            List<int> nums = new List<int>();
            List<DEVMODE> modes = FindModes(dev, w, h);
            for (int i = 0; i < modes.Count; i++)
            {
                int f = modes[i].dmDisplayFrequency;
                if (f <= 1) continue;
                if (!nums.Contains(f)) nums.Add(f);
            }
            nums.Sort(delegate (int a, int b) { return b.CompareTo(a); });
            return nums;
        }

        public static int ApplyMode(string dev, int w, int h, int prefHz, out int appliedHz)
        {
            appliedHz = 0;
            int lastRet = -99;

            List<DEVMODE> exact = FindModes(dev, w, h);
            if (exact.Count > 0)
            {
                if (prefHz > 0)
                {
                    bool found = false;
                    for (int i = 0; i < exact.Count; i++)
                    {
                        if (exact[i].dmDisplayFrequency != prefHz) continue;
                        found = true;
                        DEVMODE c = exact[i];
                        lastRet = ChangeDisplaySettingsEx(dev, ref c, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                        if (lastRet == 0) { appliedHz = c.dmDisplayFrequency; return 0; }
                    }
                    if (!found) return -2;
                }
                for (int i = 0; i < exact.Count; i++)
                {
                    DEVMODE c = exact[i];
                    lastRet = ChangeDisplaySettingsEx(dev, ref c, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                    if (lastRet == 0) { appliedHz = c.dmDisplayFrequency; return 0; }
                }
            }

            int[] rates = { 360, 300, 240, 165, 144, 120, 60 };
            for (int i = 0; i < rates.Length; i++)
            {
                DEVMODE c = Fresh();
                c.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL | DM_DISPLAYFREQUENCY;
                c.dmPelsWidth = w; c.dmPelsHeight = h; c.dmBitsPerPel = 32; c.dmDisplayFrequency = rates[i];
                if (ChangeDisplaySettingsEx(dev, ref c, IntPtr.Zero, CDS_TEST, IntPtr.Zero) != 0) continue;
                DEVMODE c2 = c;
                lastRet = ChangeDisplaySettingsEx(dev, ref c2, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                if (lastRet == 0) { appliedHz = rates[i]; return 0; }
            }
            return lastRet;
        }

        public static string ErrText(int code)
        {
            if (code == -2) return "驱动不支持该分辨率/刷新率";
            if (code == -1) return "模式切换失败(-1)";
            if (code == -3) return "需要重启才能生效";
            if (code == -4) return "显存不足(-4)";
            if (code == 1) return "需要重启系统(1)";
            return "错误码 " + code;
        }

        public static void TrimMemory()
        {
            try { SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1); } catch { }
        }

        public static bool EnableAcrylic(IntPtr hwnd, int color)
        {
            try
            {
                AccentPolicy ap = new AccentPolicy();
                ap.AccentState = 4; // ACRYLICBLURBEHIND
                ap.GradientColor = color;
                int sz = Marshal.SizeOf(typeof(AccentPolicy));
                IntPtr ptr = Marshal.AllocHGlobal(sz);
                Marshal.StructureToPtr(ap, ptr, false);
                WindowCompositionAttributeData d = new WindowCompositionAttributeData();
                d.Attribute = 19; d.SizeOfData = sz; d.Data = ptr;
                int ret = SetWindowCompositionAttribute(hwnd, ref d);
                Marshal.FreeHGlobal(ptr);
                return ret != 0;
            }
            catch { return false; }
        }
    }

    // ================= 配置 =================
    public class Settings
    {
        public int GameW = 1440, GameH = 1080;
        public int DeskW = 1920, DeskH = 1440;
        // 游戏运行时使用的刷新率，0 = 跟随系统
        public int GameHz = 0;
        public int DelayMs = 3000;
        public int PollMs = 2000;
        public string GameExes = "deltaforceclient-win64-shipping.exe,deltaforceclient.exe";
        public string MatchKeys = "deltaforce,三角洲";
        public string SelectedGame = "三角洲行动";
        public string ExcludeKeys = "launcher,uninst,bugreport,crashpad,icreate,recorder,ffmpeg,tiny,updater,setup,install,config,webview,browser,tenprotect,taslogin,es_brd,qbblink,assistant,gpu_detect,startup_runner,crashreport,downloader";

        // 手动添加的游戏：exe|显示名;exe|显示名
        public string CustomGames = "";

        // 界面缩放：0 = 跟随系统 DPI，其余为固定倍率（0.5 / 0.75 / 1 / 1.25 / 1.5 / 2）
        public float UIScale = 0f;

        // 背景主题：0 = 珠链，1 = 深空（无纹理），2 = 米白（亮色）
        public int Theme = 0;

        // 要切换分辨率的显示器：空 = 主屏，否则是 \\.\DISPLAYn
        public string DisplayDevice = "";

        public void AddCustomGame(string exe, string name)
        {
            if (string.IsNullOrEmpty(exe)) return;
            StringBuilder sb = new StringBuilder();
            string[] parts = (CustomGames == null ? "" : CustomGames).Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                int bar = p.IndexOf('|');
                string pe = bar > 0 ? p.Substring(0, bar) : p;
                if (pe.Trim().ToLowerInvariant() == exe.Trim().ToLowerInvariant()) continue;
                if (sb.Length > 0) sb.Append(";");
                sb.Append(p);
            }
            if (sb.Length > 0) sb.Append(";");
            sb.Append(exe.Replace("|", " ").Replace(";", " ") + "|" + name.Replace("|", " ").Replace(";", " "));
            CustomGames = sb.ToString();
        }

        public static string Dir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeltaResAuto");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }
        public static string IniPath() { return Path.Combine(Dir(), "settings.ini"); }
        public static string LogPath() { return Path.Combine(Dir(), "auto.log"); }
        public static string ShowFlag() { return Path.Combine(Dir(), "show.flag"); }

        public static string BackupDir() { return Path.Combine(Dir(), "backup"); }

        private static Settings TryLoadFile(string path, out bool ok)
        {
            Settings s = new Settings();
            ok = false;
            if (!File.Exists(path)) return s;
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i];
                    int eq = ln.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = ln.Substring(0, eq).Trim();
                    string v = ln.Substring(eq + 1).Trim();
                    if (k == "GameW") s.GameW = int.Parse(v);
                    else if (k == "GameH") s.GameH = int.Parse(v);
                    else if (k == "DeskW") s.DeskW = int.Parse(v);
                    else if (k == "DeskH") s.DeskH = int.Parse(v);
                    else if (k == "GameHz") s.GameHz = int.Parse(v);
                    else if (k == "DelayMs") s.DelayMs = int.Parse(v);
                    else if (k == "PollMs") s.PollMs = int.Parse(v);
                    else if (k == "GameExes") s.GameExes = v;
                    else if (k == "MatchKeys") s.MatchKeys = v;
                    else if (k == "SelectedGame") s.SelectedGame = v;
                    else if (k == "ExcludeKeys") s.ExcludeKeys = v;
                    else if (k == "CustomGames") s.CustomGames = v;
                    else if (k == "UIScale") s.UIScale = float.Parse(v);
                    else if (k == "Theme") s.Theme = int.Parse(v);
                    else if (k == "DisplayDevice") s.DisplayDevice = v;
                }
                ok = true;
            }
            catch { ok = false; }
            return s;
        }

        // 读配置：settings.ini 丢失或写坏时，自动回退到 .bak 和 backup 目录里最新的一份
        public static Settings Load()
        {
            Settings s;
            bool ok = false;
            s = TryLoadFile(IniPath(), out ok);
            if (!ok) s = TryLoadFile(IniPath() + ".bak", out ok);
            if (!ok)
            {
                try
                {
                    string dir = BackupDir();
                    if (Directory.Exists(dir))
                    {
                        string[] files = Directory.GetFiles(dir, "settings-*.ini");
                        Array.Sort(files);
                        for (int i = files.Length - 1; i >= 0; i--)
                        {
                            s = TryLoadFile(files[i], out ok);
                            if (ok) break;
                        }
                    }
                }
                catch { }
            }
            if (!ok || s == null) s = new Settings();
            if (s.PollMs < 500) s.PollMs = 500;
            if (s.DelayMs < 0) s.DelayMs = 0;
            if (s.Theme < 0 || s.Theme > 2) s.Theme = 0;
            return s;
        }

        private string BuildIni()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("GameW=" + GameW);
            sb.AppendLine("GameH=" + GameH);
            sb.AppendLine("DeskW=" + DeskW);
            sb.AppendLine("DeskH=" + DeskH);
            sb.AppendLine("GameHz=" + GameHz);
            sb.AppendLine("DelayMs=" + DelayMs);
            sb.AppendLine("PollMs=" + PollMs);
            sb.AppendLine("GameExes=" + GameExes);
            sb.AppendLine("MatchKeys=" + MatchKeys);
            sb.AppendLine("SelectedGame=" + SelectedGame);
            sb.AppendLine("ExcludeKeys=" + ExcludeKeys);
            sb.AppendLine("CustomGames=" + CustomGames);
            sb.AppendLine("UIScale=" + UIScale);
            sb.AppendLine("Theme=" + Theme);
            sb.AppendLine("DisplayDevice=" + DisplayDevice);
            return sb.ToString();
        }

        // 写配置：内容有变化时，先把旧文件留一份（.bak + backup\ 时间戳，最多保留 10 份）
        public void Save()
        {
            try
            {
                string path = IniPath();
                string text = BuildIni();
                string old = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
                if (old != null && old == text) return;
                if (old != null)
                {
                    File.Copy(path, path + ".bak", true);
                    Directory.CreateDirectory(BackupDir());
                    string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                    File.Copy(path, Path.Combine(BackupDir(), "settings-" + stamp + ".ini"), true);
                    TrimBackups(10);
                }
                File.WriteAllText(path, text, Encoding.UTF8);
            }
            catch { }
        }

        private static void TrimBackups(int keep)
        {
            try
            {
                string[] files = Directory.GetFiles(BackupDir(), "settings-*.ini");
                if (files.Length <= keep) return;
                Array.Sort(files);
                for (int i = 0; i < files.Length - keep; i++) File.Delete(files[i]);
            }
            catch { }
        }
    }

    // ================= 本机游戏扫描 =================
    public class GameInfo
    {
        public string Name;
        public string Exe;   // 进程名（含 .exe，小写）
        public string Dir;
        public override string ToString() { return Name; }
    }

    public static class GameScanner
    {
        static readonly string[] NameBlock = {
            "microsoft", "windows", "visual c++", "redistributable", ".net", "update for",
            "adobe", "office", "chrome", "edge", "firefox", "wechat", "微信", "qq",
            "wps", "java", "python", "node.js", "git", "steam", "wegame", "epic games launcher",
            "afterburner", "gpu-z", "cpu-z", "7-zip", "winrar", "obs", "bandicam",
            "nvidia", "amd", "realtek", "intel", "riot client", "driver", "sdk", "runtime",
            "antivirus", "security", "助手", "精彩时刻", "压缩", "输入法", "驱动",
            "更新程序", "卸载", "launcher", "启动器", "toolkit", "service", "support",
            "语音", "k歌", "音乐", "music", "加速器", "acceleration", "网盘", "直播", "会议",
            "录屏", "播放器", "todesk", "向日葵", "kook", "oopz", "豆包", "抖音", "夸克",
            "酷狗", "云音乐", "钉钉", "企业微信", "管家", "卫士", "组件", "白板",
            "l-connect", "vocal remover", "r-drive", "deepseek", "quark", "douyin", "doubao",
            "电竞", "陪玩", "开黑", "直播伴侣"
        };

        static readonly string[] BadExe = {
            "unins", "uninst", "launcher", "crash", "bugreport", "helper", "updat",
            "update", "setup", "install", "redist", "vc_", "dxsetup", "dxwebsetup",
            "config", "setting", "browser", "webview", "minibrowser", "service",
            "tray", "test", "benchmark", "ffmpeg", "ffprobe", "obs", "recorder",
            "icreate", "tenprotect", "taslogin", "es_brd", "qbblink", "gpu_detect",
            "startup_runner", "assistant", "report", "sdk", "editor", "tool",
            "tinydl", "tiny", "downloader", "dl.exe", "webstart"
        };

        static readonly string[] SkipDir = {
            "content", "paks", "saved", "engine", "crashes", "cache", "logs", "movies",
            "icreate", "recorder", "qbblinktrial", "livebox", "teniodl", "cachedata",
            "plugins", "config"
        };

        public static List<GameInfo> Scan()
        {
            List<GameInfo> list = new List<GameInfo>();
            Stopwatch sw = Stopwatch.StartNew();
            try { ScanUninstall(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", list, sw); } catch { }
            try { ScanUninstall(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", list, sw); } catch { }
            try { ScanUninstall(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", list, sw); } catch { }
            try { ScanSteam(list, sw); } catch { }
            try { ScanEpic(list, sw); } catch { }
            try { ScanWeGame(list, sw); } catch { }
            try { EnsureDeltaForce(list, sw); } catch { }

            List<GameInfo> sorted = new List<GameInfo>();
            for (int i = 0; i < list.Count; i++)
            {
                GameInfo gi = list[i];
                if (gi.Exe == null || gi.Exe.Length == 0) continue;
                string key = gi.Exe.ToLowerInvariant();
                bool dup = false;
                for (int j = 0; j < sorted.Count; j++)
                    if (sorted[j].Exe == key) { dup = true; break; }
                if (!dup) { gi.Exe = key; sorted.Add(gi); }
            }
            sorted.Sort(delegate (GameInfo a, GameInfo b) { return string.Compare(a.Name, b.Name, StringComparison.Ordinal); });
            return sorted;
        }

        static long FileSize(string p)
        {
            try { return new FileInfo(p).Length; } catch { return 0; }
        }

        static bool IsTrustedDir(string dir)
        {
            if (dir == null) return false;
            string low = dir.ToLowerInvariant();
            return low.IndexOf("steamapps") >= 0 || low.IndexOf("wegameapps") >= 0
                || low.IndexOf("epic games") >= 0 || low.IndexOf("gog games") >= 0
                || low.IndexOf("ubisoft") >= 0 || low.IndexOf("battle.net") >= 0;
        }

        static string Normalize(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = char.ToLowerInvariant(s[i]);
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        // 少数游戏主程序名和目录名对不上，做定向纠正
        static string PreferKnown(string gameName, string dir, string exePath)
        {
            string low = (gameName == null ? "" : gameName.ToLowerInvariant());
            if (dir != null && Directory.Exists(dir))
            {
                if (low.IndexOf("英雄联盟") >= 0 || low.IndexOf("league") >= 0)
                {
                    string p1 = Path.Combine(dir, @"LeagueClient\LeagueClientUx.exe");
                    if (File.Exists(p1)) return p1;
                }
            }
            return exePath;
        }

        static double ScoreExe(string f, string folder)
        {
            string fn = Path.GetFileName(f).ToLowerInvariant();
            string flow = f.ToLowerInvariant();
            double s = FileSize(f) / (1024.0 * 1024.0);
            if (fn.EndsWith("-win64-shipping.exe")) s += 6000;
            if (flow.IndexOf("\\binaries\\win64\\") >= 0) s += 1200;
            string bas = fn.EndsWith(".exe") ? fn.Substring(0, fn.Length - 4) : fn;
            string fb = Normalize(folder);
            if (fb.Length > 2 && bas == fb) s += 800;
            if (fb.Length > 2 && bas.IndexOf(fb) >= 0) s += 300;
            if (IsBadExe(fn)) s -= 6000;
            if (flow.IndexOf("\\tools\\") >= 0 || flow.IndexOf("\\bin\\") >= 0) s -= 900;
            if (fn.IndexOf("tft") >= 0 && fb.IndexOf("tft") < 0 && fb.IndexOf("云顶") < 0) s -= 5000;
            if (fn.IndexOf("helper") >= 0 || fn.IndexOf("guard") >= 0 || fn.IndexOf("diagnostic") >= 0) s -= 3000;
            return s;
        }

        static void Add(List<GameInfo> list, string name, string exePath, string dir)
        {
            if (exePath == null || name == null) return;
            exePath = PreferKnown(name, dir, exePath);
            string exe = Path.GetFileName(exePath).ToLowerInvariant();
            if (exe.Length == 0 || !exe.EndsWith(".exe")) return;
            if (exePath.ToLowerInvariant().IndexOf("\\windows\\") >= 0) return;
            for (int i = 0; i < list.Count; i++) if (list[i].Exe == exe) return;
            GameInfo gi = new GameInfo();
            gi.Name = name.Trim();
            gi.Exe = exe;
            gi.Dir = dir;
            list.Add(gi);
        }

        static bool NameBlocked(string name)
        {
            string low = name.ToLowerInvariant();
            for (int i = 0; i < NameBlock.Length; i++)
                if (low.IndexOf(NameBlock[i]) >= 0) return true;
            return false;
        }

        static string Val(RegistryKey k, string key)
        {
            try
            {
                object o = k.GetValue(key);
                if (o == null) return null;
                string s = o.ToString().Trim();
                return s.Length == 0 ? null : s;
            }
            catch { return null; }
        }

        static string CleanPath(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            if (s.StartsWith("\"")) s = s.Substring(1);
            if (s.EndsWith("\"")) s = s.TrimEnd('"');
            int comma = s.LastIndexOf(',');
            if (comma > 2 && s.ToLowerInvariant().EndsWith(".exe,0")) s = s.Substring(0, comma);
            s = s.Replace('/', '\\');
            return s;
        }

        static void ScanUninstall(RegistryKey root, string path, List<GameInfo> outList, Stopwatch sw)
        {
            RegistryKey k = null;
            try { k = root.OpenSubKey(path, false); } catch { }
            if (k == null) return;
            try
            {
                string[] subs = k.GetSubKeyNames();
                for (int i = 0; i < subs.Length; i++)
                {
                    if (sw.Elapsed.TotalSeconds > 20) break;
                    RegistryKey sk = null;
                    try
                    {
                        sk = k.OpenSubKey(subs[i], false);
                        if (sk == null) continue;
                        string name = Val(sk, "DisplayName");
                        if (name == null || NameBlocked(name)) continue;

                        object sys = sk.GetValue("SystemComponent");
                        if (sys != null)
                        {
                            int sc = 0;
                            int.TryParse(sys.ToString(), out sc);
                            if (sc == 1) continue;
                        }
                        string parent = Val(sk, "ParentKeyName");
                        if (parent != null) continue;

                        string loc = CleanPath(Val(sk, "InstallLocation"));
                        string icon = CleanPath(Val(sk, "DisplayIcon"));

                        string dir = null;
                        if (loc != null && Directory.Exists(loc)) dir = loc;
                        if (dir == null && icon != null && File.Exists(icon)) dir = AscendLauncher(Path.GetDirectoryName(icon));
                        if (dir == null) dir = FindWeGameDir(name);

                        if (dir == null) continue;
                        string exe = FindMainExe(dir, sw);
                        if (exe == null && icon != null && File.Exists(icon) && !IsBadExe(Path.GetFileName(icon).ToLowerInvariant())) exe = icon;
                        if (exe == null) continue;
                        // 来源门槛：可信游戏目录 / UE Shipping 特征 / 主程序足够大，三者其一
                        bool trusted = IsTrustedDir(dir);
                        bool ue = exe.ToLowerInvariant().EndsWith("-win64-shipping.exe");
                        if (!trusted && !ue && FileSize(exe) < 10L * 1024 * 1024) continue;
                        Add(outList, name, exe, dir);
                    }
                    catch { }
                    finally { if (sk != null) sk.Close(); }
                }
            }
            finally { k.Close(); }
        }

        static string AscendLauncher(string dir)
        {
            if (dir == null) return null;
            string d = dir;
            for (int i = 0; i < 3 && d != null; i++)
            {
                string leaf = Path.GetFileName(d).ToLowerInvariant();
                if (leaf == "launcher" || leaf == "bin" || leaf == "binaries" || leaf == "win64"
                    || leaf == "x64" || leaf == "x86" || leaf == "app")
                {
                    string up = Path.GetDirectoryName(d);
                    if (up == null || up.Length < 4) break;
                    d = up;
                    continue;
                }
                break;
            }
            return Directory.Exists(d) ? d : null;
        }

        static string FindWeGameDir(string name)
        {
            string[] drives = DriveLetters();
            for (int i = 0; i < drives.Length; i++)
            {
                string root = drives[i] + "\\WeGameApps";
                if (!Directory.Exists(root)) continue;
                string direct = Path.Combine(root, name);
                if (Directory.Exists(direct)) return direct;
                try
                {
                    string[] subs = Directory.GetDirectories(root);
                    for (int j = 0; j < subs.Length; j++)
                    {
                        string nm = Path.GetFileName(subs[j]);
                        if (nm.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf(nm, StringComparison.OrdinalIgnoreCase) >= 0) return subs[j];
                    }
                }
                catch { }
            }
            return null;
        }

        static string[] DriveLetters()
        {
            List<string> r = new List<string>();
            try
            {
                DriveInfo[] ds = DriveInfo.GetDrives();
                for (int i = 0; i < ds.Length; i++)
                    if (ds[i].DriveType == DriveType.Fixed) r.Add(ds[i].Name.TrimEnd('\\'));
            }
            catch { }
            if (r.Count == 0) r.Add("C:");
            return r.ToArray();
        }

        static bool IsBadExe(string fn)
        {
            for (int i = 0; i < BadExe.Length; i++)
                if (fn.IndexOf(BadExe[i]) >= 0) return true;
            return false;
        }

        static bool SkipDirName(string leaf)
        {
            string low = leaf.ToLowerInvariant();
            for (int i = 0; i < SkipDir.Length; i++)
                if (low == SkipDir[i]) return true;
            return false;
        }

        class Node { public string Dir; public int Depth; }

        static string FindMainExe(string dir, Stopwatch sw)
        {
            if (dir == null || !Directory.Exists(dir)) return null;
            string best = null; double bestScore = -1e9;
            string folder = Path.GetFileName(dir);

            Queue<Node> q = new Queue<Node>();
            Node n0 = new Node(); n0.Dir = dir; n0.Depth = 0;
            q.Enqueue(n0);
            int visited = 0;

            while (q.Count > 0 && visited < 800 && sw.Elapsed.TotalSeconds < 25)
            {
                Node cur = q.Dequeue();
                visited++;
                string[] files = null;
                try { files = Directory.GetFiles(cur.Dir, "*.exe"); } catch { }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string f = files[i];
                        string fn = Path.GetFileName(f).ToLowerInvariant();
                        if (IsBadExe(fn)) continue;
                        double sc = ScoreExe(f, folder);
                        if (sc > bestScore) { bestScore = sc; best = f; }
                    }
                }
                if (cur.Depth < 4)
                {
                    string[] subs = null;
                    try { subs = Directory.GetDirectories(cur.Dir); } catch { }
                    if (subs != null)
                    {
                        for (int i = 0; i < subs.Length; i++)
                        {
                            string leaf = Path.GetFileName(subs[i]);
                            if (SkipDirName(leaf)) continue;
                            Node nn = new Node(); nn.Dir = subs[i]; nn.Depth = cur.Depth + 1;
                            q.Enqueue(nn);
                        }
                    }
                }
                if (bestScore > 5000 && visited > 40) break;
            }

            if (bestScore <= 0) return null;
            return best;
        }

        static void ScanSteam(List<GameInfo> outList, Stopwatch sw)
        {
            string steam = null;
            RegistryKey k = null;
            try { k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false); } catch { }
            if (k != null)
            {
                try { steam = CleanPath(Val(k, "SteamPath")); } catch { }
                k.Close();
            }
            if (steam == null)
            {
                string[] cand = { "C:\\Steam", "C:\\Program Files (x86)\\Steam", "D:\\Steam", "E:\\Steam", "F:\\Steam" };
                for (int i = 0; i < cand.Length; i++) if (Directory.Exists(cand[i])) { steam = cand[i]; break; }
            }
            if (steam == null || !Directory.Exists(steam)) return;

            List<string> libs = new List<string>();
            libs.Add(steam);
            string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                try
                {
                    string[] lines = File.ReadAllLines(vdf, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string ln = lines[i].Trim();
                        if (ln.Length == 0) continue;
                        int p = ln.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                        if (p < 0) continue;
                        int q1 = ln.IndexOf('"', p + 6);
                        int q2 = ln.IndexOf('"', q1 + 1);
                        if (q1 > 0 && q2 > q1)
                        {
                            string path = ln.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\\", "\\").Replace('/', '\\');
                            if (Directory.Exists(path) && !libs.Contains(path)) libs.Add(path);
                        }
                    }
                }
                catch { }
            }

            for (int l = 0; l < libs.Count; l++)
            {
                if (sw.Elapsed.TotalSeconds > 30) return;
                string common = Path.Combine(libs[l], @"steamapps\common");
                if (!Directory.Exists(common)) continue;
                string[] manifests = null;
                try { manifests = Directory.GetFiles(Path.Combine(libs[l], "steamapps"), "appmanifest_*.acf"); } catch { }
                if (manifests == null) continue;
                for (int i = 0; i < manifests.Length; i++)
                {
                    if (sw.Elapsed.TotalSeconds > 30) return;
                    string nm = ReadAcfValue(manifests[i], "name");
                    string folder = ReadAcfValue(manifests[i], "installdir");
                    if (nm == null || folder == null) continue;
                    string dir = Path.Combine(common, folder);
                    if (!Directory.Exists(dir)) continue;
                    string exe = FindMainExe(dir, sw);
                    if (exe == null) continue;
                    Add(outList, nm, exe, dir);
                }
            }
        }

        static string ReadAcfValue(string file, string key)
        {
            try
            {
                using (StreamReader sr = new StreamReader(file, Encoding.UTF8))
                {
                    for (int i = 0; i < 60; i++)
                    {
                        string ln = sr.ReadLine();
                        if (ln == null) break;
                        int p = ln.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
                        if (p < 0) continue;
                        int q1 = ln.IndexOf('"', p + key.Length + 2);
                        int q2 = ln.IndexOf('"', q1 + 1);
                        if (q1 > 0 && q2 > q1) return ln.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\\", "\\");
                    }
                }
            }
            catch { }
            return null;
        }

        static void ScanEpic(List<GameInfo> outList, Stopwatch sw)
        {
            string dir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
            if (!Directory.Exists(dir)) return;
            string[] files = null;
            try { files = Directory.GetFiles(dir, "*.item"); } catch { }
            if (files == null) return;
            for (int i = 0; i < files.Length; i++)
            {
                if (sw.Elapsed.TotalSeconds > 35) return;
                string nm = ReadJsonValue(files[i], "DisplayName");
                string loc = ReadJsonValue(files[i], "InstallLocation");
                if (nm == null || loc == null) continue;
                loc = loc.Replace("\\\\", "\\");
                if (!Directory.Exists(loc)) continue;
                string exe = FindMainExe(loc, sw);
                if (exe == null) continue;
                Add(outList, nm, exe, loc);
            }
        }

        static string ReadJsonValue(string file, string key)
        {
            try
            {
                string txt = File.ReadAllText(file, Encoding.UTF8);
                int p = txt.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
                if (p < 0) return null;
                int q1 = txt.IndexOf('"', p + key.Length + 2);
                int q2 = txt.IndexOf('"', q1 + 1);
                if (q1 < 0 || q2 <= q1) return null;
                return txt.Substring(q1 + 1, q2 - q1 - 1);
            }
            catch { return null; }
        }

        static void ScanWeGame(List<GameInfo> outList, Stopwatch sw)
        {
            string[] drives = DriveLetters();
            for (int d = 0; d < drives.Length; d++)
            {
                string root = drives[d] + "\\WeGameApps";
                if (!Directory.Exists(root)) continue;
                string[] subs = null;
                try { subs = Directory.GetDirectories(root); } catch { }
                if (subs == null) continue;
                for (int i = 0; i < subs.Length; i++)
                {
                    if (sw.Elapsed.TotalSeconds > 35) return;
                    string nm = Path.GetFileName(subs[i]);
                    string low = nm.ToLowerInvariant();
                    if (low == "common_apps" || low == "downloading" || low == "rail_apps"
                        || low == "rail_user_data" || low == "game" || low == "temp") continue;
                    string exe = FindMainExe(subs[i], sw);
                    if (exe == null) continue;
                    Add(outList, nm, exe, subs[i]);
                }
            }
        }

        static void EnsureDeltaForce(List<GameInfo> outList, Stopwatch sw)
        {
            string[] drives = DriveLetters();
            for (int i = 0; i < drives.Length; i++)
            {
                string p1 = drives[i] + "\\Delta Force\\Delta Force\\DeltaForce\\Binaries\\Win64\\DeltaForceClient-Win64-Shipping.exe";
                string p2 = drives[i] + "\\Program Files\\Delta Force\\Delta Force\\DeltaForce\\Binaries\\Win64\\DeltaForceClient-Win64-Shipping.exe";
                if (File.Exists(p1)) { Add(outList, "三角洲行动", p1, Path.GetDirectoryName(p1)); return; }
                if (File.Exists(p2)) { Add(outList, "三角洲行动", p2, Path.GetDirectoryName(p2)); return; }
            }
        }
    }

    // ================= 检测引擎 =================
    public class Engine
    {
        public volatile bool GameRunning;
        public volatile bool Paused;
        public volatile bool Switched;
        public volatile int SwitchCount;
        public volatile string LastMsg = "正在初始化...";
        public volatile string GameProcName = "";
        public volatile string LastActionTime = "--:--:--";
        public volatile bool ShowRequested;

        private Thread _th;
        private volatile bool _stop;
        private string _dev;
        private string _target = "";
        private string _switchDev = "";
        private bool _lastGame;
        private DateTime _stateAt;
        private Native.DEVMODE _saved;
        private bool _savedValid;
        private int _gameW, _gameH, _gameHz, _deskW, _deskH, _deskHz;
        private DateTime _retryAt = DateTime.MinValue;
        private int _failStreak;
        private readonly object _logLock = new object();

        private int _pollMs, _delayMs;
        private string[] _exes, _match, _exclude;

        public Engine(Settings cfg)
        {
            _gameW = cfg.GameW; _gameH = cfg.GameH; _gameHz = cfg.GameHz;
            _deskW = cfg.DeskW; _deskH = cfg.DeskH; _deskHz = 0;
            _pollMs = cfg.PollMs; _delayMs = cfg.DelayMs;
            _exes = Split(cfg.GameExes);
            _match = Split(cfg.MatchKeys);
            _exclude = Split(cfg.ExcludeKeys);
            _target = cfg.DisplayDevice == null ? "" : cfg.DisplayDevice;
        }

        private static string[] Split(string s)
        {
            if (s == null) return new string[0];
            string[] raw = s.Split(',');
            List<string> outList = new List<string>();
            for (int i = 0; i < raw.Length; i++)
            {
                string t = raw[i].Trim().ToLowerInvariant();
                if (t.Length > 0) outList.Add(t);
            }
            return outList.ToArray();
        }

        public void ApplySettings(Settings cfg)
        {
            _gameW = cfg.GameW; _gameH = cfg.GameH; _gameHz = cfg.GameHz;
            _deskW = cfg.DeskW; _deskH = cfg.DeskH;
            _pollMs = cfg.PollMs; _delayMs = cfg.DelayMs;
            _exes = Split(cfg.GameExes);
            _match = Split(cfg.MatchKeys);
            _exclude = Split(cfg.ExcludeKeys);
            _target = cfg.DisplayDevice == null ? "" : cfg.DisplayDevice;
            _retryAt = DateTime.MinValue;
            _failStreak = 0;
            Log("设置已更新：监测 " + (cfg.SelectedGame.Length > 0 ? cfg.SelectedGame : "自定义") + "，游戏 " + _gameW + "×" + _gameH
                + (_gameHz > 0 ? " @" + _gameHz + "Hz" : " @跟随系统"));
        }

        public void Start()
        {
            _dev = Native.PrimaryDevice();
            if (_dev == null) _dev = @"\\.\DISPLAY1";
            string n;
            bool g = DetectGame(out n);
            _lastGame = g; _stateAt = DateTime.Now;
            GameRunning = g; GameProcName = n != null ? n : "";
            Log(g ? "启动时游戏已在运行，将在延迟后切换" : "已开始监听，等待游戏启动");
            _th = new Thread(Loop);
            _th.IsBackground = true;
            _th.Priority = ThreadPriority.Normal;
            _th.Start();
        }

        public void Stop()
        {
            _stop = true;
            if (_th != null) { try { _th.Join(2000); } catch { } }
            ForceRestore();
            Log("监听已停止");
        }

        private void Loop()
        {
            while (!_stop)
            {
                try
                {
                if (Paused) { Thread.Sleep(700); continue; }

                Native.ProcEntry hit = FindMatch();
                if (hit == null)
                {
                    if (GameRunning)
                    {
                        GameRunning = false; GameProcName = "";
                        Log("检测到游戏已退出");
                        SleepMs(_delayMs);
                        if (!_stop && Switched) DoRestore();
                        continue;
                    }
                    // 恢复失败时不能就此停住：游戏已退出但仍处于游戏分辨率，要继续重试
                    if (Switched)
                    {
                        DoRestore();
                        SleepMs(_pollMs);
                        continue;
                    }
                    SleepMs(_pollMs);
                    continue;
                }

                if (!GameRunning)
                {
                    GameRunning = true; GameProcName = hit.Exe;
                    Log("检测到游戏启动：" + hit.Exe);
                }
                SleepMs(_delayMs);
                if (_stop) break;
                if (!Paused && !Switched) DoSwitch();
                if (Paused) continue;

                // 已确认游戏在跑：优先内核等待退出；拿不到句柄就退化为低频轮询
                bool gone = WaitExit(hit, true);
                if (_stop) break;
                if (!gone)
                {
                    while (!_stop && !Paused && FindMatch() != null) SleepMs(Math.Min(_pollMs, 1000));
                    if (_stop) break;
                }

                // 退出后再确认一次，避开“启动器退出→主进程接管”的空窗被误判
                Native.ProcEntry again = FindMatch();
                if (again != null) continue;
                SleepMs(700);
                again = FindMatch();
                if (again != null) continue;

                GameRunning = false; GameProcName = "";
                Log("检测到游戏已退出");
                SleepMs(_delayMs);
                if (!_stop && Switched) DoRestore();
                }
                catch (Exception ex)
                {
                    // 任何意外异常都不能让监听线程死掉（否则界面会一直“等待游戏启动”）
                    Log("监听异常：" + ex.Message);
                    Thread.Sleep(500);
                }
            }
            if (Switched) ForceRestore();
        }

        private void SleepMs(int ms)
        {
            int left = ms;
            while (left > 0 && !_stop)
            {
                int step = left > 200 ? 200 : left;
                Thread.Sleep(step);
                left -= step;
            }
        }

        // 返回 true 表示确认进程已退出；false 表示拿不到句柄或被打断，交回主循环处理
        private bool WaitExit(Native.ProcEntry e, bool retrySwitch)
        {
            try
            {
                Process p = Process.GetProcessById(e.Pid);
                try
                {
                    while (!_stop && !Paused)
                    {
                        // 首次切换可能因全屏独占/驱动忙而失败，游戏仍在运行时按重试间隔再试
                        if (retrySwitch && !Switched) DoSwitch();
                        if (p.WaitForExit(1200)) return true;
                    }
                    return false;
                }
                finally { p.Dispose(); }
            }
            catch
            {
                // 受保护进程（反作弊）可能拒绝句柄，退化为轮询
                return false;
            }
        }

        // 去掉 -win64-shipping / -client 之类的后缀，取游戏主体名，用于兜底模糊匹配
        private static string Stem(string exe)
        {
            string s = exe.ToLowerInvariant();
            if (s.EndsWith(".exe")) s = s.Substring(0, s.Length - 4);
            string[] tails = new string[]
            {
                "-win64-shipping", "-win32-shipping", "-win64-shipping-", "-shipping",
                "-win64", "-win32", "-x64", "_shipping", "-client", "-launcher"
            };
            for (int i = 0; i < tails.Length; i++)
            {
                string t = tails[i];
                if (t.EndsWith("-")) continue;
                if (s.EndsWith(t)) { s = s.Substring(0, s.Length - t.Length); break; }
            }
            return s;
        }

        private Native.ProcEntry FindMatch()
        {
            List<Native.ProcEntry> ps = Native.Processes();
            if (ps.Count == 0) return null;

            string[] stems = null;
            if (_exes.Length > 0)
            {
                stems = new string[_exes.Length];
                for (int i = 0; i < _exes.Length; i++) stems[i] = Stem(_exes[i]);
            }

            // 先精确匹配，命中就返回
            for (int i = 0; i < ps.Count; i++)
            {
                string f = ps[i].Exe;
                if (f == null || f.Length == 0) continue;
                string low = f.ToLowerInvariant();
                bool excluded = false;
                for (int e = 0; e < _exclude.Length; e++)
                    if (_exclude[e].Length > 0 && low.IndexOf(_exclude[e]) >= 0) { excluded = true; break; }
                if (excluded) continue;

                if (_exes.Length > 0)
                {
                    for (int e = 0; e < _exes.Length; e++)
                        if (low == _exes[e]) return ps[i];
                }
                if (_match.Length > 0)
                {
                    for (int m = 0; m < _match.Length; m++)
                        if (_match[m].Length > 0 && low.IndexOf(_match[m]) >= 0) return ps[i];
                }
            }

            // 再按主体名兜底（覆盖改名/分进程的情况）
            if (stems != null)
            {
                for (int i = 0; i < ps.Count; i++)
                {
                    string f = ps[i].Exe;
                    if (f == null || f.Length == 0) continue;
                    string low = f.ToLowerInvariant();
                    bool excluded = false;
                    for (int e = 0; e < _exclude.Length; e++)
                        if (_exclude[e].Length > 0 && low.IndexOf(_exclude[e]) >= 0) { excluded = true; break; }
                    if (excluded) continue;

                    string st = Stem(low);
                    for (int e = 0; e < stems.Length; e++)
                    {
                        string want = stems[e];
                        if (want.Length < 4) continue;
                        if (st == want) return ps[i];
                        if (want.Length >= 7 && st.IndexOf(want) >= 0) return ps[i];
                    }
                }
            }
            return null;
        }

        private bool DetectGame(out string name)
        {
            name = null;
            Native.ProcEntry e = FindMatch();
            if (e == null) return false;
            name = e.Exe;
            return true;
        }

        private void DoSwitch()
        {
            if (DateTime.Now < _retryAt) return;
            string dev = TargetDevice();

            int cw, ch, chz;
            if (!Native.GetCurrent(dev, out cw, out ch, out chz))
            {
                _failStreak++;
                _retryAt = DateTime.Now.AddSeconds(5);
                Log("读取当前显示模式失败，暂不切换");
                return;
            }

            // 先保存原始模式：即使当前已经是游戏分辨率，退出时也应该回到进入游戏前的状态
            Native.DEVMODE before = Native.Fresh();
            before.dmPelsWidth = cw;
            before.dmPelsHeight = ch;
            before.dmDisplayFrequency = chz;
            _saved = before;
            _savedValid = true;
            _switchDev = dev;

            if (cw == _gameW && ch == _gameH && (_gameHz <= 0 || Math.Abs(chz - _gameHz) <= 1))
            {
                Switched = true;
                Log("当前已是 " + _gameW + "×" + _gameH + (_gameHz > 0 ? " @" + _gameHz + "Hz" : "") + "，无需切换");
                return;
            }

            int hz;
            int ret = Native.ApplyMode(dev, _gameW, _gameH, _gameHz, out hz);
            if (ret == 0)
            {
                Switched = true; SwitchCount++;
                LastActionTime = DateTime.Now.ToString("HH:mm:ss");
                _failStreak = 0;
                Log("已切换桌面分辨率 → " + _gameW + "×" + _gameH + " @" + hz + "Hz");
            }
            else
            {
                _failStreak++;
                _retryAt = DateTime.Now.AddSeconds(_failStreak >= 3 ? 30 : 5);
                Log("切换失败：" + Native.ErrText(ret));
            }
        }

        private void DoRestore()
        {
            if (!Switched && !_savedValid)
            {
                Log("当前没有需要恢复的显示模式");
                return;
            }
            if (DateTime.Now < _retryAt) return;

            // 切到哪块屏，就回到哪块屏；中途改目标屏不会把旧屏的分辨率套到新屏
            string dev = (_switchDev != null && _switchDev.Length > 0) ? _switchDev : TargetDevice();

            int w, h, hz;
            if (_savedValid)
            {
                w = _saved.dmPelsWidth; h = _saved.dmPelsHeight; hz = _saved.dmDisplayFrequency;
            }
            else
            {
                w = _deskW; h = _deskH; hz = _deskHz;
            }
            int gotHz;
            int ret = Native.ApplyMode(dev, w, h, hz, out gotHz);
            if (ret == 0)
            {
                Switched = false; _savedValid = false; _switchDev = ""; _failStreak = 0;
                LastActionTime = DateTime.Now.ToString("HH:mm:ss");
                Log("已恢复桌面分辨率 → " + w + "×" + h + " @" + gotHz + "Hz");
            }
            else
            {
                _failStreak++;
                _retryAt = DateTime.Now.AddSeconds(_failStreak >= 3 ? 30 : 5);
                Log("恢复失败：" + Native.ErrText(ret));
            }
        }

        private string TargetDevice()
        {
            if (_target != null && _target.Length > 0) return _target;
            if (_dev != null && _dev.Length > 0) return _dev;
            return @"\\.\DISPLAY1";
        }

        public void ForceRestore()
        {
            _retryAt = DateTime.MinValue;
            _failStreak = 0;
            DoRestore();
        }

        public void Log(string msg)
        {
            LastMsg = msg;
            lock (_logLock)
            {
                try
                {
                    string p = Settings.LogPath();
                    if (File.Exists(p) && new FileInfo(p).Length > 262144) File.Delete(p);
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            }
        }
    }

    // ================= 浅色玻璃按钮 =================
    public class GlassButton : Button
    {
        private bool hover, pressed;
        public Color BtnColor { get; set; }
        public bool Primary { get; set; }

        public GlassButton()
        {
            BtnColor = Color.FromArgb(0, 110, 200);
            Primary = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; pressed = false; Invalidate(); };
            MouseDown += delegate { pressed = true; Invalidate(); };
            MouseUp += delegate { pressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.FromArgb(244, 238, 229));

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ux.Round(r, 10))
            {
                if (Primary)
                {
                    int a = pressed ? 255 : (hover ? 240 : 225);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(a, BtnColor))) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 255))) g.DrawPath(p, path);
                    TextRenderer.DrawText(g, Text, Font, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    Color fill = pressed ? Color.FromArgb(255, 253, 248) : (hover ? Color.FromArgb(255, 253, 248) : Color.FromArgb(252, 250, 244));
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (Pen p = new Pen(Color.FromArgb(170, 160, 148, 132))) g.DrawPath(p, path);
                    TextRenderer.DrawText(g, Text, Font, r, Color.FromArgb(38, 34, 29), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    // ================= 浅色下拉框 =================
    public class LightCombo : ComboBox
    {
        public LightCombo()
        {
            FlatStyle = FlatStyle.Flat;
            DropDownStyle = ComboBoxStyle.DropDownList;
            BackColor = Color.FromArgb(255, 253, 248);
            ForeColor = Color.FromArgb(60, 56, 50);
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 20;
            DropDownHeight = 240;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = ((e.State & DrawItemState.Selected) != 0) && DroppedDown;
            Color bg = sel ? Color.FromArgb(0, 110, 200) : Color.FromArgb(255, 253, 248);
            Color fg = sel ? Color.White : Color.FromArgb(60, 56, 50);
            using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font, e.Bounds, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    // ================= 绘制辅助 =================
    public static class Ux
    {
        public static readonly Color Cream = Color.FromArgb(247, 242, 233);
        public static readonly Color TextDark = Color.FromArgb(42, 39, 35);
        public static readonly Color TextMid = Color.FromArgb(88, 82, 74);
        public static readonly Color TextLight = Color.FromArgb(126, 118, 107);
        public static readonly Color Accent = Color.FromArgb(0, 110, 200);
        public static readonly Color Good = Color.FromArgb(22, 150, 90);
        public static readonly Color Warn = Color.FromArgb(200, 130, 20);
        public static readonly Color Border = Color.FromArgb(70, 180, 170, 155);

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad < 1) rad = 1;
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 卡片用不透明深蓝底：背景噪点不再透上来，字不会被花底影响清晰度
        public static void Card(Graphics g, Rectangle r, int rad, int alpha)
        {
            using (GraphicsPath path = Round(r, rad))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, r.Height + 1),
                    Color.FromArgb(234, 28, 30, 34), Color.FromArgb(246, 13, 15, 18), LinearGradientMode.Vertical))
                    g.FillPath(b, path);
                using (Pen pen = new Pen(Color.FromArgb(32, 168, 176, 196), 1f))
                    g.DrawPath(pen, path);
            }
        }

        public static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, s, f, r, c, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | extra);
        }
    }

    // ================= 界面基础 =================
    public static class UC
    {
        public static readonly Color BgTop = Color.FromArgb(17, 18, 21);
        public static readonly Color BgBot = Color.FromArgb(6, 7, 9);
        public static Color CardBg = Color.FromArgb(26, 255, 255, 255);
        public static Color Border = Color.FromArgb(48, 255, 255, 255);
        public static Color Ink = Color.FromArgb(238, 241, 246);
        public static Color Ink2 = Color.FromArgb(196, 202, 212);
        public static Color Ink3 = Color.FromArgb(142, 147, 158);
        public static Color Accent = Color.FromArgb(96, 200, 232);
        public static Color AccentSoft = Color.FromArgb(22, 44, 54);
        public static Color Good = Color.FromArgb(108, 194, 226);
        public static Color Ink2L = Color.FromArgb(174, 180, 192);
        public static bool Light;

        // 主题切换只改这一组语义色；深色/米白的所有控件都从这里取色
        public static void ApplyTheme(bool light)
        {
            Light = light;
            if (light)
            {
                CardBg = Color.FromArgb(252, 248, 240);
                Border = Color.FromArgb(168, 206, 192, 168);
                Ink = Color.FromArgb(43, 42, 38);
                Ink2 = Color.FromArgb(78, 74, 66);
                Ink3 = Color.FromArgb(122, 116, 104);
                Ink2L = Color.FromArgb(94, 89, 79);
                Accent = Color.FromArgb(38, 112, 124);
                AccentSoft = Color.FromArgb(222, 236, 236);
                Good = Color.FromArgb(62, 132, 98);
            }
            else
            {
                CardBg = Color.FromArgb(26, 255, 255, 255);
                Border = Color.FromArgb(48, 255, 255, 255);
                Ink = Color.FromArgb(238, 241, 246);
                Ink2 = Color.FromArgb(196, 202, 212);
                Ink3 = Color.FromArgb(142, 147, 158);
                Ink2L = Color.FromArgb(174, 180, 192);
                Accent = Color.FromArgb(96, 200, 232);
                AccentSoft = Color.FromArgb(22, 44, 54);
                Good = Color.FromArgb(108, 194, 226);
            }
        }

        // 内置字体随 exe 分发：解压到本地后注册给 GDI，任何电脑、任何缩放比都是同一套字形，
        // 并且文字按“整数设备像素”栅格化，不会因为缩放矩阵而发虚
        public static readonly string FontDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeltaResAuto", "fonts");

        static readonly string[] fontFiles = new string[] { "mis_r.ttf", "mis_m.ttf" };

        public static readonly System.Drawing.Text.PrivateFontCollection UiFonts =
            new System.Drawing.Text.PrivateFontCollection();

        static readonly bool fontsReady = PrepareFonts();

        static bool PrepareFonts()
        {
            try
            {
                System.IO.Directory.CreateDirectory(FontDir);
                System.Reflection.Assembly asm = typeof(UC).Assembly;
                bool ok = true;
                foreach (string n in fontFiles)
                {
                    string dst = System.IO.Path.Combine(FontDir, n);
                    using (System.IO.Stream s = asm.GetManifestResourceStream(n))
                    {
                        if (s == null) { ok = false; continue; }
                        bool need = true;
                        if (System.IO.File.Exists(dst))
                            need = new System.IO.FileInfo(dst).Length != s.Length;
                        if (need)
                        {
                            byte[] buf = new byte[s.Length];
                            s.Read(buf, 0, buf.Length);
                            System.IO.File.WriteAllBytes(dst, buf);
                        }
                    }
                    if (System.IO.File.Exists(dst))
                    {
                        AddFontResourceEx(dst, 0x10, IntPtr.Zero);
                        try { UiFonts.AddFontFile(dst); } catch { ok = false; }
                    }
                }
                return ok && UiFonts.Families.Length > 0;
            }
            catch { return false; }
        }

        public static Font F(string fam, float size, FontStyle st)
        {
            try
            {
                foreach (FontFamily ff in UiFonts.Families)
                    if (string.Equals(ff.Name, fam, StringComparison.OrdinalIgnoreCase))
                        return new Font(ff, size, st, GraphicsUnit.Point);
            }
            catch { }
            try
            {
                Font nf = new Font(fam, size, st, GraphicsUnit.Point);
                if (string.Equals(nf.FontFamily.Name, fam, StringComparison.OrdinalIgnoreCase)) return nf;
                nf.Dispose();
            }
            catch { }
            try { return new Font("微软雅黑", size, st, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, size, st, GraphicsUnit.Point); }
        }

        public static readonly Font Title = F("MiSans Medium", 15f, FontStyle.Regular);
        public static readonly Font Big = F("MiSans", 14f, FontStyle.Regular);
        public static readonly Font Body = F("MiSans", 10.5f, FontStyle.Regular);
        public static readonly Font Med = F("MiSans Medium", 10.5f, FontStyle.Regular);
        public static readonly Font Small = F("MiSans", 9.6f, FontStyle.Regular);
        public static readonly Font Tiny = F("MiSans", 9.1f, FontStyle.Regular);
        public static readonly Font BtnF = F("MiSans Medium", 11f, FontStyle.Regular);

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad < 1) rad = 1;
            if (r.Width < rad * 2) rad = r.Width / 2;
            if (r.Height < rad * 2) rad = r.Height / 2;
            if (rad < 1) rad = 1;
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void SoftShadow(Graphics g, Rectangle r, int rad, int strength)
        {
            for (int i = 5; i >= 1; i--)
            {
                Rectangle rr = new Rectangle(r.X - i, r.Y - i + 2, r.Width + i * 2, r.Height + i * 2);
                using (GraphicsPath p = Round(rr, rad + i))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(strength, 0, 0, 0)))
                    g.FillPath(b, p);
            }
        }

        // 玻璃卡片：只有一层柔和的纵向色阶，不画描边、不叠阴影，避免任何边缘痕迹
        public static void Card(Graphics g, Rectangle r, int rad)
        {
            using (GraphicsPath p = Round(r, rad))
            {
                Color top = Light ? Color.FromArgb(252, 249, 242) : Color.FromArgb(234, 28, 30, 34);
                Color bot = Light ? Color.FromArgb(242, 235, 222) : Color.FromArgb(246, 13, 15, 18);
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(r.X, r.Y, r.Width, r.Height + 1),
                    top, bot, LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Light ? Color.FromArgb(238, 130, 114, 90) : Color.FromArgb(40, 168, 176, 196), 1f))
                    g.DrawPath(pen, p);
                if (!Light)
                {
                    using (Pen hp = new Pen(Color.FromArgb(10, 240, 246, 255), 1f))
                        g.DrawLine(hp, r.X + rad + 3, r.Y + 1, r.Right - rad - 3, r.Y + 1);
                }
            }
        }

        // 文字用 GDI 渲染并打开网格拟合，笔画锐利清晰；深色底上层次分明
        // 全局界面缩放比：按屏幕 DPI 计算，整窗与字体一起放大，避免高分屏下字挤爆控件
        public static float S = 1f;

        static readonly System.Collections.Generic.Dictionary<string, Font> fontCache =
            new System.Collections.Generic.Dictionary<string, Font>();

        // 把点值字体换算成“设计像素”尺寸：GDI+ 的像素字体不受屏幕 DPI 影响，
        // 再交给缩放矩阵统一放大，不会出现字被算两次的情况
        public static Font FS(Font f)
        {
            if (f == null) return f;
            float px = f.SizeInPoints * 96f / 72f;
            string key = f.FontFamily.Name + "|" + f.Style.ToString() + "|" + px.ToString("0.##");
            Font got = null;
            if (fontCache.TryGetValue(key, out got)) return got;
            Font nf;
            try { nf = new Font(f.FontFamily, px, f.Style, GraphicsUnit.Pixel); }
            catch { nf = new Font("Microsoft YaHei UI", px, f.Style, GraphicsUnit.Pixel); }
            fontCache[key] = nf;
            return nf;
        }

        public static int PX(int v) { return Math.Abs(S - 1f) <= 0.02f ? v : (int)Math.Round(v * S); }

        public static Rectangle R(Rectangle r)
        {
            if (Math.Abs(S - 1f) <= 0.02f) return r;
            return new Rectangle((int)Math.Round(r.X * S), (int)Math.Round(r.Y * S),
                (int)Math.Round(r.Width * S), (int)Math.Round(r.Height * S));
        }

        // ================= 文字：走原生 GDI，按设备像素出字 =================
        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr CreateFont(int h, int w, int esc, int orient, int weight, int italic, int underline,
            int strike, int charset, int outp, int clip, int quality, int pitch, string face);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern int SetBkMode(IntPtr hdc, int mode);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern int SetTextColor(IntPtr hdc, int color);
        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int AddFontResourceEx(string name, int fl, IntPtr res);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int DrawText(IntPtr hdc, string text, int count, ref RECT rect, int flags);

        struct RECT { public int left, top, right, bottom; }

        static readonly System.Collections.Generic.Dictionary<string, IntPtr> hfonts =
            new System.Collections.Generic.Dictionary<string, IntPtr>();

        static IntPtr HFont(string face, int px, int weight)
        {
            string key = face + "|" + px.ToString() + "|" + weight.ToString();
            IntPtr h;
            if (hfonts.TryGetValue(key, out h)) return h;
            h = CreateFont(-px, 0, 0, 0, weight, 0, 0, 0, 134, 0, 0, 5, 0, face);
            hfonts[key] = h;
            return h;
        }

        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl)
        {
            if (string.IsNullOrEmpty(s)) return;

            float k = Math.Abs(S - 1f) <= 0.02f ? 1f : S;
            int px = (int)Math.Round(f.SizeInPoints * 96f / 72f * k);
            if (px < 6) px = 6;
            int weight = (f.Style & FontStyle.Bold) != 0 ? 700 : 400;

            int flags = 0x20 /*DT_SINGLELINE*/ | 0x800 /*DT_NOPREFIX*/;
            if ((fl & TextFormatFlags.VerticalCenter) != 0) flags |= 0x4 /*DT_VCENTER*/;
            else if ((fl & TextFormatFlags.Bottom) != 0) flags |= 0x8 /*DT_BOTTOM*/;
            if ((fl & TextFormatFlags.Right) != 0) flags |= 0x2 /*DT_RIGHT*/;
            else if ((fl & TextFormatFlags.HorizontalCenter) != 0) flags |= 0x1 /*DT_CENTER*/;
            if ((fl & TextFormatFlags.EndEllipsis) != 0) flags |= 0x8000 /*DT_END_ELLIPSIS*/;

            RECT rc = new RECT();
            rc.left = (int)Math.Round(r.X * k);
            rc.top = (int)Math.Round(r.Y * k);
            rc.right = (int)Math.Round((r.X + r.Width) * k);
            rc.bottom = (int)Math.Round((r.Y + r.Height) * k);

            IntPtr hf = HFont(f.FontFamily.Name, px, weight);
            IntPtr hdc = g.GetHdc();
            try
            {
                SetBkMode(hdc, 1 /*TRANSPARENT*/);
                SetTextColor(hdc, (c.B << 16) | (c.G << 8) | c.R);
                IntPtr prev = SelectObject(hdc, hf);
                DrawText(hdc, s, -1, ref rc, flags);
                SelectObject(hdc, prev);
            }
            finally { g.ReleaseHdc(hdc); }
        }

        public static void Chevron(Graphics g, int x, int y, Color c)
        {
            using (Pen p = new Pen(c, 1.6f))
            {
                g.DrawLine(p, x - 4, y - 1, x, y + 3);
                g.DrawLine(p, x, y + 3, x + 4, y - 1);
            }
        }
    }

    // ================= 按钮 =================
    public class NiceBtn : Control
    {
        public int Kind;      // 0 主按钮 1 次按钮 2 文字按钮
        private bool hov;

        public NiceBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Font = UC.BtnF;
            Cursor = Cursors.Hand;
        }

        // 小节标签：左侧一枚青色短标，右侧文字
        private void SectionLabel(Graphics g, string text, Rectangle rc)
        {
            int cy = rc.Y + rc.Height / 2;
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, UC.Accent)))
                g.FillRectangle(tb, rc.X, cy - 5, 2, 10);
            UC.Text(g, text, UC.Small, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnMouseEnter(EventArgs e) { hov = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hov = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            MainForm mf = FindForm() as MainForm;
            if (mf != null) mf.Backdrop(g, this);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Kind == 0)
            {
                Color c1 = hov ? Color.FromArgb(132, 170, 255) : Color.FromArgb(104, 146, 255);
                Color c2 = hov ? Color.FromArgb(66, 108, 236) : Color.FromArgb(52, 92, 222);
                using (GraphicsPath p = UC.Round(r, 12))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, Width, Height + 1), c1, c2, LinearGradientMode.Vertical))
                        g.FillPath(b, p);
                }
                UC.Text(g, Text, Font, new Rectangle(0, 0, Width, Height), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else if (Kind == 1)
            {
                using (GraphicsPath p = UC.Round(r, 12))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(hov ? 24 : 17, 255, 255, 255))) g.FillPath(b, p);
                }
                UC.Text(g, Text, Font, new Rectangle(0, 0, Width, Height), hov ? Color.FromArgb(255, 255, 255) : Color.FromArgb(216, 221, 232),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                using (GraphicsPath p = UC.Round(r, 9))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(hov ? 24 : 15, 255, 255, 255))) g.FillPath(b, p);
                }
                UC.Text(g, Text, UC.Small, new Rectangle(0, 0, Width, Height), hov ? Color.FromArgb(130, 172, 255) : UC.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    // ================= 开关 =================
    public class UToggle : Control
    {
        private bool _on;
        public event EventHandler Changed;

        public UToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size(40, 20);
            Cursor = Cursors.Hand;
        }

        // 小节标签：左侧一枚青色短标，右侧文字
        private void SectionLabel(Graphics g, string text, Rectangle rc)
        {
            int cy = rc.Y + rc.Height / 2;
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, UC.Accent)))
                g.FillRectangle(tb, rc.X, cy - 5, 2, 10);
            UC.Text(g, text, UC.Small, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        public bool On
        {
            get { return _on; }
            set { if (_on != value) { _on = value; Invalidate(); } }
        }

        protected override void OnClick(EventArgs e)
        {
            _on = !_on;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            MainForm mf = FindForm() as MainForm;
            if (mf != null) mf.Backdrop(g, this);

            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = UC.Round(track, Height / 2))
            using (SolidBrush b = new SolidBrush(_on ? Color.FromArgb(76, 112, 214) : Color.FromArgb(16, 255, 255, 255)))
                g.FillPath(b, p);
            int d = Height - 6;
            Rectangle knob = new Rectangle(_on ? Width - d - 3 : 3, 3, d, d);
            using (GraphicsPath p = UC.Round(knob, d / 2))
            using (SolidBrush b = new SolidBrush(_on ? Color.FromArgb(240, 244, 255) : Color.FromArgb(130, 136, 150)))
                g.FillPath(b, p);
        }
    }

    // ================= 下拉选择 =================
    public class UPicker : Control
    {
        public bool Plain;          // true：只显示文字 + 箭头（用于卡片内）
        public Color Surface = Color.Empty; // 位于卡片内时，直接铺该底色
        public string[] Items = new string[0];
        public int Index;
        public event EventHandler Changed;
        private bool hov;

        public UPicker()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Font = UC.Body;
            Cursor = Cursors.Hand;
        }

        public string Value
        {
            get { return (Index >= 0 && Index < Items.Length) ? Items[Index] : ""; }
        }

        public void SetItems(string[] items, string want)
        {
            Items = items;
            int idx = Array.IndexOf(items, want);
            Index = idx >= 0 ? idx : 0;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hov = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hov = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Items.Length == 0) return;

            ContextMenuStrip cm = new ContextMenuStrip();
            cm.Font = UC.Body;
            cm.BackColor = Color.FromArgb(22, 24, 28);
            cm.ForeColor = Color.FromArgb(230, 235, 242);
            cm.ShowImageMargin = false;
            cm.Renderer = new ToolStripProfessionalRenderer(new CreamColors());
            cm.MaximumSize = new Size(240, 420);
            for (int i = 0; i < Items.Length; i++)
            {
                int idx = i;
                ToolStripMenuItem it = new ToolStripMenuItem(Items[i]);
                it.Checked = (i == Index);
                it.Click += delegate
                {
                    Index = idx;
                    Invalidate();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                };
                cm.Items.Add(it);
            }
            cm.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Surface != Color.Empty)
            {
                using (SolidBrush sb = new SolidBrush(Surface)) g.FillRectangle(sb, ClientRectangle);
            }
            else
            {
                MainForm mf = FindForm() as MainForm;
                if (mf != null) mf.Backdrop(g, this);
            }

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (!Plain)
            {
                using (GraphicsPath p = UC.Round(r, 10))
                {
                    using (SolidBrush b = new SolidBrush(hov ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(16, 255, 255, 255))) g.FillPath(b, p);
                }
            }
            UC.Chevron(g, Width - 12, Height / 2 - 1, UC.Ink3);
            UC.Text(g, Value, Font, new Rectangle(12, 0, Width - 34, Height), hov ? UC.Ink : UC.Ink2,
                TextFormatFlags.VerticalCenter | (Plain ? TextFormatFlags.Right : TextFormatFlags.Left));
        }
    }

    public class CreamColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(48, 54, 66); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(48, 54, 66); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(48, 54, 66); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(72, 78, 92); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(22, 24, 28); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(22, 24, 28); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(22, 24, 28); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(22, 24, 28); } }
        public override Color MenuBorder { get { return Color.FromArgb(74, 80, 94); } }
    }

    // ================= 游戏选择行 =================
    public class UGamePick : Control
    {
        public string Caption = "";
        public string Sub = "";
        private bool hov;

        public UGamePick()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }

        // 小节标签：左侧一枚青色短标，右侧文字
        private void SectionLabel(Graphics g, string text, Rectangle rc)
        {
            int cy = rc.Y + rc.Height / 2;
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, UC.Accent)))
                g.FillRectangle(tb, rc.X, cy - 5, 2, 10);
            UC.Text(g, text, UC.Small, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnMouseEnter(EventArgs e) { hov = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hov = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            MainForm mf = FindForm() as MainForm;
            if (mf != null) mf.Backdrop(g, this);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            UC.SoftShadow(g, r, 12, 5);
            using (GraphicsPath p = UC.Round(r, 12))
            {
                using (SolidBrush b = new SolidBrush(hov ? Color.FromArgb(36, 250, 245, 215) : Color.FromArgb(22, 250, 245, 215))) g.FillPath(b, p);
            }

            Rectangle ic = new Rectangle(16, (Height - 30) / 2, 30, 30);
            using (GraphicsPath p = UC.Round(ic, 9))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 58, 70))) g.FillPath(b, p);
            }
            // 显示器图标
            using (Pen pen = new Pen(Color.FromArgb(118, 190, 220), 1.6f))
            {
                Rectangle scr = new Rectangle(ic.X + 8, ic.Y + 8, 14, 10);
                using (GraphicsPath p = UC.Round(scr, 3)) g.DrawPath(pen, p);
                g.DrawLine(pen, ic.X + 15, ic.Y + 18, ic.X + 15, ic.Y + 22);
                g.DrawLine(pen, ic.X + 11, ic.Y + 22, ic.X + 19, ic.Y + 22);
            }

            UC.Text(g, Caption, UC.Med, new Rectangle(56, Height / 2 - 19, Width - 96, 20), UC.Ink,
                TextFormatFlags.VerticalCenter);
            UC.Text(g, Sub, UC.Tiny, new Rectangle(56, Height / 2 + 1, Width - 96, 18), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
            UC.Chevron(g, Width - 22, Height / 2 - 1, UC.Ink3);
        }
    }

    // ================= 窗口按钮 =================
    public class IconBtn : Control
    {
        public bool IsClose;
        private bool hov;

        public IconBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size(28, 28);
            Cursor = Cursors.Hand;
        }

        // 小节标签：左侧一枚青色短标，右侧文字
        private void SectionLabel(Graphics g, string text, Rectangle rc)
        {
            int cy = rc.Y + rc.Height / 2;
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, UC.Accent)))
                g.FillRectangle(tb, rc.X, cy - 5, 2, 10);
            UC.Text(g, text, UC.Small, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnMouseEnter(EventArgs e) { hov = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hov = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            MainForm mf = FindForm() as MainForm;
            if (mf != null) mf.Backdrop(g, this);

            Color c = hov
                ? (IsClose ? Color.FromArgb(242, 100, 96) : Color.FromArgb(246, 247, 251))
                : Color.FromArgb(172, 177, 190);
            int cx = Width / 2, cy = Height / 2;
            using (Pen p = new Pen(c, 1.4f))
            {
                if (IsClose)
                {
                    g.DrawLine(p, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(p, cx + 4, cy - 4, cx - 4, cy + 4);
                }
                else
                {
                    g.DrawLine(p, cx - 4, cy, cx + 4, cy);
                }
            }
        }
    }

    // ================= 下拉浮层 =================
    public class UxMenu : Form
    {
        private readonly string[] titles;
        private readonly string[] subs;
        private readonly int sel;
        private readonly Action<int> pick;
        private readonly int boxW;
        private readonly int rowH;

        private int top;
        private int hot = -1;
        private int viewH;
        private int contentH;
        private int shownAt;

        private const int PadY = 8;
        private const int MaxView = 372;

        private static readonly Font fItem = UC.F("Noto Sans SC", 9.6f, FontStyle.Regular);
        private static readonly Font fSel = UC.F("Noto Sans SC Medium", 9.8f, FontStyle.Regular);
        private static readonly Font fSub = UC.F("Noto Sans SC", 8.4f, FontStyle.Regular);

        public UxMenu(string[] titles, string[] subs, int sel, int boxW, Action<int> pick)
        {
            this.titles = titles;
            this.subs = subs;
            this.sel = sel;
            this.pick = pick;
            this.boxW = boxW;
            this.rowH = (subs != null && subs.Length > 0) ? 46 : 32;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = UC.Light ? Color.FromArgb(246, 241, 232) : Color.FromArgb(24, 26, 33);
            Font = fItem;

            contentH = titles.Length * rowH;
            int h = PadY * 2 + Math.Min(contentH, MaxView);
            ClientSize = new Size(UC.PX(boxW), UC.PX(h));
            viewH = h - PadY * 2;
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
                int light = UC.Light ? 1 : 0;
                Native.DwmSetWindowAttribute(Handle, 20, ref light, 4);
            }
            catch { }
        }

        public void ShowAt(Form owner, Rectangle anchor)
        {
            Rectangle wa = Screen.FromRectangle(anchor).WorkingArea;
            int x = anchor.Left;
            int y = anchor.Bottom + 6;
            if (y + Height > wa.Bottom - 8) y = anchor.Top - Height - 6;
            if (y < wa.Top + 8) y = wa.Top + 8;
            if (x + Width > wa.Right - 8) x = wa.Right - Width - 8;
            if (x < wa.Left + 8) x = wa.Left + 8;
            Location = new Point(x, y);
            Show(owner);
            shownAt = Environment.TickCount;
        }

        private int RowAt(Point p)
        {
            if (Math.Abs(UC.S - 1f) > 0.02f) p = new Point((int)Math.Round(p.X / UC.S), (int)Math.Round(p.Y / UC.S));
            int y = p.Y - PadY + top;
            if (y < 0 || y >= contentH) return -1;
            int i = y / rowH;
            return (i >= 0 && i < titles.Length) ? i : -1;
        }

        // 小节标签：左侧一枚青色短标，右侧文字
        private void SectionLabel(Graphics g, string text, Rectangle rc)
        {
            int cy = rc.Y + rc.Height / 2;
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, UC.Accent)))
                g.FillRectangle(tb, rc.X, cy - 5, 2, 10);
            UC.Text(g, text, UC.Small, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if (Math.Abs(UC.S - 1f) > 0.02f) g.ScaleTransform(UC.S, UC.S);

            Rectangle full = new Rectangle(0, 0, boxW, PadY * 2 + viewH);
            Color topC = UC.Light ? Color.FromArgb(251, 247, 239) : Color.FromArgb(34, 36, 44);
            Color botC = UC.Light ? Color.FromArgb(238, 230, 216) : Color.FromArgb(22, 24, 30);
            using (LinearGradientBrush b = new LinearGradientBrush(full, topC, botC, LinearGradientMode.Vertical))
            {
                b.GammaCorrection = true;
                g.FillRectangle(b, full);
            }

            g.SetClip(new Rectangle(0, PadY, boxW, viewH));

            int first = Math.Max(0, top / rowH);
            int last = Math.Min(titles.Length - 1, (top + viewH) / rowH + 1);

            for (int i = first; i <= last; i++)
            {
                Rectangle r = new Rectangle(6, PadY + i * rowH - top, boxW - 12, rowH - 2);
                bool isSel = (i == sel);
                bool isHot = (i == hot);

                if (isHot || isSel)
                {
                    Color rowBg = UC.Light
                        ? (isHot ? Color.FromArgb(232, 224, 208) : Color.FromArgb(238, 230, 216))
                        : (isHot ? Color.FromArgb(40, 48, 64) : Color.FromArgb(28, 35, 48));
                    using (GraphicsPath p = UC.Round(r, 8))
                    using (SolidBrush b = new SolidBrush(rowBg))
                        g.FillPath(b, p);
                }

                if (subs != null && subs.Length > 0)
                {
                    UC.Text(g, titles[i], isSel ? fSel : fItem, new Rectangle(r.X + 12, r.Y + 4, r.Width - 46, 22),
                        isSel ? UC.Ink : (UC.Light ? UC.Ink : Color.FromArgb(230, 235, 242)), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    UC.Text(g, subs[i], fSub, new Rectangle(r.X + 12, r.Y + 22, r.Width - 46, 16),
                        UC.Light ? UC.Ink3 : Color.FromArgb(148, 154, 164), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    UC.Text(g, titles[i], isSel ? fSel : fItem, new Rectangle(r.X + 12, r.Y, r.Width - 46, r.Height),
                        isSel ? UC.Ink : (UC.Light ? UC.Ink : Color.FromArgb(224, 230, 238)), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                if (isSel)
                {
                    using (SolidBrush b = new SolidBrush(UC.Accent))
                        g.FillEllipse(b, r.Right - 20, r.Y + r.Height / 2 - 3, 6, 6);
                }
            }

            g.ResetClip();

            if (contentH > viewH)
            {
                int trackH = viewH - 10;
                int thumbH = Math.Max(30, (int)(trackH * (double)viewH / contentH));
                int maxTop = contentH - viewH;
                int ty = PadY + 5 + (maxTop <= 0 ? 0 : (int)((trackH - thumbH) * (double)top / maxTop));
                using (GraphicsPath p = UC.Round(new Rectangle(boxW - 8, ty, 3, thumbH), 2))
                using (SolidBrush b = new SolidBrush(UC.Light ? Color.FromArgb(110, 70, 62, 50) : Color.FromArgb(84, 255, 255, 255)))
                    g.FillPath(b, p);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = RowAt(e.Location);
            if (i != hot)
            {
                hot = i;
                Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot >= 0) { hot = -1; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = RowAt(e.Location);
            if (i < 0) return;
            if (pick != null) pick(i);
            Close();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (contentH <= viewH) return;
            top -= (e.Delta / 120) * rowH;
            if (top < 0) top = 0;
            if (top > contentH - viewH) top = contentH - viewH;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Environment.TickCount - shownAt < 120) return;
            Close();
        }
    }

    // ================= 面板 =================
    public class MainForm : Form
    {
        private Engine eng;
        private Settings cfg;
        private string dev;

        private string[] delayList = new string[] { "立即", "2 秒", "3 秒", "5 秒" };
        private string[] pollList = new string[] { "1 秒", "2 秒", "3 秒" };
        private int delayIdx, pollIdx;
        private string[] resList = new string[0];
        private int gameResIdx, deskResIdx;
        private string[] hzList = new string[] { "跟随系统" };
        private int hzIdx;
        private string procText = "";
        private System.Windows.Forms.Timer uiTimer;

        // 所有控件都在窗口里一次性画完：没有子控件就没有背景接缝，也就没有毛边
        private string gameCaption = "", gameSub = "";
        private string rescanText = "重新检测";
        private bool rescanReady = true;
        private bool tgAutoOn, tgPauseOn;
        private string hotId = "", downId = "";

        private readonly Rectangle rcMin = new Rectangle(338, 10, 32, 32);
        private readonly Rectangle rcClose = new Rectangle(376, 10, 32, 32);
        private readonly Rectangle rcGame = new Rectangle(20, 214, 380, 54);
        private readonly Rectangle rcRescan = new Rectangle(288, 178, 100, 24);
        private readonly Rectangle rcAdd = new Rectangle(190, 178, 90, 24);
        private readonly Rectangle rcProc = new Rectangle(20, 332, 380, 40);
        private readonly Rectangle rcApply = new Rectangle(20, 597, 380, 44);
        private readonly Rectangle rcRestore = new Rectangle(20, 653, 380, 36);
        private readonly Rectangle rcTgAuto = new Rectangle(116, 705, 40, 20);
        private readonly Rectangle rcTgPause = new Rectangle(302, 705, 40, 20);
        private readonly Rectangle rcGameRes = new Rectangle(220, 392, 184, 34);
        private readonly Rectangle rcGameHz = new Rectangle(220, 442, 184, 34);
        private readonly Rectangle rcDeskRes = new Rectangle(220, 492, 184, 34);
        private readonly Rectangle rcDelay = new Rectangle(112, 551, 78, 32);
        private readonly Rectangle rcPoll = new Rectangle(294, 551, 78, 32);

        private List<GameInfo> games = new List<GameInfo>();
        private volatile bool scanning;
        private string scanMsg = "";

        private string sTitle = "", sSub = "", sCount = "", sLog = "", sRes = "", sHz = "";
        private Color cState = UC.Good;

        private Icon icoSmall, icoBig;

        private const int W = 420;
        private const int H = 849;

        // 系统 DPI 对应的自动缩放比（界面里选“自动”时用它）
        public static float DpiScale = 1f;

        private readonly Rectangle rcScale = new Rectangle(234, 294, 88, 24);
        private readonly Rectangle rcScreen = new Rectangle(146, 294, 84, 24);
        private readonly Rectangle rcTheme = new Rectangle(326, 294, 82, 24);

        private static int UISize(int v) { return UC.PX(v); }

        // 窗口不能超出屏幕，超了就自动收一点
        private static void ClampScaleToScreen()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            float capW = (wa.Width - 24) / (float)W;
            float capH = (wa.Height - 24) / (float)H;
            float cap = Math.Min(capW, capH);
            if (UC.S > cap && cap >= 0.5f) UC.S = (float)Math.Floor(cap * 20f) / 20f;
        }

        // 鼠标设备坐标 → 设计坐标
        private static Point LPoint(Point p)
        {
            if (Math.Abs(UC.S - 1f) <= 0.02f) return p;
            return new Point((int)Math.Round(p.X / UC.S), (int)Math.Round(p.Y / UC.S));
        }

        private readonly Rectangle rcStatus = new Rectangle(20, 68, 380, 96);
        private readonly Rectangle rcRes = new Rectangle(20, 388, 380, 148);
        private readonly Rectangle rcLog = new Rectangle(20, 763, 380, 44);
        private Bitmap bgCache;
        private static Image BgImage;

        static MainForm()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("bg_theme.jpg"))
                    if (s != null) BgImage = Image.FromStream(s);
            }
            catch { }
        }

        public MainForm(Engine engine, Settings settings)
        {
            eng = engine; cfg = settings;
            UC.ApplyTheme(cfg.Theme == 2);
            cState = UC.Good;
            dev = cfg.DisplayDevice != null && cfg.DisplayDevice.Length > 0 ? cfg.DisplayDevice : Native.PrimaryDevice();
            if (dev == null) dev = @"\\.\DISPLAY1";

            Text = "自动检测分辨率";
            AutoScaleMode = AutoScaleMode.None;      // 手动按 DPI 缩放，绝不让 WinForms 自己按字体算
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClampScaleToScreen();
            ClientSize = new Size(UISize(W), UISize(H));
            MaximizeBox = false;
            MinimizeBox = false;
            Font = UC.Body;
            DoubleBuffered = true;
            KeyPreview = true;

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("app.ico"))
                    if (s != null) Icon = new Icon(s, new Size(32, 32));
            }
            catch { }

            BuildUI();
            BuildBgCache();

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 800;
            uiTimer.Tick += delegate { RefreshState(); };
            uiTimer.Start();
            RefreshState();
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // 用真正的小尺寸图标替换 WinForms 自动缩放出来的模糊版本
                Size smallSz = SystemInformation.SmallIconSize;
                Size bigSz = SystemInformation.IconSize;
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s1 = asm.GetManifestResourceStream("app.ico"))
                    if (s1 != null) icoSmall = new Icon(s1, smallSz);
                using (Stream s2 = asm.GetManifestResourceStream("app.ico"))
                    if (s2 != null) icoBig = new Icon(s2, bigSz);
                if (icoSmall != null) Native.SendMessage(Handle, 0x0080, IntPtr.Zero, icoSmall.Handle);
                if (icoBig != null) Native.SendMessage(Handle, 0x0080, (IntPtr)1, icoBig.Handle);
            }
            catch { }
            try
            {
                int light = 0;
                Native.DwmSetWindowAttribute(Handle, 20, ref light, 4);
                int round = 2; // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
                Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
            }
            catch { }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) MinimizeToTray(false);
            base.OnKeyDown(e);
        }


        private string HitTest(Point p)
        {
            if (rcClose.Contains(p)) return "close";
            if (rcMin.Contains(p)) return "min";
            if (rcAdd.Contains(p)) return "addgame";
            if (rcScreen.Contains(p)) return "screen";
            if (rcScale.Contains(p)) return "scale";
            if (rcTheme.Contains(p)) return "theme";
            if (rcRescan.Contains(p)) return "rescan";
            if (rcApply.Contains(p)) return "apply";
            if (rcRestore.Contains(p)) return "restore";
            if (rcTgAuto.Contains(p)) return "tga";
            if (rcTgPause.Contains(p)) return "tgp";
            if (rcGame.Contains(p)) return "game";
            if (rcGameRes.Contains(p)) return "gres";
            if (rcGameHz.Contains(p)) return "ghz";
            if (rcDeskRes.Contains(p)) return "dres";
            if (rcDelay.Contains(p)) return "delay";
            if (rcPoll.Contains(p)) return "poll";
            return "";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string id = HitTest(LPoint(e.Location));
            if (id != hotId)
            {
                hotId = id;
                Cursor = id.Length > 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
            if (hotId.Length > 0 || downId.Length > 0)
            {
                hotId = "";
                downId = "";
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            string id = HitTest(LPoint(e.Location));
            if (id.Length == 0) return;
            downId = id;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            string was = downId;
            if (was.Length > 0)
            {
                downId = "";
                Invalidate();
            }
            if (was.Length > 0 && was == HitTest(LPoint(e.Location))) Fire(was);
        }

        private void Fire(string id)
        {
            switch (id)
            {
                case "close": MinimizeToTray(true); break;
                case "min": WindowState = FormWindowState.Minimized; break;
                case "rescan": if (rescanReady) StartScan(); break;
                case "addgame": AddGameFromFile(); break;
                case "scale": ShowScaleMenu(); break;
                case "theme": ShowThemeMenu(); break;
                case "screen": ShowScreenMenu(); break;
                case "apply": ApplyUI(); break;
                case "restore": eng.ForceRestore(); RefreshState(); break;
                case "tga":
                    tgAutoOn = !tgAutoOn;
                    Runtime.SetAutostart(tgAutoOn);
                    eng.Log(tgAutoOn ? "已开启开机自启（静默后台）" : "已关闭开机自启");
                    RefreshState();
                    break;
                case "tgp":
                    tgPauseOn = !tgPauseOn;
                    eng.Paused = tgPauseOn;
                    eng.Log(tgPauseOn ? "已暂停监听" : "已恢复监听");
                    RefreshState();
                    break;
                case "game": ShowGameMenu(); break;
                case "gres": ShowResMenu(true); break;
                case "dres": ShowResMenu(false); break;
                case "ghz": ShowHzMenu(); break;
                case "delay": ShowPick(true); break;
                case "poll": ShowPick(false); break;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ActiveControl = null;
        }

        // 子控件渲染时回填同一张背景，保证圆角无接缝
        public void Backdrop(Graphics g, Control c)
        {
            Rectangle r = c.ClientRectangle;
            GraphicsState st = g.Save();
            g.SetClip(new Rectangle(r.X, r.Y, r.Width + 1, r.Height + 1));
            g.TranslateTransform(-c.Left, -c.Top);
            PaintBg(g, new Rectangle(c.Left, c.Top, r.Width, r.Height));
            g.Restore(st);
        }

        private void PaintBg(Graphics g, Rectangle clip)
        {
            if (bgCache == null) BuildBgCache();
            g.DrawImage(bgCache, new Rectangle(0, 0, W, H), 0, 0, bgCache.Width, bgCache.Height, GraphicsUnit.Pixel);
        }

        private static int ClampIndex(int v, int max)
        {
            if (v < 0) return 0;
            if (v >= max) return max - 1;
            return v;
        }

        // 只柔化纹理层：卡片、文字、输入框仍然按原分辨率绘制，避免整层发糊
        private static Bitmap SoftTexture(Image src, int dw, int dh, int radius)
        {
            Bitmap bmp = new Bitmap(dw, dh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 9, 10, 8));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, dw, dh));
            }
            if (radius >= 1)
            {
                // 两次盒式模糊近似高斯，幅度很小，只去掉珠链的高频边缘
                BoxBlur(bmp, radius);
                BoxBlur(bmp, radius);
            }
            return bmp;
        }

        private static void BoxBlur(Bitmap bmp, int radius)
        {
            if (radius < 1) return;
            int w = bmp.Width, h = bmp.Height;
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);
            int stride = d.Stride;
            byte[] a = new byte[stride * h];
            byte[] b = new byte[a.Length];
            try
            {
                Marshal.Copy(d.Scan0, a, 0, a.Length);
                int win = radius * 2 + 1;

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    int sa = 0, sr = 0, sg = 0, sb = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int j = row + ClampIndex(k, w) * 4;
                        sb += a[j]; sg += a[j + 1]; sr += a[j + 2]; sa += a[j + 3];
                    }
                    for (int x = 0; x < w; x++)
                    {
                        int j = row + x * 4;
                        b[j] = (byte)(sb / win);
                        b[j + 1] = (byte)(sg / win);
                        b[j + 2] = (byte)(sr / win);
                        b[j + 3] = (byte)(sa / win);
                        int jl = row + ClampIndex(x - radius, w) * 4;
                        int jr = row + ClampIndex(x + radius + 1, w) * 4;
                        sb -= a[jl]; sg -= a[jl + 1]; sr -= a[jl + 2]; sa -= a[jl + 3];
                        sb += a[jr]; sg += a[jr + 1]; sr += a[jr + 2]; sa += a[jr + 3];
                    }
                }

                for (int x = 0; x < w; x++)
                {
                    int col = x * 4;
                    int sa = 0, sr = 0, sg = 0, sb = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int j = ClampIndex(k, h) * stride + col;
                        sb += b[j]; sg += b[j + 1]; sr += b[j + 2]; sa += b[j + 3];
                    }
                    for (int y = 0; y < h; y++)
                    {
                        int j = y * stride + col;
                        a[j] = (byte)(sb / win);
                        a[j + 1] = (byte)(sg / win);
                        a[j + 2] = (byte)(sr / win);
                        a[j + 3] = (byte)(sa / win);
                        int jt = ClampIndex(y - radius, h) * stride + col;
                        int jb = ClampIndex(y + radius + 1, h) * stride + col;
                        sb -= b[jt]; sg -= b[jt + 1]; sr -= b[jt + 2]; sa -= b[jt + 3];
                        sb += b[jb]; sg += b[jb + 1]; sr += b[jb + 2]; sa += b[jb + 3];
                    }
                }

                Marshal.Copy(a, 0, d.Scan0, a.Length);
            }
            finally
            {
                bmp.UnlockBits(d);
            }
        }

        // 背景：融合纹理图 + 顶部/整体光罩 + 逐像素柔光与抖动（既保留肌理又不抢字）
        private void BuildBgCache()
        {
            // cx, cy, rx, ry, 强度, R, G, B
            bool light = cfg != null && cfg.Theme == 2;
            double[] L = light ? new double[]
            {
                150, -40, 640, 520, 0.10, 255, 252, 244,  // 左上暖白
                40, -110, 420, 340, 0.06, 255, 255, 250, // 顶部柔光
                560, 900, 520, 470, 0.10, 220, 230, 228  // 右下冷灰
            } : new double[]
            {
                150, -40, 640, 520, 0.20, 84, 96, 120,     // 左上橄榄光
                40, -110, 420, 340, 0.10, 150, 160, 182, // 顶部高光
                560, 900, 520, 470, 0.22, 74, 132, 170   // 右下琥珀光
            };

            int dw = Math.Max(1, (int)Math.Round(W * UC.S));
            int dh = Math.Max(1, (int)Math.Round(H * UC.S));
            bgCache = new Bitmap(dw, dh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics gg = Graphics.FromImage(bgCache))
            {
                gg.Clear(light ? Color.FromArgb(255, 246, 241, 232) : Color.FromArgb(255, 9, 10, 8));
                gg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                if (light)
                {
                    using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Rectangle(0, 0, dw, dh),
                        Color.FromArgb(255, 251, 247, 239), Color.FromArgb(255, 234, 226, 212),
                        System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                        gg.FillRectangle(lg, 0, 0, dw, dh);
                }
                else if (BgImage != null && (cfg == null || cfg.Theme != 1))
                {
                    int softR = Math.Max(1, (int)Math.Round(1.6 * UC.S));
                    using (Bitmap soft = SoftTexture(BgImage, dw, dh, softR))
                        gg.DrawImage(soft, new Rectangle(0, 0, dw, dh));
                }
                else gg.Clear(Color.FromArgb(255, 10, 12, 16));

                if (!light)
                {
                    int h1 = Math.Max(1, (int)(170 * UC.S));
                    using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Rectangle(0, 0, dw, h1),
                        Color.FromArgb(80, 6, 7, 9), Color.FromArgb(0, 6, 7, 9),
                        System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                        gg.FillRectangle(lg, 0, 0, dw, h1);

                    int y0 = (int)(380 * UC.S);
                    int h2 = dh - y0;
                    if (h2 > 1)
                    {
                        using (var lg2 = new System.Drawing.Drawing2D.LinearGradientBrush(
                            new Rectangle(0, y0, dw, h2),
                            Color.FromArgb(0, 7, 8, 10), Color.FromArgb(70, 7, 8, 10),
                            System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                            gg.FillRectangle(lg2, 0, y0, dw, h2);
                    }
                    using (var b = new SolidBrush(Color.FromArgb(38, 7, 8, 10)))
                        gg.FillRectangle(b, 0, 0, dw, dh);
                }
            }

            BitmapData d = bgCache.LockBits(new Rectangle(0, 0, dw, dh), ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = d.Stride;
                byte[] buf = new byte[stride * dh];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                Random rnd = new Random(11);

                for (int y = 0; y < dh; y++)
                {
                    double ly0 = y / UC.S;
                    int row = y * stride;

                    for (int x = 0; x < dw; x++)
                    {
                        int j = row + x * 4;
                        double bl = buf[j], cg = buf[j + 1], cr = buf[j + 2];
                        double lx0 = x / UC.S;

                        for (int i = 0; i < L.Length; i += 8)
                        {
                            double dx = (lx0 - L[i]) / L[i + 2];
                            double dy = (ly0 - L[i + 1]) / L[i + 3];
                            double dd = dx * dx + dy * dy;
                            if (dd >= 1.0) continue;
                            double k = L[i + 4] * Math.Pow(1.0 - Math.Sqrt(dd), 1.6);
                            cr += k * (L[i + 5] - cr);
                            cg += k * (L[i + 6] - cg);
                            bl += k * (L[i + 7] - bl);
                        }

                        double n = (rnd.NextDouble() - 0.5) * 1.4;
                        buf[j] = Clamp8((int)Math.Round(bl + n));
                        buf[j + 1] = Clamp8((int)Math.Round(cg + n));
                        buf[j + 2] = Clamp8((int)Math.Round(cr + n));
                        buf[j + 3] = 255;
                    }
                }

                Marshal.Copy(buf, 0, d.Scan0, buf.Length);
            }
            finally
            {
                bgCache.UnlockBits(d);
            }
        }

        private static byte Clamp8(int v)
        {
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
        }
        // 小节标签：左侧一枚青色短标，右侧文字
        private void SectionLabel(Graphics g, string text, Rectangle rc)
        {
            int cy = rc.Y + rc.Height / 2;
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(170, UC.Accent)))
                g.FillRectangle(tb, rc.X, cy - 5, 2, 10);
            UC.Text(g, text, UC.Small, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height), UC.Ink2L,
                TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if (Math.Abs(UC.S - 1f) > 0.02f) g.ScaleTransform(UC.S, UC.S);
            PaintBg(g, ClientRectangle);

            // 窗口 1px 包边，让边缘在深底上立得住
            using (Pen wb = new Pen(UC.Light ? Color.FromArgb(160, 150, 134, 108) : Color.FromArgb(62, 160, 176, 200), 1f))
                g.DrawRectangle(wb, 0.5f, 0.5f, W - 1f, H - 1f);
            using (Pen wa = new Pen(UC.Light ? Color.FromArgb(170, UC.Accent) : Color.FromArgb(120, 110, 196, 232), 1f))
            {
                g.DrawLine(wa, 0.5f, 0.5f, 26, 0.5f);
                g.DrawLine(wa, 0.5f, 0.5f, 0.5f, 26);
            }

            // 顶栏：左侧一道琥珀标记，战术感
            using (SolidBrush ab = new SolidBrush(Color.FromArgb(UC.Light ? 170 : 96, UC.Accent)))
                g.FillRectangle(ab, 20, 15, 3, 26);
            using (SolidBrush ab2 = new SolidBrush(Color.FromArgb(UC.Light ? 130 : 96, UC.Accent)))
                g.FillRectangle(ab2, 20, 45, 3, 12);
            UC.Text(g, "自动检测分辨率", UC.Title, new Rectangle(31, 14, 260, 28), UC.Ink,
                TextFormatFlags.VerticalCenter);
            UC.Text(g, "桌面分辨率 · 全自动切换", UC.Tiny, new Rectangle(31, 44, 270, 14), UC.Ink3,
                TextFormatFlags.VerticalCenter);

            // 状态卡
            UC.Card(g, rcStatus, 14);
            using (SolidBrush b = new SolidBrush(cState))
                g.FillEllipse(b, 38, 93, 8, 8);
            using (Pen p = new Pen(Color.FromArgb(70, cState), 1f))
                g.DrawEllipse(p, 35, 90, 14, 14);

            UC.Text(g, sTitle, UC.Med, new Rectangle(54, 85, 180, 22), cState, TextFormatFlags.VerticalCenter);
            UC.Text(g, sSub, UC.Small, new Rectangle(54, 107, 180, 18), UC.Ink2L, TextFormatFlags.VerticalCenter);
            UC.Text(g, sCount, UC.Tiny, new Rectangle(54, 129, 184, 16), UC.Ink3, TextFormatFlags.VerticalCenter);

            using (Pen p = new Pen(UC.Light ? Color.FromArgb(165, 120, 104, 82) : Color.FromArgb(30, 238, 244, 252), 1f))
                g.DrawLine(p, 244, 91, 244, 151);

            UC.Text(g, sRes, UC.Big, new Rectangle(252, 85, 132, 26), eng.Switched ? UC.Accent : UC.Ink,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            UC.Text(g, sHz, UC.Small, new Rectangle(252, 113, 132, 18), UC.Ink2L,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            UC.Text(g, "当前桌面", UC.Tiny, new Rectangle(252, 133, 132, 14), UC.Ink3,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            // 监测游戏标题
            SectionLabel(g, "监测游戏", new Rectangle(22, 180, 120, 20));

            // 目标进程输入框外框
            using (GraphicsPath p = UC.Round(rcProc, 12))
            {
                using (SolidBrush b = new SolidBrush(UC.Light ? Color.FromArgb(246, 250, 246, 236) : Color.FromArgb(226, 9, 10, 12))) g.FillPath(b, p);
                using (Pen pen = new Pen(UC.Light ? Color.FromArgb(246, 126, 110, 86) : Color.FromArgb(40, 168, 180, 200), 1f)) g.DrawPath(pen, p);
            }
            UC.Text(g, procText.Length > 0 ? procText : "未选择游戏", UC.Med, new Rectangle(34, 340, 352, 24),
                procText.Length > 0 ? UC.Ink : UC.Ink3, TextFormatFlags.VerticalCenter);

            // 目标进程标题
            SectionLabel(g, "目标进程", new Rectangle(22, 297, 120, 18));

            // 屏幕 / 缩放：平时只有文字，指上去才浮一层淡蓝底
            bool hsp = hotId == "screen";
            Chip(g, rcScreen, 12, hsp || downId == "screen");
            UC.Text(g, "屏幕 " + ScreenLabel(), UC.Tiny, rcScreen,
                hsp ? Color.FromArgb(UC.Ink.R, UC.Ink.G, UC.Ink.B) : UC.Ink2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            bool hsc = hotId == "scale";
            Chip(g, rcScale, 12, hsc || downId == "scale");
            UC.Text(g, "缩放 " + ScaleLabel(), UC.Tiny, rcScale,
                hsc ? Color.FromArgb(UC.Ink.R, UC.Ink.G, UC.Ink.B) : UC.Ink2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            bool hst = hotId == "theme";
            Chip(g, rcTheme, 12, hst || downId == "theme");
            UC.Text(g, "主题 " + ThemeLabel(), UC.Tiny, rcTheme,
                hst ? Color.FromArgb(UC.Ink.R, UC.Ink.G, UC.Ink.B) : UC.Ink2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 分辨率卡
            UC.Card(g, rcRes, 14);
            UC.Text(g, "游戏运行时", UC.Body, new Rectangle(40, 400, 170, 24), UC.Ink2, TextFormatFlags.VerticalCenter);
            UC.Text(g, "刷新率", UC.Body, new Rectangle(40, 450, 170, 24), UC.Ink2, TextFormatFlags.VerticalCenter);
            UC.Text(g, "退出游戏后", UC.Body, new Rectangle(40, 500, 170, 24), UC.Ink2, TextFormatFlags.VerticalCenter);

            if (resList.Length > 0)
            {
                UC.Text(g, resList[gameResIdx], UC.Med, new Rectangle(228, 397, 130, 30), UC.Ink,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                UC.Chevron(g, 370, 412, UC.Ink3);
                UC.Text(g, resList[deskResIdx], UC.Med, new Rectangle(228, 497, 130, 30), UC.Ink,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                UC.Chevron(g, 370, 512, UC.Ink3);
            }
            if (hzList.Length > 0)
            {
                UC.Text(g, hzList[hzIdx], UC.Med, new Rectangle(228, 447, 130, 30), UC.Ink,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                UC.Chevron(g, 370, 462, UC.Ink3);
            }
            using (Pen p = new Pen(UC.Light ? Color.FromArgb(180, 118, 102, 80) : Color.FromArgb(26, 238, 244, 252), 1f))
            {
                g.DrawLine(p, 38, 436, 382, 436);
                g.DrawLine(p, 38, 486, 382, 486);
            }

            // 延迟/间隔：带框下拉，与上面胶囊同款
            Chip(g, rcDelay, 10, hotId == "delay" || downId == "delay");
            Chip(g, rcPoll, 10, hotId == "poll" || downId == "poll");
            UC.Text(g, delayList[delayIdx], UC.Med, new Rectangle(124, 555, 40, 24), UC.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            UC.Chevron(g, 176, 567, UC.Ink3);
            UC.Text(g, pollList[pollIdx], UC.Med, new Rectangle(306, 555, 40, 24), UC.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            UC.Chevron(g, 358, 567, UC.Ink3);

            // 延迟行标题
            SectionLabel(g, "切换延迟", new Rectangle(40, 555, 64, 24));
            SectionLabel(g, "检测间隔", new Rectangle(222, 555, 64, 24));

            // 开关标签
            UC.Text(g, "开机自启", UC.Small, new Rectangle(40, 705, 70, 20), UC.Ink2, TextFormatFlags.VerticalCenter);
            UC.Text(g, "暂停监听", UC.Small, new Rectangle(228, 705, 70, 20), UC.Ink2, TextFormatFlags.VerticalCenter);

            // 日志卡
            UC.Card(g, rcLog, 12);
            SectionLabel(g, "最近事件", new Rectangle(22, 739, 140, 16));
            UC.Text(g, sLog, UC.Small, new Rectangle(36, 763, 348, 44), UC.Ink2L,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 作者信息
            UC.Text(g, "作者  冷渡-Van1sh", UC.Tiny, new Rectangle(22, 817, 180, 18),
                UC.Ink3, TextFormatFlags.VerticalCenter);
            UC.Text(g, "BUG 反馈  794841182", UC.Tiny, new Rectangle(218, 817, 180, 18),
                UC.Ink3, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            PaintWidgets(g);
        }

        // ============ 控件：全部在同一张画布上完成，避免任何子控件接缝 ============
        private static void Pill(Graphics g, Rectangle r, int rad, Color top, Color bot)
        {
            using (GraphicsPath p = UC.Round(r, rad))
            {
                if (top == bot)
                {
                    using (SolidBrush b = new SolidBrush(top)) g.FillPath(b, p);
                }
                else
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        new Rectangle(r.X, r.Y, r.Width, r.Height + 1), top, bot, LinearGradientMode.Vertical))
                        g.FillPath(b, p);
                }
            }
        }

        // 小胶囊按钮：有底色、有 1px 描边，和纯蓝主按钮区分开
        private static void Chip(Graphics g, Rectangle r, int rad, bool hot)
        {
            using (GraphicsPath p = UC.Round(r, rad))
            {
                Color top, bot, line;
                if (UC.Light)
                {
                    top = Color.FromArgb(hot ? 250 : 244, 250, 246, 238);
                    bot = Color.FromArgb(hot ? 242 : 234, 238, 230, 216);
                    line = Color.FromArgb(hot ? 255 : 238, 130, 114, 90);
                }
                else
                {
                    top = Color.FromArgb(hot ? 168 : 138, 52, 57, 68);
                    bot = Color.FromArgb(hot ? 156 : 126, 38, 42, 52);
                    line = Color.FromArgb(hot ? 196 : 146, 172, 188, 218);
                }
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(r.X, r.Y, r.Width, r.Height + 1),
                    top, bot, LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(line, 1f))
                    g.DrawPath(pen, p);
            }
        }

        // 圆头笔画：用带圆帽的粗线代替细线，边缘更平滑
        private static void Bar(Graphics g, float cx, float cy, float len, float th, float angle, Color c)
        {
            float rad = angle * (float)Math.PI / 180f;
            float hx = (float)Math.Cos(rad) * len / 2f;
            float hy = (float)Math.Sin(rad) * len / 2f;
            using (Pen pen = new Pen(c, th))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - hx, cy - hy, cx + hx, cy + hy);
            }
        }

        private void PaintCaption(Graphics g, Rectangle r, bool close)
        {
            bool hov = hotId == (close ? "close" : "min");
            bool dwn = downId == (close ? "close" : "min");

            if (hov || dwn)
            {
                Color bg = close
                    ? (dwn ? Color.FromArgb(222, 82, 76) : Color.FromArgb(200, 74, 68))
                    : (UC.Light
                        ? (dwn ? Color.FromArgb(44, 43, 42, 38) : Color.FromArgb(28, 43, 42, 38))
                        : (dwn ? Color.FromArgb(52, 255, 255, 255) : Color.FromArgb(34, 255, 255, 255)));
                Pill(g, r, 10, bg, bg);
            }

            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            Color ink = (close && (hov || dwn)) ? Color.White : (UC.Light ? UC.Ink : Color.FromArgb(204, 210, 220));

            if (close)
            {
                Bar(g, cx, cy, 11f, 2.6f, 45f, ink);
                Bar(g, cx, cy, 11f, 2.6f, -45f, ink);
            }
            else
            {
                Bar(g, cx, cy, 13f, 2.6f, 0f, ink);
            }
        }

        private void PaintToggle(Graphics g, Rectangle r, bool on, bool hov)
        {
            if (UC.Light)
            {
                Pill(g, r, r.Height / 2,
                    on ? Color.FromArgb(hov ? 168 : 150, UC.Accent) : Color.FromArgb(hov ? 76 : 54, 120, 112, 98),
                    on ? Color.FromArgb(hov ? 190 : 172, 28, 86, 96) : Color.FromArgb(hov ? 62 : 42, 120, 112, 98));
            }
            else
            {
                Pill(g, r, r.Height / 2,
                    on ? Color.FromArgb(hov ? 130 : 114, 132, 198, 228) : Color.FromArgb(hov ? 34 : 22, 250, 245, 215),
                    on ? Color.FromArgb(hov ? 92 : 78, 88, 154, 186) : Color.FromArgb(hov ? 22 : 14, 250, 245, 215));
            }

            int d = r.Height - 4;
            int kx = on ? r.Right - d - 2 : r.X + 2;
            Rectangle knob = new Rectangle(kx, r.Y + 2, d, d);
            if (UC.Light)
            {
                Pill(g, knob, d / 2,
                    on ? Color.FromArgb(252, 248, 240) : Color.FromArgb(240, 234, 222),
                    on ? Color.FromArgb(244, 238, 226) : Color.FromArgb(230, 222, 206));
            }
            else
            {
                Pill(g, knob, d / 2,
                    on ? Color.White : Color.FromArgb(200, 206, 216),
                    on ? Color.FromArgb(240, 244, 250) : Color.FromArgb(166, 172, 182));
            }
        }

        private void PaintWidgets(Graphics g)
        {
            // 游戏选择行
            bool hg = hotId == "game";
            if (UC.Light)
            {
                Pill(g, rcGame, 14,
                    Color.FromArgb(hg ? 252 : 246, 250, 246, 238),
                    Color.FromArgb(hg ? 244 : 236, 236, 228, 212));
                using (GraphicsPath p = UC.Round(rcGame, 14))
                using (Pen pen = new Pen(Color.FromArgb(190, 130, 114, 90), 1f))
                    g.DrawPath(pen, p);
            }
            else
            {
                Pill(g, rcGame, 14,
                    Color.FromArgb(hg ? 132 : 108, 96, 104, 126),
                    Color.FromArgb(hg ? 118 : 94, 58, 64, 80));
            }

            Rectangle ic = new Rectangle(rcGame.X + 16, rcGame.Y + 12, 30, 30);
            if (UC.Light)
                Pill(g, ic, 9, Color.FromArgb(170, UC.Accent), Color.FromArgb(200, 24, 78, 88));
            else
                Pill(g, ic, 9, Color.FromArgb(100, 106, 132), Color.FromArgb(50, 56, 74));
            using (Pen pen = new Pen(UC.Light ? Color.FromArgb(252, 248, 240) : Color.FromArgb(230, 236, 246), 1.8f))
            {
                Rectangle scr = new Rectangle(ic.X + 8, ic.Y + 8, 14, 10);
                using (GraphicsPath p = UC.Round(scr, 3)) g.DrawPath(pen, p);
                g.DrawLine(pen, ic.X + 15, ic.Y + 18, ic.X + 15, ic.Y + 22);
                g.DrawLine(pen, ic.X + 11, ic.Y + 22, ic.X + 19, ic.Y + 22);
            }

            UC.Text(g, gameCaption, UC.Med, new Rectangle(rcGame.X + 56, rcGame.Y + 9, rcGame.Width - 100, 20),
                UC.Ink, TextFormatFlags.VerticalCenter);
            UC.Text(g, gameSub, UC.Tiny, new Rectangle(rcGame.X + 56, rcGame.Y + 27, rcGame.Width - 100, 18),
                UC.Ink2L, TextFormatFlags.VerticalCenter);
            UC.Chevron(g, rcGame.Right - 24, rcGame.Y + 25, UC.Ink3);

            // 添加游戏（手动选 exe）
            bool had = hotId == "addgame";
            bool dad = downId == "addgame";
            Chip(g, rcAdd, 12, had || dad);
            Color addInk = had || dad ? Color.FromArgb(UC.Ink.R, UC.Ink.G, UC.Ink.B) : UC.Ink2;
            UC.Text(g, "+ 添加", UC.Small, rcAdd, addInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 重新检测
            bool hr = hotId == "rescan" && rescanReady;
            Chip(g, rcRescan, 12, hr);
            Color rInk = !rescanReady ? Color.FromArgb(132, 137, 146) : (hr ? Color.FromArgb(UC.Ink.R, UC.Ink.G, UC.Ink.B) : UC.Ink2);
            UC.Text(g, rescanText, UC.Small, rcRescan, rInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 应用设置
            bool ha = hotId == "apply";
            bool da = downId == "apply";
            if (UC.Light)
            {
                Pill(g, rcApply, 14,
                    da ? Color.FromArgb(26, 86, 98) : (ha ? Color.FromArgb(44, 124, 136) : Color.FromArgb(38, 112, 124)),
                    da ? Color.FromArgb(18, 66, 76) : (ha ? Color.FromArgb(34, 104, 114) : Color.FromArgb(28, 90, 100)));
                UC.Text(g, "应用设置", UC.BtnF, rcApply, Color.FromArgb(252, 248, 240),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                Pill(g, rcApply, 14,
                    da ? Color.FromArgb(66, 146, 190) : (ha ? Color.FromArgb(114, 200, 238) : Color.FromArgb(94, 184, 224)),
                    da ? Color.FromArgb(44, 114, 154) : (ha ? Color.FromArgb(70, 156, 198) : Color.FromArgb(58, 138, 178)));
                UC.Text(g, "应用设置", UC.BtnF, rcApply, Color.FromArgb(10, 28, 38),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 立即恢复桌面分辨率
            bool hr2 = hotId == "restore";
            bool dr2 = downId == "restore";
            if (UC.Light)
            {
                Pill(g, rcRestore, 14,
                    dr2 ? Color.FromArgb(220, 210, 194) : (hr2 ? Color.FromArgb(242, 234, 220) : Color.FromArgb(232, 224, 208)),
                    dr2 ? Color.FromArgb(208, 198, 180) : (hr2 ? Color.FromArgb(230, 222, 206) : Color.FromArgb(218, 208, 190)));
                using (GraphicsPath p = UC.Round(rcRestore, 14))
                using (Pen pen = new Pen(Color.FromArgb(180, 130, 114, 90), 1f))
                    g.DrawPath(pen, p);
                UC.Text(g, "立即恢复桌面分辨率", UC.Small, rcRestore,
                    dr2 ? UC.Ink : UC.Ink2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                Pill(g, rcRestore, 14,
                    dr2 ? Color.FromArgb(70, 76, 88) : (hr2 ? Color.FromArgb(86, 92, 106) : Color.FromArgb(64, 70, 82)),
                    dr2 ? Color.FromArgb(46, 50, 58) : (hr2 ? Color.FromArgb(56, 62, 74) : Color.FromArgb(42, 46, 54)));
                UC.Text(g, "立即恢复桌面分辨率", UC.Small, rcRestore,
                    hr2 ? Color.White : Color.FromArgb(214, 220, 230),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 开关
            PaintToggle(g, rcTgAuto, tgAutoOn, hotId == "tga");
            PaintToggle(g, rcTgPause, tgPauseOn, hotId == "tgp");

            // 右上角窗口按钮
            PaintCaption(g, rcMin, false);
            PaintCaption(g, rcClose, true);
        }

        private void BuildUI()
        {
            FillDelay();
            FillPoll();

            games = CustomGameList();
            FillGameCombo();
            procText = FirstExe(cfg.GameExes);
            FillResPickers();
            FillHz();

            tgAutoOn = Runtime.IsAutostart();
            tgPauseOn = eng.Paused;
        }

        protected override void WndProc(ref Message m)
        {
            // 禁止最大化：标题栏双击、Win+Up、系统菜单都不生效
            if (m.Msg == 0xA3) // WM_NCLBUTTONDBLCLK
            {
                if ((int)m.WParam == 2 /*HTCAPTION*/) { m.Result = IntPtr.Zero; return; }
            }
            if (m.Msg == 0x112) // WM_SYSCOMMAND
            {
                int cmd = (int)m.WParam & 0xFFF0;
                if (cmd == 0xF030 || cmd == 0xF032) { m.Result = IntPtr.Zero; return; }
            }
            if (m.Msg == 0x84) // WM_NCHITTEST
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1)
                {
                    long lp = m.LParam.ToInt64();
                    int sx = (short)(lp & 0xFFFF);
                    int sy = (short)((lp >> 16) & 0xFFFF);
                    Point p = PointToClient(new Point(sx, sy));
                    if (p.Y >= 0 && p.Y < UISize(56) && p.X < UISize(336)) m.Result = (IntPtr)2;
                }
                return;
            }
            base.WndProc(ref m);
        }

        private static string FirstExe(string list)
        {
            if (list == null) return "";
            string[] p = list.Split(',');
            return p.Length > 0 ? p[0].Trim() : "";
        }

        private UxMenu curMenu;
        private DateTime menuClosedAt = DateTime.MinValue;

        // 同一个触发区再点一次＝收起；若是这次点击刚把浮层关掉，则不再重新弹出
        private void PopupMenu(Rectangle anchor, string[] titles, string[] subs, int sel, int width, Action<int> pick)
        {
            if (curMenu != null && curMenu.Visible)
            {
                curMenu.Close();
                return;
            }
            if ((DateTime.Now - menuClosedAt).TotalMilliseconds < 280) return;

            UxMenu m = new UxMenu(titles, subs, sel, width, pick);
            m.FormClosed += delegate
            {
                menuClosedAt = DateTime.Now;
                if (curMenu == m) curMenu = null;
            };
            m.ShowAt(this, RectangleToScreen(UC.R(anchor)));
            curMenu = m;
        }

        // 把 settings.ini 里手动添加的游戏解析成列表
        private List<GameInfo> CustomGameList()
        {
            List<GameInfo> list = new List<GameInfo>();
            string[] parts = (cfg.CustomGames == null ? "" : cfg.CustomGames).Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                int bar = p.IndexOf('|');
                string exe = (bar > 0 ? p.Substring(0, bar) : p).Trim().ToLowerInvariant();
                string name = bar > 0 ? p.Substring(bar + 1).Trim() : exe;
                if (exe.Length == 0) continue;
                GameInfo gi = new GameInfo();
                gi.Name = name.Length > 0 ? name : exe;
                gi.Exe = exe;
                gi.Dir = "";
                list.Add(gi);
            }
            return list;
        }

        private void AddGameFromFile()
        {
            string picked = null;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "选择要监测的游戏主程序";
                dlg.Filter = "程序文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                dlg.CheckFileExists = true;
                dlg.Multiselect = false;
                string cur = procText.Trim();
                if (cur.Length == 0) cur = FirstExe(cfg.GameExes);
                if (cur.Length > 0)
                {
                    for (int i = 0; i < games.Count; i++)
                    {
                        if (games[i].Exe == cur.ToLowerInvariant() && games[i].Dir != null && games[i].Dir.Length > 0)
                        {
                            dlg.InitialDirectory = games[i].Dir;
                            break;
                        }
                    }
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                picked = dlg.FileName;
            }
            if (picked == null || !File.Exists(picked)) return;

            string exe = Path.GetFileName(picked).ToLowerInvariant();
            string name = "";
            try
            {
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(picked);
                if (vi != null)
                {
                    name = (vi.FileDescription == null ? "" : vi.FileDescription.Trim());
                    if (name.Length < 2) name = (vi.ProductName == null ? "" : vi.ProductName.Trim());
                }
            }
            catch { }
            if (name.Length < 2) name = Path.GetFileNameWithoutExtension(picked);

            GameInfo gi = new GameInfo();
            gi.Name = name;
            gi.Exe = exe;
            gi.Dir = Path.GetDirectoryName(picked);

            for (int i = games.Count - 1; i >= 0; i--)
                if (games[i].Exe == exe) games.RemoveAt(i);
            games.Insert(0, gi);

            cfg.AddCustomGame(exe, name);
            cfg.SelectedGame = name;
            cfg.GameExes = exe;
            cfg.MatchKeys = "";
            cfg.Save();
            eng.ApplySettings(cfg);

            gameCaption = name;
            gameSub = exe;
            procText = exe;
            scanMsg = "已添加 " + name;
            eng.Log("手动添加游戏：" + name + "（" + exe + "）");
            Invalidate();
            RefreshState();
        }

        private static readonly float[] ScaleValues = { 0f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };

        private static string ScaleText(float s)
        {
            string v = s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (v.EndsWith(".0")) v = v.Substring(0, v.Length - 2);
            return "x" + v;
        }

        private string ScaleLabel()
        {
            float s = cfg.UIScale >= 0.4f ? cfg.UIScale : DpiScale;
            return ScaleText(s);
        }

        private string ThemeLabel()
        {
            if (cfg.Theme == 1) return "深空";
            if (cfg.Theme == 2) return "米白";
            return "珠链";
        }

        private string ScreenLabel()
        {
            string cur = cfg.DisplayDevice == null ? "" : cfg.DisplayDevice;
            if (cur.Length == 0) return "主屏";
            List<string[]> ds = Native.Devices();
            for (int i = 0; i < ds.Count; i++)
                if (ds[i][0] == cur) return (i == 0 ? "主屏" : "屏" + (i + 1));
            return "屏" + cur.Replace(@"\\.\", "");
        }

        private void SetScreen(string devName)
        {
            cfg.DisplayDevice = devName == null ? "" : devName;
            cfg.Save();
            dev = cfg.DisplayDevice.Length > 0 ? cfg.DisplayDevice : Native.PrimaryDevice();
            if (dev == null) dev = @"\\.\DISPLAY1";
            FillResPickers();
            eng.ApplySettings(cfg);
            eng.Log("目标屏幕已切换：" + ScreenLabel());
            Invalidate();
            RefreshState();
        }

        private void ShowScreenMenu()
        {
            List<string[]> ds = Native.Devices();
            List<string> t = new List<string>();
            List<string> sub = new List<string>();
            bool auto = cfg.DisplayDevice == null || cfg.DisplayDevice.Length == 0;
            int sel = 0;

            t.Add("跟随主屏");
            sub.Add("在主显示器上切换分辨率");
            for (int i = 0; i < ds.Count; i++)
            {
                int w, h, hz;
                Native.GetCurrent(ds[i][0], out w, out h, out hz);
                t.Add("显示器 " + (i + 1));
                sub.Add(w + " × " + h + " · " + hz + "Hz" + ((ds[i][1].IndexOf("主屏") >= 0) ? " · 主" : ""));
                if (!auto && cfg.DisplayDevice == ds[i][0]) sel = i + 1;
            }

            PopupMenu(rcScreen, t.ToArray(), sub.ToArray(), sel, 250, delegate (int idx)
            {
                if (idx < 0) return;
                if (idx == 0) SetScreen("");
                else if (idx - 1 < ds.Count) SetScreen(ds[idx - 1][0]);
            });
        }

        private void ShowThemeMenu()
        {
            string[] titles = new string[] { "珠链", "深空", "米白" };
            string[] subs = new string[]
            {
                "保留珠链纹理 · 轻微柔化",
                "去掉纹理 · 纯深色底，最不抢字",
                "米白亮色 · 暖纸质感，适合白天"
            };
            PopupMenu(rcTheme, titles, subs, cfg.Theme, 220, delegate (int idx)
            {
                if (idx < 0 || idx >= titles.Length) return;
                cfg.Theme = idx;
                cfg.Save();
                UC.ApplyTheme(idx == 2);
                if (bgCache != null) { bgCache.Dispose(); bgCache = null; }
                BuildBgCache();
                RefreshState();
                Invalidate();
                eng.Log("背景主题：" + ThemeLabel());
            });
        }

        private void ShowScaleMenu()
        {
            List<string> t = new List<string>();
            List<string> sub = new List<string>();
            int sel = 0;
            float cur = cfg.UIScale >= 0.4f ? cfg.UIScale : 0f;
            for (int i = 0; i < ScaleValues.Length; i++)
            {
                float v = ScaleValues[i];
                if (Math.Abs(cur - v) < 0.01f) sel = i;
                if (v <= 0f)
                {
                    t.Add("自动");
                    sub.Add("跟随系统缩放 · 当前 " + ScaleText(DpiScale));
                }
                else
                {
                    t.Add(ScaleText(v) + " 倍");
                    int ww = (int)Math.Round(W * v), hh = (int)Math.Round(H * v);
                    sub.Add("窗口 " + ww + " × " + hh);
                }
            }
            PopupMenu(rcScale, t.ToArray(), sub.ToArray(), sel, 210, delegate (int idx)
            {
                if (idx < 0 || idx >= ScaleValues.Length) return;
                ApplyScale(ScaleValues[idx]);
            });
        }

        private void ApplyScale(float v)
        {
            float s = v >= 0.4f ? v : DpiScale;
            cfg.UIScale = v;
            cfg.Save();
            if (Math.Abs(s - UC.S) < 0.005f) { Invalidate(); return; }

            Rectangle old = Bounds;
            Point center = new Point(old.Left + old.Width / 2, old.Top + old.Height / 2);

            UC.S = s;
            ClampScaleToScreen();
            if (bgCache != null) { bgCache.Dispose(); bgCache = null; }
            ClientSize = new Size(UISize(W), UISize(H));
            BuildBgCache();

            Rectangle wa = Screen.FromPoint(center).WorkingArea;
            int nx = center.X - Width / 2;
            int ny = center.Y - Height / 2;
            if (nx < wa.Left) nx = wa.Left;
            if (ny < wa.Top) ny = wa.Top;
            if (nx + Width > wa.Right) nx = wa.Right - Width;
            if (ny + Height > wa.Bottom) ny = wa.Bottom - Height;
            Location = new Point(nx, ny);
            Invalidate();
        }

        private void ShowGameMenu()
        {
            List<string> t = new List<string>();
            List<string> s = new List<string>();
            int cur = -1;

            if (games.Count == 0)
            {
                t.Add("还没有游戏列表");
                s.Add("点右上角「重新检测」扫描本机游戏");
            }
            else
            {
                for (int i = 0; i < games.Count; i++)
                {
                    t.Add(games[i].Name);
                    s.Add(games[i].Exe);
                    if (games[i].Name == cfg.SelectedGame) cur = i;
                }
            }

            PopupMenu(rcGame, t.ToArray(), s.ToArray(), cur, 320, delegate (int idx)
            {
                if (idx < 0 || idx >= games.Count) return;
                GameInfo gi = games[idx];
                gameCaption = gi.Name;
                gameSub = gi.Exe;
                procText = gi.Exe;
                cfg.SelectedGame = gi.Name;
                cfg.GameExes = gi.Exe;
                cfg.MatchKeys = "";
                cfg.Save();
                eng.ApplySettings(cfg);
                Invalidate();
                RefreshState();
            });
        }

        private void FillGameCombo()
        {
            gameCaption = cfg.SelectedGame.Length > 0 ? cfg.SelectedGame : "选择要监测的游戏";
            gameSub = FirstExe(cfg.GameExes);
            Invalidate();
        }

        private void FillResPickers()
        {
            List<string> list = Native.Resolutions(dev);
            string wantG = cfg.GameW + " × " + cfg.GameH;
            string wantD = cfg.DeskW + " × " + cfg.DeskH;
            if (!list.Contains(wantG)) list.Insert(0, wantG);
            if (!list.Contains(wantD)) list.Insert(0, wantD);
            resList = list.ToArray();
            gameResIdx = Array.IndexOf(resList, wantG);
            if (gameResIdx < 0) gameResIdx = 0;
            deskResIdx = Array.IndexOf(resList, wantD);
            if (deskResIdx < 0) deskResIdx = 0;
        }

        private void FillHz()
        {
            int w = cfg.GameW, h = cfg.GameH;
            if (resList.Length > 0)
            {
                int pw, ph;
                if (ParseRes(resList[gameResIdx], out pw, out ph)) { w = pw; h = ph; }
            }
            List<int> nums = Native.RefreshRates(dev, w, h);
            List<string> list = new List<string>();
            list.Add("跟随系统");
            for (int i = 0; i < nums.Count; i++) list.Add(nums[i] + " Hz");
            hzList = list.ToArray();
            string want = cfg.GameHz > 0 ? cfg.GameHz + " Hz" : "跟随系统";
            hzIdx = Array.IndexOf(hzList, want);
            if (hzIdx < 0) hzIdx = 0;
        }

        private int HzValue(int idx)
        {
            if (idx <= 0 || idx >= hzList.Length) return 0;
            int v;
            if (!int.TryParse(hzList[idx].Replace("Hz", "").Trim(), out v)) return 0;
            return v;
        }

        private void ShowHzMenu()
        {
            if (hzList.Length == 0) return;
            PopupMenu(rcGameHz, hzList, null, hzIdx, 180, delegate (int idx)
            {
                if (idx < 0 || idx >= hzList.Length) return;
                hzIdx = idx;
                Invalidate();
            });
        }

        private void ShowResMenu(bool isGame)
        {
            if (resList.Length == 0) return;
            int cur = isGame ? gameResIdx : deskResIdx;
            PopupMenu(isGame ? rcGameRes : rcDeskRes, resList, null, cur, 210, delegate (int idx)
            {
                if (idx < 0 || idx >= resList.Length) return;
                if (isGame) { gameResIdx = idx; FillHz(); } else deskResIdx = idx;
                Invalidate();
            });
        }

        private void ShowPick(bool isDelay)
        {
            string[] list = isDelay ? delayList : pollList;
            int cur = isDelay ? delayIdx : pollIdx;
            PopupMenu(isDelay ? rcDelay : rcPoll, list, null, cur, 150, delegate (int idx)
            {
                if (idx < 0 || idx >= list.Length) return;
                if (isDelay) delayIdx = idx; else pollIdx = idx;
                Invalidate();
            });
        }

        private void FillDelay()
        {
            delayIdx = cfg.DelayMs <= 0 ? 0 : (cfg.DelayMs <= 2000 ? 1 : (cfg.DelayMs <= 3000 ? 2 : 3));
        }

        private void FillPoll()
        {
            pollIdx = cfg.PollMs <= 1000 ? 0 : (cfg.PollMs <= 2000 ? 1 : 2);
        }

        private static int DelayValue(int idx)
        {
            if (idx == 1) return 2000;
            if (idx == 2) return 3000;
            if (idx == 3) return 5000;
            return 0;
        }

        private static int PollValue(int idx)
        {
            if (idx == 0) return 1000;
            if (idx == 1) return 2000;
            return 3000;
        }

        private static bool ParseRes(string s, out int w, out int h)
        {
            w = 0; h = 0;
            if (s == null) return false;
            string[] p = s.Replace(" ", "").Split('×');
            if (p.Length != 2) return false;
            return int.TryParse(p[0], out w) && int.TryParse(p[1], out h);
        }

        private void ApplyUI()
        {
            int w, h;
            if (resList.Length > 0 && ParseRes(resList[gameResIdx], out w, out h)) { cfg.GameW = w; cfg.GameH = h; }
            if (resList.Length > 0 && ParseRes(resList[deskResIdx], out w, out h)) { cfg.DeskW = w; cfg.DeskH = h; }
            cfg.GameHz = HzValue(hzIdx);
            cfg.DelayMs = DelayValue(delayIdx);
            cfg.PollMs = PollValue(pollIdx);
            string exe = procText.Trim().ToLowerInvariant();
            if (exe.Length > 0) cfg.GameExes = exe.IndexOf(".exe") >= 0 ? exe : exe + ".exe";
            cfg.Save();
            eng.ApplySettings(cfg);
            eng.Log("设置已应用：游戏 " + cfg.GameW + "×" + cfg.GameH + (cfg.GameHz > 0 ? " @" + cfg.GameHz + "Hz" : " @跟随系统")
                + " · 桌面 " + cfg.DeskW + "×" + cfg.DeskH);
            RefreshState();
        }

        private void StartScan()
        {
            if (scanning) return;
            scanning = true;
            rescanText = "扫描中…";
            rescanReady = false;
            scanMsg = "正在扫描本机游戏…";
            eng.Log("开始扫描本机已安装游戏");
            Invalidate();

            Thread t = new Thread(delegate ()
            {
                List<GameInfo> list = null;
                try { list = GameScanner.Scan(); } catch { list = new List<GameInfo>(); }
                try { BeginInvoke((MethodInvoker)delegate { OnScanDone(list); }); }
                catch { }
            });
            t.IsBackground = true;
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();
        }

        private void OnScanDone(List<GameInfo> list)
        {
            scanning = false;
            rescanReady = true;
            rescanText = "重新检测";
            // 手动添加的排在最前，扫描结果不覆盖它们
            List<GameInfo> merged = CustomGameList();
            for (int i = 0; i < list.Count; i++)
            {
                bool dup = false;
                for (int j = 0; j < merged.Count; j++)
                    if (merged[j].Exe == list[i].Exe) { dup = true; break; }
                if (!dup) merged.Add(list[i]);
            }
            games = merged;
            scanMsg = "扫描完成，发现 " + list.Count + " 个游戏";
            eng.Log("扫描完成，发现 " + list.Count + " 个游戏");
            if (games.Count > 0)
            {
                // 只回填界面显示，不改写用户已经选好的目标进程
                int idx = -1;
                for (int i = 0; i < games.Count; i++)
                    if (games[i].Name == cfg.SelectedGame || games[i].Exe == FirstExe(cfg.GameExes)) { idx = i; break; }
                if (idx >= 0)
                {
                    GameInfo gi = games[idx];
                    gameCaption = gi.Name;
                    gameSub = gi.Exe;
                    procText = gi.Exe;
                    Invalidate();
                }
            }
            RefreshState();
        }

        private void RefreshState()
        {
            string title, sub;
            Color color;
            if (scanning) { title = "正在扫描本机游戏"; sub = "稍等，正在读取安装目录…"; color = Ux.Warn; }
            else if (eng.Paused) { title = "已暂停监听"; sub = "打开托盘菜单可随时恢复"; color = UC.Ink3; }
            else if (eng.Switched) { title = "游戏中 · 已切换"; sub = "桌面已切到 " + cfg.GameW + " × " + cfg.GameH; color = UC.Accent; }
            else
            {
                title = "自动监听中";
                sub = "等待 " + (cfg.SelectedGame.Length > 0 ? cfg.SelectedGame : "游戏") + " 启动";
                color = UC.Good;
            }

            string count = eng.SwitchCount > 0 ? ("已自动切换 " + eng.SwitchCount + " 次  ·  最近 " + eng.LastActionTime) : "还没有触发过自动切换";
            string log = (scanMsg.Length > 0 ? scanMsg : eng.LastMsg);
            if (log.Length == 0) log = "等待启动";
            if (log.Length > 46) log = log.Substring(0, 45) + "…";

            string res = "", hz = "";
            int w, h, hz2;
            Native.GetCurrent(dev, out w, out h, out hz2);
            res = w + " × " + h;
            hz = hz2 + " Hz";

            bool ch = title != sTitle || sub != sSub || count != sCount || log != sLog || res != sRes || hz != sHz || color != cState;
            sTitle = title; sSub = sub; sCount = count; sLog = log; sRes = res; sHz = hz; cState = color;
            tgPauseOn = eng.Paused;
            tgAutoOn = Runtime.IsAutostart();
            if (ch) Invalidate();
        }

        public void ShowPanel()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            ShowInTaskbar = true;
            TopMost = true;
            TopMost = false;
            Activate();
            // 首次打开且还没有游戏列表时，自动悄悄扫一遍
            if (!scanning && games.Count == 0)
            {
                System.Windows.Forms.Timer t0 = new System.Windows.Forms.Timer();
                t0.Interval = 900;
                t0.Tick += delegate { t0.Stop(); t0.Dispose(); if (games.Count == 0) StartScan(); };
                t0.Start();
            }
        }

        private void MinimizeToTray(bool tip)
        {
            Hide();
            ShowInTaskbar = false;
            if (tip) Runtime.ShowBalloon("已在后台自动运行\n双击托盘图标可再次打开面板");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!Runtime.Exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MinimizeToTray(true);
                return;
            }
            base.OnFormClosing(e);
        }
    }

    // ================= 托盘 =================
    public class TrayApp : ApplicationContext
    {
        private MainForm form;
        private NotifyIcon tray;
        private Engine eng;
        private Settings cfg;
        private System.Windows.Forms.Timer watch;
        private int _watchTick;

        public TrayApp(Settings settings, bool show)
        {
            cfg = settings;
            Runtime.Context = this;

            eng = new Engine(cfg);
            Runtime.Engine = eng;

            tray = new NotifyIcon();
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Size si = SystemInformation.SmallIconSize;
                using (Stream s = asm.GetManifestResourceStream("app.ico"))
                    if (s != null) tray.Icon = new Icon(s, si);
            }
            catch { }
            if (tray.Icon == null) tray.Icon = SystemIcons.Application;
            tray.Text = "自动检测分辨率 · 自动监听中";
            tray.Visible = true;
            tray.DoubleClick += delegate { EnsurePanel(); };

            ContextMenuStrip cm = new ContextMenuStrip();
            cm.BackColor = Color.FromArgb(250, 246, 238);
            cm.ForeColor = Color.FromArgb(70, 65, 58);
            ToolStripMenuItem mShow = new ToolStripMenuItem("显示面板");
            mShow.Click += delegate { EnsurePanel(); };
            ToolStripMenuItem mPause = new ToolStripMenuItem("暂停监听");
            mPause.Click += delegate
            {
                eng.Paused = !eng.Paused;
                eng.Log(eng.Paused ? "已暂停监听" : "已恢复监听");
            };
            ToolStripMenuItem mRestore = new ToolStripMenuItem("立即恢复桌面分辨率");
            mRestore.Click += delegate { eng.ForceRestore(); };
            ToolStripMenuItem mExit = new ToolStripMenuItem("退出程序");
            mExit.Click += delegate { Runtime.QuitApp(); };
            cm.Items.Add(mShow);
            cm.Items.Add(mPause);
            cm.Items.Add(mRestore);
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add(mExit);
            ToolStripMenuItem mAbout = new ToolStripMenuItem("作者 冷渡-Van1sh · BUG 反馈 794841182");
            mAbout.Enabled = false;
            cm.Items.Add(mAbout);
            tray.ContextMenuStrip = cm;

            eng.Start();

            if (show) EnsurePanel();

            watch = new System.Windows.Forms.Timer();
            watch.Interval = 700;
            watch.Tick += delegate
            {
                if (File.Exists(Settings.ShowFlag()))
                {
                    try { File.Delete(Settings.ShowFlag()); } catch { }
                    EnsurePanel();
                }
                _watchTick++;
                if ((_watchTick % 80) == 0) Native.TrimMemory();
            };
            watch.Start();
        }

        public MainForm EnsurePanel()
        {
            if (form == null) form = new MainForm(eng, cfg);
            form.ShowPanel();
            return form;
        }

        public NotifyIcon Tray { get { return tray; } }

        public void Quit()
        {
            try { eng.Stop(); } catch { }
            try { if (tray != null) tray.Visible = false; } catch { }
            try { if (form != null) form.Dispose(); } catch { }
            ExitThread();
        }
    }

    public static class Runtime
    {
        public static TrayApp Context;
        public static Engine Engine;
        public static volatile bool Exiting;

        public static void ShowBalloon(string text)
        {
            try
            {
                if (Context == null || Context.Tray == null) return;
                Context.Tray.BalloonTipTitle = "自动检测分辨率";
                Context.Tray.BalloonTipText = text;
                Context.Tray.ShowBalloonTip(2500);
            }
            catch { }
        }

        public static void QuitApp()
        {
            Exiting = true;
            if (Context != null) Context.Quit();
        }

        public static bool IsAutostart()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    return k != null && k.GetValue("DeltaResAuto") != null;
            }
            catch { return false; }
        }

        public static void SetAutostart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("DeltaResAuto", "\"" + Application.ExecutablePath + "\" --bg");
                    else k.DeleteValue("DeltaResAuto", false);
                }
            }
            catch { }
        }
    }

    // ================= 入口 =================
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try { RunApp(args); }
            catch (Exception ex) { CrashLog(ex); }
        }

        // 任何启动期异常都落盘，便于在别人电脑上排查闪退
        private static void CrashLog(Exception ex)
        {
            try
            {
                if (ex == null) return;
                string dir = Settings.Dir();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + "\r\n", Encoding.UTF8);
                try
                {
                    MessageBox.Show("程序遇到错误：" + ex.Message + "\r\n\r\n日志已记录到：\r\n" + path,
                        "自动检测分辨率", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
            catch { }
        }

        // 轻量运行日志：启动 / 退出 / 上次是否异常结束
        private static void AppendLog(string text)
        {
            try
            {
                string dir = Settings.Dir();
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "auto.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        private static void RunApp(string[] args)
        {
            // 默认按屏幕 DPI 缩放；用户在界面里选了固定倍率则用固定倍率
            float autoS = 1f;
            try
            {
                using (Graphics gg = Graphics.FromHwnd(IntPtr.Zero)) autoS = gg.DpiX / 96f;
                if (autoS < 0.75f) autoS = 1f;
            }
            catch { autoS = 1f; }
            MainForm.DpiScale = autoS;
            UC.S = autoS;

            bool bg = false, restoreOnly = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--bg") bg = true;
                if (args[i] == "--restore") restoreOnly = true;
            }

            Settings cfg = Settings.Load();

            string lockPath = Path.Combine(Settings.Dir(), "run.lock");
            bool dirtyExit = false;
            try
            {
                dirtyExit = File.Exists(lockPath);
                Directory.CreateDirectory(Settings.Dir());
                File.WriteAllText(lockPath, DateTime.Now.ToString("s"), Encoding.UTF8);
            }
            catch { }
            AppendLog(dirtyExit ? "程序启动（上次没有正常退出，可能闪退过）" : "程序启动");
            if (cfg.UIScale >= 0.4f && cfg.UIScale <= 4f) UC.S = cfg.UIScale;
            try
            {
                float ov;
                if (float.TryParse(Environment.GetEnvironmentVariable("DRS_SCALE"), out ov) && ov >= 0.4f && ov <= 4f)
                    UC.S = ov;
            }
            catch { }

            bool scanOnly = false;
            for (int i = 0; i < args.Length; i++) if (args[i] == "--scan") scanOnly = true;
            if (scanOnly)
            {
                System.Collections.Generic.List<GameInfo> gs = GameScanner.Scan();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("共 " + gs.Count + " 个");
                for (int i = 0; i < gs.Count; i++)
                    sb.AppendLine(gs[i].Name + "  |  " + gs[i].Exe + "  |  " + gs[i].Dir);
                try { File.WriteAllText(Path.Combine(Settings.Dir(), "scan.txt"), sb.ToString(), Encoding.UTF8); } catch { }
                return;
            }
            if (restoreOnly)
            {
                string dev = cfg.DisplayDevice != null && cfg.DisplayDevice.Length > 0 ? cfg.DisplayDevice : Native.PrimaryDevice();
                if (dev == null) dev = @"\\.\DISPLAY1";
                int hz;
                Native.ApplyMode(dev, cfg.DeskW, cfg.DeskH, 0, out hz);
                return;
            }

            bool created;
            Mutex mtx = new Mutex(true, "DeltaResAuto_SingleInstance_v1", out created);
            if (!created)
            {
                try { File.WriteAllText(Settings.ShowFlag(), "1"); } catch { }
                AppendLog("已有实例在运行，已发送“唤出窗口”请求");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { CrashLog(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            { CrashLog(e.ExceptionObject as Exception); };

            Application.ApplicationExit += delegate
            {
                try { File.Delete(lockPath); } catch { }
                AppendLog("程序正常退出");
            };

            TrayApp ctx = new TrayApp(cfg, !bg);
            Application.Run(ctx);

            try { File.Delete(lockPath); } catch { }

            GC.KeepAlive(mtx);
        }
    }
}
































