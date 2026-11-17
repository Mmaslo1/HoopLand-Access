using System;
using System.Runtime.InteropServices;
using MelonLoader;

namespace HoopLandAccess
{
    /// <summary>Speaks through the running screen reader (JAWS, NVDA...) using Tolk.dll in the game folder.
    /// Every spoken line is also written to the MelonLoader log, so tests can be checked afterwards.</summary>
    internal static class Speech
    {
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void Tolk_Load();
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void Tolk_Unload();
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string text, bool interrupt);
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        private static bool _ready;
        public static string LastSpoken { get; private set; } = "";

        public static void Init()
        {
            try
            {
                Tolk_Load();
                _ready = true;
                IntPtr p = Tolk_DetectScreenReader();
                string sr = p == IntPtr.Zero ? "none" : Marshal.PtrToStringUni(p);
                MelonLogger.Msg("Speech ready. Screen reader: " + sr);
            }
            catch (Exception e) { MelonLogger.Error("Tolk failed to load: " + e.Message); }
        }

        public static void Shutdown() { if (_ready) { try { Tolk_Unload(); } catch { } } }

        /// <param name="interrupt">true = cut off what is being said (menu moves); false = queue after it.</param>
        public static void Say(string text, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            LastSpoken = text;
            MelonLogger.Msg("SAY: " + text);
            if (_ready) { try { Tolk_Output(text, interrupt); } catch { } }
        }
    }
}
