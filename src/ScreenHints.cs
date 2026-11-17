using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Extra words for items whose meaning sits elsewhere on screen.
    /// Each case is based on a screen dump (UserData\HoopLandAccess_screen.txt).</summary>
    internal static class ScreenHints
    {
        public static string Extra(GameObject go)
        {
            // Exhibition matchup: "Team" buttons. The team name is a "City Name" text on that side.
            // Proven 2026-11-15: Matchup Screen/Panel/VS/Road Team Button/Buttons/Team Button,
            // name in Matchup Screen/Panel/Road Team/City Name ("Boston CELTICS").
            if (go.name == "Team Button")
            {
                string side = MenuReader.HasAncestor(go.transform, "Road Team Button") ? "Road Team"
                            : MenuReader.HasAncestor(go.transform, "Home Team Button") ? "Home Team" : null;
                Transform panel = FindAncestor(go.transform, "Panel");
                if (side != null && panel != null)
                {
                    Transform city = panel.Find(side + "/City Name");
                    Text t = city != null ? city.GetComponent<Text>() : null;
                    if (t != null) return MenuReader.Clean(t.text);
                }
            }
            return "";
        }

        /// <summary>More words after a picker's value: what the picker changes elsewhere on screen.</summary>
        public static string CyclerExtra(GameObject go)
        {
            // Season Select Team: "Current Team" picker; the team card is ".../Select Team/Panel"
            // (name, division, ranks, roster, cap, starters). Proven 2026-11-15 screen dump.
            if (MenuReader.HasAncestor(go.transform, "Current Team"))
            {
                Transform st = FindAncestor(go.transform, "Select Team");
                if (st != null) return MenuReader.ReadPanel(st.Find("Panel"));
            }
            return "";
        }

        /// <summary>Full replacement text for special items, or null.</summary>
        public static string Override(GameObject go)
        {
            // Front Office staff: ".../Staff/Group/Head Coach" with Stats/Player Name, Stats/Level ("LV 72") or
            // Stats/Position. Say the job from the object name. Proven 2026-11-15 (dump_fr_office).
            if (go.transform.parent != null && go.transform.parent.name == "Group" && go.transform.parent.parent != null && go.transform.parent.parent.name == "Staff")
            {
                var w = new System.Collections.Generic.List<string> { MenuReader.NameToWords(go.name) };
                foreach (Text t in go.GetComponentsInChildren<Text>(false))
                {
                    string s = MenuReader.Clean(t.text);
                    if (s.Length == 0) continue;
                    if (s.StartsWith("LV ")) s = "level " + s.Substring(3);
                    if (!w.Contains(s)) w.Add(s);
                }
                return string.Join(", ", w);
            }
            // Trade Creator: each "Team Column" has the team name in "Cycle Team N/Input Field/Text" (or a fixed name),
            // its assets and "Add Trade Asset" in Viewport/Content, and "Cap Space". Proven 2026-11-15 (dump_fr_trade).
            if (MenuReader.HasAncestor(go.transform, "Create Trade") && MenuReader.HasAncestor(go.transform, "Content"))
            {
                Transform col = FindAncestor(go.transform, "Team Column");
                string team = "", cap = "";
                if (col != null)
                {
                    foreach (Text t in col.GetComponentsInChildren<Text>(false))
                    {
                        if (team.Length == 0 && t.gameObject.name == "Text" && t.transform.parent != null && t.transform.parent.name == "Input Field") team = MenuReader.Clean(t.text);
                        if (cap.Length == 0 && t.gameObject.name == "Cap Space") cap = MenuReader.Clean(t.text);
                    }
                }
                string own = MenuReader.Clean(string.Join(", ", System.Linq.Enumerable.Select(go.GetComponentsInChildren<Text>(false), x => MenuReader.Clean(x.text))));
                return (team.Length > 0 ? team + ": " : "") + own + (go.name == "Add Trade Asset" && cap.Length > 0 ? ". " + cap : "");
            }
            if (go.name == "Confirm Button" && MenuReader.HasAncestor(go.transform, "Create Trade"))
            {
                Transform st = FindAncestor(go.transform, "Create Trade");
                foreach (Text t in st.GetComponentsInChildren<Text>(false))
                    if (t.gameObject.name == "Value" && t.transform.parent != null && t.transform.parent.name == "Approval")
                        return "CONFIRM trade. Chance they accept: " + MenuReader.Clean(t.text);
            }
            // Front Office facilities: ".../Facilites/Group/Medical Facility/Buttons/Upgrade Button", Header + Value (grade).
            if (go.name == "Upgrade Button" && MenuReader.HasAncestor(go.transform, "Facilites"))
            {
                Transform f = go.transform.parent != null ? go.transform.parent.parent : null;
                string head = f?.Find("Header")?.GetComponent<Text>() is Text h ? MenuReader.Clean(h.text) : MenuReader.NameToWords(f != null ? f.name : "Facility");
                string grade = f?.Find("Value")?.GetComponent<Text>() is Text v ? MenuReader.Clean(v.text) : "";
                return head + " facility, grade " + grade + ". Press to see the upgrade.";
            }
            // Player Upgrades: ".../Upgrades/<Category>/Group/<Attribute>/Buttons/Upgrade Button".
            // Attribute row has a name Text and a value Text; the button's Text is the cost;
            // points left per category are in "<Category>/Group/XP/Points/Value". Proven 2026-11-15 (dump_upgrades).
            if (go.name == "Upgrade Button" && go.transform.parent != null && go.transform.parent.parent != null)
            {
                Transform attr = go.transform.parent.parent;
                Transform group = attr.parent;
                Transform cat = group != null ? group.parent : null;
                var words = new System.Collections.Generic.List<string>();
                foreach (Text t in attr.GetComponentsInChildren<Text>(false))
                    if (!t.transform.IsChildOf(go.transform)) { string s = MenuReader.Clean(t.text); if (s.Length > 0) words.Add(s); }
                Text cost = go.GetComponentInChildren<Text>(false);
                Text pts = cat != null ? cat.Find("Group/XP/Points/Value")?.GetComponent<Text>() : null;
                string name = words.Count > 0 ? words[0] : MenuReader.NameToWords(attr.name);
                string rating = words.Count > 1 ? ", rating " + words[1] : "";
                string c = cost != null && MenuReader.Clean(cost.text).Length > 0 ? ", costs " + MenuReader.Clean(cost.text) : "";
                string p = pts != null ? ". " + cat.name + " points left: " + MenuReader.Clean(pts.text) : "";
                return name + rating + c + p;
            }
            return null;
        }

        public static Transform FindAncestor(Transform t, string name)
        {
            for (; t != null; t = t.parent) if (t.name == name) return t;
            return null;
        }
    }
}

