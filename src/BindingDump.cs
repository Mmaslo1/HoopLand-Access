using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoopLandAccess
{
    /// <summary>Writes every input action and its bindings (keyboard and gamepad) to UserData\HoopLandAccess_bindings.txt,
    /// so we know which controller buttons the game uses. Runs once a few seconds after start.</summary>
    internal static class BindingDump
    {
        private static bool _done;

        public static void Update()
        {
            if (_done || Time.unscaledTime < 6f) return;
            _done = true;
            var sb = new StringBuilder();
            foreach (InputActionAsset asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                sb.AppendLine("=== Asset: " + asset.name);
                foreach (InputActionMap map in asset.actionMaps)
                {
                    sb.AppendLine("  -- Map: " + map.name);
                    foreach (InputAction a in map.actions)
                    {
                        sb.Append("    ").Append(a.name).Append(":");
                        foreach (InputBinding b in a.bindings)
                            sb.Append(" [").Append(b.effectivePath).Append(b.isPartOfComposite ? " (" + b.name + ")" : "").Append("]");
                        sb.AppendLine();
                    }
                }
            }
            string path = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "HoopLandAccess_bindings.txt");
            File.WriteAllText(path, sb.ToString());
            Mod.Log("Bindings written to " + path);
        }
    }
}
