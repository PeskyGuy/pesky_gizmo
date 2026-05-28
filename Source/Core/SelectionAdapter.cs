using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace PeskyGizmo
{
    /// <summary>
    /// Unifies access to the current selection, regardless of whether it lives in the
    /// map Selector or the world Selector (planet view).
    /// </summary>
    public static class SelectionAdapter
    {
        // True when the planet/world view is currently shown. While the world is open we
        // must completely ignore the map Selector — otherwise a leftover map-pawn selection
        // would bleed into our world UI.
        public static bool IsWorldViewOpen
        {
            get
            {
                try { return WorldRendererUtility.WorldSelected; }
                catch { /* World state unavailable during loading; safe to assume not world view */ return false; }
            }
        }

        public static bool IsWorldMode
        {
            get
            {
                try
                {
                    return IsWorldViewOpen
                        && Find.WorldSelector != null
                        && Find.WorldSelector.NumSelectedObjects > 0;
                }
                catch { /* World state unavailable during loading; safe to assume not world mode */ return false; }
            }
        }

        public static int NumSelected
        {
            get
            {
                if (IsWorldViewOpen) return Find.WorldSelector?.NumSelectedObjects ?? 0;
                return Find.Selector?.NumSelected ?? 0;
            }
        }

        public static object? Single()
        {
            if (IsWorldViewOpen)
            {
                var list = Find.WorldSelector?.SelectedObjects;
                return (list != null && list.Count == 1) ? (object)list[0] : null;
            }
            var sel = Find.Selector;
            if (sel == null || sel.NumSelected != 1) return null;
            return (object?)sel.SingleSelectedThing ?? sel.SelectedZone;
        }

        public static IEnumerable<object> All()
        {
            if (IsWorldViewOpen)
                return Find.WorldSelector?.SelectedObjects?.Cast<object>() ?? Enumerable.Empty<object>();
            return Find.Selector?.SelectedObjectsListForReading?.Cast<object>()
                   ?? Enumerable.Empty<object>();
        }
    }
}
