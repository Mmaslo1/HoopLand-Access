using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Step 4: passing.
    /// - While you hold the ball, the game's pass target is spoken when it changes: "Pass: Jones, open, right."
    /// - F3 or L3 (when your team has the ball): every teammate with open/covered, distance and direction,
    ///   and the icon-pass button shown over him when the game knows it.
    /// - "You are Jones." when the game hands you control of another player (team modes).
    /// Sources: PlayerManager.currentPassTarget (static, readable), PlayerController.IsOpen(),
    /// PlayerController.passIcon (SpriteRenderer; sprite names logged to learn the button mapping).</summary>
    internal static class Passing
    {
        private static int _targetId, _candId, _meId, _candMe;
        private static float _candSince, _candMeSince;
        private static bool _targetOpen;
        private static bool _iconsLogged;

        public static void Update()
        {
            if (!MatchInfo.Active) { _targetId = 0; _meId = 0; return; }
            PlayerController me = CourtMap.Me();
            if (me == null || me.playerData == null) return;

            // Control switched to another player (stable for half a second).
            int mid = me.GetInstanceID();
            if (mid != _meId)
            {
                if (mid != _candMe) { _candMe = mid; _candMeSince = Time.unscaledTime; }
                else if (Time.unscaledTime - _candMeSince > 0.5f)
                {
                    bool first = _meId == 0;
                    _meId = mid;
                    if (!first) Speech.Say("You are " + Squad.Name(me) + ".", interrupt: false);
                }
            }

            if (!Squad.Same(Squad.Handler(), me)) { _targetId = 0; return; }
            PlayerController t = null;
            try { t = PlayerManager.currentPassTarget; } catch { }
            if (t == null || t.playerData == null || Squad.Same(t, me)) return;
            int id = t.GetInstanceID();
            bool open = IsOpen(t);
            if (id != _targetId)
            {
                // The target follows your stick: wait until it settles for a quarter second.
                if (id != _candId) { _candId = id; _candSince = Time.unscaledTime; return; }
                if (Time.unscaledTime - _candSince < 0.4f) return;
                bool firstThisPossession = _targetId == 0;
                _targetId = id; _targetOpen = open;
                Speech.Say("Pass: " + Squad.Name(t) + ", " + (open ? "open" : "covered") + ", " + Squad.Where(me.transform.position, t.transform.position, withFeet: false) + ".", interrupt: !firstThisPossession);
            }
            else if (open && !_targetOpen)
            {
                _targetOpen = true;
                Speech.Say(Squad.Name(t) + " open.", interrupt: true);
            }
            else if (!open) _targetOpen = false;
        }

        private static bool IsOpen(PlayerController p) { try { return p.IsOpen(); } catch { return false; } }

        /// <summary>F3 / L3: all teammates.</summary>
        public static void SayTeammates()
        {
            PlayerController me = CourtMap.Me();
            if (!MatchInfo.Active || me == null) { Speech.Say("No match running."); return; }
            var mates = Squad.Mates(me);
            if (mates.Count == 0) { Speech.Say("No teammates found."); return; }
            PlayerController h = Squad.Handler();
            PlayerController target = null;
            try { target = PlayerManager.currentPassTarget; } catch { }
            // Nearest first.
            mates.Sort((a, b) => CourtMap.Flat(a.transform.position, me.transform.position).CompareTo(CourtMap.Flat(b.transform.position, me.transform.position)));
            var sb = new StringBuilder();
            if (h == null) sb.Append("Ball loose. ");
            else if (!Squad.SameTeam(h, me)) sb.Append("Other team has the ball. ");
            foreach (PlayerController m in mates)
            {
                sb.Append(Squad.Name(m));
                if (Squad.Same(m, h)) sb.Append(", has the ball");
                else sb.Append(IsOpen(m) ? ", open" : ", covered");
                if (Squad.Same(m, target) && Squad.Same(h, me)) sb.Append(", pass target");
                string icon = IconButton(m);
                if (icon != null) sb.Append(", icon ").Append(icon);
                sb.Append(", ").Append(Squad.Where(me.transform.position, m.transform.position)).Append(". ");
            }
            Speech.Say(sb.ToString());
            LogIcons(mates);
        }

        /// <summary>The button shown over a teammate while Icon Pass (Right Shift / RB) is held.
        /// PlayerManager.buttonIcons are named button_south/east/west/north (seen 2026-11-15).
        /// Keyboard: HYPOTHESIS the four action arrows match south = Down, east = Right, west = Left, north = Up.</summary>
        public static string IconButton(PlayerController m)
        {
            try
            {
                SpriteRenderer r = m.passIcon;
                if (r == null || r.sprite == null || !r.enabled || !r.gameObject.activeInHierarchy) return null;
                string n = r.sprite.name.ToLowerInvariant();
                if (n.Contains("south")) return "A, Down arrow";
                if (n.Contains("east")) return "B, Right arrow";
                if (n.Contains("west")) return "X, Left arrow";
                if (n.Contains("north")) return "Y, Up arrow";
                return Icons.NameOfSprite(r.sprite);
            }
            catch { return null; }
        }

        /// <summary>Development: which sprite the game put in each teammate's pass icon (for icon pass).</summary>
        private static void LogIcons(List<PlayerController> mates)
        {
            try
            {
                PlayerManager pm = Squad.PM();
                if (pm != null && pm.buttonIcons != null)
                    for (int i = 0; i < pm.buttonIcons.Length; i++)
                        Mod.Log("buttonIcons[" + i + "] = " + (pm.buttonIcons[i] != null ? pm.buttonIcons[i].name : "null"));
                foreach (PlayerController m in mates)
                    Mod.Log("passIcon " + Squad.Name(m) + " = " + (m.passIcon != null && m.passIcon.sprite != null ? m.passIcon.sprite.name : "none")
                        + " shown=" + (m.passIcon != null && m.passIcon.enabled && m.passIcon.gameObject.activeInHierarchy));
            }
            catch (System.Exception e) { Mod.Log("LogIcons: " + e.Message); }
        }
    }
}
