using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Step 3: shooting.
    /// While YOUR player holds shoot, a tone plays whose pitch follows the shot meter:
    /// it rises toward the green zone and falls after it. Release at the highest pitch.
    /// Source (proven 2026-11-15, Shootaround trace): Basketball.shotBegan / shooter.isPlayer / shotReleased,
    /// and the ball's shotPower, which climbs 0 -> 1.0 at the green zone, then drops.
    /// After the shot, the game's own feedback word is spoken (PlayerController.DisplayShotFeedback).</summary>
    internal static class Shooting
    {
        /// <summary>0 = rising tone, 1 = count-in beeps (beep, beep, BEEP = release), 2 = off.</summary>
        public static int Mode = 0;
        public static string ModeName() => Mode == 0 ? "Shot help: rising tone." : Mode == 1 ? "Shot help: count-in beeps. Release on the third, higher beep." : "Shot help off.";

        // count-in state
        private static float _lastH, _lastHTime;
        private static int _moving;
        private static bool _scheduled;
        private static double _predicted, _peakTime;
        private static float _peakPower;
        private static bool _charging;
        private static float _calibrateAt, _missCheckAt;
        private static int _scoreAtShot;
        private static Vector3 _shotPos;

        public static void Update()
        {
            PlayerController s = Basketball.shooter;
            bool charging = Mode != 2 && Basketball.shotBegan && !Basketball.shotReleased && s != null && s.isPlayer;
            if (!charging)
            {
                if (_charging)
                {
                    _charging = false; Tone.Volume = 0f;
                    if (Mode == 1 && _scheduled)
                        Mod.Log("Count-in: predicted green at " + _predicted.ToString("F3") + ", meter peak (power " + _peakPower.ToString("F3") + ") at " + _peakTime.ToString("F3")
                            + ", difference " + ((_peakTime - _predicted) * 1000).ToString("F0") + " ms");
                    _calibrateAt = Time.unscaledTime + 0.3f; // ShotData is filled in just after release
                    if (MatchInfo.Active) { _missCheckAt = Time.unscaledTime + 3f; _scoreAtShot = MatchInfo.TotalScore(); }
                    _shotPos = s != null ? s.transform.position : Vector3.zero;
                }
                // In a match a made shot gets a commentator line and a new score; a miss gets nothing. Say it.
                if (_missCheckAt > 0 && Time.unscaledTime >= _missCheckAt)
                {
                    _missCheckAt = 0;
                    if (MatchInfo.TotalScore() == _scoreAtShot) Speech.Say("Missed.", interrupt: false);
                }
                if (_calibrateAt > 0 && Time.unscaledTime >= _calibrateAt)
                {
                    _calibrateAt = 0;
                    var sd = Basketball.lastShotData;
                    float world = CourtMap.NearestHoopDistance(_shotPos);
                    Mod.Log("Calibrate: shot from " + _shotPos.ToString("F2") + " world dist " + world.ToString("F2") + " game says " + (sd != null ? sd.dis : -1) + " ft");
                }
                return;
            }
            Basketball b = s.basketball;
            float power = b != null ? Mathf.Clamp01(b.shotPower) : 0f;
            if (!_charging)
            {
                _charging = true; Tone.Frequency = 220f;
                _moving = 0; _scheduled = false; _peakPower = 0; _lastHTime = -1;
            }
            if (power > _peakPower) { _peakPower = power; _peakTime = Tone.Now; }

            if (Mode == 0)
            {
                // Square the power so the top of the climb is spread out: the last part matters most.
                Tone.Frequency = 220f + 880f * power * power;
                Tone.Volume = 0.8f;
                Tone.Pan = 0f;
                return;
            }

            // Mode 1, count-in: the meter handle moves at a steady speed, so we can work out when it
            // reaches the green zone and play beep, beep, BEEP landing exactly on it. People can hit a
            // beat they hear coming far more precisely than they can react to a single beep.
            Tone.Volume = 0f;
            if (_scheduled || b == null || b.shotbarHandle == null || b.shotbarHandle.Length == 0) return;
            float h = b.shotbarHandle[0].localPosition.x;
            float now = Time.unscaledTime;
            if (power > 0.05f && _lastHTime > 0 && now > _lastHTime)
            {
                float v = (h - _lastH) / (now - _lastHTime);
                if (v > 5f) _moving++;
                if (_moving >= 2)
                {
                    ShotBar bar = Object.FindObjectOfType<ShotBar>();
                    float green = bar != null ? bar.green.localPosition.x + bar.green.rect.width * 0.5f : 2.3f;
                    // measured 2026-11-15: real peak comes ~20 ms before this estimate, so move it earlier
                    double tg = Tone.Now + (green - h) / v - 0.02;
                    _predicted = tg; _scheduled = true;
                    const double gap = 0.12;
                    if (tg - 2 * gap > Tone.Now) Tone.BeepAt(tg - 2 * gap, 0.04, 700f);
                    if (tg - gap > Tone.Now) Tone.BeepAt(tg - gap, 0.04, 700f);
                    Tone.BeepAt(tg - 0.01, 0.06, 1300f);
                }
            }
            _lastH = h; _lastHTime = now;
        }

        /// <summary>Speaks the game's shot feedback ("PERFECT", "LATE"...). Only for your own player.</summary>
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.DisplayShotFeedback))]
        private static class FeedbackPatch
        {
            private static void Prefix(PlayerController __instance, string __0)
            {
                Mod.Log("Shot feedback: '" + __0 + "' player=" + (__instance != null && __instance.isPlayer));
                if (__instance == null || !__instance.isPlayer || string.IsNullOrWhiteSpace(__0)) return;
                Speech.Say(MenuReader.Clean(__0), interrupt: true);
            }
        }
    }
}





