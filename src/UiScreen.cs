using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>The menu items on the current screen, in reading order (top to bottom, left to right).
    /// Also gives the game a "straight list" Up/Down order, so the controller and keyboard both move
    /// through items one by one instead of jumping around the screen.</summary>
    internal static class UiScreen
    {
        public static readonly List<Selectable> Items = new List<Selectable>();
        private static float _nextRebuild;
        private static readonly HashSet<int> _lastIds = new HashSet<int>();

        /// <summary>Original Left/Right targets, remembered the first time we see an item.</summary>
        private static readonly Dictionary<int, (Selectable left, Selectable right)> _origLR = new();

        public static bool StraightList = true;

        /// <summary>Set true on the frame the screen changed (most items are new).</summary>
        public static bool ScreenChanged { get; private set; }

        public static void Update()
        {
            ScreenChanged = false;
            if (Time.unscaledTime < _nextRebuild) return;
            _nextRebuild = Time.unscaledTime + 0.25f;
            Rebuild();
        }

        public static void ForceRebuild() { _nextRebuild = 0; Rebuild(); }

        private static void Rebuild()
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Canvas root = sel != null ? RootCanvas(sel) : null;

            var found = new List<Selectable>();
            foreach (Selectable s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.gameObject.activeInHierarchy || !s.IsInteractable()) continue;
                if (s.GetComponent<Scrollbar>() != null) continue;          // scroll bars are noise
                if (root != null && RootCanvas(s.gameObject) != root) continue; // other screens/overlays
                found.Add(s);
            }

            Items.Clear();
            Items.AddRange(ListOrder(NumberOrder(ReadingOrder(found))));
            BuildStops();
            _itemIds.Clear();
            foreach (var it in Items) _itemIds.Add(it.GetInstanceID());

            var ids = new HashSet<int>(Items.Select(i => i.GetInstanceID()));
            int same = ids.Count(id => _lastIds.Contains(id));
            if (ids.Count > 0 && same * 2 < ids.Count) ScreenChanged = true;
            _lastIds.Clear();
            foreach (int id in ids) _lastIds.Add(id);

            if (StraightList) ApplyStraightList();
        }

        private static Canvas RootCanvas(GameObject go)
        {
            Canvas c = go.GetComponentInParent<Canvas>();
            return c != null ? c.rootCanvas : null;
        }

        /// <summary>Groups items into rows (similar height on screen), rows top to bottom, each row left to right.</summary>
        private static List<Selectable> ReadingOrder(List<Selectable> list)
        {
            var withPos = list.Select(s => (s, p: s.transform.position, h: Height(s))).OrderByDescending(x => x.p.y).ToList();
            var rows = new List<List<(Selectable s, Vector3 p, float h)>>();
            foreach (var it in withPos)
            {
                var row = rows.LastOrDefault();
                if (row != null && Mathf.Abs(row[0].p.y - it.p.y) < Mathf.Max(row[0].h, it.h) * 0.5f) row.Add(it);
                else rows.Add(new List<(Selectable, Vector3, float)> { it });
            }
            return rows.SelectMany(r => r.OrderBy(x => x.p.x).Select(x => x.s)).ToList();
        }

        /// <summary>Rows of one list (3 or more selectable siblings under one parent) are kept together,
        /// starting where the list's first row is. Two lists side by side (trade: players and draft picks)
        /// were read interleaved row by row (seen 2026-11-15).</summary>
        private static List<Selectable> ListOrder(List<Selectable> list)
        {
            var count = new Dictionary<int, int>();
            foreach (var s in list)
            {
                int p = s.transform.parent != null ? s.transform.parent.GetInstanceID() : 0;
                count[p] = count.TryGetValue(p, out int c) ? c + 1 : 1;
            }
            var result = new List<Selectable>();
            var done = new HashSet<int>();
            foreach (var s in list)
            {
                int p = s.transform.parent != null ? s.transform.parent.GetInstanceID() : 0;
                if (p == 0 || count[p] < 3) { result.Add(s); continue; }
                if (!done.Add(p)) continue;
                foreach (var t in list)
                    if (t.transform.parent != null && t.transform.parent.GetInstanceID() == p) result.Add(t);
            }
            return result;
        }

        /// <summary>Items that differ only by a number in their path ("Save 1", "Save 5"...) are put in
        /// number order inside the places they already take, so a grid of slots reads 1, 2, 3...</summary>
        private static List<Selectable> NumberOrder(List<Selectable> list)
        {
            var digits = new System.Text.RegularExpressions.Regex(@"\d+");
            var keyed = list.Select((s, i) => (s, i, path: Numbered(FullPath(s.transform)))).ToList();
            foreach (var grp in keyed.GroupBy(k => digits.Replace(k.path, "#")))
            {
                var g = grp.ToList();
                if (g.Count < 3 || !digits.IsMatch(g[0].path)) continue;
                var slots = g.Select(x => x.i).OrderBy(i => i).ToList();
                var sorted = g.OrderBy(x => NumberKey(digits, x.path)).ToList();
                for (int k = 0; k < slots.Count; k++) list[slots[k]] = sorted[k].s;
            }
            return list;
        }

        private static long NumberKey(System.Text.RegularExpressions.Regex digits, string path)
        {
            long key = 0;
            foreach (System.Text.RegularExpressions.Match m in digits.Matches(path))
                key = key * 1000 + (long.TryParse(m.Value, out long v) ? v % 1000 : 0);
            return key;
        }

        /// <summary>Path up to the last part that carries a number ("…/Save 4/Buttons/Load" -> "…/Save 4"),
        /// so "New" and "Load" buttons of different slots still sort together.</summary>
        private static string Numbered(string path)
        {
            string[] parts = path.Split('/');
            for (int i = parts.Length - 1; i >= 0; i--)
                if (System.Text.RegularExpressions.Regex.IsMatch(parts[i], @"\d")) return string.Join("/", parts, 0, i + 1);
            return path;
        }

        private static string FullPath(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static float Height(Selectable s)
        {
            RectTransform rt = s.GetComponent<RectTransform>();
            return rt != null ? Mathf.Abs(rt.rect.height * rt.lossyScale.y) : 1f;
        }

        // ---------- option groups ----------
        // A row of buttons where one is the current choice (e.g. SFX VOLUME: Off Low Mid Max,
        // or the Audio/Control/Gameplay tabs). The game shows the current one with a different
        // text color (white vs blue, proven 2026-11-15 in Settings).

        public class Group { public List<Selectable> Members; public Selectable Chosen; public Button CycleLeft, CycleRight; public bool IsCycler => CycleLeft != null || CycleRight != null; }
        private static readonly Dictionary<int, Group> _groupOf = new();

        /// <summary>Stops for Up/Down: one per option group (its chosen item), one per other item.</summary>
        public static readonly List<Selectable> Stops = new List<Selectable>();

        public static Group GroupOf(GameObject go) =>
            go != null && _groupOf.TryGetValue(go.GetInstanceID(), out var g) ? g : null;

        private static void BuildStops()
        {
            _groupOf.Clear();
            foreach (var byParent in Items.GroupBy(s => s.transform.parent != null ? s.transform.parent.GetInstanceID() : 0))
            {
                var members = byParent.OrderBy(s => s.transform.position.x).ToList();
                // Picker: "Cycle Left" [value] "Cycle Right". One stop; Left/Right press the arrows.
                Selectable cl = members.FirstOrDefault(m => m.gameObject.name.StartsWith("Cycle Left") || m.gameObject.name.StartsWith("Decrease"));
                Selectable cr = members.FirstOrDefault(m => m.gameObject.name.StartsWith("Cycle Right") || m.gameObject.name.StartsWith("Increase"));
                if (cl != null || cr != null)
                {
                    Selectable mid = members.FirstOrDefault(m => m != cl && m != cr) ?? cl ?? cr;
                    var pg = new Group { Members = members, Chosen = mid,
                        CycleLeft = cl != null ? cl.GetComponent<Button>() : null,
                        CycleRight = cr != null ? cr.GetComponent<Button>() : null };
                    foreach (var m in members) _groupOf[m.gameObject.GetInstanceID()] = pg;
                    continue;
                }
                if (members.Count < 2 || members.Any(m => m.GetComponent<Button>() == null)) continue;
                // Option buttons carry one word each ("Off", "Mid"); score cards etc. carry many.
                if (members.Any(m => m.GetComponentsInChildren<Text>(false).Length != 1)) continue;
                Selectable chosen = FindChosen(members);
                if (chosen == null) continue;
                var g = new Group { Members = members, Chosen = chosen };
                foreach (var m in members) _groupOf[m.gameObject.GetInstanceID()] = g;
            }

            Stops.Clear();
            var seen = new HashSet<Group>();
            foreach (Selectable s in Items)
            {
                Group g = GroupOf(s.gameObject);
                if (g == null) Stops.Add(s);
                else if (seen.Add(g)) Stops.Add(g.Chosen);
            }
        }

        private static Selectable FindChosen(List<Selectable> members)
        {
            var colors = new List<Color>();
            foreach (var m in members)
            {
                Text t = m.GetComponentInChildren<Text>(false);
                if (t == null) return null;
                colors.Add(t.color);
            }
            if (members.Count == 2)
            {
                if (Same(colors[0], colors[1])) return null;
                return Bright(colors[0]) > Bright(colors[1]) ? members[0] : members[1];
            }
            for (int i = 0; i < colors.Count; i++)
            {
                bool odd = true;
                Color other = colors[i == 0 ? 1 : 0];
                for (int j = 0; j < colors.Count && odd; j++)
                    if (j != i && (Same(colors[j], colors[i]) || !Same(colors[j], other))) odd = false;
                if (odd) return members[i];
            }
            return null;
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.1f;
        private static float Bright(Color c) => c.r + c.g + c.b;

        /// <summary>Stop index of an item (its group's stop if it is in a group).</summary>
        public static int StopIndexOf(GameObject go)
        {
            if (go == null) return -1;
            Group g = GroupOf(go);
            if (g != null) return Stops.FindIndex(s => GroupOf(s.gameObject) == g);
            return Stops.FindIndex(s => s != null && s.gameObject == go);
        }

        /// <summary>Up/Down = previous/next setting or item in reading order.
        /// Left/Right = next choice inside an option group, otherwise what the game had.</summary>
        private static void ApplyStraightList()
        {
            int n = Stops.Count;
            if (n < 1) return;
            foreach (Selectable s in Items)
            {
                int id = s.GetInstanceID();
                if (!_origLR.TryGetValue(id, out var lr))
                {
                    lr = (s.FindSelectableOnLeft(), s.FindSelectableOnRight());
                    _origLR[id] = lr;
                }
                int k = StopIndexOf(s.gameObject);
                if (k < 0) continue;
                Navigation nav = s.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = n > 1 ? Stops[(k - 1 + n) % n] : null;
                nav.selectOnDown = n > 1 ? Stops[(k + 1) % n] : null;
                Group g = GroupOf(s.gameObject);
                if (g != null && g.IsCycler)
                {
                    nav.selectOnLeft = null;   // the mod presses the arrows instead
                    nav.selectOnRight = null;
                }
                else if (g != null)
                {
                    int i = g.Members.IndexOf(s);
                    nav.selectOnLeft = i > 0 ? g.Members[i - 1] : null;
                    nav.selectOnRight = i < g.Members.Count - 1 ? g.Members[i + 1] : null;
                }
                else
                {
                    nav.selectOnLeft = lr.left;
                    nav.selectOnRight = lr.right;
                }
                s.navigation = nav;
            }
        }
        private static readonly HashSet<int> _itemIds = new();
        /// <summary>Is this one of the working items on screen? (by instance id: Il2Cpp wrappers are not reference-equal)</summary>
        public static bool IsItem(Selectable s) => s != null && _itemIds.Contains(s.GetInstanceID());

        public static int IndexOf(GameObject go) => Items.FindIndex(s => s != null && s.gameObject == go);

        /// <summary>Writes everything on screen to a file so Claude can study menus it cannot see.</summary>
        private static void Tree(StringBuilder sb, Transform t, int depth)
        {
            if (depth > 7) return;
            string line = new string(' ', depth * 2) + t.name + (t.gameObject.activeInHierarchy ? "" : " (off)");
            Text tx = t.GetComponent<Text>();
            if (tx != null) line += " text='" + MenuReader.Clean(tx.text) + "' color=" + tx.color.ToString("F2");
            Image im = t.GetComponent<Image>();
            line += " scale=" + t.localScale.ToString("F2") + " lpos=" + t.localPosition.ToString("F1");
            if (im != null) line += " img " + (im.sprite != null ? im.sprite.name : "-") + " w=" + im.rectTransform.rect.width.ToString("F1") + " h=" + im.rectTransform.rect.height.ToString("F1") + " fill=" + im.fillAmount.ToString("F2") + " color=" + im.color.ToString("F2") + (im.gameObject.activeInHierarchy && im.enabled ? "" : " HIDDEN");
            sb.AppendLine(line);
            for (int i = 0; i < t.childCount; i++) Tree(sb, t.GetChild(i), depth + 1);
        }

        public static void Dump(string path)
        {
            var sb = new StringBuilder();
            // Full tree of named panels that are not buttons (attributes, cards), for working out values shown as pictures.
            foreach (string want in new[] { "Attributes", "Player Data" })
                foreach (Transform t in Object.FindObjectsOfType<Transform>())
                    if (t.name == want && t.gameObject.activeInHierarchy) { sb.AppendLine("=== Tree " + Path(t) + " ==="); Tree(sb, t, 0); break; }
            try
            {
                sb.AppendLine("=== Assistance: OffensiveAssistance()=" + Il2Cpp.GameSettings.OffensiveAssistance() + " DefensiveAssistance()=" + Il2Cpp.GameSettings.DefensiveAssistance());
                foreach (Il2Cpp.GameSettings gs in Resources.FindObjectsOfTypeAll<Il2Cpp.GameSettings>())
                {
                    if (gs == null) continue;
                    sb.AppendLine("  GameSettings " + Path(gs.transform) + " active=" + gs.gameObject.activeInHierarchy
                        + " off=" + (gs.offensiveAssistance != null ? Path(gs.offensiveAssistance) + " active=" + gs.offensiveAssistance.gameObject.activeInHierarchy + " self=" + gs.offensiveAssistance.gameObject.activeSelf : "null")
                        + " def=" + (gs.defensiveAssistance != null ? Path(gs.defensiveAssistance) + " active=" + gs.defensiveAssistance.gameObject.activeInHierarchy + " self=" + gs.defensiveAssistance.gameObject.activeSelf : "null"));
                }
            }
            catch (System.Exception e) { sb.AppendLine("Assistance probe failed: " + e.Message); }
            sb.AppendLine("=== Selectables in reading order ===");
            foreach (Selectable s in Items)
            {
                sb.Append(Path(s.transform)).Append(" | ").Append(s.GetIl2CppType().Name);
                sb.Append(" | pos ").Append(s.transform.position.ToString("F0"));
                Image img = s.GetComponent<Image>();
                if (img != null) sb.Append(" | img color ").Append(img.color.ToString("F2"));
                sb.Append(" | words: ").Append(MenuReader.Describe(s.gameObject));
                sb.AppendLine();
                // Everything inside the item: images (sprite names often name a key or icon) and components.
                foreach (Transform ch in s.GetComponentsInChildren<Transform>(true))
                {
                    var comps = ch.GetComponents<Component>().Select(k => k.GetIl2CppType().Name).Where(n => n != "Transform" && n != "RectTransform");
                    Image im = ch.GetComponent<Image>();
                    string sprite = im != null && im.sprite != null ? " sprite=" + im.sprite.name + " w=" + im.rectTransform.rect.width.ToString("F1") + " fill=" + im.fillAmount.ToString("F2") + " type=" + im.type : "";
                    sb.Append("      - ").Append(ch.name).Append(ch.gameObject.activeInHierarchy ? "" : " (hidden)")
                      .Append(" [").Append(string.Join(",", comps)).Append("]").Append(sprite).AppendLine();
                }
            }
            sb.AppendLine("=== All visible texts ===");
            foreach (Text t in Object.FindObjectsOfType<Text>())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                sb.Append(Path(t.transform)).Append(" | size ").Append(t.fontSize)
                  .Append(" | color ").Append(t.color.ToString("F2"))
                  .Append(" | pos ").Append(t.transform.position.ToString("F0"))
                  .Append(" | \"").Append(MenuReader.Clean(t.text)).AppendLine("\"");
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static string Path(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; t != null && i < 8; i++, t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}








