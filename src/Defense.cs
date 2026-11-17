using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Step 5: defense. Speech only, short.
    /// - When the other team gets the ball: "Defense. You guard Smith."
    /// - "Past you, left!" when your man gets closer to your hoop than you.
    /// - "Steal!" when the ball handler you guard is in reach and the game says you can steal.
    /// - "Jump!" when an opponent near you starts a shot (Block = Up arrow / Y).
    /// - F4 or R3 on defense: where your man is and which way to step to stand between him and the hoop.
    /// Sources (readable names, 1.12.1 interop): PlayerController.GetDefender(), CanSteal(),
    /// InFrontCourt(); Basketball.lastBallHandler / looseBall / shooter / shotBegan.
    /// HYPOTHESIS: GetDefender() on an attacker returns the player assigned to guard him (logged to check).
    /// HYPOTHESIS: steal reach ~1.1 units (~5 ft); logged with CanSteal so we can tune it.</summary>
    internal static class Defense
    {
        public const float StealReach = 1.1f;       // world units
        public const float BlockReach = 1.6f;       // world units (~7 ft)

        private static bool _onD, _past, _inReach, _jumpSaid;
        private static int _manId, _candManId;
        private static float _candSince, _nextPast, _nextLog, _nextSteal;
        private static PlayerController _man;
        private static float _quietUntil;

        /// <summary>Urgent cues share one short gap so they never pile up (Jump beats Past beats Steal).</summary>
        private static bool Cue(string text, float gap = 0.9f)
        {
            if (Time.unscaledTime < _quietUntil) return false;
            _quietUntil = Time.unscaledTime + gap;
            Speech.Say(text, interrupt: true);
            return true;
        }

        public static bool OnDefense { get; private set; }

        public static void Update()
        {
            if (!MatchInfo.Active) { if (_onD) Reset(); return; }
            PlayerController me = CourtMap.Me();
            if (me == null || me.playerData == null) return;

            PlayerController h = Squad.Handler();
            // Defense = the ball belongs to the other team. While it is loose or in the air we keep the last state.
            if (h != null) OnDefense = !Squad.SameTeam(h, me);
            if (!OnDefense) { if (_onD) Reset(); return; }

            PlayerController man = MyMan(me);
            if (man == null) return;
            int id = man.GetInstanceID();
            if (!_onD)
            {
                _onD = true; _manId = _candManId = id; _man = man; _past = false;
                Speech.Say("Defense. You guard " + Squad.Name(man) + ".", interrupt: false);
                _quietUntil = Time.unscaledTime + 1.2f; // let the intro finish
            }
            else if (id != _manId)
            {
                // Matchups flicker during switches: only announce a new man after 1 second.
                if (id != _candManId) { _candManId = id; _candSince = Time.unscaledTime; }
                else if (Time.unscaledTime - _candSince > 1.5f)
                {
                    _manId = id; _man = man; _past = false;
                    Speech.Say("Now guarding " + Squad.Name(man) + ".", interrupt: false);
                }
            }
            man = _man != null && _man.gameObject.activeInHierarchy ? _man : man;

            Vector3? hoopN = DefendedHoop(man);
            if (hoopN == null) return;
            Vector3 hoop = hoopN.Value, pm = man.transform.position, pme = me.transform.position;
            float dMan = CourtMap.Flat(pm, hoop), dMe = CourtMap.Flat(pme, hoop), apart = CourtMap.Flat(pm, pme);
            bool manHasBall = Squad.Same(h, man);

            // 0. Block first: an opponent near you starts a shot.
            PlayerController s = Basketball.shooter;
            bool shooting = Basketball.shotBegan && !Basketball.shotReleased;
            bool shotNear = shooting && s != null && !Squad.SameTeam(s, me) && CourtMap.Flat(s.transform.position, pme) < BlockReach;
            if (shotNear && !_jumpSaid) { _jumpSaid = true; _quietUntil = 0; Cue("Jump!", 1.2f); }
            if (!Basketball.shotBegan) _jumpSaid = false;

            // 1. Beaten: your man is clearly nearer the hoop than you, and inside 30 feet.
            if (!_past && dMan + 0.35f < dMe && dMan * CourtMap.FeetPerUnit < 30f && Time.unscaledTime >= _nextPast && !shooting)
            {
                string dir = apart * CourtMap.FeetPerUnit < 3f ? "" : Squad.Where(pme, pm, withFeet: false);
                if (Cue(dir.Length > 0 && dir != "right next to you" ? "Past you, " + dir + "!" : "Past you!"))
                { _past = true; _nextPast = Time.unscaledTime + 2.5f; }
            }
            else if (_past && dMe + 0.15f < dMan) _past = false;

            // 2. Steal: he has the ball and is in reach.
            bool canSteal = false;
            try { canSteal = me.CanSteal(); } catch { }
            bool reach = manHasBall && apart < StealReach && canSteal && !shooting;
            if (reach && !_inReach && Time.unscaledTime >= _nextSteal && Cue("Steal!"))
                _nextSteal = Time.unscaledTime + 2f;
            _inReach = reach;

            // Development log twice a second, to tune reach and check GetDefender.
            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 1f;
                Mod.Log("D: man=" + Squad.Name(man) + " byGame=" + _byGame + " apart=" + apart.ToString("F2") + " dMe=" + dMe.ToString("F2")
                    + " dMan=" + dMan.ToString("F2") + " ball=" + manHasBall + " canSteal=" + canSteal + " past=" + _past + " how=" + _how);
                if (Time.unscaledTime >= _nextMatchLog) { _nextMatchLog = Time.unscaledTime + 5f; Mod.Log("DM: " + MatchupInfo(me)); }
            }
        }

        private static void Reset() { _onD = false; _past = false; _inReach = false; _man = null; _manId = _candManId = 0; }

        private static bool _byGame;
        private static float _nextMatchLog;

        /// <summary>Reads an enum field by name and returns it as a number (the enum's own type name is
        /// scrambled and changes with updates, so we never name it).</summary>
        private static int EnumField(PlayerController p, string field)
        {
            try
            {
                var prop = typeof(PlayerController).GetProperty(field);
                return prop != null ? System.Convert.ToInt32(prop.GetValue(p)) : -1;
            }
            catch { return -1; }
        }

        /// <summary>Position names of the matchup enum (PlayerController: R, PG, SG, SF, PF, C).</summary>
        private static readonly string[] Pos = { "R", "PG", "SG", "SF", "PF", "C" };

        private static string _how = "";

        /// <summary>Your man: the attacker whose position (playerType) is your defensiveMatchup;
        /// else the attacker whose GetDefender() is you; else the nearest attacker.
        /// HYPOTHESIS: defensiveMatchup = the position you are assigned to guard (checked in the D: log).</summary>
        public static PlayerController MyMan(PlayerController me)
        {
            var opps = Squad.Opponents(me);
            int want = EnumField(me, "defensiveMatchup");
            if (want > 0)
                foreach (PlayerController o in opps)
                    if (EnumField(o, "playerType") == want) { _how = "matchup " + (want < Pos.Length ? Pos[want] : want.ToString()); _byGame = true; return o; }
            PlayerController nearest = null;
            float best = float.MaxValue;
            foreach (PlayerController o in opps)
            {
                try { if (Squad.Same(o.GetDefender(), me)) { _how = "GetDefender"; _byGame = true; return o; } } catch { }
                float d = CourtMap.Flat(o.transform.position, me.transform.position);
                if (d < best) { best = d; nearest = o; }
            }
            _how = "nearest"; _byGame = false;
            return nearest;
        }

        /// <summary>Development: matchup data for the log.</summary>
        private static string MatchupInfo(PlayerController me)
        {
            var sb = new System.Text.StringBuilder("me " + Squad.Name(me) + " type=" + EnumField(me, "playerType") + " dm=" + EnumField(me, "defensiveMatchup") + " |");
            foreach (PlayerController o in Squad.Opponents(me))
            {
                bool mark = false;
                try { mark = o.defensiveTarget != null && o.defensiveTarget.enabled && o.defensiveTarget.gameObject.activeInHierarchy; } catch { }
                string gd = "";
                try { gd = Squad.Name(o.GetDefender()); } catch { }
                sb.Append(" " + Squad.Name(o) + ":t" + EnumField(o, "playerType") + (mark ? ":MARK" : "") + ":gd=" + gd);
            }
            return sb.ToString();
        }

        /// <summary>The hoop the attackers go to = the hoop the attacker attacks.</summary>
        public static Vector3? DefendedHoop(PlayerController attacker) => CourtMap.AttackHoop(attacker);

        /// <summary>F4 / R3 on defense: "Smith has the ball, 9 feet, left and up. Step left to get between him and the hoop."</summary>
        public static void SayMan()
        {
            PlayerController me = CourtMap.Me();
            if (!MatchInfo.Active || me == null) { Speech.Say("No match running."); return; }
            if (!OnDefense) { Speech.Say("Your team has the ball."); return; }
            PlayerController man = _man != null ? _man : MyMan(me);
            if (man == null) { Speech.Say("Nobody to guard."); return; }
            Vector3 pm = man.transform.position, pme = me.transform.position;
            string line = "Guarding " + Squad.Name(man) + (Squad.Same(Squad.Handler(), man) ? ", he has the ball" : "") + ", " + Squad.Where(pme, pm) + ". ";
            Vector3? hoopN = DefendedHoop(man);
            if (hoopN != null)
            {
                // The guard spot: one unit (about 4 feet) from him, toward the hoop.
                Vector3 hoop = hoopN.Value;
                Vector2 toHoop = new Vector2(hoop.x - pm.x, hoop.y - pm.y);
                float gap = Mathf.Min(1f, toHoop.magnitude * 0.5f);
                Vector3 spot = pm + (Vector3)(toHoop.normalized * gap);
                float dx = (spot.x - pme.x) * CourtMap.FeetPerUnit, dy = (spot.y - pme.y) * CourtMap.FeetPerUnit;
                string step = Squad.DirWords(dx, dy);
                bool between = CourtMap.Flat(pme, hoop) < CourtMap.Flat(pm, hoop);
                if (step.Length == 0) line += "Good spot, you are between him and the hoop.";
                else line += (between ? "You are between him and the hoop. To tighten up, step " : "Step ") + step + ".";
            }
            Speech.Say(line);
        }
    }
}
