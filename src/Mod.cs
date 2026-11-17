using System;
using MelonLoader;

[assembly: MelonInfo(typeof(HoopLandAccess.Mod), "Hoop Land Access", HoopLandAccess.Mod.Version, "Claude for the player")]
[assembly: MelonGame("Koality Game", "Hoop Land")]

namespace HoopLandAccess
{
    /// <summary>Entry point. Keeps the per-frame loop small: each feature lives in its own file.</summary>
    public class Mod : MelonMod
    {
        public const string Version = "0.8.0";

        public static void Log(string s) => MelonLogger.Msg(s);

        private static MelonPreferences_Entry<bool> _straightPref;
        private static MelonPreferences_Entry<int> _modePref;
        private static bool _trace;

        public override void OnInitializeMelon()
        {
            var cat = MelonPreferences.CreateCategory("HoopLandAccess");
            _straightPref = cat.CreateEntry("StraightListMenus", true, "Up/Down moves through menu items one by one in reading order");
            UiScreen.StraightList = _straightPref.Value;
            Speech.Init();
            Tone.Start();
            _modePref = cat.CreateEntry("ShotHelp", 0, "0 rising tone, 1 count-in beeps, 2 off");
            Shooting.Mode = _modePref.Value;
            Log("Hoop Land Access " + Version + " loaded.");
        }

        private bool _greeted;
        private string _lastError;

        public override void OnUpdate()
        {
            try
            {
                float now = UnityEngine.Time.unscaledTime;
                if (!_greeted && now > 2f)
                {
                    _greeted = true;
                    Speech.Say("Hoop Land Access " + Version + " ready.", interrupt: false);
                }

                Keys.Update(now);
                UiScreen.Update();
                Rebind.Update(MenuReader.SelectedObject());
                if (TextEntry.Active) TextEntry.Update();
                else if (!Rebind.Waiting) HandleKeys();
                MatchInfo.Update();
                Shooting.Update();
                Defense.Update();
                Passing.Update();
                Boundary.Update();
                Assist.Update();
                BindingDump.Update();
                if (_trace) MatchProbe.ShotTrace();
                if (!TextEntry.Active) MenuReader.Update();
            }
            catch (Exception e)
            {
                if (_lastError != e.Message) { _lastError = e.Message; MelonLogger.Error(e.ToString()); }
            }
        }

        private static float _pauseCheckAt;

        private static void HandleKeys()
        {
            // F10 (development): log a snapshot of match data for Claude.
            if (Keys.Pressed(Keys.F(10))) MatchProbe.Snapshot();

            // Match information (F-keys never clash with the game's play keys)
            // Controller in a match: the game uses sticks, D-pad, A B X Y, LB, RB, View, Menu.
            // Free (proven from the game's bindings 2026-11-15): LT, RT, L3, R3.
            bool live = MatchInfo.Active || MenuReader.InLiveGame();
            if (Keys.Pressed(Keys.F(5)) || (live && Keys.PadPressed(Keys.RT))) MatchInfo.SayStatus();
            if (Keys.Pressed(Keys.F(6)) || (live && Keys.PadPressed(Keys.LT))) MatchInfo.SayBall();
            // R3: on offense the rim, on defense your man.  L3: on offense your teammates, on defense the last commentator line.
            if (live && Keys.PadPressed(Keys.RightStick)) { if (Defense.OnDefense) Defense.SayMan(); else CourtMap.SayRim(); }
            if (live && Keys.PadPressed(Keys.LeftStick))
            {
                if (!Defense.OnDefense) Passing.SayTeammates();
                else Speech.Say(MatchInfo.LastLine.Length > 0 ? MatchInfo.LastLine : "Nothing said yet.");
            }
            if (Keys.Pressed(Keys.F(8))) CourtMap.SayRim();
            if (Keys.Pressed(Keys.F(3))) { if (live) Passing.SayTeammates(); else Profile.SayBarsOnScreen(); }
            if (Keys.Pressed(Keys.F(4))) Defense.SayMan();
            if (live && Keys.Pressed(Keys.F(1))) Speech.Say(MatchInfo.LastLine.Length > 0 ? MatchInfo.LastLine : "Nothing said yet.");
            if (Keys.Pressed(Keys.F(7)))
            {
                Shooting.Mode = (Shooting.Mode + 1) % 3;
                _modePref.Value = Shooting.Mode; MelonPreferences.Save();
                Speech.Say(Shooting.ModeName());
            }
            if (Keys.Pressed(Keys.F(9))) { _trace = !_trace; Speech.Say(_trace ? "Shot trace on." : "Shot trace off."); }

            // Reading
            if ((!live && Keys.Pressed(Keys.F(1))) || (!live && Keys.PadPressed(Keys.RightStick))) MenuReader.RepeatCurrent();
            if (Keys.Pressed(Keys.F(2)) || (!live && Keys.PadPressed(Keys.LeftStick))) MenuReader.ReadScreen();
            // Keyboard menu control (the game has keyboard controls for playing, not for menus).
            // During a match the arrows, Space and Shift are the game's own action keys: hands off.
            if (MenuReader.InLiveGame())
            {
                // Escape is the game's own Pause key. If the game did not pause within half a second
                // (seen once in the tutorial), press its Pause button for it.
                if (Keys.Pressed(Keys.Escape)) _pauseCheckAt = UnityEngine.Time.unscaledTime + 0.5f;
                if (_pauseCheckAt > 0 && UnityEngine.Time.unscaledTime >= _pauseCheckAt)
                {
                    _pauseCheckAt = 0;
                    if (MenuReader.InLiveGame()) MenuReader.PressByName("Pause Button");
                }
                return;
            }
            _pauseCheckAt = 0; // the game paused by itself
            // During play nothing is selected: the arrows are the game's action keys, not menu keys.
            if (MatchInfo.Active && MenuReader.SelectedObject() == null) return;
            if (Keys.PressedOrRepeat(Keys.Down) || (Keys.PressedOrRepeat(Keys.Tab) && !Keys.Held(Keys.Shift))) MenuReader.MoveBy(+1);
            if (Keys.PressedOrRepeat(Keys.Up) || (Keys.PressedOrRepeat(Keys.Tab) && Keys.Held(Keys.Shift))) MenuReader.MoveBy(-1);
            // Controller D-pad Left/Right on a picker: press its arrows (the game would only move focus).
            if (Keys.PadPressed(Keys.DpadLeft)) MenuReader.TryCycleSelected(false);
            if (Keys.PadPressed(Keys.DpadRight)) MenuReader.TryCycleSelected(true);
            if (Keys.PressedOrRepeat(Keys.Left)) MenuReader.Sideways(false);
            if (Keys.PressedOrRepeat(Keys.Right)) MenuReader.Sideways(true);
            if (Keys.Pressed(Keys.Home)) MenuReader.MoveTo(first: true);
            if (Keys.Pressed(Keys.End)) MenuReader.MoveTo(first: false);
            if (Keys.Pressed(Keys.Enter) || Keys.Pressed(Keys.Space)) MenuReader.Activate();
            if (Keys.Pressed(Keys.Escape) || Keys.Pressed(Keys.Backspace)) MenuReader.Back();

            // F11: straight-list menus on/off (saved)
            if (Keys.Pressed(Keys.F(11)))
            {
                UiScreen.StraightList = !UiScreen.StraightList;
                _straightPref.Value = UiScreen.StraightList;
                MelonPreferences.Save();
                Speech.Say(UiScreen.StraightList
                    ? "Straight list menus on. Up and down go through every item in order."
                    : "Straight list menus off. Restart the game to get the game's own layout back.");
            }

            // F12: save a description of this screen for Claude
            if (Keys.Pressed(Keys.F(12)))
            {
                string path = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "HoopLandAccess_screen.txt");
                UiScreen.Dump(path);
                Speech.Say("Screen saved for Claude.");
            }
        }

        public override void OnApplicationQuit() => Speech.Shutdown();
    }
}



















