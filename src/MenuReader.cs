using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Speaks the selected menu item when it changes or its value changes, the screen title
    /// when a new screen opens, and handles keyboard menu control.</summary>
    internal static class MenuReader
    {
        private static int _lastId;
        private static string _lastLabel = "";
        private static string _lastTitle = "";
        private static string _lastRow = "";
        private static string _lastSay = "";
        private static float _lastSayTime;

        /// <summary>True while a match is being played and no menu (pause etc.) is open:
        /// the focused thing then sits in the in-game HUD called "Game UI".</summary>
        public static bool InLiveGame(GameObject sel)
        {
            if (sel == null) return false;
            Canvas c = sel.GetComponentInParent<Canvas>();
            return c != null && c.rootCanvas.name == "Game UI";
        }

        public static bool InLiveGame() => InLiveGame(Selected());

        private static string RootName(GameObject go) { Canvas c = go.GetComponentInParent<Canvas>(); return c != null ? c.rootCanvas.name : "?"; }

        public static bool HasAncestor(Transform t, string name)
        {
            for (; t != null; t = t.parent) if (t.name == name) return true;
            return false;
        }
        private static float _nextCheck;

        public static void Update()
        {
            if (Time.unscaledTime < _nextCheck && !UiScreen.ScreenChanged) return;
            _nextCheck = Time.unscaledTime + 0.08f;

            GameObject sel = Selected();
            if (sel == null) return;
            if (InLiveGame(sel) && !HasAncestor(sel.transform, "Plays Menu")) return;

            int id = sel.GetInstanceID();
            string label = Describe(sel, withRow: false);
            bool screenChanged = UiScreen.ScreenChanged;
            if (id == _lastId && label == _lastLabel)
            {
                if (!screenChanged) return;
                if (ScreenTitle(sel) == _lastTitle) return; // same item, same screen: nothing new
            }

            bool sameItem = id == _lastId;
            _lastId = id;
            _lastLabel = label;

            // Group name ("MODES", "Shot Meter") only when entering a new group.
            string say = label;
            string row = LastWasComplete ? _lastRow : RowLabel(sel);
            var cyc = UiScreen.GroupOf(sel);
            if (cyc != null && cyc.IsCycler && label.EndsWith("left and right to change")) row = _lastRow; // value already in label
            if (!sameItem && row != _lastRow)
            {
                _lastRow = row;
                if (row.Length > 0 && !label.StartsWith(row)) say = row + ", " + label;
                var g = UiScreen.GroupOf(sel);
                if (g != null && g.Chosen != null && g.Chosen.gameObject != sel)
                    say += ". Current: " + OwnWords(g.Chosen.gameObject);
            }
            if (screenChanged || !sameItem)
            {
                string title = ScreenTitle(sel);
                if (title.Length > 0 && title != _lastTitle && !say.StartsWith(title))
                {
                    _lastTitle = title;
                    say = title + ". " + say;
                }
            }
            if (string.IsNullOrEmpty(say)) return;
            if (say == _lastSay && Time.unscaledTime - _lastSayTime < 1.5f) return; // screen settling
            _lastSay = say; _lastSayTime = Time.unscaledTime;
            Speech.Say(say, interrupt: true);
            if (!sameItem) Mod.Log("Selected: [" + RootName(sel) + "] " + UiScreen.Path(sel.transform) + " -> " + label);
        }

        private static GameObject Selected()
        {
            EventSystem es = EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            return sel != null && sel.activeInHierarchy ? sel : null;
        }

        /// <summary>F1: repeat the item with its place in the list.</summary>
        /// <summary>Speak the selected item again on the next update (after a change).</summary>
        /// <summary>Accept the current item's text silently (after typing, "Done. Ace." was already said).</summary>
        public static void SkipNextChange()
        {
            GameObject sel = Selected();
            if (sel == null) return;
            _lastId = sel.GetInstanceID();
            _lastLabel = Describe(sel, withRow: false);
        }

        public static void ForceRespeak() { _lastLabel = ""; _lastId = 0; _lastSay = ""; }

        public static GameObject SelectedObject() => Selected();

        public static void RepeatCurrent()
        {
            GameObject sel = Selected();
            if (sel == null) { Speech.Say("Nothing selected."); return; }
            int i = UiScreen.StopIndexOf(sel);
            string pos = i >= 0 ? ". " + (i + 1) + " of " + UiScreen.Stops.Count : "";
            string cur = "";
            var g = UiScreen.GroupOf(sel);
            if (g != null && g.Chosen != null && g.Chosen.gameObject != sel) cur = ". Current: " + OwnWords(g.Chosen.gameObject);
            Speech.Say(Describe(sel) + cur + pos);
        }

        // ---------- keyboard control ----------

        public static void MoveBy(int step)
        {
            UiScreen.ForceRebuild();
            var items = UiScreen.Stops;
            if (items.Count == 0) { Speech.Say("No menu items here."); return; }
            int i = UiScreen.StopIndexOf(Selected());
            int next = i < 0 ? 0 : (i + step + items.Count) % items.Count;
            Mod.Log("MoveBy " + step + ": " + i + " -> " + next + " of " + items.Count);
            Select(items[next].gameObject);
        }

        public static void MoveTo(bool first)
        {
            UiScreen.ForceRebuild();
            var items = UiScreen.Stops;
            if (items.Count == 0) return;
            Select(items[first ? 0 : items.Count - 1].gameObject);
        }

        private static void Select(GameObject go)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(go);
        }

        /// <summary>Left/Right: same as the controller, so sliders and pickers change value.</summary>
        public static void Sideways(bool right)
        {
            GameObject sel = Selected();
            if (sel == null) return;
            if (TryCycle(sel, right)) return;
            Selectable s = sel.GetComponent<Selectable>();
            if (s == null) return;
            var data = new AxisEventData(EventSystem.current) { moveDir = right ? MoveDirection.Right : MoveDirection.Left };
            s.OnMove(data);
        }

        /// <summary>On a picker ("Cycle Left" value "Cycle Right"), presses the arrow button and keeps
        /// focus on the value so the new value is spoken. Used by keyboard and controller.</summary>
        public static void TryCycleSelected(bool right) { GameObject s = Selected(); if (s != null) TryCycle(s, right); }

        public static bool TryCycle(GameObject sel, bool right)
        {
            var g = UiScreen.GroupOf(sel);
            if (g == null || !g.IsCycler) return false;
            Button arrow = right ? g.CycleRight : g.CycleLeft;
            if (arrow == null) { Speech.Say(right ? "End of list." : "Start of list."); return true; }
            arrow.OnSubmit(new BaseEventData(EventSystem.current));
            Mod.Log("Cycle " + (right ? "right" : "left") + " on " + UiScreen.Path(sel.transform));
            if (g.Chosen != null) Select(g.Chosen.gameObject);
            _lastLabel = "";            // always speak the value, even if it reads the same
            _nextCheck = Time.unscaledTime + 0.05f; // let the game update the value first
            return true;
        }

        public static void Activate()
        {
            GameObject sel = Selected();
            if (sel == null) { Speech.Say("Nothing selected."); return; }
            var data = new BaseEventData(EventSystem.current);
            Button b = sel.GetComponent<Button>();
            if (b != null)
            {
                // Set Lineup: Enter on a player picks him, Enter on a second player swaps them (silent in the game).
                bool lineupRow = HasAncestor(sel.transform, "Lineup") && HasAncestor(sel.transform, "Players") && sel.GetComponentsInChildren<Il2Cpp.StarRating>(false).Length > 0;
                string who = lineupRow ? RowName(sel) : "";
                b.OnSubmit(data);
                if (lineupRow)
                {
                    if (_lineupPick.Length == 0) { _lineupPick = who; Speech.Say("Picked " + who + ". Choose who to swap with and press Enter.", interrupt: false); }
                    else { Speech.Say("Swapped " + _lineupPick + " and " + who + ".", interrupt: false); _lineupPick = ""; ForceRespeak(); }
                }
                else _lineupPick = "";
                return;
            }
            Toggle t = sel.GetComponent<Toggle>();
            if (t != null) { t.OnSubmit(data); return; }
            InputField f = sel.GetComponent<InputField>();
            if (f != null) { TextEntry.Begin(f); return; }
            Speech.Say("This item cannot be pressed from the keyboard yet.");
            Mod.Log("Activate: no Button/Toggle on " + UiScreen.Path(sel.transform));
        }

        /// <summary>Presses the first active button with this object name.</summary>
        private static string _lineupPick = "";

        /// <summary>Player name in a list row: the longest text with a space (first + last name).</summary>
        private static string RowName(GameObject row)
        {
            string best = "";
            foreach (Text t in row.GetComponentsInChildren<Text>(false))
            {
                string c = Clean(t.text);
                if (c.Contains(" ") && !c.Any(char.IsDigit) && c.Length > best.Length) best = c;
            }
            return best.Length > 0 ? best : "player";
        }

        public static bool PressByName(string name)
        {
            foreach (Selectable s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.gameObject.activeInHierarchy || s.gameObject.name != name) continue;
                Button b = s.GetComponent<Button>();
                if (b == null) continue;
                b.OnSubmit(new BaseEventData(EventSystem.current));
                return true;
            }
            return false;
        }

        private static readonly string[] BackWords = { "back", "cancel", "close", "done", "exit", "return" };

        /// <summary>Escape/Backspace: presses the screen's Back (or Cancel/Close) button.</summary>
        public static void Back()
        {
            _lineupPick = "";
            UiScreen.ForceRebuild();
            foreach (string w in BackWords)
            {
                foreach (Selectable s in UiScreen.Items)
                {
                    string name = s.gameObject.name.ToLowerInvariant();
                    string words = OwnWords(s.gameObject).ToLowerInvariant();
                    if (words == w || name == w || name == w + " button")
                    {
                        Button b = s.GetComponent<Button>();
                        if (b != null) { b.OnSubmit(new BaseEventData(EventSystem.current)); return; }
                    }
                }
            }
            Speech.Say("No back button on this screen.");
        }

        // ---------- describing items ----------

        /// <summary>Spoken text for one item: row label (if any), its words, its state.</summary>
        /// <summary>True when the last Describe gave a full sentence (table row, special item): no row label in front.</summary>
        public static bool LastWasComplete;

        public static string Describe(GameObject go, bool withRow = true)
        {
            string special = ScreenHints.Override(go);
            if (special != null) { LastWasComplete = true; return special; }
            string tableRow = Table.DescribeRow(go);
            if (tableRow != null) { LastWasComplete = true; return tableRow; }
            LastWasComplete = false;
            var cg = UiScreen.GroupOf(go);
            if (cg != null && cg.IsCycler && (cg.Chosen == null || cg.Chosen.gameObject.name.StartsWith("Cycle") || cg.Chosen.gameObject.name.StartsWith("Decrease") || cg.Chosen.gameObject.name.StartsWith("Increase")))
            {
                string value = RowLabel(go);
                if (value.Length == 0) value = PickerRowValue(go);
                // Value shown on a middle button ("Scoring Option/First Scoring Option/Button/Text"), not in the label.
                Transform prow = go.transform.parent != null ? go.transform.parent.parent : null;
                Text mid = prow != null ? prow.Find("Button/Text")?.GetComponent<Text>() : null;
                if (mid != null && mid.isActiveAndEnabled && Clean(mid.text).Length > 0 && !value.Contains(Clean(mid.text))) value += ", " + Clean(mid.text);
                // Rows numbered 1, 2, 3 ("Usage Rate 2") belong to scoring options 1, 2, 3 (Offensive Gameplan).
                if (prow != null && Regex.IsMatch(prow.name, @" \d$") && (HasAncestor(go.transform, "Offensive Gameplan") || HasAncestor(go.transform, "Defensive Gameplan"))) value += ", option " + prow.name.Substring(prow.name.Length - 1);
                string more = ScreenHints.CyclerExtra(go);
                return (value.Length > 0 ? value : "Picker") + ", left and right to change" + (more.Length > 0 ? ". " + more : "");
            }
            var parts = new List<string>();
            string own = OwnWords(go);
            string row = withRow ? RowLabel(go) : "";
            if (row.Length > 0 && !own.StartsWith(row)) parts.Add(row);
            if (own.Length == 0 && row.Length == 0 && go.transform.parent != null)
            {
                // e.g. Hair Style/Button: a button with no words next to the row's Header
                Transform h = go.transform.parent.Find("Header");
                Text ht = h != null ? h.GetComponent<Text>() : null;
                if (ht != null && ht.isActiveAndEnabled) own = Clean(ht.text) + ", choose from list";
            }
            // Middle "Button" of a picker row (Decrease / Button / Increase): say the row's Header too.
            if (go.name == "Button" && go.transform.parent != null && go.transform.parent.Find("Buttons/Decrease Button") != null)
            {
                Text hd = go.transform.parent.Find("Header")?.GetComponent<Text>();
                if (hd != null && hd.isActiveAndEnabled && own.Length > 0) own = Clean(hd.text) + ", " + own + ", press Enter to choose from list";
            }
            parts.Add(own.Length > 0 ? own : NameToWords(go.name));

            if (go.name.Contains("Color"))
            {
                // The swatch color sits on the button or one of its child images: take the most colorful.
                Color best = Color.white; float bestSat = -1;
                foreach (Image img in go.GetComponentsInChildren<Image>(false))
                {
                    Color.RGBToHSV(img.color, out _, out float sat, out float val);
                    float score = sat + (1 - val) * 0.5f;
                    if (score > bestSat) { bestSat = score; best = img.color; }
                }
                parts.Add(go.transform.parent != null && go.transform.parent.parent != null && go.transform.parent.parent.name.Contains("Skin") ? SkinTone(best) : ColorName(best));
            }

            string extra = ScreenHints.Extra(go);
            if (extra.Length > 0) parts.Add(extra);

            var grp = UiScreen.GroupOf(go);
            if (grp != null && !grp.IsCycler && grp.Chosen != null && grp.Chosen.gameObject == go) parts.Add("selected");

            Toggle tog = go.GetComponent<Toggle>();
            if (tog != null) parts.Add(tog.isOn ? "on" : "off");

            Slider sl = go.GetComponent<Slider>();
            if (sl != null)
            {
                string v = sl.wholeNumbers ? ((int)sl.value).ToString() : sl.value.ToString("0.##");
                parts.Add(v + " of " + (sl.wholeNumbers ? ((int)sl.maxValue).ToString() : sl.maxValue.ToString("0.##")));
            }
            return string.Join(", ", parts.Where(p => p.Length > 0));
        }

        /// <summary>Words inside the item itself.</summary>
        public static string OwnWords(GameObject go)
        {
            string ratings = Ratings.Describe(go);
            if (ratings != null) return ratings;
            var parts = new List<string>();
            InputField field = go.GetComponent<InputField>();
            // Texts that share a parent form one entry ("1 ATL 0-0" in a standings row); entries are
            // separated by commas. Repeats are kept unless they come twice in a row (seen 2026-11-15:
            // standings lost every "0-0" after the first).
            int lastParent = 0;
            foreach (Text t in go.GetComponentsInChildren<Text>(false))
            {
                if (field != null && field.placeholder != null && t.gameObject == field.placeholder.gameObject) continue;
                if (t.gameObject.name == "Value" && t.transform.parent != null && t.transform.parent.GetComponent<Text>() != null) continue; // read with its label (rating bars)
                string s = Clean(t.text);
                if (s.Length == 0) continue;
                string v = BarValue(t.transform);
                if (v.Length > 0) s += " " + v;
                int par = t.transform.parent != null ? t.transform.parent.GetInstanceID() : 0;
                bool rowPart = parts.Count > 0 && par == lastParent && t.transform.parent != null && t.transform.parent.GetInstanceID() != go.transform.GetInstanceID()
                               && t.transform.parent.GetComponent<Text>() == null && t.transform.parent.childCount <= 8;
                lastParent = par;
                if (rowPart) { parts[parts.Count - 1] += " " + s; continue; }
                if (parts.Count == 0 || parts[parts.Count - 1] != s) parts.Add(s);
            }
            foreach (string key in Icons.NamesIn(go))
                if (!parts.Contains(key)) parts.Add(key);
            foreach (var st in go.GetComponentsInChildren<Il2Cpp.StarRating>(false))
            {
                string stars = Table.Stars(st);
                if (stars != null) parts.Add(stars);
            }
            return string.Join(", ", parts);
        }

        /// <summary>Picker rows like "Hair Style": Row/Header ("HAIR STYLE"), Row/Input Field/Text ("STYLE 5"),
        /// arrows in Row/Buttons. Proven from the Player Appearance dump 2026-11-15.
        /// Without a header, falls back to the row's own name ("Player Panel").</summary>
        private static string PickerRowValue(GameObject go)
        {
            Transform row = go.transform.parent != null ? go.transform.parent.parent : null;
            if (row == null) return "";
            var parts = new List<string>();
            Transform header = row.Find("Header");
            Text ht = header != null ? header.GetComponent<Text>() : null;
            if (ht != null && ht.isActiveAndEnabled) parts.Add(Clean(ht.text));
            Transform val = row.Find("Input Field/Text");
            Text vt = val != null ? val.GetComponent<Text>() : null;
            if (vt != null && vt.isActiveAndEnabled && Clean(vt.text).Length > 0) parts.Add(Clean(vt.text));
            if (parts.Count == 0) parts.Add(NameToWords(go.transform.parent.name));
            return string.Join(", ", parts.Where(p => p.Length > 0));
        }

        private static bool _barLogged;

        /// <summary>How full a bar is compared with its background, 0..1 (-1 if unknown).</summary>
        private static float Portion(Transform bar, Transform bg)
        {
            if (bar == null) return -1;
            Image img = bar.GetComponent<Image>();
            if (img != null && img.type == Image.Type.Filled) return img.fillAmount;
            RectTransform r = bar.GetComponent<RectTransform>();
            RectTransform b = bg != null ? bg.GetComponent<RectTransform>() : null;
            if (r == null || b == null || b.rect.width <= 0) return -1;
            return Mathf.Clamp01(r.rect.width * Mathf.Abs(r.localScale.x) / (b.rect.width * Mathf.Abs(b.localScale.x)));
        }

        /// <summary>Rating bars (My Player attributes): a label Text with children "Value" (a number,
        /// often hidden), "Fill" (the bar) and "Max" (potential). Proven from the My Player dump 2026-11-15.</summary>
        private static string BarValue(Transform label)
        {
            Transform val = label.Find("Value");
            Transform fill = label.Find("Fill");
            if (val == null && fill == null) return "";
            string num = "";
            Text vt = val != null ? val.GetComponent<Text>() : null;
            if (vt != null && Clean(vt.text).Length > 0) num = Clean(vt.text);
            Image fi = fill != null ? fill.GetComponent<Image>() : null;
            Transform bg = label.Find("Background");
            float f = Portion(fill, bg);
            if (num.Length == 0 && f >= 0) num = Mathf.RoundToInt(f * 100).ToString();
            float m = Portion(label.Find("Max"), bg);
            if (m > f + 0.01f) num += ", max " + Mathf.RoundToInt(m * 100);
            if (!_barLogged && fill != null)
            {
                _barLogged = true;
                RectTransform fr = fill.GetComponent<RectTransform>(), br = bg != null ? bg.GetComponent<RectTransform>() : null;
                Mod.Log("Bar debug " + label.name + ": fillAmount=" + (fi != null ? fi.fillAmount : -1) + " type=" + (fi != null ? fi.type.ToString() : "?")
                    + " fillW=" + fr.rect.width + " scaleX=" + fr.localScale.x + " anchors=" + fr.anchorMin.x + "-" + fr.anchorMax.x
                    + " bgW=" + (br != null ? br.rect.width : -1));
            }
            return num;
        }

        /// <summary>The label of the row the item sits in, e.g. "Shot Meter" for an "On" button.
        /// Looks up to 3 parents for text that does not belong to any button.</summary>
        private static string RowLabel(GameObject go)
        {
            Transform siblingsParent = go.transform.parent;
            Transform a = siblingsParent;
            for (int level = 0; level < 3 && a != null; level++, a = a.parent)
            {
                if (a.GetComponent<Canvas>() != null) break;            // reached the whole screen
                // Only climb while everything clickable inside belongs to this item's own row.
                bool onlyMyRow = true;
                foreach (Selectable s in a.GetComponentsInChildren<Selectable>(false))
                    if (s.transform.parent != siblingsParent && UiScreen.IsItem(s)) { onlyMyRow = false; break; } // ignore display-only boxes
                if (!onlyMyRow) break;

                var labels = new List<string>();
                foreach (Text t in a.GetComponentsInChildren<Text>(false))
                {
                    Selectable owner = t.GetComponentInParent<Selectable>();
                    if (owner != null && UiScreen.IsItem(owner)) continue; // belongs to a working button
                    string s = Clean(t.text);
                    if (s.Length > 0 && !labels.Contains(s)) labels.Add(s);
                }
                if (labels.Count > 0 && labels.Count <= 3) return string.Join(", ", labels);
                if (labels.Count > 3) break;
            }
            return "";
        }
        /// <summary>Biggest text on the screen that is not a button: usually the title.</summary>
        private static string ScreenTitle(GameObject sel)
        {
            Canvas c = sel.GetComponentInParent<Canvas>();
            if (c == null) return "";
            // The game shows each screen's name in a "Scrolling Header" (MAIN MENU, EXHIBITION...).
            foreach (Text t in c.rootCanvas.GetComponentsInChildren<Text>(false))
                if ((HasAncestor(t.transform, "Scrolling Header") || (HasAncestor(t.transform, "Season Header") && t.transform.parent != null && t.transform.parent.name == "Mask")) && Clean(t.text).Length > 0) return Clean(t.text);
            Text best = null;
            foreach (Text t in c.rootCanvas.GetComponentsInChildren<Text>(false))
            {
                if (t.GetComponentInParent<Selectable>() != null) continue;
                if (Clean(t.text).Length == 0 || Clean(t.text).Length > 40) continue;
                if (best == null || t.fontSize > best.fontSize ||
                    (t.fontSize == best.fontSize && t.transform.position.y > best.transform.position.y)) best = t;
            }
            return best != null ? Clean(best.text) : "";
        }

        /// <summary>Reads every visible text on screen, top to bottom, left to right.</summary>
        public static void ReadScreen()
        {
            var items = new List<(float y, float x, string s, float h)>();
            var ratingPanels = new List<Transform>();
            foreach (Il2Cpp.DisplayAttributes da in Object.FindObjectsOfType<Il2Cpp.DisplayAttributes>())
            {
                if (da == null || !da.isActiveAndEnabled) continue;
                string r = Ratings.Describe(da.gameObject);
                if (r == null) continue;
                ratingPanels.Add(da.transform);
                items.Add((da.transform.position.y, da.transform.position.x, r, 1f));
            }
            // Skill bar panels (Create Player, Player Profile scouting report): read bar heights (Profile.ReadBars).
            foreach (Transform tr in Object.FindObjectsOfType<Transform>())
            {
                if (tr.name != "Attributes" || !tr.gameObject.activeInHierarchy) continue;
                string line = Profile.ReadBars(tr);
                if (line.Length == 0) continue;
                ratingPanels.Add(tr);
                items.Add((tr.position.y, tr.position.x, "Skills. " + line, 1f));
            }
            // Buttons holding several texts (hub panels, table rows, cards) are read whole, as the item reader
            // says them; otherwise panels side by side were mixed line by line (seen 2026-11-15, season hub).
            foreach (Selectable sel in Object.FindObjectsOfType<Selectable>())
            {
                if (sel == null || !sel.isActiveAndEnabled) continue;
                int n = 0;
                foreach (Text t in sel.GetComponentsInChildren<Text>(false)) if (t.isActiveAndEnabled && Clean(t.text).Length > 0) n++;
                if (n < 3 || ratingPanels.Exists(p => sel.transform.IsChildOf(p))) continue;
                string words = Describe(sel.gameObject, withRow: false);
                if (words.Length == 0) continue;
                ratingPanels.Add(sel.transform);
                items.Add((sel.transform.position.y, sel.transform.position.x, words, 0.3f));
            }
            AddTexts(items, Object.FindObjectsOfType<Text>(), t => ratingPanels.Exists(p => t.transform.IsChildOf(p)));
            foreach (Image img in Object.FindObjectsOfType<Image>())
            {
                string key = Icons.NameOf(img);
                if (key == null) continue;
                RectTransform rt = img.rectTransform;
                items.Add((img.transform.position.y, img.transform.position.x, key, Mathf.Abs(rt.rect.height * rt.lossyScale.y)));
            }
            if (items.Count == 0) { Speech.Say("No text on screen."); return; }
            Speech.Say(Lines(items));
        }

        /// <summary>Short all-capital words such as "GP", "FG%", "3PT", "OPPONENT": a column name.</summary>
        private static bool IsColumnName(string s)
        {
            if (s.Length == 0 || s.Length > 10) return false;
            foreach (char ch in s) if (char.IsLower(ch) || (char.IsDigit(ch) && ch != '3')) return false;
            return s.Any(char.IsLetter);
        }

        private static readonly string[] Positions = { "PG", "SG", "SF", "PF", "C", "G", "F" };

        private static bool IsLabelName(string n)
        {
            n = n.ToLowerInvariant();
            return n == "header" || n == "title" || n == "label";
        }

        /// <summary>Adds visible texts as reading items. A "Header"/"Title"/"Label" text is joined with its
        /// sibling texts ("ROSTER 10/15", "OFFENSE 5TH"), because on screen the value sits under the label
        /// and line-by-line reading tore them apart (seen 2026-11-15, Select Team).</summary>
        public static void AddTexts(List<(float y, float x, string s, float h)> items, IEnumerable<Text> texts, System.Func<Text, bool> skip = null)
        {
            var used = new HashSet<int>();
            var list = texts.Where(t => t != null && t.isActiveAndEnabled && t.color.a >= 0.05f && Clean(t.text).Length > 0 && (skip == null || !skip(t))).ToList();
            foreach (Text t in list)
            {
                if (!IsLabelName(t.gameObject.name) || t.transform.parent == null) continue;
                var vals = new List<string>();
                foreach (Text o in list)
                    if (o.GetInstanceID() != t.GetInstanceID() && o.transform.parent != null && o.transform.parent.GetInstanceID() == t.transform.parent.GetInstanceID() && !IsLabelName(o.gameObject.name))
                    { vals.Add(Clean(o.text)); used.Add(o.GetInstanceID()); }
                if (vals.Count == 0) continue;
                used.Add(t.GetInstanceID());
                RectTransform rt0 = t.rectTransform;
                items.Add((t.transform.position.y, t.transform.position.x, Clean(t.text) + " " + string.Join(" ", vals), Mathf.Abs(rt0.rect.height * rt0.lossyScale.y)));
            }
            foreach (Text t in list)
            {
                if (used.Contains(t.GetInstanceID())) continue;
                RectTransform rt = t.rectTransform;
                string words = Clean(t.text);
                // "PG/Player Name" = "J. Rahm": say the position too.
                Transform par = t.transform.parent;
                if (par != null && (t.gameObject.name == "Player Name" || t.gameObject.name == "Name") && System.Array.IndexOf(Positions, par.name) >= 0)
                    words = par.name + " " + words;
                // "Championships/Text" = "x 0" -> "Championships 0"
                else if (par != null && Regex.IsMatch(words, @"^x \d+$")) words = NameToWords(par.name) + " " + words.Substring(2);
                items.Add((t.transform.position.y, t.transform.position.x, words, Mathf.Abs(rt.rect.height * rt.lossyScale.y)));
            }
        }

        /// <summary>Joins reading items into lines (top to bottom, left to right).</summary>
        public static string Lines(List<(float y, float x, string s, float h)> items)
        {

            // Group into lines: texts whose heights overlap are on one line, read left to right.
            var lines = new List<List<(float y, float x, string s, float h)>>();
            foreach (var it in items.OrderByDescending(i => i.y))
            {
                var line = lines.LastOrDefault();
                if (line != null && Mathf.Abs(line[0].y - it.y) < Mathf.Max(line[0].h, it.h) * 0.5f) line.Add(it);
                else lines.Add(new List<(float, float, string, float)> { it });
            }
            var sb = new StringBuilder();
            var said = new HashSet<string>();
            List<(float y, float x, string s, float h)> header = null;
            foreach (var line in lines)
            {
                var sorted = line.OrderBy(i => i.x).ToList();
                // A table header line ("GP, MIN, FG%, PTS"): remember it and name the columns of the value lines under it.
                if (sorted.Count >= 4 && sorted.All(i => IsColumnName(i.s))) header = sorted;
                bool values = header != null && !ReferenceEquals(sorted, header) && sorted.Count >= 2 && sorted.Count(i => i.s.Any(char.IsDigit)) * 2 >= sorted.Count;
                float tol = 0;
                if (values)
                {
                    var gaps = new List<float>();
                    for (int k = 1; k < header.Count; k++) gaps.Add(header[k].x - header[k - 1].x);
                    gaps.Sort();
                    tol = gaps[gaps.Count / 2] * 0.45f;
                }
                var words = new List<string>();
                foreach (var it in sorted)
                {
                    if (!said.Add(it.s + "@" + line[0].y.ToString("F1") + "/" + it.x.ToString("F1"))) continue;
                    string w = it.s;
                    if (values)
                    {
                        var best = header.OrderBy(hh => Mathf.Abs(hh.x - it.x)).First();
                        if (Mathf.Abs(best.x - it.x) <= tol && !string.Equals(best.s, it.s, System.StringComparison.OrdinalIgnoreCase)) w = best.s + " " + it.s;
                    }
                    words.Add(w);
                }
                for (int k = words.Count - 1; k > 0; k--) if (words[k] == words[k - 1]) words.RemoveAt(k); // "SETTINGS, SETTINGS"
                if (words.Count > 0) sb.Append(string.Join(", ", words)).Append(". ");
            }
            return sb.ToString();
        }

        /// <summary>All texts under one panel, read in lines (for summaries such as the team on Select Team).</summary>
        public static string ReadPanel(Transform panel)
        {
            if (panel == null) return "";
            var items = new List<(float y, float x, string s, float h)>();
            AddTexts(items, panel.GetComponentsInChildren<Text>(false));
            return items.Count > 0 ? Lines(items) : "";
        }

        /// <summary>Rough everyday name for a color, e.g. "dark blue".</summary>
        public static string SkinTone(Color c)
        {
            Color.RGBToHSV(c, out _, out _, out float v);
            return v > 0.9f ? "very light skin" : v > 0.75f ? "light skin" : v > 0.6f ? "medium skin" : v > 0.45f ? "tan skin" : v > 0.3f ? "brown skin" : "dark brown skin";
        }

        public static string ColorName(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.15f) return "black";
            if (s < 0.15f) return v > 0.85f ? "white" : v > 0.5f ? "light gray" : "dark gray";
            string[] names = { "red", "orange", "yellow", "lime green", "green", "teal", "cyan", "sky blue", "blue", "purple", "magenta", "pink" };
            float[] tops = { 0.04f, 0.10f, 0.17f, 0.25f, 0.40f, 0.47f, 0.53f, 0.58f, 0.69f, 0.79f, 0.88f, 0.96f };
            string n = "red";
            for (int i = 0; i < tops.Length; i++) if (h < tops[i]) { n = names[i]; break; }
            string shade = v < 0.45f ? "dark " : (s < 0.45f ? "pale " : "");
            return shade + n;
        }

        private static readonly Regex Tags = new Regex("<[^>]+>");
        private static readonly Regex Spaces = new Regex(@"\s+");

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Tags.Replace(s, " ");
            return Spaces.Replace(s, " ").Trim();
        }

        /// <summary>"PlayGameButton" -> "Play Game" when an item has no visible words.</summary>
        public static string NameToWords(string n)
        {
            n = Regex.Replace(n ?? "", @"\(Clone\)|Button|Btn|_", " ");
            n = Regex.Replace(n, "([a-z])([A-Z])", "$1 $2");
            return Spaces.Replace(n, " ").Trim();
        }
    }
}
























