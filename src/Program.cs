// PinPeek —— 置顶小窗浏览器：边玩游戏边看攻略视频，全局热键控制播放
// 特点：超薄工具栏 / 无地址栏（可切换）/ 支持缩放 / 可拖边缩放 / 不下载页面 / 无壳无管理员无限制
// 编译：见 build.ps1（.NET Framework 4.8 自带 csc + WebView2 SDK）

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PinPeek
{
    internal class Config
    {
        public string Url = "https://www.bilibili.com";
        public int X = -1, Y = -1, W = 960, H = 600;
        public int OpacityPercent = 100;
        public int ZoomPercent = 100;
        public bool TopMost = true;
        public bool Locked = false;
        public bool ClickThrough = false;
        public bool ShowAddress = false;
        public bool Frameless = true;   // 无边框（可切换成系统边框）
        public string PauseHotkey = "~";    // 暂停/继续
        public string BackHotkey = "5";     // 快退
        public string ForwardHotkey = "6";  // 快进
        public int SeekSeconds = 5;         // 每次快进/快退的秒数
        public bool AutoElevate = true;  // 启动时自动请求管理员权限（游戏是全屏/管理员时热键才收得到）
        public bool SmartPass = true;    // 智能放行：单键热键只在全屏程序 / 白名单程序 / 自己窗口里才吃键
        public List<string> HotkeyApps = new List<string>();   // 【强制当热键】名单（纠正被误判的游戏），小写 exe 名
        public List<string> TypingApps = new List<string>();   // 【打字程序】名单：在里面打字时 5/6/~ 不吃键
        public List<string[]> Bookmarks = new List<string[]>();
        private string path;

        public static Config Load(string p)
        {
            Config c = new Config();
            c.path = p;
            // 默认收藏只在配置文件里没有 [bookmark] 时才补，避免每启动一次就重复叠加一批
            if (!File.Exists(p)) { AddDefaultBookmarks(c); AddDefaultTypingApps(c); return c; }
            bool typingKeySeen = false;
            try
            {
                string section = "";
                foreach (string raw in File.ReadAllLines(p, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).ToLower();
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLower();
                    string v = line.Substring(eq + 1).Trim();
                    if (section == "bookmark")
                    {
                        string[] parts = v.Split('|');
                        if (parts.Length >= 2 && !c.Bookmarks.Exists(x => x[0] == parts[0] && x[1] == parts[1]))
                            c.Bookmarks.Add(new string[] { parts[0], parts[1] });
                        continue;
                    }
                    switch (k)
                    {
                        case "url": c.Url = v; break;
                        case "x": c.X = Int(v, c.X); break;
                        case "y": c.Y = Int(v, c.Y); break;
                        case "w": c.W = Int(v, c.W); break;
                        case "h": c.H = Int(v, c.H); break;
                        case "opacity": c.OpacityPercent = Int(v, c.OpacityPercent); break;
                        case "zoom": c.ZoomPercent = Int(v, c.ZoomPercent); break;
                        case "topmost": c.TopMost = B(v); break;
                        case "locked": c.Locked = B(v); break;
                        case "clickthrough": c.ClickThrough = B(v); break;
                        case "showaddress": c.ShowAddress = B(v); break;
                        case "frameless": c.Frameless = B(v); break;
                        case "pausehotkey": c.PauseHotkey = v; break;
                        case "backhotkey": c.BackHotkey = v; break;
                        case "forwardhotkey": c.ForwardHotkey = v; break;
                        case "seekseconds": c.SeekSeconds = Math.Max(1, Int(v, c.SeekSeconds)); break;
                        case "autoelevate": c.AutoElevate = B(v); break;
                        case "smartpass": c.SmartPass = B(v); break;
                        case "hotkeyapps":
                            foreach (string one in v.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string e = one.Trim().ToLowerInvariant();
                                if (!e.EndsWith(".exe")) e += ".exe";
                                if (!c.HotkeyApps.Contains(e)) c.HotkeyApps.Add(e);
                            }
                            break;
                        case "typingapps":
                            typingKeySeen = true;
                            foreach (string one in v.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string e = one.Trim().ToLowerInvariant();
                                if (!e.EndsWith(".exe")) e += ".exe";
                                if (!c.TypingApps.Contains(e)) c.TypingApps.Add(e);
                            }
                            break;
                    }
                }
            }
            catch { }
            if (c.Bookmarks.Count == 0) AddDefaultBookmarks(c);
            if (!typingKeySeen && c.TypingApps.Count == 0) AddDefaultTypingApps(c);
            if (c.OpacityPercent < 30) c.OpacityPercent = 30;
            if (c.OpacityPercent > 100) c.OpacityPercent = 100;
            if (c.ZoomPercent < 30) c.ZoomPercent = 30;
            if (c.ZoomPercent > 300) c.ZoomPercent = 300;
            return c;
        }

        private static void AddDefaultBookmarks(Config c)
        {
            c.Bookmarks.Add(new string[] { "B站首页", "https://www.bilibili.com" });
            c.Bookmarks.Add(new string[] { "B站搜索", "https://search.bilibili.com" });
            c.Bookmarks.Add(new string[] { "米游社", "https://www.miyoushe.com" });
            c.Bookmarks.Add(new string[] { "抖音", "https://www.douyin.com" });
        }

        // 默认的「打字程序」：在这些程序里 5 / 6 / ~ 正常输入，不当热键。
        // 游戏（无论全屏还是窗口化）不在这里面，所以照常当热键。
        private static readonly string[] DefaultTypingApps = new string[]
        {
            "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe", "360se.exe", "360chrome.exe", "sogouexplorer.exe",
            "wechat.exe", "weixin.exe", "qq.exe", "qqnt.exe", "tim.exe", "dingtalk.exe", "wework.exe", "feishu.exe", "telegram.exe", "discord.exe", "slack.exe", "teams.exe", "ms-teams.exe",
            "winword.exe", "excel.exe", "powerpnt.exe", "wps.exe", "et.exe", "wpp.exe", "onenote.exe",
            "notepad.exe", "notepad++.exe", "sublime_text.exe", "code.exe", "devenv.exe", "idea64.exe", "pycharm64.exe", "gvim.exe",
            "typora.exe", "obsidian.exe", "notion.exe", "xnviewmp.exe",
            // 注意：不要放 explorer.exe —— 桌面（Program Manager）也算 explorer，放进来会导致桌面上热键失效。
            // 资源管理器地址栏输入靠“焦点有文本光标”那条规则兜住。
            "mstsc.exe", "putty.exe", "xshell.exe", "windowsterminal.exe", "cmd.exe", "powershell.exe", "pwsh.exe"
        };

        private static void AddDefaultTypingApps(Config c)
        {
            foreach (string e in DefaultTypingApps)
                if (!c.TypingApps.Contains(e)) c.TypingApps.Add(e);
        }

        private static int Int(string s, int d) { int r; return int.TryParse(s, out r) ? r : d; }
        private static bool B(string s) { return s == "1" || s.ToLower() == "true" || s.ToLower() == "yes"; }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("; PinPeek 配置（可手工编辑；删掉本文件即恢复默认）");
                sb.AppendLine("[set]");
                sb.AppendLine("url=" + Url);
                sb.AppendLine("x=" + X);
                sb.AppendLine("y=" + Y);
                sb.AppendLine("w=" + W);
                sb.AppendLine("h=" + H);
                sb.AppendLine("opacity=" + OpacityPercent);
                sb.AppendLine("zoom=" + ZoomPercent);
                sb.AppendLine("topmost=" + (TopMost ? "1" : "0"));
                sb.AppendLine("locked=" + (Locked ? "1" : "0"));
                sb.AppendLine("clickthrough=" + (ClickThrough ? "1" : "0"));
                sb.AppendLine("showaddress=" + (ShowAddress ? "1" : "0"));
                sb.AppendLine("frameless=" + (Frameless ? "1" : "0"));
                sb.AppendLine("pausehotkey=" + PauseHotkey);
                sb.AppendLine("backhotkey=" + BackHotkey);
                sb.AppendLine("forwardhotkey=" + ForwardHotkey);
                sb.AppendLine("seekseconds=" + SeekSeconds);
                sb.AppendLine("autoelevate=" + (AutoElevate ? "1" : "0"));
                sb.AppendLine("smartpass=" + (SmartPass ? "1" : "0"));
                sb.AppendLine("hotkeyapps=" + string.Join(",", HotkeyApps.ToArray()));
                sb.AppendLine("typingapps=" + string.Join(",", TypingApps.ToArray()));
                sb.AppendLine();
                sb.AppendLine("[bookmark]");
                foreach (string[] b in Bookmarks) sb.AppendLine(b[0] + "=" + b[0] + "|" + b[1]);
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    // ---------- 热键工具 ----------
    internal static class HotkeyUtil
    {
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;

        public static bool TryParse(string s, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) return false;
                string low = p.ToLower();
                if (i < parts.Length - 1 || parts.Length == 1 && IsModifierWord(low))
                {
                    if (low == "ctrl" || low == "control") { mods |= MOD_CONTROL; continue; }
                    if (low == "alt") { mods |= MOD_ALT; continue; }
                    if (low == "shift") { mods |= MOD_SHIFT; continue; }
                    if (low == "win") { mods |= MOD_WIN; continue; }
                }
                uint v;
                if (!TryVk(low, out v)) return false;
                vk = v;
            }
            return vk != 0;
        }

        private static bool IsModifierWord(string low)
        {
            return low == "ctrl" || low == "control" || low == "alt" || low == "shift" || low == "win";
        }

        private static bool TryVk(string low, out uint vk)
        {
            vk = 0;
            if (low.Length == 1)
            {
                char c = low[0];
                if (c >= 'a' && c <= 'z') { vk = (uint)(char.ToUpper(c)); return true; }
                if (c >= '0' && c <= '9') { vk = (uint)c; return true; }
                switch (c)
                {
                    case '~': case '`': vk = 0xC0; return true;
                    case '-': vk = 0xBD; return true;
                    case '=': vk = 0xBB; return true;
                    case '[': vk = 0xDB; return true;
                    case ']': vk = 0xDD; return true;
                    case '\\': vk = 0xDC; return true;
                    case ';': vk = 0xBA; return true;
                    case '\'': vk = 0xDE; return true;
                    case ',': vk = 0xBC; return true;
                    case '.': vk = 0xBE; return true;
                    case '/': vk = 0xBF; return true;
                }
            }
            switch (low)
            {
                case "space": case "空格": vk = 0x20; return true;
                case "tab": vk = 0x09; return true;
                case "enter": case "return": vk = 0x0D; return true;
                case "esc": case "escape": vk = 0x1B; return true;
                case "backspace": vk = 0x08; return true;
                case "insert": vk = 0x2D; return true;
                case "delete": case "del": vk = 0x2E; return true;
                case "home": vk = 0x24; return true;
                case "end": vk = 0x23; return true;
                case "pageup": case "pgup": vk = 0x21; return true;
                case "pagedown": case "pgdn": vk = 0x22; return true;
                case "up": vk = 0x26; return true;
                case "down": vk = 0x28; return true;
                case "left": vk = 0x25; return true;
                case "right": vk = 0x27; return true;
                case "oem3": vk = 0xC0; return true;
                case "oemminus": vk = 0xBD; return true;
                case "oemplus": vk = 0xBB; return true;
            }
            if (low.Length >= 2 && low[0] == 'f')
            {
                int n;
                if (int.TryParse(low.Substring(1), out n) && n >= 1 && n <= 24) { vk = (uint)(0x6F + n); return true; }
            }
            return false;
        }

        public static string Format(uint mods, uint vk)
        {
            StringBuilder sb = new StringBuilder();
            if ((mods & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & MOD_WIN) != 0) sb.Append("Win+");
            sb.Append(VkName(vk));
            return sb.ToString();
        }

        public static string VkName(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            switch (vk)
            {
                case 0xC0: return "~";
                case 0xBD: return "-";
                case 0xBB: return "=";
                case 0xDB: return "[";
                case 0xDD: return "]";
                case 0xDC: return "\\";
                case 0xBA: return ";";
                case 0xDE: return "'";
                case 0xBC: return ",";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0x20: return "空格";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Esc";
                case 0x08: return "Backspace";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x26: return "↑";
                case 0x28: return "↓";
                case 0x25: return "←";
                case 0x27: return "→";
            }
            return "VK_0x" + vk.ToString("X2");
        }
    }

    internal class MainForm : Form
    {
        // ---------- Win32 ----------
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;

        private const string JS_ZOOM_HOOK =
@"(function(){
 if (window.__mbZoomHooked) return; window.__mbZoomHooked = true;
 window.addEventListener('wheel', function(e){
   if (e.ctrlKey) { e.preventDefault(); window.chrome.webview.postMessage('zoom:' + (e.deltaY < 0 ? 1 : -1)); }
 }, {passive:false});
 window.addEventListener('keydown', function(e){
   if (!e.ctrlKey) return;
   var k = e.key;
   if (k === '=' || k === '+') { e.preventDefault(); window.chrome.webview.postMessage('zoom:1'); }
   else if (k === '-') { e.preventDefault(); window.chrome.webview.postMessage('zoom:-1'); }
   else if (k === '0') { e.preventDefault(); window.chrome.webview.postMessage('zoom:reset'); }
 });
 window.addEventListener('message', function(){});
})();";

        private Config cfg;
        private WebView2 web;
        private Panel bar;
        private TextBox addr;
        private Label status;
        private NotifyIcon tray;
        private bool ready = false;
        private int zoomPct;
        private int retryCount = 0;
        private readonly HashSet<string> navRetried = new HashSet<string>();
        private string cfgPath;
        private List<Button> rightButtons = new List<Button>();
        private Button addrToggle;
        private Button pauseBtn;
        private int leftEdgeX = 4;
        private bool exiting = false;            // 已经进入退出流程（防重入）
        private bool resourcesDisposed = false;  // 钩子/热键/托盘/浏览器内核已释放
        private bool videoPaused = false;

        public MainForm(Config c, string cfgFile)
        {
            cfg = c;
            cfgPath = cfgFile;
            zoomPct = cfg.ZoomPercent;
            BuildUi();
            LoadPage();
            if (Program.SelfTestExitSeconds > 0) StartSelfTestExit();
        }

        // 自检：到点自动走一遍真实退出流程（验证进程不会残留）
        private void StartSelfTestExit()
        {
            LogLine("SELFTEST exit in " + Program.SelfTestExitSeconds + "s");
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = Math.Max(1, Program.SelfTestExitSeconds) * 1000;
            t.Tick += delegate { t.Stop(); t.Dispose(); RealExit(); };
            t.Start();
        }

        // ---------- 界面 ----------
        private void BuildUi()
        {
            Text = "PinPeek";
            BackColor = Color.FromArgb(20, 20, 24);
            StartPosition = FormStartPosition.Manual;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = cfg.X < 0 ? wa.Right - cfg.W - 24 : cfg.X;
            int y = cfg.Y < 0 ? wa.Top + 40 : cfg.Y;
            Bounds = new Rectangle(x, y, cfg.W, cfg.H);
            MinimumSize = new Size(280, 220);
            Padding = new Padding(4);          // 关键：留出 4px 边框，鼠标才能拖到窗体边缘缩放
            DoubleBuffered = true;
            TopMost = cfg.TopMost;
            Opacity = cfg.OpacityPercent / 100.0;
            ApplyFrameMode();

            // 工具栏
            bar = new Panel();
            bar.Height = 26;
            bar.Dock = DockStyle.Top;
            bar.BackColor = Color.FromArgb(28, 28, 34);
            bar.MouseDown += DragWindow;
            bar.MouseDoubleClick += delegate { ToggleMaximize(); };
            Controls.Add(bar);

            int bx = 2;
            bx = AddBtn("\uE72B", bx, delegate { Go(-1); }, "后退（Alt+←）");
            bx = AddBtn("\uE72A", bx, delegate { Go(1); }, "前进（Alt+→）");
            bx = AddBtn("\uE72C", bx, delegate { Reload(); }, "刷新（F5）");
            bx = AddBtn("\uE774", bx, delegate { ToggleAddress(); }, "地址栏开/关（Ctrl+L）");
            addrToggle = (Button)bar.Controls[bar.Controls.Count - 1];

            addr = new TextBox();
            addr.BorderStyle = BorderStyle.None;
            addr.BackColor = Color.FromArgb(44, 44, 52);
            addr.ForeColor = Color.WhiteSmoke;
            addr.Font = new Font("Microsoft YaHei UI", 9f);
            addr.Top = 5;
            addr.Left = bx + 2;
            addr.Height = 20;
            addr.Visible = cfg.ShowAddress;
            addr.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Navigate(addr.Text); }
                else if (e.KeyCode == Keys.Escape) { ToggleAddress(); }
            };
            bar.Controls.Add(addr);

            int rx = 4;
            rx = AddRightBtn("\uE8BB", rx, delegate { RealExit(); }, "退出");
            rx = AddRightBtn("\uE740", rx, delegate { ToggleMaximize(); }, "最大化 / 还原（双击工具栏同效）");
            rx = AddRightBtn("\uE921", rx, delegate { MinimizeToTaskbar(); }, "最小化到任务栏");
            rx = AddRightBtn("\uE769", rx, delegate { TogglePauseVideo(); }, "暂停 / 继续 视频（全局热键可改）");
            pauseBtn = (Button)bar.Controls[bar.Controls.Count - 1];
            rx = AddRightBtn("\uE718", rx, delegate { ToggleTopMost(); }, "置顶开关");
            rx = AddRightBtn("\uE712", rx, MoreMenu, "更多设置");

            // 状态条（很薄，只显示提示）
            status = new Label();
            status.Height = 16;
            status.Dock = DockStyle.Bottom;
            status.BackColor = Color.FromArgb(28, 28, 34);
            status.ForeColor = Color.FromArgb(150, 160, 180);
            status.Font = new Font("Microsoft YaHei UI", 7.5f);
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Text = "  就绪";
            Controls.Add(status);

            web = new WebView2();
            web.Dock = DockStyle.Fill;
            web.DefaultBackgroundColor = Color.FromArgb(20, 20, 24);
            Controls.Add(web);
            web.BringToFront();

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "PinPeek 置顶小窗";
            tray.Visible = true;
            ContextMenuStrip tm = new ContextMenuStrip();
            tm.Items.Add("显示/隐藏", null, delegate { ToggleVisible(); });
            tm.Items.Add("置顶开关", null, delegate { ToggleTopMost(); });
            tm.Items.Add("退出", null, delegate { RealExit(); });
            tray.ContextMenuStrip = tm;
            tray.DoubleClick += delegate { ToggleVisible(); };

            KeyPreview = true;
            KeyDown += OnFormKeyDown;
            FormClosing += delegate { SaveState(); };
            LayoutBar();
        }

        private void ApplyFrameMode()
        {
            if (cfg.Frameless)
            {
                FormBorderStyle = FormBorderStyle.None;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                Padding = new Padding(0);
            }
        }

        // ---------- 工具栏布局 ----------
        private int AddBtn(string glyph, int left, EventHandler click, string tip)
        {
            Button b = MakeBtn(glyph, tip, click);
            b.Left = left;
            b.Top = 2;
            bar.Controls.Add(b);
            leftEdgeX = left + b.Width + 1;
            return leftEdgeX;
        }

        private int AddRightBtn(string glyph, int rightOffset, EventHandler click, string tip)
        {
            Button b = MakeBtn(glyph, tip, click);
            b.Top = 2;
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(b);
            rightButtons.Add(b);
            return rightOffset;
        }

        private Button MakeBtn(string glyph, string tip, EventHandler click)
        {
            Button b = new Button();
            b.Text = glyph;
            b.Width = 24;
            b.Height = 22;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 74);
            b.BackColor = Color.FromArgb(28, 28, 34);
            b.ForeColor = Color.Gainsboro;
            b.Font = new Font("Segoe MDL2 Assets", 9.5f);
            b.TabStop = false;
            b.Cursor = Cursors.Hand;
            ToolTip tp = new ToolTip();
            tp.SetToolTip(b, tip);
            b.Click += click;
            return b;
        }

        private void LayoutBar()
        {
            if (bar == null || rightButtons.Count == 0) return;
            int x = bar.Width - 4;
            foreach (Button b in rightButtons)
            {
                x -= b.Width + 1;
                b.Left = x;
            }
            int leftEdge = leftEdgeX + 2;
            int rightEdge = x - 6;
            if (addr != null)
            {
                if (addr.Visible && rightEdge - leftEdge > 80)
                {
                    addr.Left = leftEdge;
                    addr.Width = rightEdge - leftEdge;
                    addr.BringToFront();
                }
                else if (addr.Visible)
                {
                    addr.Visible = false;
                }
            }
        }

        // ---------- 拖动 / 缩放 ----------
        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (cfg.Locked) { SetStatus("位置已锁定（右键更多 → 解锁）"); return; }
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_APP_ACTION)
            {
                DoAction(m.WParam.ToInt32());
                return;
            }
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id > 100)
                {
                    int act = id - 100;
                    Binding b = FindBinding(act);
                    if (b != null)
                    {
                        int now = Environment.TickCount;
                        if (unchecked(now - b.LastTick) > 250)
                        {
                            b.LastTick = now;
                            LogLine("WM_HOTKEY " + b.Label);
                            DoAction(act);
                        }
                    }
                }
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal &&
                FormBorderStyle == FormBorderStyle.None && !cfg.Locked)
            {
                int lp = m.LParam.ToInt32();
                Point p = PointToClient(new Point(lp & 0xFFFF, (lp >> 16) & 0xFFFF));
                int g = 6;
                bool left = p.X <= g, right = p.X >= ClientSize.Width - g;
                bool top = p.Y <= g, bottom = p.Y >= ClientSize.Height - g;
                if (top && left) m.Result = (IntPtr)13;
                else if (top && right) m.Result = (IntPtr)14;
                else if (bottom && left) m.Result = (IntPtr)16;
                else if (bottom && right) m.Result = (IntPtr)17;
                else if (left) m.Result = (IntPtr)10;
                else if (right) m.Result = (IntPtr)11;
                else if (top) m.Result = (IntPtr)12;
                else if (bottom) m.Result = (IntPtr)15;
            }
        }

        // ---------- 浏览器 ----------
        private async void LoadPage()
        {
            try
            {
                string udf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinPeek", "EBWebView");
                Directory.CreateDirectory(udf);
                CoreWebView2EnvironmentOptions opt = new CoreWebView2EnvironmentOptions();
                bool debugMode = File.Exists(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "debug.flag"));
                if (debugMode) opt.AdditionalBrowserArguments = "--remote-debugging-port=9222";
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, udf, opt);
                await web.EnsureCoreWebView2Async(env);
                ready = true;

                CoreWebView2Settings st = web.CoreWebView2.Settings;
                st.AreDefaultContextMenusEnabled = true;
                st.IsStatusBarEnabled = false;
                st.AreDevToolsEnabled = debugMode;
                st.IsZoomControlEnabled = true;

                // 点击链接需要新窗口时，直接在本窗口打开
                web.CoreWebView2.NewWindowRequested += delegate (object s, CoreWebView2NewWindowRequestedEventArgs e)
                {
                    e.Handled = true;
                    if (!string.IsNullOrEmpty(e.Uri)) Navigate(e.Uri);
                };

                // 关键修复：页面被当附件下载时——拦下，然后把它当作普通页面重新打开
                web.CoreWebView2.DownloadStarting += delegate (object s, CoreWebView2DownloadStartingEventArgs e)
                {
                    string uri = "";
                    try { uri = e.DownloadOperation.Uri; } catch { }
                    e.Cancel = true;
                    LogDebug("DOWNLOAD CANCELLED: " + uri);
                    if (!string.IsNullOrEmpty(uri) && (uri.StartsWith("http://") || uri.StartsWith("https://"))
                        && !navRetried.Contains(uri))
                    {
                        navRetried.Add(uri);
                        SetStatus("该链接被站点当成附件下发，正在改用页面方式打开…");
                        Task.Delay(200).ContinueWith(delegate
                        {
                            try { BeginInvoke((Action)delegate { Navigate(uri); }); } catch { }
                        });
                    }
                    else
                    {
                        SetStatus("已阻止下载：" + SafeName(uri) + "（需要下载请用 ⋯ 菜单里的“在系统浏览器打开”）");
                    }
                };

                web.CoreWebView2.WebMessageReceived += delegate (object s, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    string msg = "";
                    try { msg = e.TryGetWebMessageAsString(); } catch { }
                    if (msg == null) return;
                    if (msg == "zoom:1") SetZoom(zoomPct + 10);
                    else if (msg == "zoom:-1") SetZoom(zoomPct - 10);
                    else if (msg == "zoom:reset") SetZoom(100);
                };

                web.CoreWebView2.NavigationCompleted += delegate (object s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    if (e.IsSuccess)
                    {
                        retryCount = 0;
                        SetStatus("已加载 " + zoomPct + "%");
                        ApplyZoom();
                        InjectZoomHook();
                    }
                    else
                    {
                        string failed = cfg.Url;
                        if (retryCount < 2)
                        {
                            retryCount++;
                            SetStatus("加载失败（" + e.WebErrorStatus + "），重试 " + retryCount + "/2 …");
                            Task.Delay(1500).ContinueWith(delegate
                            {
                                try { BeginInvoke((Action)delegate { Navigate(failed); }); } catch { }
                            });
                        }
                        else SetStatus("加载失败：" + e.WebErrorStatus + "（按 ⟳ 重试）");
                    }
                    SyncAddress();
                };

                web.CoreWebView2.SourceChanged += delegate { SyncAddress(); };
                Navigate(cfg.Url);
            }
            catch (Exception ex)
            {
                SetStatus("WebView2 初始化失败：" + ex.Message);
                MessageBox.Show("WebView2 运行时初始化失败：\n" + ex.Message +
                    "\n\n请确认已安装 Microsoft Edge WebView2 Runtime（Win10/11 一般自带）。", "PinPeek");
            }
        }

        private static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.LastIndexOfAny(new char[] { '/', '\\' });
            string n = i >= 0 ? s.Substring(i + 1) : s;
            return n.Length > 60 ? n.Substring(0, 60) : n;
        }

        private void InjectZoomHook()
        {
            try { web.CoreWebView2.ExecuteScriptAsync(JS_ZOOM_HOOK); } catch { }
        }

        private void ApplyZoom()
        {
            try
            {
                double z = zoomPct / 100.0;
                web.CoreWebView2.ExecuteScriptAsync(
                    "try{document.documentElement.style.zoom='" + z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "';}catch(e){}");
            }
            catch { }
        }

        private void SetZoom(int pct)
        {
            if (pct < 30) pct = 30;
            if (pct > 300) pct = 300;
            zoomPct = pct;
            cfg.ZoomPercent = pct;
            ApplyZoom();
            SetStatus("缩放 " + zoomPct + "%");
        }

        private void SyncAddress()
        {
            try
            {
                if (!ready) return;
                cfg.Url = web.CoreWebView2.Source;
                if (addr.Visible && !addr.Focused) addr.Text = cfg.Url;
            }
            catch { }
        }

        private void Navigate(string input)
        {
            if (!ready) return;
            string u = (input ?? "").Trim();
            if (u.Length == 0) return;
            if (!u.Contains("://") && !u.StartsWith("about:") && !u.StartsWith("file:") && !u.StartsWith("edge:"))
            {
                bool looksUrl = u.Contains(".") && !u.Contains(" ");
                u = looksUrl ? "https://" + u : "https://search.bilibili.com/all?keyword=" + Uri.EscapeDataString(u);
            }
            try
            {
                LogDebug("NAVIGATE: " + u);
                web.CoreWebView2.Navigate(u);
                cfg.Url = u;
            }
            catch (Exception ex) { SetStatus("导航失败：" + ex.Message); }
        }

        private void Go(int dir)
        {
            if (!ready) return;
            try
            {
                if (dir < 0 && web.CoreWebView2.CanGoBack) web.CoreWebView2.GoBack();
                else if (dir > 0 && web.CoreWebView2.CanGoForward) web.CoreWebView2.GoForward();
            }
            catch { }
        }

        private void Reload() { try { if (ready) web.CoreWebView2.Reload(); } catch { } }

        private void ToggleAddress()
        {
            if (addr == null) return;
            addr.Visible = !addr.Visible;
            cfg.ShowAddress = addr.Visible;
            LayoutBar();
            if (addr.Visible)
            {
                try { addr.Text = ready ? web.CoreWebView2.Source : cfg.Url; } catch { }
                addr.Focus();
                addr.SelectAll();
            }
        }

        // ---------- 菜单 ----------
        private void MoreMenu(object sender, EventArgs e)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("缩放：" + zoomPct + "%  （Ctrl+滚轮 / Ctrl+加减）");
            m.Items.Add("   放大 10%", null, delegate { SetZoom(zoomPct + 10); });
            m.Items.Add("   缩小 10%", null, delegate { SetZoom(zoomPct - 10); });
            m.Items.Add("   重置 100%", null, delegate { SetZoom(100); });
            ToolStripMenuItem op = new ToolStripMenuItem("透明度");
            int[] steps = new int[] { 100, 90, 80, 70, 60, 50, 40 };
            foreach (int s in steps)
            {
                int v = s;
                op.DropDownItems.Add(s + "%", null, delegate { SetOpacity(v); });
            }
            m.Items.Add(op);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("暂停 / 继续 视频", null, delegate { TogglePauseVideo(); });
            Binding bk = FindBinding(ACT_BACK), fk = FindBinding(ACT_FWD);
            m.Items.Add("快退 " + cfg.SeekSeconds + " 秒", null, delegate { SeekVideo(-cfg.SeekSeconds); });
            m.Items.Add("快进 " + cfg.SeekSeconds + " 秒", null, delegate { SeekVideo(cfg.SeekSeconds); });
            ToolStripMenuItem hkMenu = new ToolStripMenuItem("设置快捷键…");
            hkMenu.DropDownItems.Add("暂停/继续：" + KeyText(ACT_PAUSE), null, delegate { ChangeHotkey(ACT_PAUSE); });
            hkMenu.DropDownItems.Add("快退：" + KeyText(ACT_BACK), null, delegate { ChangeHotkey(ACT_BACK); });
            hkMenu.DropDownItems.Add("快进：" + KeyText(ACT_FWD), null, delegate { ChangeHotkey(ACT_FWD); });
            m.Items.Add(hkMenu);
            m.Items.Add("最小化到任务栏", null, delegate { MinimizeToTaskbar(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("置顶：" + (TopMost ? "开" : "关"), null, delegate { ToggleTopMost(); });
            m.Items.Add("位置锁定：" + (cfg.Locked ? "开" : "关"), null, delegate { ToggleLock(); });
            m.Items.Add("单击穿透：" + (cfg.ClickThrough ? "开" : "关"), null, delegate { ToggleClickThrough(); });
            m.Items.Add("无边框模式：" + (cfg.Frameless ? "开" : "关"), null, delegate { ToggleFrameless(); });
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem bm = new ToolStripMenuItem("收藏站点");
            foreach (string[] b in cfg.Bookmarks)
            {
                string url = b[1];
                bm.DropDownItems.Add(b[0], null, delegate { Navigate(url); });
            }
            bm.DropDownItems.Add(new ToolStripSeparator());
            bm.DropDownItems.Add("把当前页加入收藏", null, delegate
            {
                if (!ready) return;
                string u = web.CoreWebView2.Source;
                if (!cfg.Bookmarks.Exists(x => x[1] == u))
                {
                    cfg.Bookmarks.Add(new string[] { u.Length > 30 ? u.Substring(0, 30) : u, u });
                    SaveState();
                    SetStatus("已收藏：" + u);
                }
                else SetStatus("这个页面已经在收藏里了");
            });
            m.Items.Add(bm);
            m.Items.Add("在系统浏览器中打开当前页", null, delegate
            {
                try { System.Diagnostics.Process.Start(ready ? web.CoreWebView2.Source : cfg.Url); } catch { }
            });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("显示快捷键说明", null, delegate { ShowHelp(); });
            m.Items.Add("热键不灵？以管理员身份重启", null, delegate { RestartAsAdmin(); });
            m.Items.Add("启动时自动提权：" + (cfg.AutoElevate ? "开" : "关"), null, delegate
            {
                cfg.AutoElevate = !cfg.AutoElevate;
                SaveState();
                SetStatus("启动时自动提权：" + (cfg.AutoElevate ? "开（下次启动生效）" : "关"));
            });
            m.Items.Add("单键热键智能放行（打字时不抢键）：" + (cfg.SmartPass ? "开" : "关"), null, delegate
            {
                cfg.SmartPass = !cfg.SmartPass;
                SaveState();
                RegisterAll(false);
                SetStatus("智能放行：" + (cfg.SmartPass
                    ? "开 —— 游戏里当热键，浏览器/聊天/办公软件里正常输入 5 6 ~"
                    : "关 —— 完全原版行为：任何程序里都当热键（打不出 5 6 ~）"));
            });
            ToolStripMenuItem tl = new ToolStripMenuItem("打字程序名单（在这里面不抢按键）");
            tl.DropDownItems.Add("把当前前台程序加入", null, delegate { AddForegroundAppToTypingList(); });
            if (cfg.TypingApps.Count > 0)
            {
                tl.DropDownItems.Add(new ToolStripSeparator());
                foreach (string app in cfg.TypingApps.ToArray())
                {
                    string one = app;
                    tl.DropDownItems.Add("移除 " + one, null, delegate
                    {
                        cfg.TypingApps.Remove(one);
                        SaveState();
                        SetStatus("已从打字程序名单移除：" + one);
                    });
                }
            }
            m.Items.Add(tl);
            ToolStripMenuItem wl = new ToolStripMenuItem("强制当热键名单（游戏被误判时用）");
            wl.DropDownItems.Add("把当前前台程序加入", null, delegate { AddForegroundAppToWhitelist(); });
            if (cfg.HotkeyApps.Count > 0)
            {
                wl.DropDownItems.Add(new ToolStripSeparator());
                foreach (string app in cfg.HotkeyApps.ToArray())
                {
                    string one = app;
                    wl.DropDownItems.Add("移除 " + one, null, delegate
                    {
                        cfg.HotkeyApps.Remove(one);
                        SaveState();
                        SetStatus("已从强制当热键名单移除：" + one);
                    });
                }
            }
            m.Items.Add(wl);
            m.Items.Add("创建开机自启（管理员，免 UAC 提示）", null, delegate { CreateAutoStartTask(); });
            m.Items.Add("查看热键日志", null, delegate { OpenHotkeyLog(); });
            m.Items.Add("热键诊断…", null, delegate { ShowHotkeyDiagnostics(); });
            m.Items.Add("打开配置文件", null, delegate { OpenConfig(); });
            m.Items.Add("退出", null, delegate { RealExit(); });
            m.Show(bar.PointToScreen(new Point(Math.Max(0, bar.Width - 200), bar.Height)));
        }

        private void ShowHelp()
        {
            string msg =
                "【窗口】\n" +
                "  拖工具栏移动；拖窗口四边缩放；双击工具栏最大化/还原\n" +
                "  工具栏右侧：⋯ 设置 ｜ 📌 置顶 ｜ ⏸ 暂停 ｜ ─ 最小化 ｜ ⤢ 最大化 ｜ ✕ 退出\n" +
                "  关掉窗口(✕) / Alt+F4 = 彻底退出，后台不留进程；只想收起来请点 ─\n\n" +
                "【全局热键】（游戏里也生效）\n" +
                "  暂停/继续：" + KeyText(ACT_PAUSE) + "\n" +
                "  快退 " + cfg.SeekSeconds + " 秒：" + KeyText(ACT_BACK) + "\n" +
                "  快进 " + cfg.SeekSeconds + " 秒：" + KeyText(ACT_FWD) + "\n" +
                "  改键：⋯ → 设置快捷键…，按下想用的键即可\n" +
                "  注意：设成单键后该键会被全局拦下（例如 4/5 在网页里打不出来）\n" +
                "  若游戏中没反应：⋯ → 热键不灵？以管理员身份重启\n\n" +
                "【缩放】\n" +
                "  Ctrl + 鼠标滚轮    缩放网页\n" +
                "  Ctrl + = / - / 0   放大 / 缩小 / 重置\n\n" +
                "【常用键】\n" +
                "  F5 刷新    Alt+←/→ 前进后退    Ctrl+L 地址栏\n" +
                "  F11 全屏    Ctrl+Shift+Q 退出";
            MessageBox.Show(msg, "PinPeek 使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string KeyText(int action)
        {
            Binding b = FindBinding(action);
            if (b == null) return "未设置";
            string k = HotkeyUtil.Format(b.Mods, b.Vk);
            return b.RegOk ? k : k + "（钩子模式）";
        }

        private void OpenConfig()
        {
            try { System.Diagnostics.Process.Start("notepad.exe", cfgPath); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void OpenHotkeyLog()
        {
            try
            {
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinPeek", "hotkey.log");
                if (!File.Exists(f)) { MessageBox.Show("还没有日志。先按几下热键再来看。", "PinPeek"); return; }
                System.Diagnostics.Process.Start("notepad.exe", f);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void RestartAsAdmin()
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                SaveState();
                System.Diagnostics.Process.Start(psi);
                RealExit();   // 统一退出通道：先退干净自己，新进程才能拿到单实例锁
            }
            catch (Exception ex)
            {
                MessageBox.Show("提权重启被取消或失败：" + ex.Message, "PinPeek");
            }
        }

        // 用计划任务实现“开机自动以管理员身份运行、且不弹 UAC”
        private void CreateAutoStartTask()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string args = "/create /tn \"PinPeek\" /tr \"\\\"" + exe + "\\\"\" /sc onlogon /rl highest /f";
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", args);
                psi.UseShellExecute = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                if (!Program.IsElevated()) psi.Verb = "runas";
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
                p.WaitForExit(8000);
                if (p.ExitCode == 0)
                    MessageBox.Show("已创建开机自启任务（管理员权限，登录时自动运行，不弹 UAC）。\n\n" +
                        "想取消：任务计划程序里删除名为 PinPeek 的任务，或命令行执行\n" +
                        "schtasks /delete /tn PinPeek /f", "PinPeek",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("创建计划任务失败（退出码 " + p.ExitCode + "）。\n可以手动运行：\nschtasks " + args,
                        "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建计划任务出错：" + ex.Message, "PinPeek");
            }
        }

        // ---------- 功能 ----------
        private void ToggleVisible()
        {
            if (Visible) Hide(); else { Show(); Activate(); }
        }

        private void ToggleTopMost()
        {
            TopMost = !TopMost;
            cfg.TopMost = TopMost;
            SetStatus("置顶：" + (TopMost ? "开" : "关"));
        }

        private void ToggleLock()
        {
            cfg.Locked = !cfg.Locked;
            SetStatus(cfg.Locked ? "位置已锁定" : "位置已解锁");
        }

        private void ToggleClickThrough()
        {
            cfg.ClickThrough = !cfg.ClickThrough;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE).ToInt32();
            if (cfg.ClickThrough) ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            else ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(Handle, GWL_EXSTYLE, (IntPtr)ex);
            SetStatus("单击穿透：" + (cfg.ClickThrough ? "开" : "关"));
        }

        private void ToggleFrameless()
        {
            cfg.Frameless = !cfg.Frameless;
            ApplyFrameMode();
            if (cfg.Frameless) Padding = new Padding(4);
            SetStatus("无边框：" + (cfg.Frameless ? "开" : "关"));
        }

        private void SetOpacity(int pct)
        {
            Opacity = pct / 100.0;
            cfg.OpacityPercent = pct;
            SetStatus("透明度：" + pct + "%");
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        // 最小化到任务栏（不是隐藏到托盘）
        private void MinimizeToTaskbar()
        {
            WindowState = FormWindowState.Minimized;
            SetStatus("已最小化到任务栏");
        }

        // 暂停 / 继续 页面里的视频（页面上有多个 video 时取第一个正在播/有数据的）
        private void TogglePauseVideo()
        {
            if (!ready) return;
            const string js =
                "(function(){" +
                " var vs=[].slice.call(document.querySelectorAll('video'));" +
                " if(!vs.length) return 'novideo';" +
                " var v=vs.filter(function(x){return x.readyState>0;})[0]||vs[0];" +
                " if(v.paused){ v.play(); return 'playing'; } else { v.pause(); return 'paused'; }" +
                "})()";
            try
            {
                web.CoreWebView2.ExecuteScriptAsync(js).ContinueWith(delegate (Task<string> t)
                {
                    string r = "";
                    try { r = (t.Result ?? "").Trim('"'); } catch { }
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (r == "paused") { videoPaused = true; UpdatePauseGlyph(); SetStatus("视频已暂停（再按一次继续）"); }
                            else if (r == "playing") { videoPaused = false; UpdatePauseGlyph(); SetStatus("视频继续播放"); }
                            else SetStatus("当前页面没找到可暂停的视频（需在视频播放页）");
                        });
                    }
                    catch { }
                });
            }
            catch (Exception ex) { SetStatus("暂停失败：" + ex.Message); }
        }

        private void UpdatePauseGlyph()
        {
            if (pauseBtn == null) return;
            pauseBtn.Text = videoPaused ? "\uE768" : "\uE769";  // 播放 / 暂停
        }

        // 快进 / 快退：直接改 video.currentTime，秒数可在 ini 里配（seekseconds）
        private void SeekVideo(double delta)
        {
            if (!ready) return;
            string js =
                "(function(d){" +
                " var vs=[].slice.call(document.querySelectorAll('video'));" +
                " if(!vs.length) return 'novideo';" +
                " var v=vs.filter(function(x){return x.readyState>0;})[0]||vs[0];" +
                " var dur=isFinite(v.duration)?v.duration:1e9;" +
                " var t=v.currentTime+d; if(t<0)t=0; if(t>dur-0.2)t=Math.max(0,dur-0.2);" +
                " v.currentTime=t;" +
                " var m=Math.floor(t/60), s=Math.floor(t%60);" +
                " return 'ok:'+m+':'+(s<10?'0':'')+s;" +
                "})(" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            try
            {
                web.CoreWebView2.ExecuteScriptAsync(js).ContinueWith(delegate (Task<string> t)
                {
                    string r = "";
                    try { r = (t.Result ?? "").Trim('"'); } catch { }
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (r.StartsWith("ok:"))
                                SetStatus((delta > 0 ? "快进 " : "快退 ") + Math.Abs(delta).ToString("0.#") +
                                          " 秒 → " + r.Substring(3));
                            else if (r == "novideo")
                                SetStatus("当前页面没找到视频（需在视频播放页）");
                            else
                                SetStatus("无法跳转（可能是直播或未加载完）");
                        });
                    }
                    catch { }
                });
            }
            catch (Exception ex) { SetStatus("跳转失败：" + ex.Message); }
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { Reload(); e.Handled = true; }
            else if (e.KeyCode == Keys.F11) { ToggleMaximize(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L) { if (!addr.Visible) ToggleAddress(); else addr.Focus(); e.Handled = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.Q) { RealExit(); }
            else if (e.KeyCode == Keys.MediaPlayPause) { DoAction(ACT_PAUSE); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)) { SetZoom(zoomPct + 10); e.Handled = true; }
            else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)) { SetZoom(zoomPct - 10); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D0) { SetZoom(100); e.Handled = true; }
        }

        // ---------- 全局热键：注册式 + 低级键盘钩子双保险 ----------
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // ---- 诊断用：查前台窗口，以及它是不是以管理员身份在跑 ----
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int nMax);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr proc, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int len, out int retLen);

        // 全屏判定 / 显示器信息
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        // 前台线程的键盘焦点信息（用来判断焦点是不是在输入框里：hwndCaret 非零 = 有文本光标）
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO pgui);

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public int rcCaretLeft, rcCaretTop, rcCaretRight, rcCaretBottom;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_APP_ACTION = 0x8001;   // 钩子线程 → UI 线程
        private const int ACT_PAUSE = 1, ACT_BACK = 2, ACT_FWD = 3;

        private class Binding
        {
            public int Action;
            public string Label;       // 暂停/继续、快退、快进
            public string Combo;       // 形如 ~ / 4 / Ctrl+Alt+P
            public uint Mods, Vk;
            public bool RegOk;
            public int LastTick;
        }

        private readonly List<Binding> bindings = new List<Binding>();
        private HookProc hookProc;
        private IntPtr hookHandle = IntPtr.Zero;
        // 钩子回调里只往这个内存队列塞一行记录，写盘放到 UI 线程（文件 IO 会拖慢钩子回调，超时会被系统摘钩）
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> hookLog =
            new System.Collections.Concurrent.ConcurrentQueue<string>();
        private IntPtr lastHookFg = IntPtr.Zero;   // 最近一次收到热键时的前台窗口
        private int lastHookTick = 0;
        private int lastPassTick = 0;              // 上一次“放行”日志的时间（限流用）
        private System.Windows.Forms.Timer diagTimer;
        private bool fgWarned = false;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitBindings();
            RegisterAll(true);
            InstallKeyboardHook();
            StartDiagTimer();
            if (!Program.IsElevated())
                SetStatus("⚠ 当前是普通权限：前台是管理员程序时热键会失效（⋯ → 热键不灵？以管理员身份重启）");
        }

        // 每 1.5 秒做两件事：把钩子收到的事件刷进日志、检查前台程序权限是否比自己高
        private void StartDiagTimer()
        {
            if (diagTimer != null) return;
            diagTimer = new System.Windows.Forms.Timer();
            diagTimer.Interval = 1500;
            diagTimer.Tick += delegate { FlushHookLog(); CheckForegroundPermission(); };
            diagTimer.Start();
        }

        private void FlushHookLog()
        {
            if (hookLog.IsEmpty) return;
            string line;
            string fgInfo = DescribeWindow(lastHookFg);
            while (hookLog.TryDequeue(out line))
                LogLine(line + "   前台=" + fgInfo);
        }

        // PinPeek 不是管理员、但前台程序是管理员时，Windows 的 UIPI 会让钩子收不到任何按键。
        // 这种情况在状态栏直接提示，免得用户以为热键坏了。
        private void CheckForegroundPermission()
        {
            if (Program.IsElevated())
            {
                if (fgWarned) { fgWarned = false; SetStatus("就绪"); }
                return;
            }
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == Handle) return;
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            if (pid == 0 || pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return;
            if (!IsPidElevated(pid)) return;
            if (fgWarned) return;
            fgWarned = true;
            string info = DescribeWindow(fg);
            SetStatus("⚠ 前台是管理员程序（" + info + "），热键收不到 → ⋯ → 热键不灵？以管理员身份重启");
            LogLine("WARN self not elevated, foreground elevated: " + info);
        }

        private static bool IsPidElevated(uint pid)
        {
            if (pid == 0) return false;
            IntPtr h = OpenProcess(0x1000, false, pid);          // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return false;
            try
            {
                IntPtr tok;
                if (!OpenProcessToken(h, 0x0008, out tok)) return false;   // TOKEN_QUERY
                try
                {
                    int elev, ret;
                    if (!GetTokenInformation(tok, 20, out elev, 4, out ret)) return false;   // TokenElevation
                    return elev != 0;
                }
                finally { CloseHandle(tok); }
            }
            finally { CloseHandle(h); }
        }

        private static string DescribeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "(无)";
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            string pname = "?";
            try { pname = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
            return pname + "「" + sb.ToString() + "」" + (IsPidElevated(pid) ? "[管理员]" : "[普通]");
        }

        private void ShowHotkeyDiagnostics()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("是否以管理员运行：" + (Program.IsElevated() ? "是" : "否 —— 前台程序是管理员时热键会收不到"));
            sb.AppendLine("键盘钩子：" + (hookHandle != IntPtr.Zero ? "已安装" : "未安装"));
            foreach (Binding b in bindings)
                sb.AppendLine("  " + b.Label + " = " + HotkeyUtil.Format(b.Mods, b.Vk) +
                              "   注册式热键:" + (b.RegOk ? "成功" : "失败（改由钩子接管）"));
            sb.AppendLine("最近一次收到按键：" + (lastHookTick == 0
                ? "本次启动后还没收到过"
                : ((Environment.TickCount - lastHookTick) / 1000) + " 秒前，当时前台=" + DescribeWindow(lastHookFg)));
            sb.AppendLine("单键热键智能放行：" + (cfg.SmartPass ? "开" : "关（任何程序里都吃键）"));
            sb.AppendLine("强制当热键名单：" + (cfg.HotkeyApps.Count == 0 ? "(空)" : string.Join(", ", cfg.HotkeyApps.ToArray())));
            sb.AppendLine("打字程序名单：" + cfg.TypingApps.Count + " 项" + (cfg.TypingApps.Count == 0 ? "" : "（" + string.Join(", ", cfg.TypingApps.ToArray()) + "）"));
            string why;
            bool active = IsHotkeyContextActive(out why);
            sb.AppendLine("当前前台算不算热键场合：" + (active ? "算 —— 按键会被吃掉" : "不算 —— 按键会正常输入") + "（" + why + "）");
            sb.AppendLine("当前前台窗口：" + DescribeWindow(GetForegroundWindow()));
            sb.AppendLine("日志：" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinPeek", "hotkey.log"));
            MessageBox.Show(sb.ToString(), "PinPeek 热键诊断", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InitBindings()
        {
            bindings.Clear();
            bindings.Add(new Binding { Action = ACT_PAUSE, Label = "暂停/继续", Combo = cfg.PauseHotkey });
            bindings.Add(new Binding { Action = ACT_BACK, Label = "快退", Combo = cfg.BackHotkey });
            bindings.Add(new Binding { Action = ACT_FWD, Label = "快进", Combo = cfg.ForwardHotkey });
        }

        private Binding FindBinding(int action)
        {
            foreach (Binding b in bindings) if (b.Action == action) return b;
            return null;
        }

        private void RegisterAll(bool quiet)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Binding b in bindings)
            {
                uint mods, vk;
                if (!HotkeyUtil.TryParse(b.Combo, out mods, out vk)) { mods = 0; vk = 0; }
                if (b.RegOk) { UnregisterHotKey(Handle, 100 + b.Action); b.RegOk = false; }
                b.Mods = mods; b.Vk = vk;
                // 单键热键在「智能放行」下不能走 RegisterHotKey —— 系统级注册会无条件把数字键吞掉，
                // 那样任何地方都打不出 5 / 6。改由低级键盘钩子处理，钩子里再判断当前是不是在打字。
                if (mods == 0 && cfg.SmartPass)
                {
                    b.RegOk = false;
                    LogLine("HOTKEY " + b.Label + " " + HotkeyUtil.Format(mods, vk) + " 钩子模式(智能放行)");
                    sb.Append(b.Label + "=" + HotkeyUtil.Format(mods, vk) + "(智能放行)   ");
                    continue;
                }
                b.RegOk = RegisterHotKey(Handle, 100 + b.Action, mods, vk);
                LogLine("HOTKEY " + b.Label + " " + HotkeyUtil.Format(mods, vk) + " registerOk=" + b.RegOk);
                sb.Append(b.Label + "=" + HotkeyUtil.Format(mods, vk) + (b.RegOk ? "" : "(钩子接管)") + "   ");
            }
            if (!quiet) SetStatus("热键：" + sb.ToString().Trim());
        }

        private void DoAction(int action)
        {
            switch (action)
            {
                case ACT_PAUSE: TogglePauseVideo(); break;
                case ACT_BACK: SeekVideo(-cfg.SeekSeconds); break;
                case ACT_FWD: SeekVideo(cfg.SeekSeconds); break;
            }
        }

        // ---- 低级键盘钩子：前台是游戏/全屏程序时，注册式热键常收不到，钩子能收到 ----
        private void InstallKeyboardHook()
        {
            if (hookHandle != IntPtr.Zero) return;
            hookProc = KeyboardHookProc;
            hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(null), 0);
            LogLine("HOOK installed=" + (hookHandle != IntPtr.Zero));
        }

        private void RemoveKeyboardHook()
        {
            if (hookHandle == IntPtr.Zero) return;
            UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == 0x0100 || msg == 0x0104)          // WM_KEYDOWN / WM_SYSKEYDOWN
                {
                    KBDLLHOOKSTRUCT kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    foreach (Binding b in bindings)
                    {
                        if (b.Vk == 0 || kb.vkCode != b.Vk || !ModifiersMatch(b.Mods)) continue;
                        // 单键热键：只有“明确在打字”时放行，其余（游戏/桌面/未知程序）一律当热键
                        if (b.Mods == 0 && cfg.SmartPass)
                        {
                            string passReason;
                            if (!IsHotkeyContextActive(out passReason))
                            {
                                int passed = Environment.TickCount;
                                if (unchecked(passed - lastPassTick) > 1000)   // 放行日志每秒最多一条，避免刷屏
                                {
                                    lastPassTick = passed;
                                    lastHookFg = GetForegroundWindow();
                                    hookLog.Enqueue("PASS " + b.Label + " " + HotkeyUtil.Format(b.Mods, b.Vk) + " 原因=" + passReason);
                                }
                                continue;
                            }
                        }
                        int now = Environment.TickCount;
                        if (unchecked(now - b.LastTick) > 250)
                        {
                            b.LastTick = now;
                            lastHookTick = now;
                            lastHookFg = GetForegroundWindow();   // 只记录句柄，不做文件 IO
                            hookLog.Enqueue("HOOK TRIGGER " + b.Label + " " + HotkeyUtil.Format(b.Mods, b.Vk));
                            PostMessage(Handle, WM_APP_ACTION, (IntPtr)b.Action, IntPtr.Zero);
                        }
                        return (IntPtr)1;                    // 吞掉这次按键
                    }
                }
            }
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private bool ModifiersMatch(uint needMods)
        {
            // 游戏里手常按着 Shift 冲刺 / Ctrl 蹲，多按一个修饰键不该让单键热键失效。
            if (needMods == 0) return true;
            bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            bool win = ((GetAsyncKeyState(0x5B) & 0x8000) != 0) || ((GetAsyncKeyState(0x5C) & 0x8000) != 0);
            bool needCtrl = (needMods & HotkeyUtil.MOD_CONTROL) != 0;
            bool needAlt = (needMods & HotkeyUtil.MOD_ALT) != 0;
            bool needShift = (needMods & HotkeyUtil.MOD_SHIFT) != 0;
            bool needWin = (needMods & HotkeyUtil.MOD_WIN) != 0;
            return ctrl == needCtrl && alt == needAlt && shift == needShift && win == needWin;
        }

        // ---------- 单键热键的“什么时候才吃键” ----------
        // 默认（SmartPass 开启）几乎等于原版行为：任何程序里都当热键，只有下面几种情况才放行让按键正常输入——
        //   ① 前台进程在【打字程序名单】里（浏览器 / QQ / 微信 / Office / 编辑器 …）
        //   ② 前台窗口的焦点控件有文本光标（GetGUIThreadInfo 的 hwndCaret 非零，说明正在某个输入框里）
        // 游戏（无论全屏还是窗口化）、桌面、未知程序一律当热键 —— 上一版“只有全屏才吃键”会把窗口化游戏挡掉。
        // 【强制当热键名单】优先级最高，用来纠正被误判成打字程序的游戏。
        private static int cachedPid = 0;
        private static string cachedName = "";

        private bool IsHotkeyContextActive()
        {
            string dummy;
            return IsHotkeyContextActive(out dummy);
        }

        private bool IsHotkeyContextActive(out string reason)
        {
            reason = "";
            if (!cfg.SmartPass) { reason = "智能放行已关"; return true; }
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) { reason = "无前台"; return true; }
            if (fg == Handle) { reason = "自己窗口"; return true; }
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            string exe = ExeNameOf(pid);
            if (exe.Length > 0 && cfg.HotkeyApps.Contains(exe)) { reason = "强制热键名单"; return true; }
            if (exe.Length > 0 && cfg.TypingApps.Contains(exe)) { reason = "打字程序 " + exe; return false; }
            if (HasTextCaret(pid)) { reason = "焦点有文本光标"; return false; }
            reason = "默认当热键";
            return true;
        }

        // 前台线程的键盘焦点控件是否有光标（有光标基本等于“正在某个输入框里”）
        private static bool HasTextCaret(uint foregroundPid)
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                uint pid;
                uint tid = GetWindowThreadProcessId(fg, out pid);
                GUITHREADINFO gti = new GUITHREADINFO();
                gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
                if (!GetGUIThreadInfo(tid, ref gti)) return false;
                return gti.hwndCaret != IntPtr.Zero;
            }
            catch { return false; }
        }

        private static string ExeNameOf(uint pid)
        {
            if ((int)pid == cachedPid && cachedName.Length > 0) return cachedName;
            string n = "";
            try { n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant() + ".exe"; }
            catch { }
            cachedPid = (int)pid;
            cachedName = n;
            return n;
        }

        private static bool IsFullScreenWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) return false;
            RECT r;
            if (!GetWindowRect(hWnd, out r)) return false;
            IntPtr mon = MonitorFromWindow(hWnd, 2);   // MONITOR_DEFAULTTONEAREST
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(mon, ref mi)) return false;
            int mw = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int mh = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            int ww = r.Right - r.Left, wh = r.Bottom - r.Top;
            return ww >= mw - 4 && wh >= mh - 4;      // 覆盖整个显示器（留 4px 容差）
        }

        // 把当前前台程序加进【强制当热键】名单（个别游戏被误判成打字程序时用）
        private void AddForegroundAppToWhitelist()
        {
            IntPtr fg = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            string exe = ExeNameOf(pid);
            if (exe.Length == 0) { MessageBox.Show("没认出当前前台程序。", "PinPeek"); return; }
            cfg.TypingApps.Remove(exe);
            if (cfg.HotkeyApps.Contains(exe))
            {
                MessageBox.Show(exe + " 已经在【强制当热键】名单里了。", "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            cfg.HotkeyApps.Add(exe);
            SaveState();
            SaveState();
            MessageBox.Show("已加入【强制当热键】：" + exe + "\n以后这个程序在前台时，5 / 6 / ~ 一定会当热键用。",
                "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // 把当前前台程序加进【打字程序】名单（在里面打字时不抢键）
        private void AddForegroundAppToTypingList()
        {
            IntPtr fg = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            string exe = ExeNameOf(pid);
            if (exe.Length == 0) { MessageBox.Show("没认出当前前台程序。", "PinPeek"); return; }
            cfg.HotkeyApps.Remove(exe);
            if (cfg.TypingApps.Contains(exe))
            {
                MessageBox.Show(exe + " 已经在【打字程序】名单里了。", "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            cfg.TypingApps.Add(exe);
            SaveState();
            MessageBox.Show("已加入【打字程序】：" + exe + "\n在这个程序里打字时，5 / 6 / ~ 会正常输入、不当热键。",
                "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ChangeHotkey(int action)
        {
            Binding target = FindBinding(action);
            if (target == null) return;
            string prev = target.Combo;
            foreach (Binding b in bindings) if (b.RegOk) { UnregisterHotKey(Handle, 100 + b.Action); b.RegOk = false; }
            RemoveKeyboardHook();                       // 钩子也要停，否则按目标键会被自己吃掉
            HotkeyCaptureForm f = new HotkeyCaptureForm(target.Label, HotkeyUtil.Format(target.Mods, target.Vk));
            DialogResult dr;
            try { dr = f.ShowDialog(this); }
            finally { f.Dispose(); InstallKeyboardHook(); }
            if (dr != DialogResult.OK || f.Vk == 0)
            {
                RegisterAll(true);
                return;
            }
            target.Combo = HotkeyUtil.Format(f.Mods, f.Vk);
            if (action == ACT_PAUSE) cfg.PauseHotkey = target.Combo;
            else if (action == ACT_BACK) cfg.BackHotkey = target.Combo;
            else cfg.ForwardHotkey = target.Combo;
            RegisterAll(false);
            SaveState();
            MessageBox.Show(target.Label + " 热键已改为：" + target.Combo, "PinPeek",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- 状态 / 日志 / 退出 ----------
        private void SetStatus(string s)
        {
            if (status != null) status.Text = "  " + s;
        }

        private void LogDebug(string s)
        {
            try
            {
                string flag = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "debug.flag");
                if (!File.Exists(flag)) return;
                File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "debug.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        // 常驻诊断日志（很小，写到 %LOCALAPPDATA%\PinPeek\hotkey.log，超过 100KB 自动清空）
        private void LogLine(string s)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinPeek");
                Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "hotkey.log");
                if (File.Exists(f) && new FileInfo(f).Length > 100 * 1024) File.Delete(f);
                File.AppendAllText(f, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
            catch { }
        }

        private void SaveState()
        {
            try
            {
                if (WindowState == FormWindowState.Normal) { cfg.X = Left; cfg.Y = Top; cfg.W = Width; cfg.H = Height; }
                cfg.TopMost = TopMost;
                cfg.OpacityPercent = (int)Math.Round(Opacity * 100);
                cfg.ZoomPercent = zoomPct;
                cfg.Save();
            }
            catch { }
        }

        private void RealExit()
        {
            if (exiting) return;
            exiting = true;
            LogLine("EXIT begin");
            try { SaveState(); } catch { }

            // 硬超时兜底：3 秒内还没退干净就强制结束进程。
            // WebView2 偶尔会卡在关闭上（渲染进程没回应），那样光靠 Close/Application.Exit
            // 会让进程挂在后台——这正是"点了 × 但任务管理器里还有 PinPeek"的成因。
            System.Threading.Thread watchdog = new System.Threading.Thread(delegate ()
            {
                System.Threading.Thread.Sleep(3000);
                Program.LogText("EXIT watchdog -> hard exit");
                Environment.Exit(0);
            });
            watchdog.IsBackground = true;
            watchdog.Name = "PinPeekExitWatchdog";
            watchdog.Start();

            DisposeResources();
            try { Close(); } catch { }
            try { Application.Exit(); } catch { }
            Environment.Exit(0);   // 主线程直接结束，进程一定会消失
        }

        // 释放低级键盘钩子、全局热键、托盘图标、WebView2 内核；可重复调用
        private void DisposeResources()
        {
            if (resourcesDisposed) return;
            resourcesDisposed = true;
            try { RemoveKeyboardHook(); } catch { }
            try
            {
                foreach (Binding b in bindings)
                    if (b.RegOk) { UnregisterHotKey(Handle, 100 + b.Action); b.RegOk = false; }
            }
            catch { }
            try
            {
                if (tray != null)
                {
                    tray.Visible = false;
                    tray.ContextMenuStrip = null;
                    tray.Dispose();
                    tray = null;
                }
            }
            catch { }
            try
            {
                ready = false;
                if (web != null) web.Dispose();
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 任何来源的关闭都等于"退出"：工具栏 ✕、Alt+F4、任务栏右键关闭、系统关机。
            // 不再隐藏到托盘（想收起来用 ─ 最小化，或托盘菜单里的"显示/隐藏"）。
            if (!exiting)
            {
                e.Cancel = true;
                try
                {
                    if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { RealExit(); });
                    else RealExit();
                }
                catch { }
                return;
            }
            DisposeResources();
            base.OnFormClosing(e);
        }
    }

    // ---------- 热键捕获对话框 ----------
    internal class HotkeyCaptureForm : Form
    {
        public uint Mods = 0;
        public uint Vk = 0;
        private Label tip;

        public HotkeyCaptureForm(string label, string current)
        {
            Text = "设置热键：" + label;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 130);
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(32, 32, 38);
            ForeColor = Color.Gainsboro;

            tip = new Label();
            tip.Dock = DockStyle.Fill;
            tip.TextAlign = ContentAlignment.MiddleCenter;
            tip.Font = new Font("Microsoft YaHei UI", 9.5f);
            tip.Text = "正在设置：" + label + "\n请直接按下你想用的键（单键如 4 也可以；Esc 取消）\n当前：" + current;
            Controls.Add(tip);

            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu ||
                    e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin || e.KeyCode == Keys.None)
                    return;
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return; }
                uint mods = 0;
                if (e.Control) mods |= HotkeyUtil.MOD_CONTROL;
                if (e.Alt) mods |= HotkeyUtil.MOD_ALT;
                if (e.Shift) mods |= HotkeyUtil.MOD_SHIFT;
                Mods = mods;
                Vk = (uint)e.KeyCode;
                tip.Text = "已选择：" + HotkeyUtil.Format(Mods, Vk);
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }

    internal static class Program
    {
        // 自检开关（开发用）：--selftest-exit=秒数，到点自动走一遍退出流程。
        // 用来验证"点 ✕ 后进程是否真的消失"，含管理员权限下的情况。
        public static int SelfTestExitSeconds = 0;

        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a != null && a.StartsWith("--selftest-exit=", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(a.Substring("--selftest-exit=".Length), out SelfTestExitSeconds);
                }
            }
            try { Run(); }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "crash.log"),
                        DateTime.Now.ToString("s") + "  " + ex + "\r\n\r\n", Encoding.UTF8);
                }
                catch { }
                MessageBox.Show("启动失败：\n" + ex.Message, "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string cfgFile = Path.Combine(dir, "pinpeek.ini");
            Config cfg = Config.Load(cfgFile);

            // 自动提权：游戏（如原神）常以管理员身份运行，Windows 的 UIPI 规则会让
            // 普通权限进程收不到全局热键 / 低级键盘钩子。提权后热键在任何前台程序下都有效。
            if (!IsElevated())
            {
                LogText("START elevated=False");
                if (cfg.AutoElevate && TrySelfElevate())
                    return;   // 已经把提权后的自己拉起来，本进程退出
            }
            else
            {
                LogText("START elevated=True");
            }

            // 单实例：避免多个窗口抢同一个热键，导致“有时灵有时不灵”。
            // 退出后立刻重启（菜单里的“以管理员身份重启”）时，旧进程松锁要一点时间，所以重试几秒。
            bool created = false;
            System.Threading.Mutex mutex = null;
            for (int i = 0; i < 30 && !created; i++)
            {
                mutex = new System.Threading.Mutex(true, "PinPeek.SingleInstance.v1", out created);
                if (!created)
                {
                    mutex.Dispose();
                    mutex = null;
                    System.Threading.Thread.Sleep(200);
                }
            }
            if (!created)
            {
                MessageBox.Show("PinPeek 已经在运行了。\n请到任务栏 / 托盘图标那里找回它，不要重复启动。",
                    "PinPeek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            GC.KeepAlive(mutex);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "crash.log"),
                        DateTime.Now.ToString("s") + "  UI: " + e.Exception + "\r\n\r\n", Encoding.UTF8);
                }
                catch { }
            };
            Application.Run(new MainForm(cfg, cfgFile));
            Program.LogText("EXIT message loop returned");
            Environment.Exit(0);   // 兜底：消息循环结束后进程必须消失，不留后台残留
        }

        public static bool IsElevated()
        {
            try
            {
                System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private static bool TrySelfElevate()
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                // 命令行参数一并带过去，免得提权后丢掉（如 --selftest-exit=）
                string[] rawArgs = Environment.GetCommandLineArgs();
                StringBuilder argSb = new StringBuilder();
                for (int i = 1; i < rawArgs.Length; i++)
                {
                    if (argSb.Length > 0) argSb.Append(' ');
                    argSb.Append('"').Append(rawArgs[i]).Append('"');
                }
                psi.Arguments = argSb.ToString();
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch
            {
                // 用户点了“否”或 UAC 被禁用：以普通权限继续运行（游戏里热键会失效）
                LogText("ELEVATE cancelled -> running unelevated");
                return false;
            }
        }

        public static void LogText(string s)
        {
            try
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinPeek");
                Directory.CreateDirectory(d);
                string f = Path.Combine(d, "hotkey.log");
                if (File.Exists(f) && new FileInfo(f).Length > 100 * 1024) File.Delete(f);
                File.AppendAllText(f, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }
}








