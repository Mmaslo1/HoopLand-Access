using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Step 2: information during a match.
    /// - Speaks the game's own commentator line ("HAUSER FROM DOWNTOWN!") when it changes.
    /// - Speaks the score after it changes, the quarter when it changes.
    /// - Speaks which team has the ball when possession changes, and "Loose ball".
    /// - F5: score, quarter, clock, shot clock.  F6: who has the ball.
    /// Sources (proven 2026-11-15, game 1.12.1, CPU vs CPU exhibition):
    ///   HUD texts under "Game UI/Safe Area Y/Scoreboard/..." (Clock, Shot Clock, Period, Home|Road/Name|Score, Commentator)
    ///   Basketball.lastBallHandler / looseBall (static, readable), PlayerController.team.id, PlayerData.tid,
    ///   LeagueData.teams (TeamData.id, city, name) via GameManager.CurrentLeague().</summary>
    internal static class MatchInfo
    {
        private static Transform _board;           // ".../Scoreboard"
        private static float _nextFind, _nextPoll;
        private static string _comment = "", _homeScore = "", _roadScore = "", _period = "";
        private static int _possTeam = -1;         // team id with the ball, -2 = loose
        private static bool _wasActive;
        private static float _commentTime, _candSince;
        private static int _candTeam = -1;
        private static bool _inbounding, _mateInbound;
        private static float _mateInboundAt, _nextInbLog;

        private static bool IsNumber(string s) => s.Length > 0 && char.IsDigit(s[0]) && int.TryParse(s, out _);

        /// <summary>Last commentator line (L3 repeats it).</summary>
        public static string LastLine = "";

        /// <summary>Both scores added up (for "Missed" detection).</summary>
        public static int TotalScore() { int.TryParse(_homeScore, out int h); int.TryParse(_roadScore, out int r); return h + r; }

        public static bool Active => _board != null && _board.gameObject.activeInHierarchy;

        public static void Update()
        {
            if (_board == null || !_board.gameObject.activeInHierarchy)
            {
                if (_wasActive) { _wasActive = false; Reset(); }
                if (Time.unscaledTime < _nextFind) return;
                _nextFind = Time.unscaledTime + 1f;
                GameObject ui = GameObject.Find("Game UI");
                _board = ui != null ? ui.transform.Find("Safe Area Y/Scoreboard") : null;
                if (_board == null || !_board.gameObject.activeInHierarchy) return;
                _wasActive = true;
                // Remember current values silently so we don't announce everything at tip-off.
                _homeScore = T("Elements/Home/Score/Text"); _roadScore = T("Elements/Road/Score/Text");
                _period = T("Elements/Period/Text"); _comment = T("Commentator/Text");
                Mod.Log("Match HUD found.");
            }

            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.1f;

            // 1. Commentator line
            // The line blinks (empty / same text) while animating: speak each new line once.
            string c = T("Commentator/Text");
            if (c.Length > 0 && (c != _comment || Time.unscaledTime - _commentTime > 6f))
            {
                if (c != _comment) { Speech.Say(c, interrupt: false); LastLine = c; }
                _comment = c;
            }
            if (c.Length > 0) _commentTime = Time.unscaledTime;

            // 2. Score
            // Scores animate ("+3") before settling: only take plain numbers.
            string hs = T("Elements/Home/Score/Text"), rs = T("Elements/Road/Score/Text");
            if (IsNumber(hs) && IsNumber(rs) && (hs != _homeScore || rs != _roadScore))
            {
                _homeScore = hs; _roadScore = rs;
                Speech.Say(ScoreLine(), interrupt: false);
            }

            // 3. Quarter
            string p = T("Elements/Period/Text");
            if (p != _period)
            {
                _period = p;
                if (p.Length > 0) Speech.Say(PeriodWords(p) + ".", interrupt: false);
            }

            // 4. Possession
            // 5. Inbounding: tell the player to pass (seen 2026-11-15: a five-second violation because nobody said so)
            PlayerController inb = null;
            try { if (_pm == null) _pm = Object.FindObjectOfType<PlayerManager>(); inb = _pm != null ? _pm.GetInbounder() : null; } catch { }
            bool inbOut = false; try { inbOut = inb != null && !inb.IsInbounds(); } catch { }
            // Only while standing out of bounds (seen 2026-11-15: GetInbounder also returned the dribbler in the back court).
            bool meInbounding = inb != null && inb.isPlayer && Basketball.lastBallHandler == inb && inbOut;
            if (meInbounding != _inbounding) Mod.Log("Inbound check: me=" + meInbounding + " isInbounds=" + (inb != null ? inb.isInbounds.ToString() : "-") + " pos=" + (inb != null ? inb.transform.position.ToString("F2") : "-"));
            if (meInbounding && !_inbounding) Speech.Say("You inbound the ball. Press A to pass. Keyboard: Down arrow.", interrupt: false);
            _inbounding = meInbounding;
            // A teammate holds the ball out of bounds: he waits for you to call for it (seen 2026-11-15:
            // five-second violations until Call for Ball was pressed).
            PlayerController me = CourtMap.Me();
            bool mateInbounding = inb != null && me != null && !inb.isPlayer && Basketball.lastBallHandler == inb && Basketball.looseBall == 0
                && Squad.SameTeam(inb, me) && !Squad.Same(inb, me) && !inb.IsInbounds();
            if (mateInbounding && !_mateInbound) { _mateInboundAt = Time.unscaledTime + 1.2f; }
            if (mateInbounding && _mateInboundAt > 0 && Time.unscaledTime >= _mateInboundAt)
            {
                _mateInboundAt = 0;
                Speech.Say(Squad.Name(inb) + " inbounds. Call for the ball: A. Keyboard: Down arrow.", interrupt: false);
            }
            if (!mateInbounding) _mateInboundAt = 0;
            _mateInbound = mateInbounding;

            // Wait until possession has been steady for 0.6 s (blocks and tips flip it back and forth).
            int team = PossessionTeam();
            if (team != _candTeam) { _candTeam = team; _candSince = Time.unscaledTime; }
            if (team != _possTeam && team != -1 && Time.unscaledTime - _candSince >= 0.6f)
            {
                _possTeam = team;
                Speech.Say(team == -2 ? "Loose ball." : SideCity(Basketball.lastBallHandler) + " ball.", interrupt: false);
            }
        }

        private static void Reset() { _board = null; _pm = null; _comment = _homeScore = _roadScore = _period = ""; _possTeam = -1; }

        /// <summary>F5</summary>
        public static void SayStatus()
        {
            if (!Active) { Speech.Say("No match running."); return; }
            string clock = T("Elements/Clock/Text"), shot = T("Elements/Shot Clock/Text");
            Speech.Say(ScoreLine() + ". " + PeriodWords(T("Elements/Period/Text")) + ", " + clock + " left. Shot clock " + shot + ".");
        }

        /// <summary>F6</summary>
        public static void SayBall()
        {
            if (!Active) { Speech.Say("No match running."); return; }
            if (Basketball.looseBall > 0) { Speech.Say("Loose ball."); return; }
            PlayerController h = Basketball.lastBallHandler;
            if (h == null || h.playerData == null) { Speech.Say("Nobody has the ball."); return; }
            string who = h.isPlayer ? "You have the ball" : MatchProbe.Name(h) + " has the ball";
            Speech.Say(who + ", " + SideCity(h) + ".");
        }

        private static string ScoreLine()
        {
            return T("Elements/Road/Name/Text") + " " + _roadScore + ", " + T("Elements/Home/Name/Text") + " " + _homeScore;
        }

        /// <summary>Team id with the ball, -2 if loose, -1 if unknown (e.g. ball in the air).</summary>
        private static int PossessionTeam()
        {
            if (Basketball.looseBall > 0) return -2;
            PlayerController h = Basketball.lastBallHandler;
            if (h == null || h.playerData == null) return -1;
            return h.playerData.tid;
        }

        private static PlayerManager _pm;

        /// <summary>City of the player's side: home or road (from PlayerManager.homePlayers),
        /// scoreboard abbreviation ("ATL"), then the league's team with that short name ("Atlanta").</summary>
        private static string SideCity(PlayerController p)
        {
            if (p == null) return "";
            if (_pm == null) _pm = Object.FindObjectOfType<PlayerManager>();
            bool home = false;
            if (_pm != null && _pm.homePlayers != null)
                for (int i = 0; i < _pm.homePlayers.Count; i++)
                    if (_pm.homePlayers[i] == p) { home = true; break; }
            string abbr = T(home ? "Elements/Home/Name/Text" : "Elements/Road/Name/Text");
            try
            {
                LeagueData lg = GameManager.CurrentLeague();
                if (lg != null && lg.teams != null)
                    for (int i = 0; i < lg.teams.Count; i++)
                    {
                        TeamData td = lg.teams[i];
                        if (td != null && string.Equals(td.shortName, abbr, System.StringComparison.OrdinalIgnoreCase)) return td.city;
                    }
            }
            catch { }
            return abbr.Length > 0 ? abbr : (home ? "Home" : "Road");
        }

        private static string PeriodWords(string p)
        {
            switch (p.ToUpperInvariant())
            {
                case "1ST": return "First quarter";
                case "2ND": return "Second quarter";
                case "3RD": return "Third quarter";
                case "4TH": return "Fourth quarter";
                case "OT": return "Overtime";
                default: return p;
            }
        }

        /// <summary>"HAUSER FROM DOWNTOWN!" reads better as "Hauser from downtown!"</summary>
        private static string Sentence(string s)
        {
            if (s.Length < 2) return s;
            string low = s.ToLowerInvariant();
            return char.ToUpperInvariant(low[0]) + low.Substring(1);
        }

        private static string T(string path)
        {
            if (_board == null) return "";
            Transform t = _board.Find(path);
            Text x = t != null ? t.GetComponent<Text>() : null;
            return x != null && x.isActiveAndEnabled ? MenuReader.Clean(x.text) : "";
        }
    }
}






