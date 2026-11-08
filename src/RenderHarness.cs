using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pomodoro
{
    internal static class RenderHarness
    {
        public static void Run(string[] args)
        {
            string what = Arg(args, "--render") ?? "main";
            string outPath = Arg(args, "--out") ?? "render.png";
            double w = Num(Arg(args, "--width"), 420);
            double h = Num(Arg(args, "--height"), 720);

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var prefs = new Preferences();
            prefs.Normalize();

            var engine = new TimerEngine(prefs);

            if (what == "selftest")
            {
                SelfTest(prefs, engine, outPath);
                app.Shutdown();
                return;
            }

            if (what == "clock")
            {
                var cw = new ClockWindow(prefs);
                var croot = (FrameworkElement)cw.Content;
                cw.Content = null;
                croot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Size ds = croot.DesiredSize;
                croot.Arrange(new Rect(ds));

                var host = new Grid();
                var bg = new LinearGradientBrush(Color.FromRgb(0x24, 0x33, 0x4F), Color.FromRgb(0xB9, 0xC5, 0xD8), 35.0);
                host.Background = bg;
                host.Children.Add(croot);
                host.Measure(new Size(ds.Width, ds.Height));
                host.Arrange(new Rect(0, 0, ds.Width, ds.Height));
                Save(host, ds.Width, ds.Height, outPath);
            }
            else
            {
                var win = new MainWindow(prefs, engine);
                if (what == "settings") win.ShowSettings();
                if (what == "running")
                {
                    SetField(engine, "remaining", 1110.0);
                    SetField(engine, "running", false);
                    SetField(engine, "cycle", 2);
                    prefs.Pomodoros = 3;
                    prefs.Minutes = 75;
                    win.Refresh();
                }

                var root = (FrameworkElement)win.Content;
                root.Measure(new Size(w, h));
                root.Arrange(new Rect(0, 0, w, h));
                root.UpdateLayout();
                Save(root, w, h, outPath);
            }

            app.Shutdown();
        }

        private static void SelfTest(Preferences prefs, TimerEngine engine, string outPath)
        {
            var log = new System.Collections.Generic.List<string>();
            prefs.Sound = false;
            prefs.AutoStart = true;

            var win = new MainWindow(prefs, engine);
            var root = (FrameworkElement)win.Content;

            Click(root, "StartBtn");
            Check(log, "点击开始后进入运行状态", engine.Running);

            Click(root, "StartBtn");
            Check(log, "再次点击后暂停", !engine.Running);

            SetField(engine, "remaining", 1.0);
            SetField(engine, "running", true);
            SetField(engine, "endAt", DateTime.Now.AddSeconds(-1));
            Invoke(engine, "OnTick", new object[] { null, EventArgs.Empty });

            Check(log, "专注完成后切到短休息", engine.Mode == Mode.Short);
            Check(log, "番茄计数 +1", prefs.Pomodoros == 1);
            Check(log, "专注分钟累计 +25", prefs.Minutes == 25);
            Check(log, "自动开始休息", engine.Running);

            Click(root, "ResetBtn");
            Check(log, "重置后停止", !engine.Running);
            Check(log, "重置后时间回满", Math.Abs(engine.Remaining - engine.Total) < 0.01);

            var tabLong = (RadioButton)root.FindName("TabLong");
            tabLong.IsChecked = true;
            Check(log, "切换到长休息", engine.Mode == Mode.Long);
            Check(log, "长休息时长 = 15 分钟", engine.Total == 15 * 60);

            Click(root, "SkipBtn");
            Check(log, "跳过回到专注", engine.Mode == Mode.Focus);

            System.IO.File.WriteAllLines(outPath, log.ToArray());
        }

        private static void Click(FrameworkElement root, string name)
        {
            var button = (Button)root.FindName(name);
            if (button != null) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        }

        private static void Check(System.Collections.Generic.List<string> log, string name, bool ok)
        {
            log.Add((ok ? "PASS  " : "FAIL  ") + name);
        }

        private static void Invoke(object target, string name, object[] args)
        {
            MethodInfo m = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (m != null) m.Invoke(target, args);
        }

        private static void Save(Visual visual, double w, double h, string path)
        {
            int pw = (int)Math.Ceiling(w * 2);
            int ph = (int)Math.Ceiling(h * 2);
            var rtb = new RenderTargetBitmap(pw, ph, 192, 192, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(target, value);
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        private static double Num(string s, double fallback)
        {
            double v;
            if (s != null && double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }
    }
}