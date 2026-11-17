using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Franchise draft, draft lottery, draft pick announcement, declare-for-draft press conference
    /// and playoff bracket. These screens animate or change by themselves, so nothing is selected when
    /// the news appears: the mod speaks it.
    /// Sources (decompiled 1.12.1 interop, 2026-11-16): SeasonDraft (static draftOrder, status, pg..c, picks, record,
    /// rosterSize, currentRound, currentPickNumber, draftSelections, simulatingPicks), DraftSelection
    /// (commissionerText, round, pick, playerCard, continueButton), DraftLottery (top4, continueButton, isSimulating),
    /// DeclareForDraft (question, answer, continueButton), PlayoffBracket (eastBrackets, westBrackets,
    /// currentRoundText), PlayerData (fn, ln, pos, age, tid), TeamData (id, city, name, isPlayer),
    /// SeasonData.teamId, GameManager.CurrentLeague().
    /// HYPOTHESIS (not yet seen in play): what the status text says, and how bracket matchups are laid
    /// out. Each screen is saved to a file the first time it opens so Claude can check.</summary>
    internal static class Season
    {
        private static float _next;
        private static SeasonDraft _draft;
        private static int _draftId, _picksSeen = -1, _batch;
        private static float _lastPickAt, _batchSince;
        private static string _lastStatus = "", _lastBatchLine = "";
        private static bool _wasOnClock;
        private static int _selId, _lotteryId, _declareId;
        private static string _lastQuestion = "", _pendingQuestion = "";
        private static float _questionSince;
        private static readonly HashSet<string> _dumped = new();

        public static bool DraftActive => _draft != null && _draft.gameObject.activeInHierarchy;

        public static PlayoffBracket Bracket()
        {
            PlayoffBracket b = Object.FindObjectOfType<PlayoffBracket>();
            return b != null && b.isActiveAndEnabled ? b : null;
        }

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.3f;
            if (MatchInfo.Active) return;
            Draft();
            Selection();
            Lottery();
            Declare();
            if (Bracket() != null) DumpOnce("bracket");
        }

        // ---------- live draft ----------

        private static void Draft()
        {
            SeasonDraft d = Object.FindObjectOfType<SeasonDraft>();
            if (d == null || !d.isActiveAndEnabled) { _draft = null; return; }
            if (d.GetInstanceID() != _draftId)
            {
                _draftId = d.GetInstanceID();
                _picksSeen = Count(SeasonDraft.draftSelections);
                _lastStatus = ""; _wasOnClock = false; _batch = 0;
                DumpOnce("draft");
                Speech.Say("Draft. F5 says who is on the clock and your team's needs.", interrupt: false);
            }
            _draft = d;

            // New picks. When the game simulates quickly, picks are grouped instead of read one by one.
            int n = Count(SeasonDraft.draftSelections);
            if (n > _picksSeen && _picksSeen >= 0)
            {
                for (int i = _picksSeen; i < n; i++)
                {
                    string line = PickLine(SeasonDraft.draftSelections[i], i + 1);
                    Mod.Log("Draft pick: " + line);
                    bool fast = Time.unscaledTime - _lastPickAt < 1.2f;
                    _lastPickAt = Time.unscaledTime;
                    if (fast || _batch > 0) { if (_batch == 0) _batchSince = Time.unscaledTime; _batch++; _lastBatchLine = line; }
                    else Speech.Say(line, interrupt: false);
                }
            }
            _picksSeen = n;
            if (_batch > 0 && Time.unscaledTime - _lastPickAt > 1.5f)
            {
                Speech.Say(_batch + " more picks. Last: " + _lastBatchLine, interrupt: false);
                _batch = 0;
            }

            // Status line (who is on the clock). Spoken when it changes, and loudly when it is your turn.
            string status = Txt(d.status);
            if (status.Length > 0 && status != _lastStatus)
            {
                _lastStatus = status;
                Mod.Log("Draft status: " + status);
            }
            bool onClock = YourTurn(d);
            if (onClock && !_wasOnClock)
                Speech.Say("You are on the clock. Round " + SeasonDraft.currentRound + ", pick " + SeasonDraft.currentPickNumber
                    + ". Pick a player from the list, or press Auto Pick.", interrupt: true);
            _wasOnClock = onClock;
        }

        /// <summary>HYPOTHESIS: draftOrder is the full pick order, so the team on the clock is the one after
        /// the picks already made. Checked against the saved draft screen at the first test.</summary>
        private static TeamData OnClock()
        {
            try
            {
                var order = SeasonDraft.draftOrder;
                int made = Count(SeasonDraft.draftSelections);
                return order != null && made < order.Count ? order[made] : null;
            }
            catch { return null; }
        }

        private static bool YourTurn(SeasonDraft d)
        {
            try
            {
                TeamData t = OnClock();
                if (t == null || SeasonDraft.simulatingPicks) return false;
                LeagueData lg = GameManager.CurrentLeague();
                return t.isPlayer || (lg != null && lg.season != null && lg.season.teamId == t.id);
            }
            catch { return false; }
        }

        /// <summary>F5 on the draft screen.</summary>
        public static void SayDraftStatus()
        {
            SeasonDraft d = _draft;
            if (d == null) return;
            var parts = new List<string> { "Round " + SeasonDraft.currentRound + ", pick " + SeasonDraft.currentPickNumber + "." };
            TeamData oc = OnClock(); string team = oc != null ? TeamName(oc) : "";
            if (YourTurn(d)) parts.Add("You are on the clock.");
            else if (team.Length > 0) parts.Add("On the clock: " + team + ".");
            if (Txt(d.status).Length > 0) parts.Add(Txt(d.status) + ".");
            if (Txt(d.record).Length > 0) parts.Add("Record " + Txt(d.record) + ".");
            if (Txt(d.rosterSize).Length > 0) parts.Add("Roster " + Txt(d.rosterSize) + ".");
            var pos = new List<string>();
            void P(string name, Text t) { if (Txt(t).Length > 0) pos.Add(name + " " + Txt(t)); }
            P("Point guards", d.pg); P("shooting guards", d.sg); P("small forwards", d.sf); P("power forwards", d.pf); P("centers", d.c);
            if (pos.Count > 0) parts.Add(string.Join(", ", pos) + ".");
            if (Txt(d.picks).Length > 0) parts.Add("Picks: " + Txt(d.picks) + ".");
            if (SeasonDraft.isPaused) parts.Add("Draft paused.");
            Speech.Say(string.Join(" ", parts));
        }

        private static string PickLine(PlayerData p, int overall)
        {
            if (p == null) return "Pick " + overall + ".";
            string team = "";
            try
            {
                LeagueData lg = GameManager.CurrentLeague();
                TeamData t = null; if (lg != null && lg.teams != null) foreach (TeamData x in lg.teams) if (x != null && x.id == p.tid) { t = x; break; }
                if (t != null) team = TeamName(t) + ": ";
            }
            catch { }
            return "Pick " + overall + ". " + team + p.fn + " " + p.ln + ", " + p.pos + ", age " + p.age + ".";
        }

        // ---------- "with the first pick..." screen ----------

        private static void Selection()
        {
            DraftSelection s = Object.FindObjectOfType<DraftSelection>();
            if (s == null || !s.isActiveAndEnabled) return;
            if (s.GetInstanceID() == _selId) return;
            if (s.continueButton == null || !s.continueButton.activeInHierarchy) return; // still typing
            _selId = s.GetInstanceID();
            DumpOnce("draft_selection");
            string words = string.Join(" ", new[] { Txt(s.round), Txt(s.pick), Txt(s.commissionerText) }.Where(w => w.Length > 0));
            string card = s.playerCard != null ? MenuReader.ReadPanel(s.playerCard.transform) : "";
            Speech.Say(MenuReader.Clean(words) + " " + card, interrupt: true);
        }

        // ---------- lottery ----------

        private static void Lottery()
        {
            DraftLottery l = Object.FindObjectOfType<DraftLottery>();
            if (l == null || !l.isActiveAndEnabled) return;
            if (l.GetInstanceID() == _lotteryId || DraftLottery.isSimulating) return;
            if (l.continueButton == null || !l.continueButton.activeInHierarchy) return;
            _lotteryId = l.GetInstanceID();
            DumpOnce("lottery");
            var lines = new List<string>();
            if (l.top4 != null)
                for (int i = 0; i < l.top4.Length; i++)
                {
                    Transform t = l.top4[i];
                    string w = t != null ? MenuReader.ReadPanel(t).Trim() : "";
                    if (w.Length > 0) lines.Add("Pick " + (i + 1) + ": " + w);
                }
            Speech.Say(lines.Count > 0 ? "Lottery results. " + string.Join(" ", lines) : "Lottery done. Press F2 to read the order.", interrupt: true);
        }

        // ---------- declare for draft (career press conference) ----------

        private static void Declare()
        {
            DeclareForDraft dc = Object.FindObjectOfType<DeclareForDraft>();
            if (dc == null || !dc.isActiveAndEnabled) { _lastQuestion = ""; return; }
            if (dc.GetInstanceID() != _declareId) { _declareId = dc.GetInstanceID(); DumpOnce("declare"); }
            // The question types out letter by letter: speak it once it stops changing.
            string q = Txt(dc.question);
            if (q != _pendingQuestion) { _pendingQuestion = q; _questionSince = Time.unscaledTime; return; }
            if (q.Length == 0 || q == _lastQuestion || Time.unscaledTime - _questionSince < 0.6f) return;
            _lastQuestion = q;
            Speech.Say(q, interrupt: true);
        }

        // ---------- playoffs ----------

        /// <summary>F5 on the playoff bracket: the current round and each matchup.</summary>
        public static void SayBracket()
        {
            PlayoffBracket b = Bracket();
            if (b == null) return;
            var parts = new List<string>();
            if (Txt(b.currentRoundText).Length > 0) parts.Add(Txt(b.currentRoundText) + ".");
            GameObject shown = null;
            foreach (var arr in new[] { b.eastBrackets, b.westBrackets })
                if (arr != null && shown == null)
                    foreach (GameObject g in arr) if (g != null && g.activeInHierarchy) { shown = g; break; }
            if (shown == null) { MenuReader.ReadScreen(); return; }
            // Each child of the bracket is read as one piece (a round column or a matchup).
            for (int i = 0; i < shown.transform.childCount; i++)
            {
                Transform ch = shown.transform.GetChild(i);
                if (!ch.gameObject.activeInHierarchy) continue;
                string w = MenuReader.ReadPanel(ch).Trim();
                if (w.Length > 0) parts.Add(MenuReader.NameToWords(ch.name) + ": " + w);
            }
            Speech.Say(parts.Count > 0 ? string.Join(" ", parts) : "Bracket is empty.");
        }

        // ---------- helpers ----------

        private static int Count(Il2CppSystem.Collections.Generic.List<PlayerData> l) => l != null ? l.Count : 0;

        private static string Txt(Text t) => t != null && t.isActiveAndEnabled ? MenuReader.Clean(t.text) : "";

        private static string TeamName(TeamData t)
        {
            string s = ((t.city ?? "") + " " + (t.name ?? "")).Trim();
            return s.Length > 0 ? s : t.shortName ?? "";
        }

        /// <summary>The first time a screen opens, save its layout for Claude (proves the hypotheses above).</summary>
        private static void DumpOnce(string what)
        {
            if (!_dumped.Add(what)) return;
            try
            {
                string path = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "HoopLandAccess_" + what + ".txt");
                UiScreen.Dump(path);
                Mod.Log("Saved screen layout: " + path);
            }
            catch (System.Exception e) { Mod.Log("Dump " + what + " failed: " + e.Message); }
        }
    }
}
