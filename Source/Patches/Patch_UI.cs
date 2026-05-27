using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(UI), "get_MousePositionOnUI")]
    public static class Patch_UI_MousePositionOnUI
    {
        public static bool Prefix(ref Vector2 __result)
        {
            if (ControllerState.ControllerModeEnabled && !Patch_Input_Global.Bypassing)
            {
                // UI coordinates are already logical (divided by UIScale)
                __result = ControllerState.CursorScreenPos / Prefs.UIScale;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UI), "get_MousePositionOnUIInverted")]
    public static class Patch_UI_MousePositionOnUIInverted
    {
        public static bool Prefix(ref Vector2 __result)
        {
            if (ControllerState.ControllerModeEnabled && !Patch_Input_Global.Bypassing)
            {
                Vector2 pos = ControllerState.CursorScreenPos / Prefs.UIScale;
                pos.y = (float)UI.screenHeight - pos.y;
                __result = pos;
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(UI), "get_MousePosUIInvertedUseEventIfCan")]
    public static class Patch_UI_MousePosUIInvertedUseEventIfCan
    {
        public static bool Prefix(ref Vector2 __result)
        {
            if (ControllerState.ControllerModeEnabled && !Patch_Input_Global.Bypassing)
            {
                Vector2 pos = ControllerState.CursorScreenPos / Prefs.UIScale;
                pos.y = (float)UI.screenHeight - pos.y;
                __result = pos;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Event), "get_mousePosition")]
    public static class Patch_Event_mousePosition
    {
        public static bool Prefix(ref Vector2 __result)
        {
            if (ControllerState.ControllerModeEnabled && !Patch_Input_Global.Bypassing)
            {
                // Event.mousePosition must be in GUI-local space (after BeginGroup/matrix transforms).
                // GUIUtility.ScreenToGUIPoint inverts the current GUI.matrix for us.
                // CursorScreenPos is top-down (GUI/Event style), but ScreenToGUIPoint expects
                // bottom-up screen coords (Input.mousePosition style), so flip Y first.
                Vector2 screenPos = new Vector2(
                    ControllerState.CursorScreenPos.x,
                    Screen.height - ControllerState.CursorScreenPos.y);
                __result = GUIUtility.ScreenToGUIPoint(screenPos);
                return false;
            }
            return true;
        }
    }
}
