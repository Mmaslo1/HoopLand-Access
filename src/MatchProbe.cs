using System;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Development helper: finds where the game keeps match data, by TYPE not by scrambled name,
    /// so it keeps working after updates. F10 logs a snapshot.</summary>
    internal static class MatchProbe
    {
        private static PropertyInfo _pbpList;
        private static bool _searched;

        /// <summary>The game's list of play-by-play entries for the current game (static field of some class).</summary>
        public static Il2CppSystem.Collections.Generic.List<PlayByPlayData> PlayByPlayList()
        {
            if (!_searched)
            {
                _searched = true;
                var want = typeof(Il2CppSystem.Collections.Generic.List<PlayByPlayData>);
                foreach (Type t in typeof(PlayByPlayData).Assembly.GetTypes())
                {
                    PropertyInfo p;
                    try { p = t.GetProperties(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.PropertyType == want); }
                    catch { continue; }
                    if (p != null) { _pbpList = p; Mod.Log("Play-by-play list found: " + t.Name + "." + p.Name); break; }
                }
                if (_pbpList == null) Mod.Log("Play-by-play list: no static list found.");
            }
            try { return _pbpList?.GetValue(null) as Il2CppSystem.Collections.Generic.List<PlayByPlayData>; }
            catch { return null; }
        }

        public static void Snapshot()
        {
            Mod.Log("=== Match snapshot ===");
            Mod.Log("shooter=" + Name(Basketball.shooter) + " lastBallHandler=" + Name(Basketball.lastBallHandler)
                + " passTarget=" + Name(Basketball.passTarget) + " looseBall=" + Basketball.looseBall
                + " shotBegan=" + Basketball.shotBegan + " shotReleased=" + Basketball.shotReleased);
            var list = PlayByPlayList();
            Mod.Log("pbp count=" + (list != null ? list.Count : -1));
            if (list != null)
                for (int i = Math.Max(0, list.Count - 3); i < list.Count; i++)
                {
                    string txt = "";
                    try { txt = PlayByPlay.GetPlayText(list[i]); } catch (Exception e) { txt = "ERR " + e.Message; }
                    Mod.Log("  pbp[" + i + "] t=" + list[i].time + " period=" + list[i].period + " : " + txt);
                }
            foreach (ScoreBug sb in UnityEngine.Object.FindObjectsOfType<ScoreBug>())
                Mod.Log("ScoreBug: " + string.Join(" / ", sb.teamScores.Select(t => t != null ? t.text : "")) + " period=" + (sb.currentPeriod != null ? sb.currentPeriod.text : ""));
            foreach (UnityEngine.UI.Text t in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>())
                if (t.isActiveAndEnabled && MenuReader.HasAncestor(t.transform, "Game UI") && MenuReader.Clean(t.text).Length > 0)
                    Mod.Log("  HUD text " + UiScreen.Path(t.transform) + " = " + MenuReader.Clean(t.text));
        }

        private static float _nextShotLog;
        private static bool _wasShooting;

        /// <summary>Logs the shot meter while the human player shoots (development, 20 times a second).</summary>
        public static void ShotTrace()
        {
            bool shooting = Basketball.shotBegan && Basketball.shooter != null && Basketball.shooter.isPlayer;
            if (!shooting)
            {
                if (_wasShooting)
                {
                    _wasShooting = false;
                    var sd = Basketball.lastShotData;
                    Mod.Log("ShotTrace end. lastShotData: " + (sd != null ? "dis=" + sd.dis + " per=" + sd.per + " pts=" + sd.pts + " goal=" + sd.goal + " released=" + sd.released : "null"));
                }
                return;
            }
            if (Time.unscaledTime < _nextShotLog) return;
            _nextShotLog = Time.unscaledTime + 0.05f;
            _wasShooting = true;
            var b = Basketball.shooter.basketball;
            string handles = "";
            if (b != null && b.shotbarHandle != null)
                foreach (Transform h in b.shotbarHandle) if (h != null) handles += " h=" + h.localPosition.x.ToString("F2") + (h.gameObject.activeInHierarchy ? "" : "(off)");
            ShotBar bar = UnityEngine.Object.FindObjectOfType<ShotBar>();
            string bars = bar != null ? " green x=" + bar.green.localPosition.x.ToString("F2") + " w=" + bar.green.rect.width.ToString("F2")
                + " yellow x=" + bar.yellow.localPosition.x.ToString("F2") + " w=" + bar.yellow.rect.width.ToString("F2") + " gbw=" + ShotBar.greenBarWidth : " no ShotBar";
            Mod.Log("ShotTrace power=" + (b != null ? b.shotPower.ToString("F3") : "?") + " released=" + Basketball.shotReleased + handles + bars
                + " inPosition=" + Basketball.shooter.inPosition + " fb=" + (b != null && b.shotFeedbackText != null ? b.shotFeedbackText.text : "") + " pct=" + (b != null && b.shotPercentageText != null ? b.shotPercentageText.text : ""));
        }

        public static string Name(PlayerController p)
        {
            if (p == null || p.playerData == null) return "none";
            return p.playerData.fn + " " + p.playerData.ln;
        }
    }
}

