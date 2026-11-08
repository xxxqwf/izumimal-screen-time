using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Pomodoro
{
    public class Preferences
    {
        public int FocusMinutes = 25;
        public int ShortMinutes = 5;
        public int LongMinutes = 15;
        public int LongEvery = 4;

        public bool AutoStart = true;
        public bool Sound = true;
        public bool TopMost = false;

        public bool ShowClock = true;
        public double ClockSize = 46;
        public bool ClockShowDate = true;
        public bool ClockShowSeconds = true;
        public double ClockX = double.NaN;
        public double ClockY = double.NaN;
        public bool ClockLocked = false;
        public bool ClockBackdrop = false;
        public bool ClickThrough = false;

        public double WindowWidth = 420;
        public double WindowHeight = 720;

        public string StatsDate = "";
        public int Pomodoros = 0;
        public int Minutes = 0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string folderPath;
        private static string filePath;

        public static string FolderPath
        {
            get { Resolve(); return folderPath; }
        }

        public static string FilePath
        {
            get { Resolve(); return filePath; }
        }

        // 优先写入 %APPDATA%，不可写时回落到程序目录（便携模式）
        private static void Resolve()
        {
            if (folderPath != null) return;

            var candidates = new List<string>();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(roaming))
                candidates.Add(Path.Combine(roaming, "PomodoroTimer"));
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                candidates.Add(Path.Combine(baseDir, "PomodoroTimerData"));
                candidates.Add(baseDir);
            }

            foreach (string dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    string probe = Path.Combine(dir, ".write-test");
                    File.WriteAllText(probe, "1");
                    File.Delete(probe);
                    folderPath = dir;
                    filePath = Path.Combine(dir, "settings.ini");
                    return;
                }
                catch
                {
                    // 尝试下一个位置
                }
            }

            folderPath = candidates.Count > 0 ? candidates[0] : ".";
            filePath = Path.Combine(folderPath, "settings.ini");
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) { EnsureToday(); return; }
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();

                    int i;
                    double d;
                    switch (key)
                    {
                        case "focus": if (Int(val, out i)) FocusMinutes = i; break;
                        case "short": if (Int(val, out i)) ShortMinutes = i; break;
                        case "long": if (Int(val, out i)) LongMinutes = i; break;
                        case "longevery": if (Int(val, out i)) LongEvery = i; break;
                        case "autostart": AutoStart = Bool(val); break;
                        case "sound": Sound = Bool(val); break;
                        case "topmost": TopMost = Bool(val); break;
                        case "showclock": ShowClock = Bool(val); break;
                        case "clocksize": if (Dbl(val, out d)) ClockSize = d; break;
                        case "clockshowdate": ClockShowDate = Bool(val); break;
                        case "clockshowseconds": ClockShowSeconds = Bool(val); break;
                        case "clockx": if (Dbl(val, out d)) ClockX = d; break;
                        case "clocky": if (Dbl(val, out d)) ClockY = d; break;
                        case "clocklocked": ClockLocked = Bool(val); break;
                        case "clockbackdrop": ClockBackdrop = Bool(val); break;
                        case "clickthrough": ClickThrough = Bool(val); break;
                        case "winw": if (Dbl(val, out d)) WindowWidth = d; break;
                        case "winh": if (Dbl(val, out d)) WindowHeight = d; break;
                        case "statsdate": StatsDate = val; break;
                        case "pomodoros": if (Int(val, out i)) Pomodoros = i; break;
                        case "minutes": if (Int(val, out i)) Minutes = i; break;
                    }
                }
            }
            catch
            {
                // 配置损坏时忽略，使用默认值
            }
            Normalize();
            EnsureToday();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                var lines = new List<string>();
                lines.Add("focus=" + FocusMinutes.ToString(Inv));
                lines.Add("short=" + ShortMinutes.ToString(Inv));
                lines.Add("long=" + LongMinutes.ToString(Inv));
                lines.Add("longevery=" + LongEvery.ToString(Inv));
                lines.Add("autostart=" + (AutoStart ? "1" : "0"));
                lines.Add("sound=" + (Sound ? "1" : "0"));
                lines.Add("topmost=" + (TopMost ? "1" : "0"));
                lines.Add("showclock=" + (ShowClock ? "1" : "0"));
                lines.Add("clocksize=" + ClockSize.ToString("0.##", Inv));
                lines.Add("clockshowdate=" + (ClockShowDate ? "1" : "0"));
                lines.Add("clockshowseconds=" + (ClockShowSeconds ? "1" : "0"));
                lines.Add("clockx=" + (double.IsNaN(ClockX) ? "NaN" : ClockX.ToString("0.##", Inv)));
                lines.Add("clocky=" + (double.IsNaN(ClockY) ? "NaN" : ClockY.ToString("0.##", Inv)));
                lines.Add("clocklocked=" + (ClockLocked ? "1" : "0"));
                lines.Add("clockbackdrop=" + (ClockBackdrop ? "1" : "0"));
                lines.Add("clickthrough=" + (ClickThrough ? "1" : "0"));
                lines.Add("winw=" + WindowWidth.ToString("0.##", Inv));
                lines.Add("winh=" + WindowHeight.ToString("0.##", Inv));
                lines.Add("statsdate=" + StatsDate);
                lines.Add("pomodoros=" + Pomodoros.ToString(Inv));
                lines.Add("minutes=" + Minutes.ToString(Inv));
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "pomodoro-settings-error.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                        "folder=" + FolderPath + Environment.NewLine + ex);
                }
                catch { }
            }
        }

        public void Normalize()
        {
            FocusMinutes = Clamp(FocusMinutes, 1, 180, 25);
            ShortMinutes = Clamp(ShortMinutes, 1, 60, 5);
            LongMinutes = Clamp(LongMinutes, 1, 90, 15);
            LongEvery = Clamp(LongEvery, 1, 12, 4);
            ClockSize = Math.Max(14, Math.Min(180, ClockSize));
            WindowWidth = Math.Max(330, Math.Min(1200, WindowWidth));
            WindowHeight = Math.Max(540, Math.Min(1400, WindowHeight));
            if (Pomodoros < 0) Pomodoros = 0;
            if (Minutes < 0) Minutes = 0;
        }

        public void EnsureToday()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd", Inv);
            if (StatsDate != today)
            {
                StatsDate = today;
                Pomodoros = 0;
                Minutes = 0;
            }
        }

        private static int Clamp(int v, int min, int max, int fallback)
        {
            if (v <= 0) return fallback;
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static bool Int(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.Integer, Inv, out value);
        }

        private static bool Dbl(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float, Inv, out value);
        }

        private static bool Bool(string s)
        {
            s = s.Trim().ToLowerInvariant();
            return s == "1" || s == "true" || s == "yes" || s == "on";
        }
    }
}
