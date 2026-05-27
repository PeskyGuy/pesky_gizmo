using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(Widgets), "ButtonTextWorker",
        new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(Color), typeof(bool), typeof(bool), typeof(TextAnchor?) })]
    public static class Patch_Widgets_ButtonTextWorker
    {
        public static void Prefix(Rect rect, string label, bool active)
        {
            if (!ControllerState.ControllerModeEnabled) return;
            try
            {
                NavHelper.RecordButton(rect, label, active);
            }
            catch { }
        }

        public static void Postfix(Rect rect, bool active, ref Widgets.DraggableResult __result)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            if (active && NavHelper.ShouldSimulateClick(rect))
                __result = Widgets.DraggableResult.Pressed;

            if (NavHelper.IsSelectedAndActive(rect))
                NavHelper.DrawSelectionHighlight(rect);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.ButtonInvisible), new[] { typeof(Rect), typeof(bool) })]
    public static class Patch_Widgets_ButtonInvisible
    {
        public static void Prefix(Rect butRect)
        {
            if (!ControllerState.ControllerModeEnabled) return;
            try
            {
                NavHelper.RecordInvisibleButton(butRect);
            }
            catch { }
        }

        public static void Postfix(Rect butRect, ref bool __result)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            if (NavHelper.ShouldSimulateClick(butRect))
                __result = true;

            if (NavHelper.IsSelectedAndActive(butRect))
                NavHelper.DrawSelectionHighlight(butRect);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.ButtonInvisibleDraggable), new[] { typeof(Rect), typeof(bool) })]
    public static class Patch_Widgets_ButtonInvisibleDraggable
    {
        public static bool Prefix(Rect butRect, ref Widgets.DraggableResult __result)
        {
            if (!ControllerState.ControllerModeEnabled) return true;

            try
            {
                NavHelper.RecordInvisibleButton(butRect);
            }
            catch { }

            if (NavHelper.ShouldSimulateClick(butRect))
            {
                __result = Widgets.DraggableResult.Pressed;
                return false;
            }
            return true;
        }

        public static void Postfix(Rect butRect)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            if (NavHelper.IsSelectedAndActive(butRect))
                NavHelper.DrawSelectionHighlight(butRect);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.HorizontalSlider),
        new[] { typeof(Rect), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(string), typeof(string), typeof(string), typeof(float) })]
    public static class Patch_Widgets_HorizontalSlider
    {
        public static void Postfix(Rect rect, ref float __result, float min, float max, float roundTo)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            try
            {
                NavHelper.RecordSlider(rect, __result, min, max, roundTo);
            }
            catch { }

            if (NavHelper.TryGetSliderOverride(rect, out float overrideVal))
                __result = Mathf.Clamp(overrideVal, min, max);

            if (NavHelper.IsSelectedAndActive(rect))
                NavHelper.DrawSelectionHighlight(rect);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.TextField), new[] { typeof(Rect), typeof(string) })]
    public static class Patch_Widgets_TextField
    {
        public static void Prefix(Rect rect)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            if (NavHelper.ShouldFocusTextField(rect))
            {
                GUI.SetNextControlName("PC_TF");
                GUI.FocusControl("PC_TF");
            }
        }

        public static void Postfix(Rect rect)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            NavHelper.RecordTextField(rect);

            if (NavHelper.IsSelectedAndActive(rect))
                NavHelper.DrawSelectionHighlight(rect);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.TextArea), new[] { typeof(Rect), typeof(string), typeof(bool) })]
    public static class Patch_Widgets_TextArea
    {
        public static void Prefix(Rect rect)
        {
            if (!ControllerState.ControllerModeEnabled) return;

            if (NavHelper.ShouldFocusTextField(rect))
            {
                GUI.SetNextControlName("PC_TA");
                GUI.FocusControl("PC_TA");
            }
        }

        public static void Postfix(Rect rect, bool readOnly)
        {
            if (!ControllerState.ControllerModeEnabled) return;
            if (readOnly) return;

            NavHelper.RecordTextField(rect);

            if (NavHelper.IsSelectedAndActive(rect))
                NavHelper.DrawSelectionHighlight(rect);
        }
    }
}
