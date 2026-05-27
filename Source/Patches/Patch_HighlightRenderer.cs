using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(Window), nameof(Window.WindowOnGUI))]
    public static class Patch_HighlightRenderer
    {
        public static void Postfix()
        {
        }
    }
}
