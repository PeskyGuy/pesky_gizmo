using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PeskyGizmo
{
    public static class GizmoController
    {
        public static List<Gizmo> CurrentGizmos = new List<Gizmo>();
        private static Dictionary<Gizmo, List<Gizmo>> _groupedGizmos = new Dictionary<Gizmo, List<Gizmo>>();
        private static object? _selToken;
        private static Gizmo? _pendingActivation;

        public static bool HasGizmos => CurrentGizmos.Count > 0;

        public static void RefreshFromSelection()
        {
            try
            {
                CurrentGizmos.Clear();
                _groupedGizmos.Clear();

                bool worldOpen = RimWorld.Planet.WorldRendererUtility.WorldSelected;
                int count = SelectionAdapter.NumSelected;
                if (count == 0 && !worldOpen) { _selToken = null; return; }

                object? token = count == 1 ? SelectionAdapter.Single()
                              : (count > 1 ? (object?)count
                                           : (object?)"worldNoSel");
                if (!ReferenceEquals(token, _selToken)) _selToken = token;

                bool isWorld = SelectionAdapter.IsWorldMode || worldOpen;

                // World-only gizmos that aren't tied to a specific selected object:
                //   * WorldGrid (e.g. "View orbit", layer travel)
                //   * Form-caravan helper (Form/Send Caravan)
                //   * Currently-selected tile gizmos
                if (isWorld)
                {
                    try
                    {
                        var grid = Find.WorldGrid;
                        if (grid != null)
                            foreach (var g in grid.GetGizmos()) MergeOrAdd(g);
                    }
                    catch { /* World grid gizmos may not be available during map transition */ }
                    try
                    {
                        if (RimWorld.Planet.WorldGizmoUtility.TryGetCaravanGizmo(out var caravanGizmo) && caravanGizmo != null)
                            MergeOrAdd(caravanGizmo);
                    }
                    catch { /* Caravan gizmo probe — may fail when no caravan is selected */ }
                    try
                    {
                        var tile = Find.WorldSelector?.SelectedTile ?? RimWorld.Planet.PlanetTile.Invalid;
                        if (tile.Valid)
                            foreach (var g in tile.Tile.GetGizmos()) MergeOrAdd(g);
                    }
                    catch { /* Tile gizmo lookup — tile may be invalid during selection changes */ }
                }

                foreach (object obj in SelectionAdapter.All())
                {
                    // Anything ISelectable (Thing, Zone, WorldObject) exposes GetGizmos.
                    if (obj is ISelectable s)
                    {
                        var gs = s.GetGizmos();
                        if (gs != null) foreach (var g in gs) MergeOrAdd(g);
                    }
                    else if (obj is RimWorld.Planet.WorldObject wo)
                    {
                        var gs = wo.GetGizmos();
                        if (gs != null) foreach (var g in gs) MergeOrAdd(g);
                    }

                    // Reverse designators (Hunt, Slaughter, Tame, Cut, Deconstruct, Mine, etc.)
                    // Only meaningful for map-mode Things.
                    if (!isWorld && obj is Thing t && Find.ReverseDesignatorDatabase != null)
                    {
                        var allDesignators = Find.ReverseDesignatorDatabase.AllDesignators;
                        if (allDesignators != null)
                        {
                            foreach (var des in allDesignators)
                            {
                                var gizmo = des.CreateReverseDesignationGizmo(t);
                                if (gizmo != null) MergeOrAdd(gizmo);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] GizmoController refresh failed: {ex.Message}", 992110);
            }
        }

        private static void MergeOrAdd(Gizmo g)
        {
            if (g == null || !g.Visible) return;
            foreach (var existing in CurrentGizmos)
            {
                if (existing.GroupsWith(g))
                {
                    existing.MergeWith(g);
                    if (!_groupedGizmos.ContainsKey(existing))
                        _groupedGizmos[existing] = new List<Gizmo> { existing };
                    _groupedGizmos[existing].Add(g);
                    return;
                }
            }
            CurrentGizmos.Add(g);
            _groupedGizmos[g] = new List<Gizmo> { g };
        }

        public static void ProcessPending()
        {
            if (_pendingActivation == null) return;
            var g = _pendingActivation;
            _pendingActivation = null;
            Activate(g);
        }

        public static void RequestActivation(Gizmo g)
        {
            _pendingActivation = g;
        }

        public static void Activate(Gizmo g)
        {
            if (g == null) return;

            // Auto-draft: combat gizmo on undrafted pawn
            if (g is Command_VerbTarget)
            {
                var sel = Find.Selector;
                if (sel != null && sel.NumSelected > 0)
                {
                    foreach (var obj in sel.SelectedObjectsListForReading)
                    {
                        if (obj is Pawn p && p.drafter != null && !p.drafter.Drafted)
                            p.drafter.Drafted = true;
                    }
                }
            }

            if (g is Command cmd && cmd.Disabled)
            {
                if (!string.IsNullOrEmpty(cmd.disabledReason))
                    Messages.Message(((string)"DisabledCommand".Translate()) + ": " + cmd.disabledReason,
                                     MessageTypeDefOf.RejectInput, historical: false);
                else
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                return;
            }

            Event ev = Event.current ?? new Event { type = EventType.MouseDown, button = 0 };
            try
            {
                g.ProcessInput(ev);

                if (_groupedGizmos.TryGetValue(g, out var grp))
                {
                    foreach (var other in grp)
                    {
                        if (other == g) continue;
                        if (other is Command oc && oc.Disabled) continue;
                        try { other.ProcessInput(ev); }
                        catch { /* Best-effort grouped gizmo activation — sibling gizmos may be stale */ }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] Gizmo activation failed: {ex.Message}", 992112);
            }
        }

        public static int GetGroupCount(Gizmo g)
        {
            if (_groupedGizmos.TryGetValue(g, out var grp)) return grp.Count;
            return 1;
        }
    }

    [HarmonyPatch(typeof(Verse.GizmoGridDrawer), "DrawGizmoGridFor")]
    public static class Patch_DrawGizmoGrid
    {
        public static bool Prefix()
        {
            try
            {
                if (!ControllerState.ControllerModeEnabled) return true;
                // Always take over in world view (vanilla draws View orbit etc. with no selection).
                if (RimWorld.Planet.WorldRendererUtility.WorldSelected) return false;
                if (SelectionAdapter.NumSelected == 0) return true;
                return false; // GizmoController/InspectPanel will draw instead
            }
            catch
            {
                // Selection state may be inconsistent during loading; let vanilla handle it
                return true;
            }
        }
    }

    // Hide the vanilla floating world gizmo overlay (e.g. "Settlement | Abandon" callouts).
    [HarmonyPatch(typeof(RimWorld.Planet.WorldGizmoUtility), "WorldUIOnGUI")]
    public static class Patch_WorldGizmoUtility_WorldUIOnGUI
    {
        public static bool Prefix()
        {
            if (!ControllerState.ControllerModeEnabled) return true;
            return false; // always suppress in controller mode; our panel draws instead
        }
    }

    // Hide the entire vanilla WorldInspectPane window (chrome + contents) so our own
    // InspectPanel can take its place. Patches the base Window.WindowOnGUI and bails out
    // before any drawing happens when the instance is the world inspect pane.
    [HarmonyPatch(typeof(Window), nameof(Window.WindowOnGUI))]
    public static class Patch_Window_WindowOnGUI_SuppressWorldInspect
    {
        public static bool Prefix(Window __instance)
        {
            if (!ControllerState.ControllerModeEnabled) return true;
            if (__instance is not RimWorld.Planet.WorldInspectPane) return true;
            // Always suppress in controller mode — our InspectPanel takes its place
            // whether the selection is an object, a tile, or nothing at all.
            return false;
        }
    }

    // WorldInspectPane.ExtraOnGUI draws the inspect tab buttons separately from the window
    // body, so suppress it too. Also kill the window shadow here — WindowStack draws the
    // shadow before WindowOnGUI, so the WindowOnGUI prefix can't catch it.
    [HarmonyPatch(typeof(RimWorld.Planet.WorldInspectPane), nameof(RimWorld.Planet.WorldInspectPane.ExtraOnGUI))]
    public static class Patch_WorldInspectPane_ExtraOnGUI
    {
        public static bool Prefix(RimWorld.Planet.WorldInspectPane __instance)
        {
            if (!ControllerState.ControllerModeEnabled) return true;
            __instance.drawShadow = false;
            return false;
        }
    }

    // WITab.StillValid checks Find.WindowStack.IsOpen<WorldInspectPane>() which is false
    // because we suppress the pane in controller mode. Override it so inline tab content
    // (Planet, Terrain, etc.) can still render inside our InspectPanel.
    [HarmonyPatch(typeof(RimWorld.Planet.WITab), "StillValid", MethodType.Getter)]
    public static class Patch_WITab_StillValid
    {
        public static bool Prefix(RimWorld.Planet.WITab __instance, ref bool __result)
        {
            if (!ControllerState.ControllerModeEnabled) return true;
            if (!RimWorld.Planet.WorldRendererUtility.WorldSelected) return true;
            __result = true;
            return false;
        }
    }
}
