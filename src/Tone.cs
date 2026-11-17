using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HoopLandAccess
{
    /// <summary>A tiny tone player on its own thread (Windows waveOut), independent of the game's audio.
    /// Set Frequency and Volume at any time; Volume 0 = silent. About 40 ms latency.</summary>
    internal static class Tone
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHdr { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr h, int dev, ref WaveFormat fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);

        private const int Rate = 44100, BufSamples = 441 * 2, Buffers = 3; // 20 ms buffers
        private const uint WHDR_DONE = 1;

        public static volatile float Frequency = 440f;
        public static volatile float Volume = 0f;   // 0..1
        public static volatile float Pan = 0f;      // -1 left .. 1 right

        // Scheduled beeps (count-in mode): start time in Stopwatch ticks, length, pitch.
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static readonly object _lock = new object();
        private static readonly System.Collections.Generic.List<(long start, long end, float freq)> _beeps = new();
        private static long _sampleClock; // ticks at the start of the buffer being filled

        public static double Now => Clock.Elapsed.TotalSeconds;

        /// <summary>Plays a short beep at an absolute time (seconds on Tone.Now).</summary>
        public static void BeepAt(double when, double seconds, float freq)
        {
            long s = (long)(when * System.Diagnostics.Stopwatch.Frequency), e = s + (long)(seconds * System.Diagnostics.Stopwatch.Frequency);
            lock (_lock) _beeps.Add((s, e, freq));
        }

        public static void ClearBeeps() { lock (_lock) _beeps.Clear(); }

        private static Thread _thread;
        private static IntPtr _dev;
        private static readonly IntPtr[] _hdr = new IntPtr[Buffers], _data = new IntPtr[Buffers];
        private static double _phase;
        private static float _curVol, _curFreq = 440f, _curPan;

        public static void Start()
        {
            if (_thread != null) return;
            var fmt = new WaveFormat { wFormatTag = 1, nChannels = 2, nSamplesPerSec = Rate, wBitsPerSample = 16, nBlockAlign = 4, nAvgBytesPerSec = Rate * 4 };
            if (waveOutOpen(out _dev, -1, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { Mod.Log("Tone: could not open audio."); return; }
            int hsz = Marshal.SizeOf<WaveHdr>();
            for (int i = 0; i < Buffers; i++)
            {
                _data[i] = Marshal.AllocHGlobal(BufSamples * 4);
                _hdr[i] = Marshal.AllocHGlobal(hsz);
                var h = new WaveHdr { lpData = _data[i], dwBufferLength = (uint)(BufSamples * 4), dwFlags = 0 };
                Marshal.StructureToPtr(h, _hdr[i], false);
                waveOutPrepareHeader(_dev, _hdr[i], hsz);
                Fill(i);
                waveOutWrite(_dev, _hdr[i], hsz);
            }
            _thread = new Thread(Loop) { IsBackground = true, Name = "HoopLandAccess tone" };
            _thread.Start();
        }

        private static void Loop()
        {
            int hsz = Marshal.SizeOf<WaveHdr>();
            while (true)
            {
                for (int i = 0; i < Buffers; i++)
                {
                    var h = Marshal.PtrToStructure<WaveHdr>(_hdr[i]);
                    if ((h.dwFlags & WHDR_DONE) == 0) continue;
                    Fill(i);
                    waveOutWrite(_dev, _hdr[i], hsz);
                }
                Thread.Sleep(3);
            }
        }

        private static void Fill(int i)
        {
            var buf = new short[BufSamples * 2];
            // This buffer will be heard ~(Buffers-1) buffers from now; schedule beeps against that time.
            long bufStart = Clock.ElapsedTicks + (long)((Buffers - 1) * BufSamples / (double)Rate * System.Diagnostics.Stopwatch.Frequency);
            (long start, long end, float freq)[] beeps;
            lock (_lock) { _beeps.RemoveAll(b => b.end < Clock.ElapsedTicks - System.Diagnostics.Stopwatch.Frequency); beeps = _beeps.ToArray(); }
            float targetVol = Math.Clamp(Volume, 0f, 1f), targetFreq = Frequency, targetPan = Math.Clamp(Pan, -1f, 1f);
            for (int s = 0; s < BufSamples; s++)
            {
                // glide to avoid clicks
                _curVol += (targetVol - _curVol) * 0.004f;
                _curFreq += (targetFreq - _curFreq) * 0.01f;
                _curPan += (targetPan - _curPan) * 0.002f;
                _phase += 2 * Math.PI * _curFreq / Rate;
                if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
                // soft square-ish tone: easier to hear over game noise than a pure sine
                double v = Math.Sin(_phase) * 0.8 + Math.Sin(_phase * 3) * 0.15;
                double a = v * _curVol * 0.35 * short.MaxValue;
                long t = bufStart + (long)(s / (double)Rate * System.Diagnostics.Stopwatch.Frequency);
                foreach (var b in beeps)
                    if (t >= b.start && t < b.end)
                    {
                        double bt = (t - b.start) / (double)System.Diagnostics.Stopwatch.Frequency;
                        double env = Math.Min(1, Math.Min(bt / 0.004, (b.end - t) / (double)System.Diagnostics.Stopwatch.Frequency / 0.004));
                        a += Math.Sin(2 * Math.PI * b.freq * bt) * env * 0.45 * short.MaxValue;
                    }
                a = Math.Clamp(a, short.MinValue, short.MaxValue);
                double l = a * Math.Min(1, 1 - _curPan), r = a * Math.Min(1, 1 + _curPan);
                buf[s * 2] = (short)l; buf[s * 2 + 1] = (short)r;
            }
            Marshal.Copy(buf, 0, _data[i], buf.Length);
        }
    }
}

