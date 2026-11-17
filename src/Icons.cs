using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Names the key and button pictures the game shows (controls screens, tutorial prompts).
    /// The game keeps one table per device in its InputIconManager asset (keyboardIcons, xboxIcons...),
    /// one Sprite per key. We turn each Sprite back into its key name. Proven from Assembly-CSharp
    /// (InputIconManager, KeyboardIcons, GamepadIcons), game 1.12.1.</summary>
    internal static class Icons
    {
        private static readonly Dictionary<int, string> _names = new();
        private static float _nextTry;

        // Xbox names for the generic gamepad field names.
        private static readonly Dictionary<string, string> Pad = new()
        {
            ["buttonSouth"] = "A", ["buttonEast"] = "B", ["buttonWest"] = "X", ["buttonNorth"] = "Y",
            ["leftShoulder"] = "LB", ["rightShoulder"] = "RB", ["leftTrigger"] = "LT", ["rightTrigger"] = "RT",
            ["startButton"] = "Menu button", ["selectButton"] = "View button",
            ["dpad"] = "D-pad", ["dpadUp"] = "D-pad Up", ["dpadDown"] = "D-pad Down", ["dpadLeft"] = "D-pad Left", ["dpadRight"] = "D-pad Right",
            ["leftStick"] = "Left stick", ["leftStickUp"] = "Left stick up", ["leftStickDown"] = "Left stick down",
            ["leftStickLeft"] = "Left stick left", ["leftStickRight"] = "Left stick right", ["leftStickPress"] = "Left stick click",
            ["rightStick"] = "Right stick", ["rightStickUp"] = "Right stick up", ["rightStickDown"] = "Right stick down",
            ["rightStickLeft"] = "Right stick left", ["rightStickRight"] = "Right stick right", ["rightStickPress"] = "Right stick click",
        };

        private static void Build()
        {
            if (_names.Count > 0 || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 5f;
            foreach (InputIconManager m in Resources.FindObjectsOfTypeAll<InputIconManager>())
            {
                Add(m.keyboardIcons, keyboard: true);
                Add(m.xboxIcons, false);
                Add(m.gamepadIcons, false);
                Add(m.psIcons, false);
                Add(m.switchIcons, false);
                Add(m.steamDeckIcons, false);
            }
            Mod.Log("Icons: named " + _names.Count + " key/button pictures.");
        }

        private static void Add(object table, bool keyboard)
        {
            if (table == null) return;
            foreach (PropertyInfo p in table.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType != typeof(Sprite)) continue;
                Sprite s;
                try { s = p.GetValue(table) as Sprite; } catch { continue; }
                if (s == null) continue;
                int id = s.GetInstanceID();
                if (_names.ContainsKey(id)) continue; // first table wins (keyboard, then Xbox)
                _names[id] = keyboard ? KeyName(p.Name) : (Pad.TryGetValue(p.Name, out var n) ? n : Words(p.Name));
            }
        }

        private static string KeyName(string f)
        {
            if (f.Length == 1) return f.ToUpperInvariant();
            if (Regex.IsMatch(f, @"^f\d+$")) return f.ToUpperInvariant();
            if (f.StartsWith("num") && f.Length == 4 && char.IsDigit(f[3])) return "Numpad " + f[3];
            switch (f)
            {
                case "zero": return "0"; case "one": return "1"; case "two": return "2"; case "three": return "3";
                case "four": return "4"; case "five": return "5"; case "six": return "6"; case "seven": return "7";
                case "eight": return "8"; case "nine": return "9";
                case "leftButton": return "Left mouse button";
                case "rightButton": return "Right mouse button";
                case "middleButton": return "Middle mouse button";
                case "leftMeta": return "Left Windows key";
                case "rightMeta": return "Right Windows key";
            }
            return Words(f);
        }

        private static string Words(string f)
        {
            string w = Regex.Replace(f, "([a-z])([A-Z0-9])", "$1 $2");
            return char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant().Replace("ctrl", "control");
        }

        /// <summary>Key name for a sprite, or null.</summary>
        public static string NameOfSprite(Sprite sp)
        {
            if (sp == null) return null;
            Build();
            return _names.TryGetValue(sp.GetInstanceID(), out var n) ? n : null;
        }

        /// <summary>Key name for one picture, or null.</summary>
        public static string NameOf(Image img)
        {
            if (img == null || img.sprite == null || !img.isActiveAndEnabled) return null;
            Build();
            return _names.TryGetValue(img.sprite.GetInstanceID(), out var n) ? n : null;
        }

        /// <summary>All key pictures inside an object, e.g. "W", or "Hold, Right arrow".</summary>
        public static List<string> NamesIn(GameObject go)
        {
            var list = new List<string>();
            foreach (Image img in go.GetComponentsInChildren<Image>(false))
            {
                string n = NameOf(img);
                if (n != null && !list.Contains(n)) list.Add(n);
            }
            return list;
        }
    }
}
