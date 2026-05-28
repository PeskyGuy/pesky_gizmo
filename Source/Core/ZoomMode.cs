using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    /// <summary>
    /// Global "zoom mode" latch. Toggled by R3 (right-stick click). While Active:
    ///  - Right-stick Y drives camera zoom (map or world).
    ///  - The virtual cursor is locked to the screen centre so the right stick can't
    ///    also move the cursor.
    ///  - InspectPanel d-pad nav still works normally; this is purely a remap of the
    ///    right stick.
    /// R3 again exits the mode. Escape / B / mode change also exit.
    /// </summary>
    public static class ZoomMode
    {
        public static bool Active { get; private set; }
        public static float EnteredTime { get; private set; }
        private static Vector2 _savedCursorPos;

        public static void Toggle()
        {
            Active = !Active;
            EnteredTime = Time.unscaledTime;
            if (Active)
                _savedCursorPos = ControllerState.CursorScreenPos;
            else
                ControllerState.CursorScreenPos = _savedCursorPos;
            Messages.Message(
                Active
                    ? "[Gizmo] Zoom mode ON — Right stick to zoom, R3 to exit."
                    : "[Gizmo] Zoom mode OFF.",
                MessageTypeDefOf.SilentInput, historical: false);
        }

        public static void Exit()
        {
            if (!Active) return;
            Active = false;
            ControllerState.CursorScreenPos = _savedCursorPos;
        }
    }
}
