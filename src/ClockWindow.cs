using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Pomodoro
{
    public class ClockWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

        private readonly Preferences prefs;
        private readonly Border root;
        private readonly TextBlock timeText;
        private readonly TextBlock dateText;
        private readonly DispatcherTimer timer;

        public ClockWindow(Preferences p)
        {
            prefs = p;
            root = (Border)Xaml.Load("ClockWindow.xaml");
            Content = root;

            Title = "桌面时钟";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            timeText = (TextBlock)root.FindName("ClockTime");
            dateText = (TextBlock)root.FindName("ClockDate");

            ApplyStyle();
            UpdateText();
            PlaceInitial();

            root.MouseLeftButtonDown += OnDrag;
            root.MouseRightButtonUp += OnRightClick;
            MouseWheel += OnWheel;

            Loaded += OnLoaded;
            Closing += OnClosing;

            SourceInitialized += delegate { ApplyClickThrough(); };

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += delegate { UpdateText(); };
            timer.Start();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureOnScreen();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            prefs.ClockX = Left;
            prefs.ClockY = Top;
            prefs.Save();
        }

        private void PlaceInitial()
        {
            double x = prefs.ClockX;
            double y = prefs.ClockY;
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                Rect wa = SystemParameters.WorkArea;
                x = wa.Right - 300;
                y = wa.Top + 48;
            }
            Left = x;
            Top = y;
        }

        private void EnsureOnScreen()
        {
            double w = ActualWidth > 0 ? ActualWidth : 180;
            double h = ActualHeight > 0 ? ActualHeight : 60;
            double vx = SystemParameters.VirtualScreenLeft;
            double vy = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth;
            double vh = SystemParameters.VirtualScreenHeight;

            if (Left + w < vx + 20) Left = vx + 20;
            if (Top + h < vy + 20) Top = vy + 20;
            if (Left > vx + vw - 60) Left = vx + vw - w - 20;
            if (Top > vy + vh - 40) Top = vy + vh - h - 20;
        }

        private void UpdateText()
        {
            DateTime now = DateTime.Now;
            timeText.Text = prefs.ClockShowSeconds ? now.ToString("HH:mm:ss") : now.ToString("HH:mm");
            dateText.Text = prefs.ClockShowDate ? now.ToString("yyyy年M月d日 dddd", Zh) : "";
            dateText.Visibility = prefs.ClockShowDate ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyStyle()
        {
            timeText.FontSize = prefs.ClockSize;
            dateText.FontSize = Math.Max(10, Math.Round(prefs.ClockSize * 0.32));
            root.Background = prefs.ClockBackdrop
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0x00, 0x00, 0x00))
                : System.Windows.Media.Brushes.Transparent;
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            ChangeSize(e.Delta > 0 ? 2 : -2);
        }

        public void ChangeSize(double delta)
        {
            prefs.ClockSize = Math.Max(14, Math.Min(180, prefs.ClockSize + delta));
            ApplyStyle();
            EnsureOnScreen();
            prefs.Save();
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (prefs.ClockLocked) return;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            prefs.ClockX = Left;
            prefs.ClockY = Top;
            prefs.Save();
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            ContextMenu menu = BuildMenu();
            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            menu.Style = (Style)root.FindResource("DarkMenu");

            menu.Items.Add(Item("显示日期", true, prefs.ClockShowDate, delegate(object s, RoutedEventArgs a)
            {
                prefs.ClockShowDate = ((MenuItem)s).IsChecked;
                UpdateText();
                prefs.Save();
            }));

            menu.Items.Add(Item("显示秒", true, prefs.ClockShowSeconds, delegate(object s, RoutedEventArgs a)
            {
                prefs.ClockShowSeconds = ((MenuItem)s).IsChecked;
                UpdateText();
                prefs.Save();
            }));

            menu.Items.Add(Item("半透明背景", true, prefs.ClockBackdrop, delegate(object s, RoutedEventArgs a)
            {
                prefs.ClockBackdrop = ((MenuItem)s).IsChecked;
                ApplyStyle();
                prefs.Save();
            }));

            menu.Items.Add(new Separator());

            menu.Items.Add(Item("放大字号", false, false, delegate { ChangeSize(4); }));
            menu.Items.Add(Item("缩小字号", false, false, delegate { ChangeSize(-4); }));
            menu.Items.Add(Item("重置大小", false, false, delegate
            {
                prefs.ClockSize = 46;
                ApplyStyle();
                prefs.Save();
            }));

            menu.Items.Add(new Separator());

            menu.Items.Add(Item("锁定位置", true, prefs.ClockLocked, delegate(object s, RoutedEventArgs a)
            {
                prefs.ClockLocked = ((MenuItem)s).IsChecked;
                prefs.Save();
            }));

            menu.Items.Add(Item("鼠标穿透", true, prefs.ClickThrough, delegate(object s, RoutedEventArgs a)
            {
                prefs.ClickThrough = ((MenuItem)s).IsChecked;
                ApplyClickThrough();
                prefs.Save();
            }));

            menu.Items.Add(new Separator());

            menu.Items.Add(Item("隐藏时钟", false, false, delegate { Close(); }));

            return menu;
        }

        private MenuItem Item(string header, bool checkable, bool isChecked, RoutedEventHandler onClick)
        {
            var mi = new MenuItem();
            mi.Header = header;
            mi.Style = (Style)root.FindResource("DarkMenuItem");
            if (checkable)
            {
                mi.IsCheckable = true;
                mi.IsChecked = isChecked;
            }
            mi.Click += onClick;
            return mi;
        }

        private void ApplyClickThrough()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            int style = GetWindowLong(handle, GWL_EXSTYLE);
            if (prefs.ClickThrough) style |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            else style &= ~WS_EX_TRANSPARENT;
            SetWindowLong(handle, GWL_EXSTYLE, style);
        }

        public void SetClickThrough(bool enabled)
        {
            prefs.ClickThrough = enabled;
            ApplyClickThrough();
            prefs.Save();
        }
    }
}
