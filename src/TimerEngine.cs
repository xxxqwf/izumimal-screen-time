using System;
using System.Windows.Threading;

namespace Pomodoro
{
    public enum Mode
    {
        Focus = 0,
        Short = 1,
        Long = 2
    }

    public class TimerEngine
    {
        private readonly Preferences prefs;
        private readonly DispatcherTimer ticker;
        private DateTime endAt;

        private Mode mode;
        private int total;
        private double remaining;
        private bool running;
        private int cycle;

        public event Action Updated;
        public event Action<string> Notify;

        public TimerEngine(Preferences p)
        {
            prefs = p;
            mode = Mode.Focus;
            total = MinutesOf(mode) * 60;
            remaining = total;

            ticker = new DispatcherTimer();
            ticker.Interval = TimeSpan.FromMilliseconds(150);
            ticker.Tick += OnTick;
            ticker.Start();
        }

        public Mode Mode { get { return mode; } }
        public int Total { get { return total; } }
        public double Remaining { get { return remaining; } }
        public bool Running { get { return running; } }
        public int Cycle { get { return cycle; } }

        public void Start()
        {
            if (running) return;
            if (remaining <= 0.05) remaining = total;
            running = true;
            endAt = DateTime.Now.AddSeconds(remaining);
            Raise();
        }

        public void Pause()
        {
            if (!running) return;
            remaining = Math.Max(0, (endAt - DateTime.Now).TotalSeconds);
            running = false;
            Raise();
        }

        public void Toggle()
        {
            if (running) Pause(); else Start();
        }

        public void Reset()
        {
            running = false;
            remaining = total;
            Raise();
        }

        public void Skip()
        {
            running = false;
            Mode next = mode == Mode.Focus
                ? (cycle + 1 >= prefs.LongEvery ? Mode.Long : Mode.Short)
                : Mode.Focus;
            if (mode == Mode.Long) cycle = 0;
            SwitchTo(next, false);
        }

        public void SetMode(Mode m, bool autoStart)
        {
            if (m == mode) return;
            SwitchTo(m, autoStart);
        }

        public void RefreshDurations()
        {
            if (running) return;
            total = MinutesOf(mode) * 60;
            remaining = total;
            Raise();
        }

        public void ClampCycle(int max)
        {
            if (max < 1) max = 1;
            if (cycle > max)
            {
                cycle = max;
                Raise();
            }
        }

        private void SwitchTo(Mode m, bool autoStart)
        {
            running = false;
            mode = m;
            total = MinutesOf(m) * 60;
            remaining = total;
            if (autoStart) Start();
            else Raise();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!running) return;
            double left = (endAt - DateTime.Now).TotalSeconds;
            if (left <= 0)
            {
                remaining = 0;
                running = false;
                Raise();
                Complete();
                return;
            }
            remaining = left;
            Raise();
        }

        private void Complete()
        {
            prefs.EnsureToday();

            bool focusDone = mode == Mode.Focus;
            if (focusDone)
            {
                cycle = Math.Min(prefs.LongEvery, cycle + 1);
                prefs.Pomodoros++;
                prefs.Minutes += prefs.FocusMinutes;
            }
            else if (mode == Mode.Long)
            {
                cycle = 0;
            }
            prefs.Save();

            Chime.Play(prefs.Sound);

            Mode next;
            string message;
            if (focusDone)
            {
                next = cycle >= prefs.LongEvery ? Mode.Long : Mode.Short;
                message = "专注结束，休息一下 ☕";
            }
            else
            {
                next = Mode.Focus;
                message = mode == Mode.Long ? "长休息结束，开始新一轮 🍅" : "休息结束，继续专注 🍅";
            }

            SwitchTo(next, prefs.AutoStart);

            if (Notify != null) Notify(message);
        }

        private int MinutesOf(Mode m)
        {
            int v;
            if (m == Mode.Focus) v = prefs.FocusMinutes;
            else if (m == Mode.Short) v = prefs.ShortMinutes;
            else v = prefs.LongMinutes;
            return v < 1 ? 1 : v;
        }

        private void Raise()
        {
            if (Updated != null) Updated();
        }
    }
}
