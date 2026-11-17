using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Where things are on the court. Hoops come from Basketball.hoop (readable array of the two hoops).</summary>
    internal static class CourtMap
    {
        private static Basketball _ball;

        public static Basketball Ball()
        {
            if (_ball == null || !_ball.gameObject.activeInHierarchy) _ball = Object.FindObjectOfType<Basketball>();
            return _ball;
        }

        public static Vector3[] Hoops()
        {
            Basketball b = Ball();
            if (b == null || b.hoop == null || b.hoop.Length < 2) return null;
            return new[] { b.hoop[0].transform.position, b.hoop[1].transform.position };
        }

        public static float NearestHoopDistance(Vector3 p)
        {
            var h = Hoops();
            if (h == null) return -1;
            return Mathf.Min(Flat(p, h[0]), Flat(p, h[1]));
        }

        /// <summary>Game units to feet: measured 2026-11-15 from 3 shots (27 ft = 6.27, 20 ft = 4.49, 21 ft = 4.71 units).</summary>
        public const float FeetPerUnit = 4.4f;

        /// <summary>The human player (PlayerController.isPlayer).</summary>
        private static PlayerController _lastMe;

        /// <summary>The human player (PlayerController.isPlayer). For a moment between plays nobody is
        /// marked, so we keep the last one we saw.</summary>
        private static int _meFrame = -1;
        private static PlayerController _meNow;

        public static PlayerController Me()
        {
            // Asked many times per frame: look once per frame, from PlayerManager's lists (cheap).
            if (Time.frameCount == _meFrame) return _meNow;
            _meFrame = Time.frameCount;
            _meNow = null;
            PlayerManager pm = Squad.PM();
            if (pm != null)
                foreach (var list in new[] { pm.homePlayers, pm.roadPlayers })
                    if (list != null)
                        for (int i = 0; i < list.Count; i++)
                        {
                            PlayerController p = list[i];
                            if (p != null && p.isPlayer && p.gameObject.activeInHierarchy) { _meNow = p; break; }
                        }
            if (_meNow == null)
                foreach (PlayerController p in Object.FindObjectsOfType<PlayerController>())
                    if (p != null && p.isPlayer && p.gameObject.activeInHierarchy) { _meNow = p; break; }
            if (_meNow != null) _lastMe = _meNow;
            else _meNow = _lastMe != null && _lastMe.gameObject.activeInHierarchy ? _lastMe : null;
            return _meNow;
        }

        /// <summary>The hoop your team attacks. In a match TeamManager.direction tells the side
        /// (HYPOTHESIS: positive = right hoop, checked in the log); otherwise the nearest hoop.</summary>
        public static Vector3? AttackHoop(PlayerController me)
        {
            var h = Hoops();
            if (h == null || me == null) return null;
            // TeamManager.direction was always 0 in a test match (2026-11-15), so instead:
            // PlayerController.InFrontCourt() (readable) says if you are on your attacking half.
            // In the front court the nearer hoop is yours; in the back court the farther one.
            bool nearIs0 = Flat(me.transform.position, h[0]) < Flat(me.transform.position, h[1]);
            bool front = true;
            try { front = me.InFrontCourt(); } catch { }
            if (!MatchInfo.Active) front = true; // practice: the hoop you are near
            return (nearIs0 == front) ? h[0] : h[1];
        }

        /// <summary>F8: "Rim 20 feet, to your right, a little up."</summary>
        public static void SayRim()
        {
            PlayerController me = Me();
            if (me == null) { Speech.Say("You are not on the court."); return; }
            Vector3? hoop = AttackHoop(me);
            if (hoop == null) { Speech.Say("No hoop found."); return; }
            Vector3 p = me.transform.position, t = hoop.Value;
            int feet = Mathf.RoundToInt(Flat(p, t) * FeetPerUnit);
            float dx = t.x - p.x, dy = t.y - p.y;
            string side = Mathf.Abs(dx) * FeetPerUnit < 2 ? "straight " : dx > 0 ? "to your right" : "to your left";
            string depth = Mathf.Abs(dy) * FeetPerUnit < 3 ? "" : (dy > 0 ? ", up" : ", down");
            Mod.Log("Rim: me " + p.ToString("F2") + " hoop " + t.ToString("F2") + " dir=" + (me.team != null ? me.team.direction : 0));
            Speech.Say("Rim " + feet + " feet, " + side.Trim() + depth + ".");
        }

        /// <summary>Distance on the floor (x and y of the 2D court).</summary>
        public static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.y - b.y).magnitude;
    }
}



