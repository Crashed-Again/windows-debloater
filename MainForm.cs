using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace Cub
{
    internal sealed class BrowserChoice
    {
        public string Name;
        public string Id;
        public BrowserChoice(string name, string id) { Name = name; Id = id; }
        public override string ToString() { return Name; }
    }

    internal sealed class PinDef
    {
        public string Key; public string Title; public string AppId; public bool Default;
        public PinDef(string key, string title, string appId, bool def) { Key = key; Title = title; AppId = appId; Default = def; }
    }

    internal sealed class AppDef
    {
        public string Title; public string Patterns; public string Desc; public bool Default;
        public AppDef(string title, string patterns, string desc, bool def) { Title = title; Patterns = patterns; Desc = desc; Default = def; }
    }

    internal sealed class Profile
    {
        public Dictionary<string, bool> Toggles { get; set; } = new Dictionary<string, bool>();
        public List<string> CustomApps { get; set; } = new List<string>();
        public string Browser { get; set; } = "";
        public string Wallpaper { get; set; } = "";
        public string Extras { get; set; } = "";
    }

    internal sealed class MainForm : Form
    {
        private static int S(int v) { return Theme.S(v); }

        // ---- data ----
        private static readonly AppDef[] Apps =
        {
            new AppDef("Camera", "Microsoft.WindowsCamera", "", true),
            new AppDef("LinkedIn", "*LinkedIn*", "", true),
            new AppDef("WhatsApp", "*WhatsApp*", "", true),
            new AppDef("Xbox and Game Bar", "Microsoft.GamingApp;Microsoft.XboxApp;Microsoft.XboxGamingOverlay;Microsoft.XboxGameOverlay;Microsoft.XboxIdentityProvider;Microsoft.XboxSpeechToTextOverlay;Microsoft.Xbox.TCUI", "All Xbox components", true),
            new AppDef("Clipchamp", "Clipchamp.Clipchamp", "", true),
            new AppDef("News", "Microsoft.BingNews", "", true),
            new AppDef("Weather", "Microsoft.BingWeather", "", true),
            new AppDef("Bing Search", "Microsoft.BingSearch", "", true),
            new AppDef("Solitaire Collection", "Microsoft.MicrosoftSolitaireCollection", "", true),
            new AppDef("Microsoft Teams", "MSTeams;MicrosoftTeams", "", true),
            new AppDef("Microsoft To Do", "Microsoft.Todos", "", true),
            new AppDef("Skype", "Microsoft.SkypeApp", "", true),
            new AppDef("People", "Microsoft.People", "", true),
            new AppDef("Feedback Hub", "Microsoft.WindowsFeedbackHub", "", true),
            new AppDef("Get Help and Tips", "Microsoft.GetHelp;Microsoft.Getstarted", "", true),
            new AppDef("Mixed Reality and 3D", "Microsoft.MixedReality.Portal;Microsoft.Microsoft3DViewer;Microsoft.Print3D", "", true),
            new AppDef("Microsoft 365 hub", "Microsoft.MicrosoftOfficeHub", "", true),
            new AppDef("OneNote", "Microsoft.Office.OneNote", "", true),
            new AppDef("New Outlook", "Microsoft.OutlookForWindows", "", true),
            new AppDef("Dev Home", "Microsoft.Windows.DevHome", "", true),
            new AppDef("Power Automate", "Microsoft.PowerAutomateDesktop", "", true),
            new AppDef("Phone Link", "Microsoft.YourPhone;MicrosoftWindows.CrossDevice", "", true),
            new AppDef("Maps", "Microsoft.WindowsMaps", "", true),
            new AppDef("Media Player and Movies", "Microsoft.ZuneMusic;Microsoft.ZuneVideo", "", true),
            new AppDef("Sound Recorder", "Microsoft.WindowsSoundRecorder", "", true),
            new AppDef("Sticky Notes", "Microsoft.MicrosoftStickyNotes", "", true),
            new AppDef("Quick Assist", "MicrosoftCorporationII.QuickAssist", "", true),
            new AppDef("Microsoft Family", "MicrosoftCorporationII.MicrosoftFamily", "", true),
            new AppDef("Mail and Calendar", "Microsoft.WindowsCommunicationsApps", "", true),
            new AppDef("Wallet", "Microsoft.Wallet", "", true),
            new AppDef("Sponsored apps", "*Spotify*;*TikTok*;*Instagram*;*Facebook*;*Disney*;*Netflix*;*PrimeVideo*;*CandyCrush*;*Twitter*;*Duolingo*;*AdobeExpress*;*Hulu*", "Spotify, TikTok, Instagram, Netflix, Disney+, Candy Crush and friends", true),
            // off by default - tick them if you want them gone too
            new AppDef("Photos", "Microsoft.Windows.Photos", "Off by default", false),
            new AppDef("Paint", "Microsoft.Paint", "Off by default", false),
            new AppDef("Snipping Tool", "Microsoft.ScreenSketch", "Off by default", false),
            new AppDef("Windows Terminal", "Microsoft.WindowsTerminal", "Off by default", false)
        };

        private static readonly PinDef[] Pins =
        {
            new PinDef("calc", "Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", true),
            new PinDef("clock", "Clock", "Microsoft.WindowsAlarms_8wekyb3d8bbwe!App", true),
            new PinDef("notepad", "Notepad", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App", true),
            new PinDef("paint", "Paint", "Microsoft.Paint_8wekyb3d8bbwe!App", false),
            new PinDef("photos", "Photos", "Microsoft.Windows.Photos_8wekyb3d8bbwe!App", false),
            new PinDef("snip", "Snipping Tool", "Microsoft.ScreenSketch_8wekyb3d8bbwe!App", false),
            new PinDef("store", "Microsoft Store", "Microsoft.WindowsStore_8wekyb3d8bbwe!App", false),
            new PinDef("term", "Windows Terminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", false),
            new PinDef("settings", "Settings", "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel", false)
        };

        private static readonly BrowserChoice[] Browsers =
        {
            new BrowserChoice("Don't change my browser", ""),
            new BrowserChoice("Brave", "Brave.Brave"),
            new BrowserChoice("Firefox", "Mozilla.Firefox"),
            new BrowserChoice("Google Chrome", "Google.Chrome"),
            new BrowserChoice("Vivaldi", "Vivaldi.Vivaldi"),
            new BrowserChoice("Opera", "Opera.Opera"),
            new BrowserChoice("Keep Edge (just update it)", "Microsoft.Edge")
        };

        private static readonly string[] PageNames =
            { "Appearance", "AI", "Apps", "Start menu", "Privacy and security", "Performance", "Software", "Apply" };

        // ---- state ----
        private readonly Dictionary<string, ToggleSwitch> _t = new Dictionary<string, ToggleSwitch>();
        private readonly Dictionary<string, bool> _def = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _cat = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _appPat = new Dictionary<string, string>();   // toggle key -> patterns
        private readonly Dictionary<string, Control> _pages = new Dictionary<string, Control>();
        private readonly List<NavButton> _nav = new List<NavButton>();
        private readonly List<string> _customApps = new List<string>();

        private Panel _content, _bottom, _side;
        private FlowLayoutPanel _appsFlow;
        private ComboBox _browser, _preset;
        private TextBox _extras, _customBox, _searchBox;
        private ListView _results;
        private NButton _searchBtn;
        private Label _searchStatus;
        private Label _wpLabel, _status;
        private string _wallpaper = "";
        private RichTextBox _log;
        private ThinProgress _prog;
        private NButton _apply, _restart;
        private bool _running;

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cub");
        private static readonly string LastProfile = Path.Combine(SettingsDir, "last-profile.json");

        public MainForm()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                Theme.Scale = g.DpiX / 96f;

            Text = "Cub by NeonBear";
            try
            {
                using (Stream ico = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico"))
                    if (ico != null) Icon = new Icon(ico);
            }
            catch { }
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(1100), S(740));
            MinimumSize = new Size(S(940), S(620));
            HandleCreated += (s, e) => DarkMode.TitleBar(Handle);

            // dock order matters: Fill first, then edges
            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(S(28), S(20), S(12), 0) };
            Controls.Add(_content);
            BuildBottomBar();
            BuildSidebar();

            BuildAppearance();
            BuildAi();
            BuildApps();
            BuildStart();
            BuildPrivacy();
            BuildPerformance();
            BuildSoftware();
            BuildApplyPage();

            ShowPage("Appearance");
            LoadProfileFile(LastProfile, true);
        }

        // =========================================================== layout pieces
        private void BuildSidebar()
        {
            _side = new Panel { Dock = DockStyle.Left, Width = S(236), BackColor = Theme.Side };
            _side.Resize += (s, e) => _side.Invalidate();
            _side.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                try
                {
                    using (var ico = new Icon(Icon, new Size(S(56), S(56))))
                        g.DrawIcon(ico, new Rectangle(S(18), S(22), S(56), S(56)));
                }
                catch { }
                string ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                using (var big = new Font(Theme.Family, 20f, FontStyle.Bold, GraphicsUnit.Point))
                using (var white = new SolidBrush(Color.White))
                using (var grey = new SolidBrush(Theme.Sub))
                {
                    g.DrawString("Cub", big, white, S(84), S(14));
                    g.DrawString("by NeonBear", Theme.Body, grey, S(86), S(50));
                    g.DrawString("v" + ver, Theme.Small, grey, S(86), S(70));
                }

                // footer: dot-matrix NEONBEAR wordmark + copyright (only when there is room under the menu)
                int fy = _side.Height - S(64);
                if (fy > S(500))
                {
                    Theme.DrawDots(g, "NEONBEAR", S(22), fy, S(3), S(2), Theme.Sub);
                    using (var grey = new SolidBrush(Color.FromArgb(110, 110, 110)))
                        g.DrawString("Copyright (c) 2026 - NeonBear", Theme.Small, grey, S(20), fy + S(30));
                }
            };
            Controls.Add(_side);

            int y = S(116);
            foreach (string name in PageNames)
            {
                var nb = new NavButton { Text = name, Left = 0, Top = y, Width = S(236), Height = S(44), Tag = name };
                string captured = name;
                nb.Click += (s, e) => ShowPage(captured);
                _side.Controls.Add(nb);
                _nav.Add(nb);
                y += S(46);
            }
        }

        private void BuildBottomBar()
        {
            _bottom = new Panel { Dock = DockStyle.Bottom, Height = S(72), BackColor = Theme.Side };
            Controls.Add(_bottom);

            var lbl = new Label { Text = "Preset", ForeColor = Theme.Sub, BackColor = Theme.Side, AutoSize = true, Left = S(24), Top = S(26) };
            _bottom.Controls.Add(lbl);

            _preset = new ComboBox { Left = S(80), Top = S(22), Width = S(270) };
            StyleCombo(_preset);
            _preset.Items.AddRange(new object[]
            {
                "Recommended (my setup)", "Light touch (look + AI only)", "Aggressive (everything on)", "Nothing (clear all)"
            });
            _preset.SelectedIndex = 0;
            _preset.SelectionChangeCommitted += (s, e) => ApplyPreset(_preset.SelectedIndex);
            _bottom.Controls.Add(_preset);

            var save = new NButton { Text = "Save profile", Left = S(366), Top = S(17), Width = S(116), BackColor = Theme.Side };
            save.Click += (s, e) => SaveProfileDialog();
            _bottom.Controls.Add(save);

            var load = new NButton { Text = "Load profile", Left = S(490), Top = S(17), Width = S(116), BackColor = Theme.Side };
            load.Click += (s, e) => LoadProfileDialog();
            _bottom.Controls.Add(load);

            _apply = new NButton { Text = "Apply changes", Primary = true, Width = S(190), Height = S(42), Top = S(15), BackColor = Theme.Side };
            _apply.Click += (s, e) => OnApply();
            _bottom.Controls.Add(_apply);

            _bottom.SizeChanged += (s, e) => { _apply.Left = _bottom.Width - _apply.Width - S(28); };
            _apply.Left = _bottom.Width - _apply.Width - S(28);
        }

        private void StyleCombo(ComboBox cb)
        {
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.FlatStyle = FlatStyle.Flat;
            cb.BackColor = Theme.Input;
            cb.ForeColor = Theme.Text;
            cb.Font = Theme.Body;
        }

        private FlowLayoutPanel NewPage(string name, string title, string sub)
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Theme.Bg,
                Visible = false
            };
            flow.SizeChanged += (s, e) => FitWidth(flow);
            DarkMode.Scrollbars(flow);

            flow.Controls.Add(new Label
            {
                Text = title, Font = Theme.Title, ForeColor = Theme.Text, BackColor = Theme.Bg,
                AutoSize = false, Height = S(46), Margin = new Padding(0, 0, 0, 0)
            });
            flow.Controls.Add(new Label
            {
                Text = sub, Font = Theme.Body, ForeColor = Theme.Sub, BackColor = Theme.Bg,
                AutoSize = false, Height = S(28), Margin = new Padding(0, 0, 0, S(10))
            });

            _content.Controls.Add(flow);
            _pages[name] = flow;
            return flow;
        }

        private void FitWidth(FlowLayoutPanel flow)
        {
            int w = flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - S(6);
            if (w < S(200)) return;
            foreach (Control c in flow.Controls) c.Width = w;
        }

        private void SubHeading(FlowLayoutPanel page, string text)
        {
            page.Controls.Add(new Label
            {
                Text = text, Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Bg,
                AutoSize = false, Height = S(30), Margin = new Padding(0, S(10), 0, S(4))
            });
        }

        /// <summary>Adds a card with a title, description and a toggle switch on the right.</summary>
        private ToggleSwitch AddToggle(FlowLayoutPanel page, string key, string category, string title, string desc,
                                       bool def, bool warn = false, int height = 0)
        {
            bool compact = string.IsNullOrEmpty(desc);
            int h = height > 0 ? height : (compact ? S(46) : S(68));
            var card = new Card { Height = h, Margin = new Padding(0, 0, 0, S(8)) };

            var t = new Label
            {
                Text = title, Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Card,
                AutoSize = false, AutoEllipsis = true, Left = S(18), Height = S(24)
            };
            t.Top = compact ? (h - t.Height) / 2 : S(11);
            card.Controls.Add(t);

            Label d = null;
            if (!compact)
            {
                d = new Label
                {
                    Text = desc, Font = Theme.Small, ForeColor = warn ? Theme.Warn : Theme.Sub, BackColor = Theme.Card,
                    AutoSize = false, AutoEllipsis = true, Left = S(18), Top = S(37), Height = S(22)
                };
                card.Controls.Add(d);
            }

            var sw = new ToggleSwitch { Checked = def };
            card.Controls.Add(sw);

            card.SizeChanged += (s, e) =>
            {
                sw.Left = card.Width - sw.Width - S(20);
                sw.Top = (card.Height - sw.Height) / 2;
                int w = Math.Max(S(60), sw.Left - S(34));
                t.Width = w;
                if (d != null) d.Width = w;
            };

            page.Controls.Add(card);
            _t[key] = sw;
            _def[key] = def;
            _cat[key] = category;
            return sw;
        }

        // =========================================================== pages
        private void BuildAppearance()
        {
            var p = NewPage("Appearance", "Appearance", "How Windows looks and feels. Flip anything off to leave it alone.");
            AddToggle(p, "dark", "Appearance", "Dark mode", "Apps and Windows itself use the dark theme.", true);
            AddToggle(p, "left", "Appearance", "Taskbar icons on the left", "Windows 10 style alignment. Off = keep centered.", true);
            AddToggle(p, "thispc", "Appearance", "File Explorer opens to This PC", "Instead of Home / Quick access.", true);
            AddToggle(p, "wallpaper", "Appearance", "Set wallpaper", "Uses the NEONBEAR dot-matrix image unless you pick your own below.", true);

            var card = new Card { Height = S(70), Margin = new Padding(0, 0, 0, S(8)) };
            var cap = new Label { Text = "Wallpaper image", Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Card, AutoSize = false, Left = S(18), Top = S(11), Height = S(24), Width = S(300) };
            _wpLabel = new Label { Text = "Built-in NEONBEAR wallpaper", Font = Theme.Small, ForeColor = Theme.Sub, BackColor = Theme.Card, AutoSize = false, AutoEllipsis = true, Left = S(18), Top = S(38), Height = S(22) };
            var choose = new NButton { Text = "Choose...", Width = S(110), Height = S(34), BackColor = Theme.Card };
            var reset = new NButton { Text = "Reset", Width = S(80), Height = S(34), BackColor = Theme.Card };
            choose.Click += (s, e) => ChooseWallpaper();
            reset.Click += (s, e) => { _wallpaper = ""; _wpLabel.Text = "Built-in NEONBEAR wallpaper"; };
            card.Controls.AddRange(new Control[] { cap, _wpLabel, choose, reset });
            card.SizeChanged += (s, e) =>
            {
                reset.Left = card.Width - reset.Width - S(20);
                choose.Left = reset.Left - choose.Width - S(8);
                reset.Top = choose.Top = (card.Height - choose.Height) / 2;
                _wpLabel.Width = Math.Max(S(60), choose.Left - S(34));
            };
            p.Controls.Add(card);

            AddToggle(p, "mouse", "Appearance", "Mouse acceleration off", "Turns off \"Enhance pointer precision\" so the cursor moves 1:1.", true);
            AddToggle(p, "sticky", "Appearance", "Disable Sticky Keys shortcut", "No more popup when you tap Shift five times (also Toggle and Filter Keys).", true);
            AddToggle(p, "fileext", "Appearance", "Show file extensions", "See .txt, .exe and so on in File Explorer.", true);
            AddToggle(p, "classicmenu", "Appearance", "Classic right-click menu", "Skip \"Show more options\" on Windows 11.", true);
        }

        private void BuildAi()
        {
            var p = NewPage("AI", "AI", "Get rid of the AI add-ons nobody asked for.");
            AddToggle(p, "copilot", "AI", "Remove Copilot", "Uninstalls the app, hides the taskbar button and sets the policy that switches it off.", true);
            AddToggle(p, "recall", "AI", "Disable Recall and Click to Do", "Stops Windows taking AI snapshots of your screen and analyzing them.", true);
            AddToggle(p, "websearch", "AI", "No web results in Start search", "Search only shows your own apps and files.", true);
        }

        private void BuildApps()
        {
            var p = NewPage("Apps", "Apps", "Pick exactly which built-in apps get removed. Calculator, Clock, Notepad and the Store are never touched.");
            _appsFlow = p;
            AddToggle(p, "removeapps", "Apps", "Remove the apps ticked below", "Master switch. Off = no app is removed, whatever is ticked.", true);
            AddToggle(p, "suggest", "Apps", "Block suggested apps and ads", "Stops Windows reinstalling sponsored apps and showing tips and promotions.", true);

            // custom package box
            var card = new Card { Height = S(68), Margin = new Padding(0, 0, 0, S(8)) };
            var cap = new Label { Text = "Add your own app to remove", Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Card, AutoSize = false, Left = S(18), Top = S(8), Height = S(24), Width = S(400) };
            _customBox = new TextBox { Left = S(18), Top = S(34), Height = S(26), BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Body };
            var add = new NButton { Text = "Add", Width = S(90), Height = S(28), BackColor = Theme.Card };
            card.Controls.AddRange(new Control[] { cap, _customBox, add });
            card.SizeChanged += (s, e) =>
            {
                add.Left = card.Width - add.Width - S(20);
                add.Top = S(33);
                _customBox.Width = Math.Max(S(80), add.Left - S(18) - S(14));
            };
            add.Click += (s, e) =>
            {
                string v = _customBox.Text.Trim();
                if (v.Length == 0) return;
                AddCustomApp(v, true);
                _customBox.Clear();
            };
            _customBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    string v = _customBox.Text.Trim();
                    if (v.Length == 0) return;
                    AddCustomApp(v, true);
                    _customBox.Clear();
                }
            };
            p.Controls.Add(card);
            // tooltip text under the box
            var tip = new ToolTip();
            tip.SetToolTip(_customBox, "A package name, e.g. Microsoft.BingNews or *Spotify* (wildcards work). Run Get-AppxPackage in PowerShell to list names.");

            SubHeading(p, "Built-in apps");
            foreach (AppDef a in Apps)
            {
                string key = "app:" + a.Title;
                AddToggle(p, key, "Apps", a.Title, a.Desc, a.Default, false, string.IsNullOrEmpty(a.Desc) ? S(46) : S(62));
                _appPat[key] = a.Patterns;
            }
        }

        private void AddCustomApp(string pattern, bool on)
        {
            string key = "custom:" + pattern;
            if (_t.ContainsKey(key)) { _t[key].Checked = on; return; }
            AddToggle(_appsFlow, key, "Apps", pattern, "Custom package", on, false, S(62));
            _appPat[key] = pattern;
            _customApps.Add(pattern);
            FitWidth(_appsFlow);
        }

        private void BuildStart()
        {
            var p = NewPage("Start menu", "Start menu", "Clean it up and choose what stays pinned.");
            AddToggle(p, "hiderec", "Start", "Hide the Recommended section", "No recent files or suggestions in Start. May need Windows Pro or newer builds.", true);
            AddToggle(p, "grid", "Start", "All apps in grid view", "Instead of the category or list view.", true);
            AddToggle(p, "pins", "Start", "Replace pinned apps with my list", "Master switch for the list below. Off = keep your current pins.", true);
            SubHeading(p, "Pinned apps");
            foreach (PinDef pin in Pins)
                AddToggle(p, "pin:" + pin.Key, "Start", pin.Title, "", pin.Default);
        }

        private void BuildPrivacy()
        {
            var p = NewPage("Privacy and security", "Privacy and security", "Some of these are one-way trips, so they are flagged.");
            AddToggle(p, "telemetry", "Privacy", "Disable telemetry", "Stops the tracking service and tasks. Home and Pro still send a minimal level - a Windows limit.", true);
            AddToggle(p, "location", "Privacy", "Disable location tracking", "Turns off location services for the whole PC.", true);
            AddToggle(p, "sac", "Privacy", "Disable Smart App Control", "Caution: can not be turned back on without resetting Windows.", true, true);
            AddToggle(p, "bitlocker", "Privacy", "Disable BitLocker / device encryption", "Caution: DECRYPTS your drives (slow, runs in the background).", true, true);
            AddToggle(p, "hibernate", "Privacy", "Disable hibernation", "Frees disk space. Also turns off Fast Startup.", true, true);
        }

        private void BuildPerformance()
        {
            var p = NewPage("Performance", "Performance", "Speed over battery life.");
            AddToggle(p, "ultimate", "Performance", "Ultimate Performance power plan", "Creates and activates it. Some laptops do not offer this plan.", true);
        }

        private void BuildSoftware()
        {
            var p = NewPage("Software", "Software", "Installs and updates through winget. The browser step always runs last.");
            AddToggle(p, "quicklook", "Software", "QuickLook", "Select a file and press Space to preview it, like on macOS.", true);

            // browser picker
            var card = new Card { Height = S(80), Margin = new Padding(0, 0, 0, S(8)) };
            var cap = new Label { Text = "Browser", Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Card, AutoSize = false, Left = S(18), Top = S(11), Height = S(24), Width = S(300) };
            var sub = new Label { Text = "Installs (or updates) the one you pick. Pick Keep Edge to only update it.", Font = Theme.Small, ForeColor = Theme.Sub, BackColor = Theme.Card, AutoSize = false, AutoEllipsis = true, Left = S(18), Top = S(40), Height = S(22) };
            _browser = new ComboBox { Width = S(240) };
            StyleCombo(_browser);
            _browser.Items.AddRange(Browsers);
            _browser.SelectedIndex = 3;   // Google Chrome
            card.Controls.AddRange(new Control[] { cap, sub, _browser });
            card.SizeChanged += (s, e) =>
            {
                _browser.Left = card.Width - _browser.Width - S(20);
                _browser.Top = (card.Height - _browser.Height) / 2;
                sub.Width = Math.Max(S(60), _browser.Left - S(34));
            };
            p.Controls.Add(card);

            AddToggle(p, "browserupdate", "Software", "Update the browser if it is already installed",
                "Optional. Off = install only when missing and never run an update check.", true);

            AddToggle(p, "removeedge", "Software", "Remove Edge after the new browser installs",
                "Only runs if a different browser was picked AND installed successfully. WebView2 is kept.", true);

            // ---- winget search
            SubHeading(p, "Find and install apps");
            var sc = new Card { Height = S(318), Margin = new Padding(0, 0, 0, S(8)) };
            var scap = new Label { Text = "Search winget", Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Card, AutoSize = false, Left = S(18), Top = S(10), Height = S(24), Width = S(300) };
            _searchBox = new TextBox { Left = S(18), Top = S(40), Height = S(26), BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Body };
            _searchBtn = new NButton { Text = "Search", Width = S(100), Height = S(28), BackColor = Theme.Card };
            _results = new ListView
            {
                Left = S(18), Top = S(76), Height = S(186),
                View = View.Details, FullRowSelect = true, MultiSelect = true, HeaderStyle = ColumnHeaderStyle.None,
                BackColor = Color.FromArgb(14, 14, 14), ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
                Font = Theme.Body, HideSelection = false
            };
            _results.Columns.Add("Name", S(260));
            _results.Columns.Add("Id", S(260));
            _results.Columns.Add("Version", S(100));
            DarkMode.Scrollbars(_results);
            _searchStatus = new Label { Text = "Type a name (e.g. vlc, discord, 7zip) and press Search.", Font = Theme.Small, ForeColor = Theme.Sub, BackColor = Theme.Card, AutoSize = false, AutoEllipsis = true, Left = S(18), Top = S(278), Height = S(24) };
            var addSel = new NButton { Text = "Add selected", Width = S(130), Height = S(28), BackColor = Theme.Card };
            sc.Controls.AddRange(new Control[] { scap, _searchBox, _searchBtn, _results, _searchStatus, addSel });
            sc.SizeChanged += (s, e) =>
            {
                _searchBtn.Left = sc.Width - _searchBtn.Width - S(20);
                _searchBtn.Top = S(39);
                _searchBox.Width = Math.Max(S(80), _searchBtn.Left - S(18) - S(12));
                _results.Width = Math.Max(S(200), sc.Width - S(38));
                addSel.Left = sc.Width - addSel.Width - S(20);
                addSel.Top = S(276);
                _searchStatus.Width = Math.Max(S(60), addSel.Left - S(34));
                // keep the columns proportional to the card
                int w = _results.Width - SystemInformation.VerticalScrollBarWidth - S(4);
                _results.Columns[0].Width = (int)(w * 0.42);
                _results.Columns[1].Width = (int)(w * 0.40);
                _results.Columns[2].Width = (int)(w * 0.18);
            };
            _searchBtn.Click += (s, e) => DoSearch();
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DoSearch(); }
            };
            addSel.Click += (s, e) => AddSelectedResults();
            _results.DoubleClick += (s, e) => AddSelectedResults();
            p.Controls.Add(sc);

            // ---- the install list (editable)
            var ex = new Card { Height = S(92), Margin = new Padding(0, 0, 0, S(8)) };
            var excap = new Label { Text = "Apps that will be installed", Font = Theme.Bold, ForeColor = Theme.Text, BackColor = Theme.Card, AutoSize = false, Left = S(18), Top = S(10), Height = S(24), Width = S(400) };
            var exsub = new Label { Text = "winget package IDs, separated by commas. Search results land here; you can also type or delete IDs.", Font = Theme.Small, ForeColor = Theme.Sub, BackColor = Theme.Card, AutoSize = false, AutoEllipsis = true, Left = S(18), Top = S(35), Height = S(22) };
            _extras = new TextBox { Left = S(18), Top = S(58), Height = S(26), BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Body };
            ex.Controls.AddRange(new Control[] { excap, exsub, _extras });
            ex.SizeChanged += (s, e) =>
            {
                _extras.Width = Math.Max(S(80), ex.Width - S(36));
                exsub.Width = Math.Max(S(60), ex.Width - S(36));
            };
            p.Controls.Add(ex);
        }

        // =========================================================== winget search
        private void DoSearch()
        {
            string q = _searchBox.Text.Trim();
            if (q.Length == 0) return;
            _searchBtn.Enabled = false;
            _searchStatus.ForeColor = Theme.Sub;
            _searchStatus.Text = "Searching for \"" + q + "\"...";
            _results.Items.Clear();

            var t = new Thread(() =>
            {
                string error;
                List<WingetHit> hits = WingetSearch.Run(q, out error);
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        _searchBtn.Enabled = true;
                        if (hits == null || hits.Count == 0)
                        {
                            _searchStatus.ForeColor = Theme.Warn;
                            _searchStatus.Text = error ?? "No results.";
                            return;
                        }
                        _results.BeginUpdate();
                        foreach (WingetHit h in hits)
                        {
                            var item = new ListViewItem(h.Name);
                            item.SubItems.Add(h.Id);
                            item.SubItems.Add(h.Version);
                            _results.Items.Add(item);
                        }
                        _results.EndUpdate();
                        _searchStatus.Text = hits.Count + " result(s). Double-click or select and press Add selected.";
                    }));
                }
                catch (InvalidOperationException) { }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void AddSelectedResults()
        {
            var have = new HashSet<string>(
                (_extras.Text ?? "").Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (ListViewItem item in _results.SelectedItems)
            {
                string id = item.SubItems[1].Text;
                if (have.Add(id))
                {
                    _extras.Text = string.IsNullOrWhiteSpace(_extras.Text) ? id : _extras.Text.TrimEnd().TrimEnd(',') + ", " + id;
                    added++;
                }
            }
            _searchStatus.ForeColor = added > 0 ? Theme.Good : Theme.Sub;
            _searchStatus.Text = added > 0 ? "Added " + added + " app(s) to the install list." : "Select a result first (or it is already in the list).";
        }

        private void BuildApplyPage()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };

            // dock order: Fill first, then Top controls (added bottom-to-top)
            _log = new RichTextBox
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 14, 19), ForeColor = Theme.Text, Font = Theme.Mono,
                BorderStyle = BorderStyle.None, ReadOnly = true, DetectUrls = false
            };
            DarkMode.Scrollbars(_log);
            page.Controls.Add(_log);

            var spacer = new Panel { Dock = DockStyle.Top, Height = S(12), BackColor = Theme.Bg };
            page.Controls.Add(spacer);

            var statusRow = new Panel { Dock = DockStyle.Top, Height = S(40), BackColor = Theme.Bg };
            _status = new Label { Text = "Ready. Choose your options, then press Apply changes.", ForeColor = Theme.Sub, BackColor = Theme.Bg, AutoSize = false, Left = 0, Top = S(8), Height = S(24) };
            _restart = new NButton { Text = "Restart PC", Width = S(120), Height = S(34), Top = S(3), BackColor = Theme.Bg, Visible = false };
            _restart.Click += (s, e) =>
            {
                if (MessageBox.Show("Restart now?", "Cub", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 5") { UseShellExecute = false, CreateNoWindow = true });
            };
            statusRow.Controls.Add(_status);
            statusRow.Controls.Add(_restart);
            statusRow.SizeChanged += (s, e) =>
            {
                _restart.Left = statusRow.Width - _restart.Width - S(8);
                _status.Width = Math.Max(S(100), _restart.Left - S(10));
            };
            page.Controls.Add(statusRow);

            _prog = new ThinProgress { Dock = DockStyle.Top, BackColor = Theme.Bg, Height = S(8) };
            page.Controls.Add(_prog);

            var restoreHost = new Panel { Dock = DockStyle.Top, Height = S(58), BackColor = Theme.Bg };
            var rflow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            restoreHost.Controls.Add(rflow);
            AddToggle(rflow, "restore", "Run", "Create a restore point first", "", true);
            rflow.SizeChanged += (s, e) => { foreach (Control c in rflow.Controls) c.Width = rflow.ClientSize.Width - S(6); };
            page.Controls.Add(restoreHost);

            var title = new Label { Text = "Apply", Font = Theme.Title, ForeColor = Theme.Text, BackColor = Theme.Bg, AutoSize = false, Dock = DockStyle.Top, Height = S(46) };
            page.Controls.Add(title);

            _content.Controls.Add(page);
            _pages["Apply"] = page;
        }

        // =========================================================== navigation
        private void ShowPage(string name)
        {
            foreach (var kv in _pages) kv.Value.Visible = kv.Key == name;
            foreach (NavButton nb in _nav) nb.Selected = (string)nb.Tag == name;
        }

        // =========================================================== presets & profiles
        private void ApplyPreset(int idx)
        {
            foreach (string key in _t.Keys.ToList())
            {
                if (key == "restore") continue;
                bool v;
                switch (idx)
                {
                    case 0: v = _def[key]; break;
                    case 1: v = (_cat[key] == "Appearance" || _cat[key] == "AI") && _def[key]; break;
                    case 2: v = true; break;
                    default: v = false; break;
                }
                _t[key].Checked = v;
            }
        }

        private void ChooseWallpaper()
        {
            using (var dlg = new OpenFileDialog { Title = "Pick a wallpaper", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _wallpaper = dlg.FileName;
                    _wpLabel.Text = dlg.FileName;
                    if (_t.ContainsKey("wallpaper")) _t["wallpaper"].Checked = true;
                }
            }
        }

        private Profile CaptureProfile()
        {
            var pr = new Profile();
            foreach (var kv in _t) pr.Toggles[kv.Key] = kv.Value.Checked;
            pr.CustomApps = new List<string>(_customApps);
            pr.Browser = ((BrowserChoice)_browser.SelectedItem).Id;
            pr.Wallpaper = _wallpaper;
            pr.Extras = _extras.Text;
            return pr;
        }

        private void RestoreProfile(Profile pr)
        {
            foreach (string c in pr.CustomApps) AddCustomApp(c, true);
            foreach (var kv in pr.Toggles)
                if (_t.ContainsKey(kv.Key)) _t[kv.Key].Checked = kv.Value;
            for (int i = 0; i < _browser.Items.Count; i++)
                if (((BrowserChoice)_browser.Items[i]).Id == pr.Browser) { _browser.SelectedIndex = i; break; }
            _wallpaper = pr.Wallpaper ?? "";
            _wpLabel.Text = string.IsNullOrEmpty(_wallpaper) ? "Built-in NEONBEAR wallpaper" : _wallpaper;
            _extras.Text = pr.Extras ?? "";
        }

        private void SaveProfileFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(CaptureProfile(), new JsonSerializerOptions { WriteIndented = true }));
        }

        private void LoadProfileFile(string path, bool silent)
        {
            try
            {
                if (!File.Exists(path)) return;
                var pr = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path));
                if (pr != null) RestoreProfile(pr);
            }
            catch (Exception e)
            {
                if (!silent) MessageBox.Show("Could not load that profile: " + e.Message, "Cub");
            }
        }

        private void SaveProfileDialog()
        {
            using (var dlg = new SaveFileDialog { Title = "Save profile", Filter = "Cub profile|*.json", FileName = "cub-profile.json" })
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try { SaveProfileFile(dlg.FileName); }
                    catch (Exception e) { MessageBox.Show("Could not save: " + e.Message, "Cub"); }
                }
        }

        private void LoadProfileDialog()
        {
            using (var dlg = new OpenFileDialog { Title = "Load profile", Filter = "Cub profile|*.json" })
                if (dlg.ShowDialog(this) == DialogResult.OK) LoadProfileFile(dlg.FileName, false);
        }

        // =========================================================== apply
        private bool On(string key) { return _t.ContainsKey(key) && _t[key].Checked; }

        private Options BuildOptions()
        {
            var o = new Options();
            foreach (var kv in _t) if (kv.Value.Checked) o.On.Add(kv.Key);

            foreach (var kv in _appPat)
                if (_t[kv.Key].Checked)
                    o.AppPatterns.AddRange(kv.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            foreach (PinDef pin in Pins)
                if (On("pin:" + pin.Key)) o.Pins.Add(pin.AppId);

            var b = (BrowserChoice)_browser.SelectedItem;
            o.BrowserId = b.Id;
            o.BrowserName = b.Name;
            o.WallpaperPath = _wallpaper;
            o.ExtraIds = _extras.Text;
            return o;
        }

        private void OnApply()
        {
            if (_running) return;
            Options o = BuildOptions();

            var warn = new StringBuilder();
            if (o.Has("bitlocker")) warn.AppendLine(" - BitLocker drives will be DECRYPTED");
            if (o.Has("sac")) warn.AppendLine(" - Smart App Control will be turned off for good");
            if (o.Has("hibernate")) warn.AppendLine(" - Hibernation and Fast Startup will be turned off");
            if (o.Has("removeapps") && o.AppPatterns.Count > 0) warn.AppendLine(" - " + o.AppPatterns.Count + " app pattern(s) will be uninstalled");
            if (o.Has("removeedge") && !string.IsNullOrEmpty(o.BrowserId) && o.BrowserId != "Microsoft.Edge")
                warn.AppendLine(" - Microsoft Edge will be removed after " + o.BrowserName + " installs");

            string msg = "Ready to apply your selection.";
            if (warn.Length > 0) msg += "\n\nThis includes:\n" + warn;
            msg += "\nContinue?";
            if (MessageBox.Show(this, msg, "Cub", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try { SaveProfileFile(LastProfile); } catch { }

            _log.Clear();
            _restart.Visible = false;
            _prog.Value = 0;
            _running = true;
            _apply.Enabled = false;
            ShowPage("Apply");

            var worker = new Thread(() =>
            {
                try
                {
                    new Engine(o, AppendLog, SetProgress).Run();
                    AppendLog("All done. Restart your PC to finish everything.", LogKind.Ok);
                    if (!string.IsNullOrEmpty(o.BrowserId)) AppendLog("Set your default browser in Settings > Apps > Default apps.", LogKind.Info);
                }
                catch (Exception ex)
                {
                    AppendLog("Unexpected error: " + ex.Message, LogKind.Warn);
                }
                finally
                {
                    BeginInvoke(new Action(() =>
                    {
                        _running = false;
                        _apply.Enabled = true;
                        _restart.Visible = true;
                        _status.Text = "Finished. A restart is recommended.";
                    }));
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void SetProgress(double v, string text)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() => { _prog.Value = v; _status.Text = text; }));
        }

        private void AppendLog(string text, LogKind kind)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                Color c; string prefix = "";
                switch (kind)
                {
                    case LogKind.Step: c = Theme.Accent; prefix = "\n> "; break;
                    case LogKind.Ok:   c = Theme.Good;   prefix = "   + "; break;
                    case LogKind.Warn: c = Theme.Warn;   prefix = "   ! "; break;
                    default:           c = Theme.Sub;    prefix = "     "; break;
                }
                _log.SelectionStart = _log.TextLength;
                _log.SelectionLength = 0;
                _log.SelectionColor = c;
                _log.AppendText(prefix + text + "\n");
                _log.SelectionStart = _log.TextLength;
                _log.ScrollToCaret();
            }));
        }
    }
}
