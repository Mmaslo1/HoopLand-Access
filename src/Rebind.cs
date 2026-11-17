using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Controls screens: pressing a row makes the game wait for a new key and show
    /// "Press any button for MOVE UP" (seen 2026-11-15). While that text is up we stay out of the way
    /// (our arrows/Escape would fight the game), then read the row again with its new key.</summary>
    internal static class Rebind
    {
        public static bool Waiting { get; private set; }
        private static float _next;

        public static void Update(GameObject selected)
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.15f;
            if (!Waiting && (selected == null || !MenuReader.HasAncestor(selected.transform, "Controller Configuration"))) return;

            string prompt = null;
            foreach (Text t in Object.FindObjectsOfType<Text>())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                string s = MenuReader.Clean(t.text);
                if (s.StartsWith("Press any", System.StringComparison.OrdinalIgnoreCase)) { prompt = s; break; }
            }

            if (prompt != null && !Waiting)
            {
                Waiting = true;
                Speech.Say(prompt + ". Escape cancels.");
            }
            else if (prompt == null && Waiting)
            {
                Waiting = false;
                MenuReader.ForceRespeak();
            }
        }
    }
}
