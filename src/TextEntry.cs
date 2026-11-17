using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Typing into a text box (player names...). Enter on the box starts typing; the mod's own
    /// keys switch off so letters, Backspace and Space go to the game. Each change is spoken.
    /// Enter or Escape finishes.</summary>
    internal static class TextEntry
    {
        public static InputField Field { get; private set; }
        private static string _last = "";
        private static float _began;

        public static bool Active => Field != null;

        public static void Begin(InputField f)
        {
            Field = f;
            _began = Time.unscaledTime;
            _last = f.text ?? "";
            f.ActivateInputField();
            f.Select();
            Speech.Say("Typing. Now: " + (_last.Length > 0 ? _last : "empty") + ". Type, then press Enter.");
        }

        public static void Update()
        {
            if (Field == null) return;
            if (!Field.gameObject.activeInHierarchy) { Field = null; return; }
            string now = Field.text ?? "";
            if (now != _last)
            {
                if (now.Length == _last.Length + 1 && now.StartsWith(_last)) Speech.Say(Letter(now[now.Length - 1]));
                else if (now.Length < _last.Length) Speech.Say("Deleted " + (_last.Length > now.Length ? Letter(_last[now.Length]) : "") + ". " + (now.Length > 0 ? now : "empty"));
                else Speech.Say(now);
                _last = now;
            }
            // Finished: Enter or Escape, or the box lost focus (after a short start-up moment).
            bool lostFocus = !Field.isFocused && Time.unscaledTime - _began > 0.4f;
            if (Keys.Pressed(Keys.Enter) || Keys.Pressed(Keys.Escape) || lostFocus)
            {
                Field.DeactivateInputField();
                Speech.Say("Done. " + (now.Length > 0 ? now : "empty") + ".");
                Field = null;
                MenuReader.SkipNextChange();
            }
        }
        private static string Letter(char c) => c == ' ' ? "space" : char.IsUpper(c) ? "cap " + c : c.ToString();
    }
}


