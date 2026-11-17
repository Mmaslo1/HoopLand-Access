using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Offensive / Defensive Assistance. The game keeps both rows in Settings > Controls inside a
    /// "Mobile" group that is hidden on PC (proven 2026-11-15: GameSettings.offensiveAssistance =
    /// ".../Control Panel/Mobile/Horizontal Group/Vertical Group/Offensive Assistance", Mobile inactive;
    /// GameSettings.DefensiveAssistance() = True, OffensiveAssistance() = False on PC, so the PC game reads them).
    /// We show that group whenever the Settings screen is open, so the two rows can be read and changed
    /// like any other setting. Nothing else is changed.</summary>
    internal static class Assist
    {
        private static float _next;
        private static bool _logged;

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            foreach (GameSettings gs in Object.FindObjectsOfType<GameSettings>())
            {
                if (gs == null || !gs.gameObject.activeInHierarchy || gs.offensiveAssistance == null) continue;
                Transform mobile = ScreenHints.FindAncestor(gs.offensiveAssistance, "Mobile");
                if (mobile == null || mobile.gameObject.activeSelf) continue;
                mobile.gameObject.SetActive(true);
                // Keep only the assistance rows; hide the touch-screen rows (stick type, opacity...) of that group.
                foreach (UnityEngine.UI.Text t in mobile.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    if (t.gameObject.name != "Header" || t.transform.parent == null) continue;
                    string h = MenuReader.Clean(t.text).ToUpperInvariant();
                    if (h.Contains("ASSIST") || h.Contains("AUTO-SWITCH")) continue;
                    t.transform.parent.gameObject.SetActive(false);
                    Mod.Log("Assist: hid touch row " + h);
                }
                UiScreen.ForceRebuild();
                if (!_logged) { _logged = true; Mod.Log("Assist: showed hidden Mobile settings group " + UiScreen.Path(mobile)); }
            }
        }
    }
}
