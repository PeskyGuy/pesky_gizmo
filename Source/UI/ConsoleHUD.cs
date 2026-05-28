using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    // INVARIANT: InputConsumed is reset to false at the start of every HandleInput() call.
    // INVARIANT: ConsoleHUD delegates panel dispatch entirely to NavController.
    //             It never checks panel open/close state directly — that's NavController's job.
    public enum ConsolePanel { None, Architect, Inspect }

    public static class ConsoleHUD
    {
        // Owner: ConsoleHUD — the only file that writes ActivePanel.
        public static ConsolePanel ActivePanel = ConsolePanel.None;
        public static bool InputConsumed { get; private set; }
        public static void ConsumeInput() => InputConsumed = true;

        public static void HandleInput()
        {
            InputConsumed = false;
            if (!ControllerState.ControllerModeEnabled) return;
            if (Current.Game == null) return;

            bool ltHeld = ControllerState.IsButtonDown(GizmoMod.Settings.btnLT);
            if (ltHeld && ControllerState.ButtonXEdge)
            {
                if (ActivePanel == ConsolePanel.Architect) ActivePanel = ConsolePanel.None;
                else ArchitectPanel.Open();
                InputConsumed = true;
                return;
            }

            NavController.Tick();
            if (InputConsumed) return;

            HandleAmbientInput();
            if (InputConsumed) return;

            HandleGlobalBack();
        }

        private static void HandleGlobalBack()
        {
            if (Find.WindowStack.IsOpen<CalibrationWindow>())
                return;

            if (ControllerState.ButtonBHoldEdge)
            {
                NavController.CloseAll();
                Find.Selector?.ClearSelection();
                InputConsumed = true;
                return;
            }

            if (!ControllerState.ButtonBEdge) return;

            if (TryCloseVanillaSubmenu())  { InputConsumed = true; return; }
            if (ActivePanel != ConsolePanel.None) { ActivePanel = ConsolePanel.None; InputConsumed = true; return; }
            if (Find.DesignatorManager?.SelectedDesignator != null) { Find.DesignatorManager.Deselect(); InputConsumed = true; return; }

            if (SelectionAdapter.IsWorldViewOpen)
            {
                if (InspectPanel.AmbientFocused || Time.unscaledTime - InspectPanel.LastNavTime < 3f)
                {
                    InspectPanel.ClearAmbientFocus();
                    InspectPanel.LastNavTime = -10f;
                    InputConsumed = true;
                    return;
                }

                if (Find.WorldSelector?.NumSelectedObjects > 0)
                {
                    Find.WorldSelector.ClearSelection();
                    InputConsumed = true;
                    return;
                }

                if (Find.WorldSelector?.SelectedTile.Valid ?? false)
                {
                    Find.WorldSelector.ClearSelection();
                    InputConsumed = true;
                    return;
                }
            }
            else if (Find.Selector?.NumSelected > 0)
            {
                Find.Selector.ClearSelection();
                InputConsumed = true;
            }
        }

        internal static void HandleAmbientInput()
        {
            if (Current.Game == null) return;

            if (ArchitectPanel.HandleActiveDesignatorInput())
            {
                InputConsumed = true;
                return;
            }

            if (NavMenu.HandleInput())
            {
                InputConsumed = true;
                return;
            }

            bool ltHeld = ControllerState.IsButtonDown(GizmoMod.Settings.btnLT);
            if (ControllerState.ButtonYEdge && ltHeld && !OverlayMenu.IsOpen && Current.ProgramState == ProgramState.Playing)
            {
                OverlayMenu.Open();
                InputConsumed = true;
                return;
            }

            if (OverlayMenu.IsOpen)
            {
                OverlayMenu.HandleInput();
                if (InputConsumed) return;
            }

            if (InspectPanel.HasAmbient)
            {
                InspectPanel.HandleAmbientInput();
                if (InputConsumed) return;
            }

            if (ZoomMode.Active && ActivePanel == ConsolePanel.None
                && !NavMenu.IsOpen && Find.CameraDriver != null)
            {
                float zIn = -ControllerState.CursorAxisY;
                if (Mathf.Abs(zIn) > 0.01f)
                {
                    var cam = Find.CameraDriver;
                    float zoomRate = 80f * cam.config.zoomSpeed * cam.RootSize / 35f * Time.unscaledDeltaTime;
                    float delta = zoomRate * Mathf.Abs(zIn);
                    if (zIn > 0f)
                        cam.SetRootSize(Mathf.Clamp(cam.RootSize - delta, cam.config.sizeRange.min, cam.config.sizeRange.max));
                    else
                        cam.SetRootSize(Mathf.Clamp(cam.RootSize + delta, cam.config.sizeRange.min, cam.config.sizeRange.max));
                    InputConsumed = true;
                }
            }

            if (!InspectPanel.AmbientFocused && !NavHelper.MenuNavModeActive && !OverlayMenu.IsOpen && Find.TickManager != null)
            {
                var tm = Find.TickManager;
                if (ControllerState.DpadRightEdge)
                {
                    if (tm.CurTimeSpeed == TimeSpeed.Paused)
                        tm.TogglePaused();
                    else if ((int)tm.CurTimeSpeed < (int)TimeSpeed.Ultrafast)
                        tm.CurTimeSpeed = (TimeSpeed)((int)tm.CurTimeSpeed + 1);
                    InputConsumed = true;
                    return;
                }
                if (ControllerState.DpadLeftEdge)
                {
                    if ((int)tm.CurTimeSpeed > (int)TimeSpeed.Paused)
                        tm.CurTimeSpeed = (TimeSpeed)((int)tm.CurTimeSpeed - 1);
                    InputConsumed = true;
                    return;
                }
            }
        }

        private static bool TryCloseVanillaSubmenu()
        {
            var stack = Find.WindowStack;
            if (stack != null)
            {
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    var w = stack[i];
                    if (w == null || w.IsDebug) continue;
                    if (w.layer != WindowLayer.Dialog) continue;
                    w.Close();
                    return true;
                }
            }

            if (Find.MainTabsRoot?.OpenTab != null
                && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
            {
                Find.MainTabsRoot.ToggleTab(Find.MainTabsRoot.OpenTab);
                return true;
            }

            return false;
        }

        public static bool IsBlockingWindowOpen()
        {
            if (ActivePanel == ConsolePanel.Architect && Find.DesignatorManager?.SelectedDesignator != null)
                return false;
            return ActivePanel != ConsolePanel.None || NavMenu.IsOpen || OverlayMenu.IsOpen;
        }

        public static void Draw()
        {
            if (!ControllerState.ControllerModeEnabled) return;

            NavController.Draw();
            NavMenu.Draw();
            OverlayMenu.Draw();

            if (ActivePanel == ConsolePanel.None && Current.ProgramState == ProgramState.Playing)
            {
                ArchitectPanel.DrawActiveDesignatorOverlays();
                GizmoController.ProcessPending();
                InspectPanel.DrawAmbient();
            }
        }
    }
}
