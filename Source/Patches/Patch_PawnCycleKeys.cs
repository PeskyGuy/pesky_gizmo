using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(Verse.Root_Play), "Update")]
    public static class Patch_PawnCycleKeys
    {
        public static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.CurrentMap == null) return;
            if (LongEventHandler.ShouldWaitForEvent) return;
            if (Find.WindowStack != null && Find.WindowStack.WindowsForcePause &&
                Find.WindowStack.IsOpen<Dialog_Options>()) return;

            bool prevKey = Input.GetKeyDown(KeyCode.Comma);
            bool nextKey = Input.GetKeyDown(KeyCode.Period);
            if (!prevKey && !nextKey) return;

            CycleColonist(forward: nextKey);
        }

        private static void CycleColonist(bool forward)
        {
            var map = Find.CurrentMap;
            if (map == null) return;

            List<Pawn>? colonists = map.mapPawns?.FreeColonistsSpawned?.ToList();
            if (colonists == null || colonists.Count == 0) return;

            colonists.SortBy(p => p.thingIDNumber);

            Pawn? current = Find.Selector?.SingleSelectedThing as Pawn;
            int idx = current != null ? colonists.IndexOf(current) : -1;

            int next = idx < 0
                ? 0
                : (forward ? (idx + 1) % colonists.Count
                           : (idx - 1 + colonists.Count) % colonists.Count);

            Pawn? target = colonists[next];
            if (target == null) return;

            var selector = Find.Selector;
            if (selector == null) return;
            selector.ClearSelection();
            selector.Select(target, playSound: true, forceDesignatorDeselect: true);
            CameraJumper.TryJump(target);
        }
    }
}
