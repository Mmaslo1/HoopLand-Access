using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HoopLandAccess
{
    /// <summary>Reads a table row with its column names: "Hazan Allen, C, AGE 24, 2026 $4M, EXTENSION Ineligible".
    /// Many season screens (contracts, roster, stats, standings, draft) are tables: a header line of
    /// texts (or sort buttons) and rows of buttons, each with one text per column. Column = the header
    /// text standing above the row text (same x). Found by layout, not by names, so it works on any table.
    /// Seen 2026-11-15: Team Contracts read "Hazan Allen, C, 24, $4M, Ineligible" with no column names.</summary>
    internal static class Table
    {
        private static readonly HashSet<string> NoLabel = new HashSet<string> { "PLAYER NAME", "NAME", "PLAYER", "TEAM", "POS", "STARTERS", "BENCH", "REST", "INACTIVE", "" };

        private struct Cell { public float X, Y, W, H; public string S; }

        private static Cell CellOf(Text t)
        {
            // GetWorldCorners fills a managed array that Il2Cpp never copies back (all zeros, seen 2026-11-15),
            // so use the rect's center and size in world units.
            RectTransform r = t.rectTransform;
            Vector3 sc = r.lossyScale;
            Rect rc = r.rect;
            Vector3 center = r.TransformPoint(new Vector3(rc.center.x, rc.center.y, 0));
            // Width of the words themselves (preferredWidth), not of the box: boxes are often much wider.
            // The words sit left, centered or right in the box, following the text's alignment.
            float boxW = Mathf.Abs(rc.width * sc.x), wordW = Mathf.Min(boxW, Mathf.Abs(t.preferredWidth * sc.x));
            float x = center.x;
            string al = t.alignment.ToString();
            if (al.EndsWith("Left")) x = center.x - boxW / 2 + wordW / 2;
            else if (al.EndsWith("Right")) x = center.x + boxW / 2 - wordW / 2;
            return new Cell { X = x, Y = center.y, W = wordW, H = Mathf.Abs(rc.height * sc.y), S = MenuReader.Clean(t.text) };
        }

        /// <summary>The row's words with column names, or null when it is not a table row.</summary>
        public static string DescribeRow(GameObject go)
        {
            Transform rows = go.transform.parent;
            if (rows == null) return null;
            // A table: at least 3 sibling rows that are selectable.
            int similar = 0;
            for (int i = 0; i < rows.childCount && similar < 3; i++)
            {
                Transform ch = rows.GetChild(i);
                if (ch.gameObject.activeInHierarchy && ch.GetComponent<Selectable>() != null) similar++;
            }
            if (similar < 3) return Fail(go, "similar " + similar);

            var cells = go.GetComponentsInChildren<Text>(false).Where(t => t.isActiveAndEnabled && MenuReader.Clean(t.text).Length > 0).Select(CellOf).ToList();
            if (cells.Count < 3) return Fail(go, "cells " + cells.Count);
            // Median center and height: one stretched text must not decide where the row is.
            var ys = cells.Select(c => c.Y).OrderBy(v => v).ToList();
            var hs = cells.Select(c => c.H).OrderBy(v => v).ToList();
            float rowH = hs[hs.Count / 2];
            float rowTop = ys[ys.Count / 2] + rowH * 0.6f;

            // Header candidates: texts above the row, outside the rows container.
            Canvas cv = go.GetComponentInParent<Canvas>();
            if (cv == null) return null;
            var heads = new List<Cell>();
            foreach (Text t in cv.rootCanvas.GetComponentsInChildren<Text>(false))
            {
                if (!t.isActiveAndEnabled || InOtherRow(t.transform, rows)) continue;
                string s = MenuReader.Clean(t.text);
                if (s.Length == 0 || s.Length > 24) continue;
                if (MenuReader.HasAncestor(t.transform, "Scrolling Header") || MenuReader.HasAncestor(t.transform, "Season Header")) continue; // screen title
                Cell c = CellOf(t);
                if (c.Y <= rowTop) continue;
                heads.Add(c);
            }
            if (heads.Count < 2)
            {
                var all = cv.rootCanvas.GetComponentsInChildren<Text>(false).Where(t => t.isActiveAndEnabled && !t.transform.IsChildOf(rows) && MenuReader.Clean(t.text).Length > 0).Take(12)
                    .Select(t => MenuReader.Clean(t.text) + "@" + CellOf(t).Y.ToString("F2") + "/" + t.transform.position.y.ToString("F2"));
                return Fail(go, "heads " + heads.Count + " top " + rowTop.ToString("F2") + " root " + cv.rootCanvas.name + " sample " + string.Join(", ", all) + " cells " + string.Join(",", cells.Select(c => c.S + "@" + c.Y.ToString("F2"))));
            }

            // Group header candidates into lines, nearest line first; take the first line that lines up with 2+ columns.
            var lines = new List<List<Cell>>();
            foreach (Cell h in heads.OrderBy(h => h.Y))
            {
                var line = lines.LastOrDefault();
                if (line != null && Mathf.Abs(line[0].Y - h.Y) < Mathf.Max(line[0].H, h.H) * 0.5f) line.Add(h);
                else lines.Add(new List<Cell> { h });
            }
            List<Cell> header = null;
            foreach (var line in lines)
            {
                if (line.Select(x => x.S).Distinct().Count() < 2) continue;
                if (line[0].Y - rowTop > rowH * 12) break; // too far up: not this table's header
                int aligned = cells.Count(c => Nearest(line, c) != null);
                if (aligned >= 2) { header = line; break; }
            }
            if (header == null) return Fail(go, "no header; lines " + string.Join(" | ", lines.Take(4).Select(l => l[0].Y.ToString("F2") + ":" + string.Join(",", l.Select(c => c.S + "@" + c.X.ToString("F2") + "w" + c.W.ToString("F2"))))) + " cells " + string.Join(",", cells.Select(c => c.S + "@" + c.X.ToString("F2") + "w" + c.W.ToString("F2"))) + " top " + rowTop.ToString("F2") + " h " + rowH.ToString("F2"));

            // Star rating picture (StarRating component): add it as a cell under the RATING column.
            foreach (var st in go.GetComponentsInChildren<Il2Cpp.StarRating>(false))
            {
                string stars = Stars(st);
                if (stars == null) continue;
                Image im = st.GetComponent<Image>();
                Cell c = new Cell { S = stars, H = rowH, Y = cells[0].Y };
                Transform outline = st.transform.Find("Stars Mask/Stars Outline") ?? st.transform.parent;
                Vector3 ctr = (outline != null ? outline : st.transform).position;
                c.X = ctr.x; c.W = 0.05f;
                cells.Add(c);
            }
            var parts = new List<string>();
            foreach (Cell c in cells.OrderBy(c => c.X))
            {
                string h = Nearest(header, c);
                string up = h != null ? h.ToUpperInvariant() : "";
                if (h == null || NoLabel.Contains(up) || string.Equals(h, c.S, System.StringComparison.OrdinalIgnoreCase)) parts.Add(c.S);
                else parts.Add((h == "#" ? "number" : h) + " " + c.S);
            }
            return string.Join(", ", parts);
        }

        /// <summary>True when the text belongs to one of the rows (a selectable child of the rows container).
        /// A header line can sit inside the same container (Set Lineup), so the container alone is not enough.</summary>
        private static bool InOtherRow(Transform t, Transform rows)
        {
            for (Transform a = t; a != null; a = a.parent)
                if (a.parent != null && a.parent.GetInstanceID() == rows.GetInstanceID())
                {
                    // A section title ("Bench" with Pos, Rating, Minutes...) may carry a Selectable too;
                    // only rows that hold a star rating or several values count as rows.
                    Selectable sel = a.GetComponent<Selectable>();
                    if (sel == null) return false;
                    return a.GetComponentsInChildren<Il2Cpp.StarRating>(false).Length > 0 || a.GetComponentsInChildren<Text>(false).Count(x => MenuReader.Clean(x.text).Any(char.IsDigit)) >= 2;
                }
            return false;
        }

        /// <summary>"3.5 stars": the StarRating image is a mask whose width shows the filled part of five stars;
        /// the full width is the "Stars Outline" image inside it. Measured 2026-11-15: 12.2 of 17.5 = 3.5, 14.0 = 4.</summary>
        public static string Stars(Il2Cpp.StarRating st)
        {
            if (st == null || !st.isActiveAndEnabled) return null;
            Image im = st.GetComponent<Image>();
            if (im == null) return null;
            float full = 0;
            foreach (Image ch in st.GetComponentsInChildren<Image>(false))
                if (ch.gameObject.name == "Stars Outline") { full = ch.rectTransform.rect.width; break; }
            if (full <= 0) return null;
            float v = Mathf.Round(im.rectTransform.rect.width / full * 10f) / 2f;
            return v.ToString("0.#") + (v == 1 ? " star" : " stars");
        }

        private static readonly HashSet<string> _failLogged = new HashSet<string>();
        private static string Fail(GameObject go, string why)
        {
            string key = go.transform.parent.name + why.Split(' ')[0];
            if (_failLogged.Add(key)) Mod.Log("Table: not a table row (" + why + ") " + UiScreen.Path(go.transform));
            return null;
        }

        /// <summary>Header text whose x lies within the cell (or the cell's center within the header).</summary>
        private static string Nearest(List<Cell> header, Cell c)
        {
            Cell? best = null; float bestD = float.MaxValue;
            foreach (Cell h in header)
            {
                float d = Mathf.Abs(h.X - c.X);
                float tol = Mathf.Max(h.W, c.W) * 0.5f + 0.05f;
                if (d <= tol && d < bestD) { bestD = d; best = h; }
            }
            return best?.S;
        }
    }
}
