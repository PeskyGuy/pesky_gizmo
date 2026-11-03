using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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

        private static float _rtPressTime = -1f;
        private static float _lastSpeedCycleTime = -1f;

        public static void HandleInput()
        {
            InputConsumed = false;
            if (!ControllerState.ControllerModeEnabled) return;

            NavController.Tick();
            if (InputConsumed) return;

            if (Current.Game == null) return;

            bool ltHeld = ControllerState.RadialTrigger > 0.15f;
            if (ltHeld && ControllerState.ButtonXEdge)
            {
                if (!CombatHUD.IsCombatMode)
                {
                    if (ActivePanel == ConsolePanel.Architect) ActivePanel = ConsolePanel.None;
                    else ArchitectPanel.Open();
                    ControllerState.ModifierConsumed_LT = true;
                    InputConsumed = true;
                }
                return;
            }

            HandleAmbientInput();
            if (InputConsumed) return;

            HandleGlobalBack();
        }

        private static void HandleGlobalBack()
        {


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
                if (InspectPanel.AmbientFocused || Time.unscaledTime - InspectPanel.LastNavTime < 3f)
                {
                    InspectPanel.ClearAmbientFocus();
                    InspectPanel.LastNavTime = -10f;
                    InputConsumed = true;
                    return;
                }
                
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

            bool ltHeld = ControllerState.RadialTrigger > 0.15f;
            if (ControllerState.ButtonYEdge && ltHeld && Current.ProgramState == ProgramState.Playing)
            {
                OverlayMenu.Toggle();
                ControllerState.ModifierConsumed_LT = true;
                InputConsumed = true;
                return;
            }

            if (OverlayMenu.IsOpen)
            {
                OverlayMenu.HandleInput();
                if (InputConsumed) return;
            }

            if (ComparisonPanel.IsOpen)
            {
                ComparisonPanel.HandleInput();
                if (InputConsumed) return;
            }

            if (NotificationPanel.IsOpen)
            {
                NotificationPanel.HandleInput();
                if (InputConsumed) return;
            }

            if (InspectPanel.HasAmbient)
            {
                InspectPanel.HandleAmbientInput();
                if (InputConsumed) return;
            }

            if (ActivePanel == ConsolePanel.None
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

            if (!InspectPanel.AmbientFocused && !NavHelper.IsActive && !OverlayMenu.IsOpen && Find.TickManager != null)
            {
                var tm = Find.TickManager;
                bool rtHeld = ControllerState.CursorTrigger > 0.15f;

                if (rtHeld)
                {
                    if (_rtPressTime < 0f) 
                    {
                        _rtPressTime = Time.unscaledTime;
                        _lastSpeedCycleTime = -1f;
                    }
                    else if (Time.unscaledTime - _rtPressTime > 0.4f)
                    {
                        if (Time.unscaledTime - _lastSpeedCycleTime > 0.3f)
                        {
                            if (tm.CurTimeSpeed == TimeSpeed.Paused) tm.TogglePaused();
                            else if ((int)tm.CurTimeSpeed < (int)TimeSpeed.Ultrafast) tm.CurTimeSpeed = (TimeSpeed)((int)tm.CurTimeSpeed + 1);
                            else tm.CurTimeSpeed = TimeSpeed.Normal;
                            
                            _lastSpeedCycleTime = Time.unscaledTime;
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        }
                    }
                }
                else
                {
                    if (_rtPressTime > 0f)
                    {
                        if (Time.unscaledTime - _rtPressTime <= 0.4f)
                        {
                            tm.TogglePaused();
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        }
                        _rtPressTime = -1f;
                    }
                }

                if (ControllerState.DpadDownEdge && Find.Selector?.SingleSelectedThing is Pawn)
                {
                    ComparisonPanel.Toggle();
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

        private static readonly System.Collections.Generic.List<System.Func<bool>> _blockingExceptions = new System.Collections.Generic.List<System.Func<bool>>();
        public static void RegisterBlockingException(System.Func<bool> exemptWhenTrue) => _blockingExceptions.Add(exemptWhenTrue);

        public static bool IsBlockingWindowOpen()
        {
            foreach (var func in _blockingExceptions)
                if (func()) return false;
            return ActivePanel != ConsolePanel.None || NavMenu.IsOpen || OverlayMenu.IsOpen;
        }

        public static void Draw()
        {
            if (!ControllerState.ControllerModeEnabled) return;

            NavController.Draw();
            NavMenu.Draw();
            OverlayMenu.Draw();
            ComparisonPanel.Draw();
            NotificationPanel.Draw();

            if (ActivePanel == ConsolePanel.None && Current.ProgramState == ProgramState.Playing)
            {
                ArchitectPanel.DrawActiveDesignatorOverlays();
                GizmoController.ProcessPending();
                InspectPanel.DrawAmbient();
            }
        }
    }
}
