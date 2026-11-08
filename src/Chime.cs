using System;
using System.IO;
using System.Media;

namespace Pomodoro
{
    public static class Chime
    {
        private static readonly object Gate = new object();
        private static SoundPlayer player;

        public static void Play(bool enabled)
        {
            if (!enabled) return;
            try
            {
                lock (Gate)
                {
                    if (player == null) player = new SoundPlayer(BuildWav());
                }
                player.Play();
            }
            catch
            {
                // 没有音频设备时静默失败
            }
        }

        private static Stream BuildWav()
        {
            const int rate = 44100;
            const double duration = 1.7;
            int n = (int)(rate * duration);
            double[] mix = new double[n];

            double[] freqs = { 880.0, 1108.73, 1318.51, 1760.0 };
            double[] starts = { 0.0, 0.15, 0.30, 0.48 };

            for (int k = 0; k < freqs.Length; k++)
            {
                int s0 = (int)(starts[k] * rate);
                for (int i = s0; i < n; i++)
                {
                    double t = (i - s0) / (double)rate;
                    double env = Math.Exp(-t * 3.6);
                    mix[i] += Math.Sin(2 * Math.PI * freqs[k] * t) * env * 0.22;
                }
            }

            int dataSize = n * 2;
            var ms = new MemoryStream(44 + dataSize);
            var bw = new BinaryWriter(ms);

            bw.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            bw.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(rate);
            bw.Write(rate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            bw.Write(dataSize);

            for (int i = 0; i < n; i++)
            {
                int v = (int)(mix[i] * 32767);
                if (v > 32767) v = 32767;
                if (v < -32768) v = -32768;
                bw.Write((short)v);
            }

            bw.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
