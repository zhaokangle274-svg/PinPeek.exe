// MabaoClone v2 —— 仿「妈宝」的置顶小窗浏览器（自主实现）
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

namespace MabaoClone
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
        public List<string[]> Bookmarks = new List<string[]>();
        private string path;

        public static Config Load(string p)
        {
            Config c = new Config();
            c.path = p;
            c.Bookmarks.Add(new string[] { "B站首页", "https://www.bilibili.com" });
            c.Bookmarks.Add(new string[] { "B站搜索", "https://search.bilibili.com" });
            c.Bookmarks.Add(new string[] { "米游社", "https://www.miyoushe.com" });
            c.Bookmarks.Add(new string[] { "抖音", "https://www.douyin.com" });
            if (!File.Exists(p)) return c;
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
                        if (parts.Length >= 2) c.Bookmarks.Add(new string[] { parts[0], parts[1] });
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
                    }
                }
            }
            catch { }
            if (c.OpacityPercent < 30) c.OpacityPercent = 30;
            if (c.OpacityPercent > 100) c.OpacityPercent = 100;
            if (c.ZoomPercent < 30) c.ZoomPercent = 30;
            if (c.ZoomPercent > 300) c.ZoomPercent = 300;
            return c;
        }

        private static int Int(string s, int d) { int r; return int.TryParse(s, out r) ? r : d; }
        private static bool B(string s) { return s == "1" || s.ToLower() == "true" || s.ToLower() == "yes"; }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("; MabaoClone 配置（可手工编辑；删掉本文件即恢复默认）");
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
        private bool reallyExit = false;
        private bool videoPaused = false;

        public MainForm(Config c, string cfgFile)
        {
            cfg = c;
            cfgPath = cfgFile;
            zoomPct = cfg.ZoomPercent;
            BuildUi();
            LoadPage();
        }

        // ---------- 界面 ----------
        private void BuildUi()
        {
            Text = "MabaoClone";
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
            tray.Text = "MabaoClone 置顶小窗";
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
                string udf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MabaoClone", "EBWebView");
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
                    "\n\n请确认已安装 Microsoft Edge WebView2 Runtime（Win10/11 一般自带）。", "MabaoClone");
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
                cfg.Bookmarks.Add(new string[] { u.Length > 30 ? u.Substring(0, 30) : u, u });
                SaveState();
                SetStatus("已收藏：" + u);
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
            m.Items.Add("创建开机自启（管理员，免 UAC 提示）", null, delegate { CreateAutoStartTask(); });
            m.Items.Add("查看热键日志", null, delegate { OpenHotkeyLog(); });
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
                "  关掉窗口(✕) = 隐藏到托盘，托盘双击恢复；要收进任务栏请点 ─\n\n" +
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
            MessageBox.Show(msg, "MabaoClone 使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MabaoClone", "hotkey.log");
                if (!File.Exists(f)) { MessageBox.Show("还没有日志。先按几下热键再来看。", "MabaoClone"); return; }
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
                reallyExit = true;
                Close();
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("提权重启被取消或失败：" + ex.Message, "MabaoClone");
            }
        }

        // 用计划任务实现“开机自动以管理员身份运行、且不弹 UAC”
        private void CreateAutoStartTask()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string args = "/create /tn \"MabaoClone\" /tr \"\\\"" + exe + "\\\"\" /sc onlogon /rl highest /f";
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", args);
                psi.UseShellExecute = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                if (!Program.IsElevated()) psi.Verb = "runas";
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
                p.WaitForExit(8000);
                if (p.ExitCode == 0)
                    MessageBox.Show("已创建开机自启任务（管理员权限，登录时自动运行，不弹 UAC）。\n\n" +
                        "想取消：任务计划程序里删除名为 MabaoClone 的任务，或命令行执行\n" +
                        "schtasks /delete /tn MabaoClone /f", "MabaoClone",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("创建计划任务失败（退出码 " + p.ExitCode + "）。\n可以手动运行：\nschtasks " + args,
                        "MabaoClone", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建计划任务出错：" + ex.Message, "MabaoClone");
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitBindings();
            RegisterAll(true);
            InstallKeyboardHook();
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
                        int now = Environment.TickCount;
                        if (unchecked(now - b.LastTick) > 250)
                        {
                            b.LastTick = now;
                            PostMessage(Handle, WM_APP_ACTION, (IntPtr)b.Action, IntPtr.Zero);
                        }
                        LogLine("HOOK TRIGGER " + b.Label + " " + HotkeyUtil.Format(b.Mods, b.Vk));
                        return (IntPtr)1;                    // 吞掉这次按键
                    }
                }
            }
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private bool ModifiersMatch(uint needMods)
        {
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
            MessageBox.Show(target.Label + " 热键已改为：" + target.Combo, "MabaoClone",
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

        // 常驻诊断日志（很小，写到 %LOCALAPPDATA%\MabaoClone\hotkey.log，超过 100KB 自动清空）
        private void LogLine(string s)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MabaoClone");
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
            reallyExit = true;
            SaveState();
            Close();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                SetStatus("已隐藏到托盘");
                return;
            }
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            RemoveKeyboardHook();
            foreach (Binding b in bindings) if (b.RegOk) { UnregisterHotKey(Handle, 100 + b.Action); b.RegOk = false; }
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
        [STAThread]
        private static void Main()
        {
            try { Run(); }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "crash.log"),
                        DateTime.Now.ToString("s") + "  " + ex + "\r\n\r\n", Encoding.UTF8);
                }
                catch { }
                MessageBox.Show("启动失败：\n" + ex.Message, "MabaoClone", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string cfgFile = Path.Combine(dir, "mabao.ini");
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

            // 单实例：避免多个窗口抢同一个热键，导致“有时灵有时不灵”
            bool created;
            System.Threading.Mutex mutex = new System.Threading.Mutex(true, "MabaoClone.SingleInstance.v1", out created);
            if (!created)
            {
                MessageBox.Show("MabaoClone 已经在运行了。\n请到任务栏 / 托盘图标那里找回它，不要重复启动。",
                    "MabaoClone", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MabaoClone");
                Directory.CreateDirectory(d);
                string f = Path.Combine(d, "hotkey.log");
                if (File.Exists(f) && new FileInfo(f).Length > 100 * 1024) File.Delete(f);
                File.AppendAllText(f, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }
}








