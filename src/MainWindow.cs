using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace Pomodoro
{
    public class MainWindow : Window
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        private const double RingSize = 240;
        private const double RingRadius = 104;

        private readonly Preferences prefs;
        private readonly TimerEngine engine;
        private readonly FrameworkElement root;

        private readonly Grid titleBar;
        private readonly ToggleButton pinBtn;
        private readonly ToggleButton clockBtn;
        private readonly Button minBtn;
        private readonly Button closeBtn;
        private readonly Button settingsBtn;

        private readonly RadioButton tabFocus;
        private readonly RadioButton tabShort;
        private readonly RadioButton tabLong;

        private readonly TextBlock modeLabel;
        private readonly TextBlock timeText;
        private readonly TextBlock todayStats;
        private readonly TextBlock toastText;
        private readonly StackPanel dotsPanel;

        private readonly Path progressPath;
        private readonly Path glowPath;
        private readonly Ellipse glow1;

        private readonly Button startBtn;
        private readonly Button resetBtn;
        private readonly Button skipBtn;

        private readonly Border toast;
        private readonly Grid settingsOverlay;
        private readonly TextBox setFocus;
        private readonly TextBox setShort;
        private readonly TextBox setLong;
        private readonly TextBox setEvery;
        private readonly CheckBox setAuto;
        private readonly CheckBox setSound;

        private SolidColorBrush accentBrush;
        private SolidColorBrush accentSoftBrush;
        private readonly SolidColorBrush dotIdleBrush;

        private ClockWindow clock;
        private DispatcherTimer toastTimer;
        private bool syncingTabs;

        private Mode lastMode = (Mode)(-1);
        private int lastCycle = -1;
        private int lastPomodoros = -1;
        private int lastMinutes = -1;

        public MainWindow(Preferences p, TimerEngine e)
        {
            prefs = p;
            engine = e;

            root = Xaml.Load("MainWindow.xaml");
            Content = root;

            Title = "番茄时钟";
            Width = prefs.WindowWidth;
            Height = prefs.WindowHeight;
            MinWidth = 330;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0D, 0x11));
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            Topmost = prefs.TopMost;

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

            accentBrush = (SolidColorBrush)root.FindResource("Accent");
            accentSoftBrush = (SolidColorBrush)root.FindResource("AccentSoft");

            dotIdleBrush = new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
            dotIdleBrush.Freeze();

            titleBar = Get<Grid>("TitleBar");
            pinBtn = Get<ToggleButton>("PinBtn");
            clockBtn = Get<ToggleButton>("ClockBtn");
            minBtn = Get<Button>("MinBtn");
            closeBtn = Get<Button>("CloseBtn");
            settingsBtn = Get<Button>("SettingsBtn");

            tabFocus = Get<RadioButton>("TabFocus");
            tabShort = Get<RadioButton>("TabShort");
            tabLong = Get<RadioButton>("TabLong");

            modeLabel = Get<TextBlock>("ModeLabel");
            timeText = Get<TextBlock>("TimeText");
            todayStats = Get<TextBlock>("TodayStats");
            toastText = Get<TextBlock>("ToastText");
            dotsPanel = Get<StackPanel>("DotsPanel");

            progressPath = Get<Path>("ProgressPath");
            glowPath = Get<Path>("GlowPath");
            glow1 = Get<Ellipse>("Glow1");

            startBtn = Get<Button>("StartBtn");
            resetBtn = Get<Button>("ResetBtn");
            skipBtn = Get<Button>("SkipBtn");

            toast = Get<Border>("Toast");
            settingsOverlay = Get<Grid>("SettingsOverlay");
            setFocus = Get<TextBox>("SetFocus");
            setShort = Get<TextBox>("SetShort");
            setLong = Get<TextBox>("SetLong");
            setEvery = Get<TextBox>("SetEvery");
            setAuto = Get<CheckBox>("SetAuto");
            setSound = Get<CheckBox>("SetSound");

            WireEvents();
            FillSettings();

            pinBtn.IsChecked = prefs.TopMost;
            clockBtn.IsChecked = prefs.ShowClock;

            Loaded += OnLoaded;
            Closing += OnClosing;
            SourceInitialized += OnSourceInitialized;

            RenderMeta();
            Render();
        }

        private T Get<T>(string name) where T : class
        {
            return (T)root.FindName(name);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (prefs.ShowClock) ShowClock();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null) source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                FixMaximizedBounds(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        // 无边框窗口（WindowChrome）最大化时会被窗口边框撑出屏幕，这里把最大化尺寸限制在工作区内
        private static void FixMaximizedBounds(IntPtr hwnd, IntPtr lParam)
        {
            var info = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref mi))
                {
                    RECT work = mi.rcWork;
                    RECT scr = mi.rcMonitor;
                    info.ptMaxPosition.X = work.Left - scr.Left;
                    info.ptMaxPosition.Y = work.Top - scr.Top;
                    info.ptMaxSize.X = work.Right - work.Left;
                    info.ptMaxSize.Y = work.Bottom - work.Top;
                    info.ptMaxTrackSize.X = work.Right - work.Left;
                    info.ptMaxTrackSize.Y = work.Bottom - work.Top;
                }
            }
            Marshal.StructureToPtr(info, lParam, true);
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (prefs.ShowClock) prefs.ShowClock = clock != null;
            prefs.WindowWidth = ActualWidth;
            prefs.WindowHeight = ActualHeight;
            prefs.Save();
        }

        private void WireEvents()
        {
            titleBar.MouseLeftButtonDown += OnTitleBarMouseDown;

            pinBtn.Click += OnPinClick;
            clockBtn.Click += OnClockClick;
            settingsBtn.Click += delegate { ShowSettings(); };
            minBtn.Click += delegate { WindowState = WindowState.Minimized; };
            closeBtn.Click += delegate { Close(); };

            tabFocus.Checked += delegate { OnTab(Mode.Focus); };
            tabShort.Checked += delegate { OnTab(Mode.Short); };
            tabLong.Checked += delegate { OnTab(Mode.Long); };

            startBtn.Click += delegate { engine.Toggle(); };
            resetBtn.Click += delegate { engine.Reset(); };
            skipBtn.Click += delegate { engine.Skip(); };

            engine.Updated += Render;
            engine.Notify += ShowToast;

            settingsOverlay.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.OriginalSource == settingsOverlay) HideSettings();
            };

            Get<Button>("DoneBtn").Click += delegate { HideSettings(); };
            Get<Button>("CloseSettingsBtn").Click += delegate { HideSettings(); };
            Get<Button>("DefaultsBtn").Click += OnRestoreDefaults;

            TextBox[] boxes = { setFocus, setShort, setLong, setEvery };
            foreach (TextBox box in boxes)
            {
                box.PreviewTextInput += DigitsOnly;
                box.GotKeyboardFocus += delegate(object s, KeyboardFocusChangedEventArgs e) { ((TextBox)s).SelectAll(); };
            }

            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }

        private void OnPinClick(object sender, RoutedEventArgs e)
        {
            prefs.TopMost = pinBtn.IsChecked == true;
            Topmost = prefs.TopMost;
            prefs.Save();
        }

        private void OnClockClick(object sender, RoutedEventArgs e)
        {
            if (clockBtn.IsChecked == true) ShowClock();
            else HideClock();
        }

        private void OnTab(Mode mode)
        {
            if (syncingTabs) return;
            engine.SetMode(mode, false);
            HideSettings();
        }

        public void ShowClock()
        {
            if (clock == null)
            {
                clock = new ClockWindow(prefs);
                clock.Closed += OnClockClosed;
            }
            clock.Show();
            clock.Activate();
            clockBtn.IsChecked = true;
            prefs.ShowClock = true;
            prefs.Save();
        }

        private void HideClock()
        {
            if (clock != null) clock.Close();
        }

        private void OnClockClosed(object sender, EventArgs e)
        {
            clock = null;
            prefs.ShowClock = false;
            prefs.Save();
            clockBtn.IsChecked = false;
        }

        public void ShowFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Topmost = false;
            Topmost = prefs.TopMost;
            Activate();
            Focus();
        }

        public bool ClockVisible { get { return clock != null; } }

        public void ToggleClockFromTray()
        {
            if (clock != null) HideClock(); else ShowClock();
        }

        public void SetClockClickThrough(bool enabled)
        {
            if (clock == null && !enabled) return;
            if (clock == null) ShowClock();
            if (clock != null) clock.SetClickThrough(enabled);
        }

        public void Refresh()
        {
            RenderMeta();
            Render();
        }

        /* ---------------- 设置 ---------------- */

        public void ShowSettings()
        {
            FillSettings();
            settingsOverlay.Visibility = Visibility.Visible;
            setFocus.Focus();
            setFocus.SelectAll();
        }

        private void HideSettings()
        {
            if (settingsOverlay.Visibility != Visibility.Visible) return;
            ApplySettings();
            settingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void FillSettings()
        {
            setFocus.Text = prefs.FocusMinutes.ToString(CultureInfo.InvariantCulture);
            setShort.Text = prefs.ShortMinutes.ToString(CultureInfo.InvariantCulture);
            setLong.Text = prefs.LongMinutes.ToString(CultureInfo.InvariantCulture);
            setEvery.Text = prefs.LongEvery.ToString(CultureInfo.InvariantCulture);
            setAuto.IsChecked = prefs.AutoStart;
            setSound.IsChecked = prefs.Sound;
        }

        private void ApplySettings()
        {
            prefs.FocusMinutes = ClampInt(setFocus.Text, 1, 180, prefs.FocusMinutes);
            prefs.ShortMinutes = ClampInt(setShort.Text, 1, 60, prefs.ShortMinutes);
            prefs.LongMinutes = ClampInt(setLong.Text, 1, 90, prefs.LongMinutes);
            prefs.LongEvery = ClampInt(setEvery.Text, 1, 12, prefs.LongEvery);
            prefs.AutoStart = setAuto.IsChecked == true;
            prefs.Sound = setSound.IsChecked == true;

            engine.ClampCycle(prefs.LongEvery);
            engine.RefreshDurations();
            prefs.Save();
            FillSettings();
            RenderMeta();
            Render();
        }

        private void OnRestoreDefaults(object sender, RoutedEventArgs e)
        {
            prefs.FocusMinutes = 25;
            prefs.ShortMinutes = 5;
            prefs.LongMinutes = 15;
            prefs.LongEvery = 4;
            prefs.AutoStart = true;
            prefs.Sound = true;
            engine.ClampCycle(prefs.LongEvery);
            engine.RefreshDurations();
            prefs.Save();
            FillSettings();
            RenderMeta();
            Render();
            ShowToast("已恢复默认设置");
        }

        private static int ClampInt(string text, int min, int max, int fallback)
        {
            int v;
            if (!int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return fallback;
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static void DigitsOnly(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        /* ---------------- 键盘 ---------------- */

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (settingsOverlay.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    HideSettings();
                }
                return;
            }

            if (Keyboard.FocusedElement is TextBox) return;

            switch (e.Key)
            {
                case Key.Space:
                    e.Handled = true;
                    engine.Toggle();
                    break;
                case Key.R:
                    engine.Reset();
                    break;
                case Key.S:
                    engine.Skip();
                    break;
                case Key.D1:
                case Key.NumPad1:
                    engine.SetMode(Mode.Focus, false);
                    break;
                case Key.D2:
                case Key.NumPad2:
                    engine.SetMode(Mode.Short, false);
                    break;
                case Key.D3:
                    case Key.NumPad3:
                    engine.SetMode(Mode.Long, false);
                    break;
            }
        }

        /* ---------------- 渲染 ---------------- */

        private void Render()
        {
            int secs = (int)Math.Ceiling(Math.Max(0, engine.Remaining) - 1e-6);
            timeText.Text = FormatTime(secs);

            double frac = engine.Total > 0 ? Math.Max(0, Math.Min(1, engine.Remaining / engine.Total)) : 0;
            Geometry geo = Ring.Build(RingSize, RingRadius, frac);
            progressPath.Data = geo;
            glowPath.Data = geo;

            startBtn.Content = engine.Running ? "暂停" : "开始";
            Title = engine.Running ? FormatTime(secs) + " · " + ModeName(engine.Mode) : "番茄时钟";

            if (engine.Mode != lastMode || engine.Cycle != lastCycle ||
                prefs.Pomodoros != lastPomodoros || prefs.Minutes != lastMinutes)
            {
                RenderMeta();
            }
        }

        private void RenderMeta()
        {
            lastMode = engine.Mode;
            lastCycle = engine.Cycle;
            lastPomodoros = prefs.Pomodoros;
            lastMinutes = prefs.Minutes;

            modeLabel.Text = ModeCaption(engine.Mode);
            todayStats.Text = string.Format("今日 {0} 个番茄 · {1} 分钟", prefs.Pomodoros, prefs.Minutes);

            syncingTabs = true;
            tabFocus.IsChecked = engine.Mode == Mode.Focus;
            tabShort.IsChecked = engine.Mode == Mode.Short;
            tabLong.IsChecked = engine.Mode == Mode.Long;
            syncingTabs = false;

            ApplyAccent();
            UpdateDots();
        }

        private void UpdateDots()
        {
            int n = prefs.LongEvery;
            if (dotsPanel.Children.Count != n)
            {
                dotsPanel.Children.Clear();
                for (int i = 0; i < n; i++)
                {
                    var dot = new Ellipse();
                    dot.Width = 6;
                    dot.Height = 6;
                    dot.Margin = new Thickness(3.5, 0, 3.5, 0);
                    dotsPanel.Children.Add(dot);
                }
            }
            for (int i = 0; i < dotsPanel.Children.Count; i++)
            {
                ((Ellipse)dotsPanel.Children[i]).Fill = i < engine.Cycle ? accentBrush : dotIdleBrush;
            }
        }

        private void ApplyAccent()
        {
            Color c = AccentFor(engine.Mode);

            accentBrush = new SolidColorBrush(c);
            accentBrush.Freeze();
            accentSoftBrush = new SolidColorBrush(Color.FromArgb(0x29, c.R, c.G, c.B));
            accentSoftBrush.Freeze();

            root.Resources["Accent"] = accentBrush;
            root.Resources["AccentSoft"] = accentSoftBrush;

            var grad = new RadialGradientBrush();
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0x33, c.R, c.G, c.B), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1.0));
            grad.Freeze();
            glow1.Fill = grad;
        }

        private void ShowToast(string message)
        {
            toastText.Text = message;
            toast.Visibility = Visibility.Visible;

            if (toastTimer == null)
            {
                toastTimer = new DispatcherTimer();
                toastTimer.Interval = TimeSpan.FromSeconds(2.8);
                toastTimer.Tick += delegate
                {
                    toastTimer.Stop();
                    toast.Visibility = Visibility.Collapsed;
                };
            }
            toastTimer.Stop();
            toastTimer.Start();
        }

        /* ---------------- 工具 ---------------- */

        public static Color AccentFor(Mode mode)
        {
            if (mode == Mode.Short) return Color.FromRgb(0x3D, 0xDC, 0x97);
            if (mode == Mode.Long) return Color.FromRgb(0x5A, 0xA9, 0xFF);
            return Color.FromRgb(0xFF, 0x6B, 0x5E);
        }

        public static string ModeName(Mode mode)
        {
            if (mode == Mode.Short) return "短休息";
            if (mode == Mode.Long) return "长休息";
            return "专注";
        }

        public static string ModeCaption(Mode mode)
        {
            if (mode == Mode.Short) return "短休息";
            if (mode == Mode.Long) return "长休息";
            return "专注时间";
        }

        public static string FormatTime(int totalSeconds)
        {
            int s = Math.Max(0, totalSeconds);
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int sec = s % 60;
            return h > 0
                ? string.Format("{0}:{1:00}:{2:00}", h, m, sec)
                : string.Format("{0:00}:{1:00}", m, sec);
        }
    }
}
