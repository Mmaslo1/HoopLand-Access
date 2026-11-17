using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace HoopLandAccess
{
    /// <summary>Player ratings. The game draws rating bars with DisplayAttributes.UpdateAttributes(PlayerData)
    /// (readable name, stable across updates). We remember which player each bar panel shows and
    /// read the numbers from PlayerData.attributes instead of the pictures.</summary>
    [HarmonyPatch(typeof(DisplayAttributes), nameof(DisplayAttributes.UpdateAttributes))]
    internal static class Ratings
    {
        private static readonly Dictionary<int, PlayerData> _shown = new();
        private static bool _logged;

        private static void Postfix(DisplayAttributes __instance, PlayerData __0)
        {
            if (__instance == null || __0 == null) return;
            _shown[__instance.gameObject.GetInstanceID()] = __0;
        }

        private static readonly (string group, (string label, System.Func<Attributes, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>> get)[] items)[] Layout =
        {
            ("Finishing", new (string, System.Func<Attributes, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>>)[] { ("Layup", a => a.LAY), ("Dunk", a => a.DNK) }),
            ("Shooting", new (string, System.Func<Attributes, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>>)[] { ("Inside", a => a.INS), ("Mid range", a => a.MID), ("Three point", a => a.TPT), ("Free throw", a => a.FTS) }),
            ("Creating", new (string, System.Func<Attributes, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>>)[] { ("Dribbling", a => a.DRB), ("Passing", a => a.PAS) }),
            ("Defense", new (string, System.Func<Attributes, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>>)[] { ("Offensive rebound", a => a.ORE), ("Defensive rebound", a => a.DRE), ("Steal", a => a.STL), ("Block", a => a.BLK) }),
            ("Physicals", new (string, System.Func<Attributes, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>>)[] { ("Speed", a => a.SPD), ("Strength", a => a.STR), ("Stamina", a => a.STM) }),
        };

        /// <summary>Spoken ratings for an item showing a DisplayAttributes panel, or null.</summary>
        public static string Describe(GameObject go)
        {
            if (go.GetComponent<DisplayAttributes>() == null) return null;
            if (!_shown.TryGetValue(go.GetInstanceID(), out PlayerData p) || p == null || p.attributes == null) return null;
            Attributes a = p.attributes;
            var sb = new StringBuilder();
            sb.Append("Ratings of ").Append(p.fn).Append(' ').Append(p.ln).Append(". ");
            foreach (var (group, items) in Layout)
            {
                sb.Append(group).Append(": ");
                for (int i = 0; i < items.Length; i++)
                {
                    var arr = items[i].get(a);
                    if (!_logged && arr != null)
                    {
                        _logged = true;
                        var vals = new List<string>();
                        for (int k = 0; k < arr.Length; k++) vals.Add(arr[k].ToString());
                        Mod.Log("Ratings debug: LAY array = [" + string.Join(",", vals) + "]");
                    }
                    sb.Append(items[i].label).Append(' ').Append(arr != null && arr.Length > 0 ? arr[0].ToString() : "?");
                    if (arr != null && arr.Length > 1 && arr[1] > arr[0]) sb.Append(" of ").Append(arr[1]);
                    sb.Append(i < items.Length - 1 ? ", " : ". ");
                }
            }
            return sb.ToString();
        }
    }
}
