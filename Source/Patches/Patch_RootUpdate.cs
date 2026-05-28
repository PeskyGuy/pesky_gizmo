using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(Root_Play), "Update")]
    public static class Patch_RootPlayUpdate
    {
        public static void Postfix() => Patch_RootUpdate.SharedPostfix();
    }

    [HarmonyPatch(typeof(Root_Entry), "Update")]
    public static class Patch_RootEntryUpdate
    {
        public static void Postfix() => Patch_RootUpdate.SharedPostfix();
    }

    public static class Patch_RootUpdate
    {
        private static bool _wasLoading;
        private static bool _mapWasSafe;

        public static void SharedPostfix()
        {
            if (LongEventHandler.ShouldWaitForEvent)
            {
                _wasLoading = true;
                if (ControllerState.UsingController)
                {
                    UnityEngine.Cursor.visible = true;
                }
                return;
            }

            if (_wasLoading)
            {
                _wasLoading = false;
                ControllerDetector.ResetGracePeriod();
                Log.Message("[Gizmo] Loading finished, entering grace period.");
            }

            ControllerDetector.Tick();

            if (!ControllerState.ControllerConnected)
            {
                if (UnityEngine.Cursor.visible == false)
                {
                    UnityEngine.Cursor.visible = true;
                }
                return;
            }

            // Calibration window owns all controller input while open.
            if (Find.WindowStack.IsOpen<CalibrationWindow>())
                return;

            // Detect manual mouse movement or physical click to switch back to mouse mode.
            // Simulated clicks coming from the controller (ReticleController -> SendInput)
            // are excluded via IsSimulatingClick so they don't kick us out of controller mode.
            bool skipMouseCheck = Time.unscaledDeltaTime > 0.1f;
            if (ControllerState.UsingController && !skipMouseCheck)
            {
                // A real OS click is always explicit user intent: never gated by grace
                // period or sync flag. Simulated clicks from the controller are filtered
                // via IsSimulatingClick.
                bool isSimulating = ControllerState.IsSimulatingClick || Time.unscaledTime < ControllerState.SimulatedClickCooldown;
                bool mouseClicked = !isSimulating &&
                                    (Input.GetMouseButtonDown(0)
                                  || Input.GetMouseButtonDown(1)
                                  || Input.GetMouseButtonDown(2));

                // Movement check: skipped during grace period (just entered controller mode)
                // and during the frame of a SyncWindowsCursor (would look like fake movement).
                bool movementCandidate = !ControllerDetector.IsInGracePeriod
                                         && !ControllerState.IsSyncingMouse;

                if (mouseClicked || movementCandidate)
                {
                    // Bypassing = true => the get_mousePosition patch is skipped, so we get the real OS hardware mouse.
                    Patch_Input_Global.Bypassing = true;
                    Vector3 hardwarePos;
                    try
                    {
                        hardwarePos = Input.mousePosition;
                    }
                    finally
                    {
                        Patch_Input_Global.Bypassing = false;
                    }

                    float distSq = movementCandidate
                        ? (hardwarePos - ControllerState.LastMousePos).sqrMagnitude
                        : 0f;

                    // 100f sqr = 10 pixels. Tight enough to feel immediate, loose enough
                    // to ignore sub-pixel hardware jitter.
                    if (distSq > 100f || mouseClicked)
                    {
                        // User explicitly used the mouse: lock out controller-mode re-entry briefly
                        // so any noisy controller input (drift, held buttons) doesn't yank us back.
                        ControllerState.MouseModeLockoutUntil = Time.unscaledTime + 1.0f;
                        ControllerState.UsingController = false;
                        UnityEngine.Cursor.visible = true;
                        ControllerState.LastMousePos = hardwarePos;
                        Log.Message($"[Gizmo] Mouse takeover (distSq={distSq:F0}, click={mouseClicked}).");
                        return;
                    }
                }
            }

            // Always reset sync flag for the current frame processing
            ControllerState.IsSyncingMouse = false;

            InputMapper.UpdateFrame();

            // Only update LastMousePos with hardware position when in mouse mode
            // When in controller mode, LastMousePos should retain the hardware position from grace period
            if (!ControllerState.UsingController)
            {
                Patch_Input_Global.Bypassing = true;
                try
                {
                    ControllerState.LastMousePos = Input.mousePosition;
                }
                finally
                {
                    Patch_Input_Global.Bypassing = false;
                }
            }

            if (!ControllerState.ControllerModeEnabled) return;

            // In Controller Mode
            try
            {
                bool mapSafe = ControllerDetector.IsMapSafeForInput();

                if (mapSafe != _mapWasSafe)
                {
                    _mapWasSafe = mapSafe;
                    if (GizmoMod.Settings.showDebugOverlay)
                        Log.Message($"[Gizmo] mapSafe={mapSafe}.");
                }

                UnityEngine.Cursor.visible = false;
                // Bypassing = true permanently: RimWorld always sees the real OS cursor.
                // We position the OS cursor via SetCursorPos instead of patching Input.mousePosition.
                // Mouse takeover detection temporarily flips bypass to read hardware delta.
                Patch_Input_Global.Bypassing = true;

                ConsoleHUD.HandleInput();
                NavHelper.Tick();
                NavMenu.Tick();

                // Start button → toggle the in-game ESC menu (same as pressing Escape).
                if (ControllerState.ButtonStartEdge && Current.ProgramState == ProgramState.Playing)
                {
                    Find.MainTabsRoot?.ToggleTab(MainButtonDefOf.Menu);
                }

                // R3 (right-stick click) → toggle right-stick zoom mode.
                if (ControllerState.ButtonR3Edge && Current.ProgramState == ProgramState.Playing && !NavHelper.MenuNavModeActive)
                {
                    ZoomMode.Toggle();
                }

                // B button (cancel) → close topmost open window OR close ESC menu tab.
                // Only swallowed when something is actually open; otherwise it falls through
                // to ReticleController where B keeps its "deselect" behaviour.
                // Skip entirely if ConsoleHUD already consumed B (e.g. InspectPanel backing
                // out of Content mode), so we don't double-close the underlying window.
                bool bConsumedByCancel = false;
                if (ControllerState.ButtonBEdge && !ConsoleHUD.InputConsumed
                    && !Find.WindowStack.IsOpen<CalibrationWindow>())
                {
                    // 1) Highest priority: leave the planet/world view back to the colony.
                    if (Current.ProgramState == ProgramState.Playing
                        && RimWorld.Planet.WorldRendererUtility.WorldSelected)
                    {
                        CameraJumper.TryHideWorld();
                        bConsumedByCancel = true;
                    }
                    else
                    {
                        // 2) Topmost non-debug dialog/window (settings, info tabs, etc.)
                        //    but ignore the world inspect pane since that one belongs to the
                        //    world view itself.
                        Window? topWindow = null;
                        var stack = Find.WindowStack;
                        if (stack != null)
                        {
                            for (int i = stack.Count - 1; i >= 0; i--)
                            {
                                var w = stack[i];
                                if (w == null || w.IsDebug) continue;
                                if (w is RimWorld.Planet.WorldInspectPane) continue;
                                topWindow = w;
                                break;
                            }
                        }

                        if (topWindow != null)
                        {
                            topWindow.Close();
                            bConsumedByCancel = true;
                        }
                        else if (Current.ProgramState == ProgramState.Playing
                                 && Find.MainTabsRoot?.OpenTab != null
                                 && Find.MainTabsRoot.OpenTab != MainButtonDefOf.Inspect)
                        {
                            // Close any open main tab (Menu, Factions, Ideos, History, ...)
                            // Chip-launched tabs (Factions/Ideos/Xeno) sit on OpenTab, not WindowStack.
                            // Skip the default Inspect tab so B still deselects normally.
                            Find.MainTabsRoot.ToggleTab(Find.MainTabsRoot.OpenTab);
                            bConsumedByCancel = true;
                        }
                    }
                }

                bool shouldConsumeReticleInput = ConsoleHUD.InputConsumed || ConsoleHUD.IsBlockingWindowOpen() || bConsumedByCancel;
                ReticleController.Update(shouldConsumeReticleInput);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] Crash in RootUpdate: {ex}", 561234);
                UnityEngine.Cursor.visible = true;
            }
        }
    }
}
