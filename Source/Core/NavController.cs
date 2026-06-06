using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    // INVARIANT: NavController is the single dispatcher for all nav panels.
    // No file outside this class checks panel state or draws panels.
    // Cursor visibility → AnyCursorHidingPanelOpen — nowhere else.
    // Input dispatch priority → _panels order — nowhere else.
    // Cursor snap → SnapCursorTo(Rect) — nowhere else.
    public static class NavController
    {
        private static readonly INavPanel[] _panels =
        {
            ColonistPanel.Instance,
            ArchitectPanel.Instance,
            InspectPanel.Instance,
            NavHelper.Instance,
        };

        // Legacy-compat: used by NavHelper and Patch_RootUpdate to gate certain
        // behaviours when any custom Commandable/Gizmo UI is active.
        public static bool IsCustomUIActive =>
            ConsoleHUD.ActivePanel != ConsolePanel.None
            || NavMenu.IsOpen
            || OverlayMenu.IsOpen
            || InspectPanel.AmbientFocused;

        // INVARIANT: AnyCursorHidingPanelOpen is the single source of truth for
        // cursor visibility. ReticleDrawer reads ONLY this — never checks panel
        // state directly.
        public static bool AnyCursorHidingPanelOpen
        {
            get
            {
                foreach (var p in _panels)
                    if (p.IsOpen && p.HidesCursor) return true;
                return false;
            }
        }

        public static void Tick()
        {
            foreach (var p in _panels)
            {
                if (!p.IsOpen) continue;
                p.HandleInput();
                if (ConsoleHUD.InputConsumed) return;
            }
        }

        public static void Draw()
        {
            foreach (var p in _panels)
            {
                if (p.IsOpen) p.Draw();
            }
        }

        public static void CloseAll()
        {
            if (NavMenu.IsOpen)      NavMenu.Close();
            if (ColonistPanel.IsOpen) ColonistPanel.Close();
            if (ConsoleHUD.ActivePanel != ConsolePanel.None)
                ConsoleHUD.ActivePanel = ConsolePanel.None;
        }

        // INVARIANT: CursorScreenPos is only written here in controller mode.
        // No panel writes to CursorScreenPos directly. NavHelper calls SnapCursorToScreen.
        public static void SnapCursorTo(Rect localGuiRect)
        {
            if (Event.current?.type != EventType.Repaint) return;
            if (!ControllerState.UsingController) return;

            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(localGuiRect.center); }
            catch
            {
                // GUIUtility matrix not set during Layout events; safe to ignore
                return;
            }

            Vector2 unityPos = new Vector2(screenCenter.x, Screen.height - screenCenter.y);
            if (Vector2.Distance(ControllerState.CursorScreenPos, unityPos) < 0.5f) return;

            ControllerState.CursorScreenPos = unityPos;

            if (!AnyCursorHidingPanelOpen)
                MouseSimulation.SyncWindowsCursor();
        }
    }
}
