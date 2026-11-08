using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace Pomodoro
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (HasArg(args, "--render"))
            {
                try
                {
                    RenderHarness.Run(args);
                }
                catch (Exception ex)
                {
                    WriteCrashLog(ex);
                }
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\PomodoroTimer.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("番茄时钟已经在运行了，请看任务栏或系统托盘。", "番茄时钟",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    var app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    var prefs = new Preferences();
                    prefs.Load();
                    prefs.ClickThrough = false; // 鼠标穿透只在本次运行内有效，避免下次启动无法操作

                    var engine = new TimerEngine(prefs);
                    var main = new MainWindow(prefs, engine);

                    using (var tray = new TrayIcon(prefs, engine, main))
                    {
                        main.Closed += delegate
                        {
                            prefs.Save();
                            app.Shutdown();
                        };
                        app.Run(main);
                    }
                }
                catch (Exception ex)
                {
                    WriteCrashLog(ex);
                    MessageBox.Show("启动时发生错误，详细信息已写入日志：\n" + CrashLogPath,
                        "番茄时钟", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static string CrashLogPath
        {
            get { return Path.Combine(Preferences.FolderPath, "crash.log"); }
        }

        private static void WriteCrashLog(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Preferences.FolderPath);
                File.WriteAllText(CrashLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex);
            }
            catch { }
        }

        private static bool HasArg(string[] args, string name)
        {
            foreach (string a in args)
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public static class Xaml
    {
        public static FrameworkElement Load(string name)
        {
            var asm = typeof(Xaml).Assembly;
            using (Stream s = asm.GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("缺少嵌入资源: " + name);
                return (FrameworkElement)XamlReader.Load(s);
            }
        }
    }
}
