using HarmonyLib;
using RimWorld;
using Verse;

namespace PeskyGizmo
{
    internal static class SelectorBridge
    {
        private static System.Reflection.MethodInfo? _selectAllMatchingMethod;

        public static void SelectAllMatchingObjectUnderMouse()
        {
            if (_selectAllMatchingMethod == null)
                _selectAllMatchingMethod = AccessTools.Method(typeof(Selector), "SelectAllMatchingObjectUnderMouseOnScreen");

            _selectAllMatchingMethod?.Invoke(Find.Selector, null);
        }
    }
}
