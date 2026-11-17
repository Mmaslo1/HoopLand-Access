using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Who is on which team during a match, and how to say where someone is.
    /// Source: PlayerManager.homePlayers / roadPlayers (readable, proven 2026-11-15).
    /// Il2Cpp wrappers are not reference-equal, so players are compared by GetInstanceID.</summary>
    internal static class Squad
    {
        private static PlayerManager _pm;

        public static PlayerManager PM()
        {
            if (_pm == null || !_pm.gameObject.activeInHierarchy) _pm = Object.FindObjectOfType<PlayerManager>();
            return _pm;
        }

        public static bool Same(PlayerController a, PlayerController b)
            => a != null && b != null && a.GetInstanceID() == b.GetInstanceID();

        private static bool In(Il2CppSystem.Collections.Generic.List<PlayerController> list, PlayerController p)
        {
            if (list == null || p == null) return false;
            for (int i = 0; i < list.Count; i++) if (Same(list[i], p)) return true;
            return false;
        }

        private static List<PlayerController> Copy(Il2CppSystem.Collections.Generic.List<PlayerController> list)
        {
            var r = new List<PlayerController>();
            if (list == null) return r;
            for (int i = 0; i < list.Count; i++)
            {
                PlayerController p = list[i];
                if (p != null && p.gameObject.activeInHierarchy && p.playerData != null) r.Add(p);
            }
            return r;
        }

        /// <summary>Teammates of p (not p itself).</summary>
        public static List<PlayerController> Mates(PlayerController p)
        {
            PlayerManager pm = PM();
            if (pm == null || p == null) return new List<PlayerController>();
            var all = Copy(In(pm.homePlayers, p) ? pm.homePlayers : pm.roadPlayers);
            all.RemoveAll(x => Same(x, p));
            return all;
        }

        public static List<PlayerController> Opponents(PlayerController p)
        {
            PlayerManager pm = PM();
            if (pm == null || p == null) return new List<PlayerController>();
            return Copy(In(pm.homePlayers, p) ? pm.roadPlayers : pm.homePlayers);
        }

        public static bool SameTeam(PlayerController a, PlayerController b)
        {
            PlayerManager pm = PM();
            if (pm == null || a == null || b == null) return false;
            return In(pm.homePlayers, a) == In(pm.homePlayers, b);
        }

        /// <summary>The player holding the ball, or null when it is loose or in the air.</summary>
        public static PlayerController Handler()
        {
            if (Basketball.looseBall > 0) return null;
            PlayerController h = Basketball.lastBallHandler;
            return h != null && h.playerData != null ? h : null;
        }

        /// <summary>Short name: last name only ("Smith").</summary>
        public static string Name(PlayerController p)
        {
            if (p == null || p.playerData == null) return "someone";
            string ln = p.playerData.ln;
            return string.IsNullOrEmpty(ln) ? p.playerData.fn : ln;
        }

        /// <summary>"8 feet, left and up" from a to b. Screen directions, the same as W A S D:
        /// right = D, left = A, up = W (+y), down = S. Proven: W moves +y (2026-11-15).</summary>
        public static string Where(Vector3 from, Vector3 to, bool withFeet = true)
        {
            float dx = (to.x - from.x) * CourtMap.FeetPerUnit, dy = (to.y - from.y) * CourtMap.FeetPerUnit;
            int feet = Mathf.RoundToInt(new Vector2(dx, dy).magnitude);
            string d = DirWords(dx, dy);
            if (d.Length == 0) return "right next to you";
            return withFeet ? feet + " feet, " + d : d;
        }

        /// <summary>"left and up". Small parts (under a third of the bigger one, or under 2 feet) are left out.</summary>
        public static string DirWords(float dxFeet, float dyFeet)
        {
            float ax = Mathf.Abs(dxFeet), ay = Mathf.Abs(dyFeet);
            bool useX = ax >= 2f && ax >= ay * 0.35f, useY = ay >= 2f && ay >= ax * 0.35f;
            string h = dxFeet > 0 ? "right" : "left", v = dyFeet > 0 ? "up" : "down";
            if (useX && useY) return h + " and " + v;
            if (useX) return h;
            if (useY) return v;
            return "";
        }
    }
}
