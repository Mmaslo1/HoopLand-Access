using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Warnings before you step out of bounds, and before a back-court violation.
    /// - "Sideline!" / "Baseline!" once when you come within about 3 feet of a line during live play.
    /// - "Half court!" when you hold the ball in the front court and come near the half-court line.
    /// Court edges: asked from the game itself with PlayerController.StayInbounds(point), which pulls a
    /// far-away point back onto the court (HYPOTHESIS, logged as "Bounds:"). If that gives nothing
    /// usable, the edges grow from where players have actually stood.</summary>
    internal static class Boundary
    {
        private const float Warn = 0.7f;    // units (~3 ft) from the side line or half court
        private const float WarnBase = 0.3f; // baseline: the hoop is only 0.62 units in front of it (measured 2026-11-15)
        private const float Rearm = 1.2f;   // must get this far away before the same warning comes again

        private static bool _have;
        private static float _minX, _maxX, _minY, _maxY;
        private static bool _side, _base, _half;
        private static float _nextObserve, _nextEdge, _holdSince;
        private static bool _crossSaid;
        private static Vector3 _lastP;
        private static float _lastT;
        private static Vector2 _vel;
        private static float _quietUntil;
        // Learned real edges (kept for the whole session), only used if a player is ever whistled out
        // inside the StayInbounds edges. Seen 2026-11-15: step-outs at y = 5.03..5.35, StayInbounds y = 4.75, so it matches.
        private static float _edgeMaxY = 99f, _edgeMinY = -99f, _edgeMaxX = 99f, _edgeMinX = -99f;
        private static readonly System.Collections.Generic.Dictionary<int, bool> _wasIn = new();

        public static void Update()
        {
            if (!MatchInfo.Active) { _have = false; return; }
            PlayerController me = CourtMap.Me();
            if (me == null) return;
            if (!_have) Learn(me);
            if (!_have) return;
            Observe();
            LearnEdges();
            float minY = Mathf.Max(_minY, _edgeMinY), maxY = Mathf.Min(_maxY, _edgeMaxY);
            float minX = Mathf.Max(_minX, _edgeMinX), maxX = Mathf.Min(_maxX, _edgeMaxX);

            Vector3 p = me.transform.position;
            // Only during live play: not while you inbound the ball (you stand out of bounds then).
            bool inbounding = false;
            try { PlayerController ib = Squad.PM()?.GetInbounder(); inbounding = Squad.Same(ib, me) && !me.IsInbounds(); } catch { }
            if (inbounding) { _side = _base = false; return; } // after the inbound the warnings are armed again

            // Out of bounds only matters with the ball: warn only then (standing near a line without it is fine).
            // Also not while you are off the court (dead ball after a whistle, walking to the inbound spot).
            bool onCourt = true; try { onCourt = me.IsInbounds(); } catch { }
            bool hasBall = Squad.Same(Squad.Handler(), me) && onCourt && Time.unscaledTime >= _quietUntil;
            if (!onCourt) _quietUntil = Time.unscaledTime + 1.5f;
            if (!hasBall) { _side = _base = false; }
            float toSide = Mathf.Min(p.y - minY, maxY - p.y);
            float toBase = Mathf.Min(p.x - minX, maxX - p.x);
            // Speed toward each line (units per second), from the last frame. Warn when the line is less
            // than 0.8 s away at this speed (seen 2026-11-15: a fixed 3-foot zone was crossed in 0.1-0.3 s).
            float dt = Time.unscaledTime - _lastT;
            Vector2 vel = dt > 0.0001f && dt < 0.5f ? new Vector2(p.x - _lastP.x, p.y - _lastP.y) / dt : Vector2.zero;
            if (vel.magnitude > 15f) vel = Vector2.zero; // the game moved the player (reset after a whistle), not running
            _lastP = p; _lastT = Time.unscaledTime;
            _vel = Vector2.Lerp(_vel, vel, 0.3f);
            float towardSide = p.y > (minY + maxY) * 0.5f ? _vel.y : -_vel.y;
            float towardBase = p.x > (minX + maxX) * 0.5f ? _vel.x : -_vel.x;
            float sideSoon = toSide - Mathf.Max(0f, towardSide) * 0.8f;   // distance left in 0.8 s
            float baseSoon = toBase - Mathf.Max(0f, towardBase) * 0.8f;
            if (hasBall && !_side && (toSide < 0.35f || sideSoon < 0.1f)) { _side = true; Speech.Say("Sideline!", interrupt: true); }
            else if (_side && toSide > Rearm && sideSoon > 0.3f) _side = false;
            if (hasBall && !_base && (toBase < WarnBase || baseSoon < 0.05f)) { _base = true; Speech.Say("Baseline!", interrupt: true); }
            else if (_base && toBase > WarnBase + 0.5f) _base = false;

            // Half court: with the ball, after it was brought over the line.
            bool mine = Squad.Same(Squad.Handler(), me), advanced = false, front = false;
            try { advanced = PlayerManager.BallAdvanced(); front = me.InFrontCourt(); } catch { }
            float mid = (_minX + _maxX) * 0.5f;
            float toHalf = Mathf.Abs(p.x - mid);
            if (mine && advanced && front && !_half && toHalf < Warn) { _half = true; Speech.Say("Half court! Don't go back.", interrupt: true); }
            else if (_half && toHalf > Rearm) _half = false;

            // Eight-second rule: holding the ball in the back court for 3 seconds -> say which way to go.
            if (mine && !advanced && !front)
            {
                if (_holdSince == 0) _holdSince = Time.unscaledTime;
                if (!_crossSaid && Time.unscaledTime - _holdSince > 3f)
                {
                    _crossSaid = true;
                    Vector3? hoop = CourtMap.AttackHoop(me);
                    string way = hoop != null ? (hoop.Value.x > p.x ? "right" : "left") : "";
                    Speech.Say("Bring the ball over half court" + (way.Length > 0 ? ", go " + way : "") + ".", interrupt: false);
                }
            }
            else { _holdSince = 0; if (!mine) _crossSaid = false; }
        }

        /// <summary>Tightens the real court edges from the spot where any player steps from in to out of bounds
        /// (PlayerController.IsInbounds, readable). Logged as "Edge:".</summary>
        private static void LearnEdges()
        {
            if (Time.unscaledTime < _nextEdge) return;
            _nextEdge = Time.unscaledTime + 0.05f;
            PlayerManager pm = Squad.PM();
            if (pm == null) return;
            PlayerController ib = null;
            try { ib = pm.GetInbounder(); } catch { }
            foreach (var list in new[] { pm.homePlayers, pm.roadPlayers })
            {
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    PlayerController pl = list[i];
                    if (pl == null || !pl.gameObject.activeInHierarchy) continue;
                    bool inb;
                    try { inb = pl.IsInbounds(); } catch { continue; }
                    int id = pl.GetInstanceID();
                    bool had = _wasIn.TryGetValue(id, out bool was);
                    _wasIn[id] = inb;
                    if (!had || inb || !was || Squad.Same(pl, ib)) continue;
                    // This player just stepped out.
                    Vector3 v = pl.transform.position;
                    Mod.Log("Edge: " + Squad.Name(pl) + " stepped out at " + v.ToString("F2"));
                    if (Mathf.Abs(v.x) < 8.5f)
                    {
                        if (v.y > 2f) _edgeMaxY = Mathf.Min(_edgeMaxY, v.y);
                        else if (v.y < -2f) _edgeMinY = Mathf.Max(_edgeMinY, v.y);
                    }
                    else if (Mathf.Abs(v.y) < 2.8f)
                    {
                        if (v.x > 8.5f) _edgeMaxX = Mathf.Min(_edgeMaxX, v.x);
                        else if (v.x < -8.5f) _edgeMinX = Mathf.Max(_edgeMinX, v.x);
                    }
                }
            }
        }

        private static void Learn(PlayerController me)
        {
            try
            {
                Vector3 c = me.transform.position;
                float big = 200f;
                Vector3 r = me.StayInbounds(new Vector3(big, c.y, c.z)), l = me.StayInbounds(new Vector3(-big, c.y, c.z));
                Vector3 u = me.StayInbounds(new Vector3(c.x, big, c.z)), d = me.StayInbounds(new Vector3(c.x, -big, c.z));
                Mod.Log("Bounds: right " + r.ToString("F2") + " left " + l.ToString("F2") + " up " + u.ToString("F2") + " down " + d.ToString("F2"));
                if (r.x < 100 && l.x > -100 && u.y < 100 && d.y > -100 && r.x - l.x > 5 && u.y - d.y > 2)
                {
                    _minX = l.x; _maxX = r.x; _minY = d.y; _maxY = u.y; _have = true;
                    Mod.Log("Bounds from StayInbounds: x " + _minX.ToString("F2") + ".." + _maxX.ToString("F2") + " y " + _minY.ToString("F2") + ".." + _maxY.ToString("F2"));
                    return;
                }
            }
            catch (System.Exception e) { Mod.Log("Bounds: StayInbounds failed " + e.Message); }
            // Fallback: start from the hoops (baseline about 1.2 units behind each) and grow from players' spots.
            var h = CourtMap.Hoops();
            if (h == null) return;
            _minX = Mathf.Min(h[0].x, h[1].x) - 1.2f; _maxX = Mathf.Max(h[0].x, h[1].x) + 1.2f;
            float cy = (h[0].y + h[1].y) * 0.5f;
            _minY = cy - 4f; _maxY = cy + 4f;
            _fallback = true; _have = true;
            Mod.Log("Bounds fallback (HYPOTHESIS) x " + _minX.ToString("F2") + ".." + _maxX.ToString("F2") + " y " + _minY.ToString("F2") + ".." + _maxY.ToString("F2"));
        }

        private static bool _fallback;

        /// <summary>Fallback only: widen the edges to where in-bounds players have stood.</summary>
        private static void Observe()
        {
            if (!_fallback || Time.unscaledTime < _nextObserve) return;
            _nextObserve = Time.unscaledTime + 0.5f;
            PlayerManager pm = Squad.PM();
            if (pm == null) return;
            foreach (var list in new[] { pm.homePlayers, pm.roadPlayers })
            {
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    PlayerController p = list[i];
                    if (p == null) continue;
                    bool inb = false;
                    try { inb = p.IsInbounds(); } catch { }
                    if (!inb) continue;
                    Vector3 v = p.transform.position;
                    _minX = Mathf.Min(_minX, v.x); _maxX = Mathf.Max(_maxX, v.x);
                    _minY = Mathf.Min(_minY, v.y); _maxY = Mathf.Max(_maxY, v.y);
                }
            }
        }
    }
}
