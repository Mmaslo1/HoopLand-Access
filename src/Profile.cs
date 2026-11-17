using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Player Profile, scouting report: each skill is a label ("LAY") with a small colored square
    /// ("Fill" image); the number itself is hidden on screen (Value text off). Proven 2026-11-15 (dump_fr_profile2).
    /// - Fully scouted players (SCOUTING REPORT (100%)): we say the real number from PlayerData.attributes
    ///   (the player is found by the name on the card).
    /// - Not fully scouted: only the color, like a sighted player sees.
    /// Also finds players by name for other screens.</summary>
    internal static class Profile
    {
        public static PlayerData FindPlayer(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            LeagueData lg = null;
            try { lg = GameManager.CurrentLeague(); } catch { }
            if (lg == null) return null;
            PlayerData Check(Il2CppSystem.Collections.Generic.List<PlayerData> list)
            {
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    PlayerData p = list[i];
                    if (p != null && string.Equals((p.fn + " " + p.ln).Trim(), fullName.Trim(), System.StringComparison.OrdinalIgnoreCase)) return p;
                }
                return null;
            }
            if (lg.teams != null)
                for (int i = 0; i < lg.teams.Count; i++)
                {
                    PlayerData p = lg.teams[i] != null ? Check(lg.teams[i].roster) : null;
                    if (p != null) return p;
                }
            return Check(lg.freeAgents) ?? Check(lg.draftClass);
        }

        /// <summary>The attribute array (current, max) for a short label like "LAY" or "3PT".</summary>
        private static int[] Attr(PlayerData p, string label)
        {
            if (p == null || p.attributes == null) return null;
            string prop = label == "3PT" ? "TPT" : label;
            try
            {
                var pi = typeof(Attributes).GetProperty(prop);
                var arr = pi?.GetValue(p.attributes) as Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>;
                if (arr == null || arr.Length == 0) return null;
                return arr.Length > 1 ? new[] { arr[0], arr[1] } : new[] { arr[0] };
            }
            catch { return null; }
        }

        private static readonly HashSet<string> _logged = new HashSet<string>();

        /// <summary>Skill bars as a sighted player sees them: each skill label ("LAY") has a "Fill" image whose
        /// height (localScale.y, 0..1) is the value; "Max" is the potential. Category names are "Header" texts.
        /// Proven 2026-11-15 (Create Player: Fill scale 0.45, 0.30, 0.20...). Says "FINISHING: Layup 45, Dunk 30. ..."</summary>
        public static string ReadBars(Transform attributes)
        {
            // 1. Skills in screen order, each with its value and maybe a category title.
            var names = new List<string>(); var vals = new List<string>(); var cats = new List<string>();
            var ownHeader = new List<string>();
            foreach (Text t in attributes.GetComponentsInChildren<Text>(false))
            {
                Transform fill = t.transform.Find("Fill");
                if (fill == null || MenuReader.Clean(t.text).Length == 0) continue;
                int v = Mathf.RoundToInt(fill.localScale.y * 100f);
                Transform max = t.transform.Find("Max");
                int m = max != null && max.gameObject.activeInHierarchy ? Mathf.RoundToInt(max.localScale.y * 100f) : 0;
                names.Add(MenuReader.NameToWords(t.gameObject.name));
                vals.Add(v + (m > v ? " max " + m : ""));
                // Profile: "<Category>/Header" next to "<Category>/Group/<skill>".
                Transform grp = t.transform.parent, catT = grp != null ? grp.parent : null;
                Text ch = catT != null ? catT.Find("Header")?.GetComponent<Text>() : null;
                cats.Add(ch != null && ch.isActiveAndEnabled ? MenuReader.Clean(ch.text) : "");
                // Create Player: the title is a child of the middle skill of its group of three.
                Text own = t.transform.Find("Header")?.GetComponent<Text>();
                ownHeader.Add(own != null && own.isActiveAndEnabled ? MenuReader.Clean(own.text) : "");
            }
            for (int i = 0; i < names.Count; i++)
                if (ownHeader[i].Length > 0)
                    for (int k = i - 1; k <= i + 1; k++) if (k >= 0 && k < names.Count && cats[k].Length == 0) cats[k] = ownHeader[i];
            // 2. "FINISHING: Layup 30, Dunk 30, Inside 30. SHOOTING: ..."
            var sb = new StringBuilder();
            string cur = null;
            for (int i = 0; i < names.Count; i++)
            {
                if (cats[i] != cur) { cur = cats[i]; if (sb.Length > 0) sb.Append(". "); if (cur.Length > 0) sb.Append(cur).Append(": "); }
                else sb.Append(", ");
                sb.Append(names[i]).Append(' ').Append(vals[i]);
            }
            if (sb.Length > 0) sb.Append('.');
            return sb.ToString();
        }

        /// <summary>F3 outside a match: only the skill bars on screen (Create Player, Player Profile).</summary>
        public static void SayBarsOnScreen()
        {
            foreach (Transform tr in Object.FindObjectsOfType<Transform>())
            {
                if (tr.name != "Attributes" || !tr.gameObject.activeInHierarchy) continue;
                string line = ReadBars(tr);
                if (line.Length > 0) { Speech.Say("Skills. " + line); return; }
            }
            Speech.Say("No skills on this screen.");
        }

        /// <summary>"FINISHING: LAY 64, DNK 70 ..." for a Scouting Report "Attributes" panel.</summary>
        public static string DescribeAttributes(Transform attributes, string playerName, bool fullyScouted)
        {
            PlayerData p = FindPlayer(playerName);
            var sb = new StringBuilder();
            for (int c = 0; c < attributes.childCount; c++)
            {
                Transform cat = attributes.GetChild(c);
                if (!cat.gameObject.activeInHierarchy) continue;
                Text head = cat.Find("Header")?.GetComponent<Text>();
                Transform group = cat.Find("Group");
                if (head == null || group == null) continue;
                sb.Append(MenuReader.Clean(head.text)).Append(": ");
                var parts = new List<string>();
                for (int i = 0; i < group.childCount; i++)
                {
                    Transform a = group.GetChild(i);
                    Text lab = a.GetComponent<Text>();
                    if (lab == null || !a.gameObject.activeInHierarchy) continue;
                    string label = MenuReader.Clean(lab.text);
                    string name = MenuReader.NameToWords(a.name);
                    Image fill = a.Find("Fill")?.GetComponent<Image>();
                    string color = fill != null ? MenuReader.ColorName(fill.color) : "";
                    int[] v = Attr(p, label);
                    if (v != null && fill != null && _logged.Add(label + v[0]))
                        Mod.Log("Attr color: " + label + " = " + v[0] + (v.Length > 1 ? "/" + v[1] : "") + " color " + fill.color.ToString("F2") + " (" + color + ")");
                    if (fullyScouted && v != null) parts.Add(name + " " + v[0] + (v.Length > 1 && v[1] > v[0] ? " of " + v[1] : ""));
                    else parts.Add(name + " " + (color.Length > 0 ? color : "unknown"));
                }
                sb.Append(string.Join(", ", parts)).Append(". ");
            }
            return sb.ToString();
        }
    }
}
