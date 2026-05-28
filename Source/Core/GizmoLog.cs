using Verse;

namespace PeskyGizmo
{
    internal static class GizmoLog
    {
        public static void WarningOnce(string message, int key)
        {
            Log.WarningOnce("[Gizmo] " + message, key);
        }

        public static void ErrorOnce(string message, int key)
        {
            Log.ErrorOnce("[Gizmo] " + message, key);
        }

        public static void Message(string message)
        {
            Log.Message("[Gizmo] " + message);
        }
    }
}
