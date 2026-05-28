using System;
using UnityEngine;

namespace PeskyGizmo
{
    public static class GizmoApi
    {
        public static void RegisterNavTarget(Rect rect, string label, Action onActivate)
        {
            try
            {
                NavHelper.RecordCustomTarget(rect, label, onActivate);
            }
            catch (Exception ex)
            {
                Verse.Log.ErrorOnce($"[Gizmo] GizmoApi.RegisterNavTarget failed: {ex.Message}", 990011);
            }
        }

        public static bool IsFocused(Rect rect)
        {
            try   { return NavHelper.IsSelectedAndActive(rect); }
            catch { return false; }
        }

        public static bool IsControllerActive
            => ControllerState.ControllerModeEnabled;
    }
}
