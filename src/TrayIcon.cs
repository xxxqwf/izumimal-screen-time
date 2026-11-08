using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Pomodoro
{
    public class TrayIcon : IDisposable
    {
        private readonly NotifyIcon notify;

        public TrayIcon(Preferences prefs, TimerEngine engine, MainWindow main)
        {
            notify = new NotifyIcon();
            notify.Icon = LoadIcon();
            notify.Text = "番茄时钟";

            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;

            var open = new ToolStripMenuItem("打开番茄时钟");
            open.Font = new Font(open.Font, FontStyle.Bold);
            open.Click += delegate { main.ShowFromTray(); };
            menu.Items.Add(open);

            var clock = new ToolStripMenuItem("显示 / 隐藏桌面时钟");
            clock.Click += delegate { main.ToggleClockFromTray(); };
            menu.Items.Add(clock);

            var clickable = new ToolStripMenuItem("时钟可点击（取消鼠标穿透）");
            clickable.Click += delegate { main.SetClockClickThrough(false); };
            menu.Items.Add(clickable);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("退出");
            exit.Click += delegate
            {
                notify.Visible = false;
                System.Windows.Application.Current.Shutdown();
            };
            menu.Items.Add(exit);

            notify.ContextMenuStrip = menu;
            notify.DoubleClick += delegate { main.ShowFromTray(); };
            notify.Visible = true;
        }

        private static Icon LoadIcon()
        {
            var asm = typeof(TrayIcon).Assembly;
            using (Stream s = asm.GetManifestResourceStream("app.ico"))
            {
                if (s == null) return SystemIcons.Application;
                return new Icon(s);
            }
        }

        public void Dispose()
        {
            if (notify != null)
            {
                notify.Visible = false;
                notify.Dispose();
            }
        }
    }
}
