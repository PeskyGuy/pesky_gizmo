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
                NavHelper.RecordButton(rect, label, active: true);
                _customActions[ScreenCenterHash(rect)] = onActivate;
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

        private static readonly System.Collections.Generic.Dictionary<long, Action>
            _customActions = new System.Collections.Generic.Dictionary<long, Action>();

        internal static void ProcessCustomActions()
        {
        }

        private static long ScreenCenterHash(Rect rect)
        {
            Vector2 sc;
            try { sc = GUIUtility.GUIToScreenPoint(rect.center); }
            catch { sc = rect.center; }
            return (long)(sc.x * 100) * 1000000L + (long)(sc.y * 100);
        }
    }
}
