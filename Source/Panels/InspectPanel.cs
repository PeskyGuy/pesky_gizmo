using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    /// <summary>
    /// InspectPanel — the Character/Inspect tab for controller mode.
    ///
    /// Layout (when open, Mode.Tabs):
    ///   ┌─ Header ──────────────────────────┐
    ///   │  "Character"        [selected]     │
    ///   ├─ Identity ─────────────────────────┤
    ///   │  Name, age, health bar, mood bar   │
    ///   ├─ COMMANDS (gizmos) ────────────────┤
    ///   │  [Draft] [Equip] [Form] [Attack]   │
    ///   │  [Use]   [...]                     │
    ///   ├─ INFO (inspect tabs) ──────────────┤
    ///   │  [Char]  [Health] [Gear]  [Needs]  │
    ///   │  [Social] [...]                    │
    ///   ├─ Prompt bar ───────────────────────┤
    ///   │  [A:Activate]  [B:Close]           │
    ///   └────────────────────────────────────┘
    ///
    /// Navigation:
    ///   D-pad moves within the focused section (Commands or Info).
    ///   Moving past the top/bottom boundary switches focus to the other section.
    ///   A activates the selected item (gizmo or tab).
    ///   B closes the panel (or backs out of Content mode).
    ///
    /// Split across several partial files:
    ///   InspectPanel.cs          — core state, selection, action population
    ///   InspectPanel_Input.cs    — input handling, cursor helpers
    ///   InspectPanel_Drawing.cs  — ambient panel, identity, tab strip, gizmo grid
    ///   InspectPanel_Content.cs  — content popup, tab rendering, content input
    ///   InspectPanel_Bio.cs      — DrawCompactBioTab
    ///   Integrations/VSEIntegration.cs — Vanilla Skills Expanded passion icons
    /// </summary>
    public static partial class InspectPanel
    {
        public static readonly INavPanel Instance = new NavPanelAdapter(
            isOpen:      () => ConsoleHUD.ActivePanel == ConsolePanel.Inspect,
            hidesCursor: () => ConsoleHUD.ActivePanel == ConsolePanel.Inspect,
            handleInput: HandleInput,
            draw:        Draw
        );

        private enum Mode { Tabs, Content }
        private enum Section { Gizmos, Tabs }

        private static Mode _mode = Mode.Tabs;
        private static Section _focusedSection = Section.Gizmos;

        internal struct ActionItem
        {
            public string Label;
            public InspectTabBase? Tab;
            public Gizmo? Gizmo;
            public Texture2D? Icon;
            public ThingDef? ThingDef;
            public Color IconColor;
            public bool Disabled;
            public string DisabledReason;
        }

        internal static List<ActionItem> _gizmoActions = new List<ActionItem>();
        internal static List<ActionItem> _tabActions = new List<ActionItem>();
        internal static GridNavState _gizmoNav = new GridNavState { Cols = COLS, Wrap = false };
        internal static GridNavState _tabNav = new GridNavState { Cols = 1, Wrap = true };
        private static object? _capturedSelection;
        internal static Vector2 _contentScroll;
        internal static float _contentInnerHeight;
        internal static int _bioFocusIndex = -1;
        internal static List<Rect> _bioFocusRects = new List<Rect>();
        internal static List<Action> _bioFocusActions = new List<Action>();
        internal static List<string> _bioFocusInfos = new List<string>();
        internal static List<int> _bioFocusSections = new List<int>();
        internal static Rect _bioPopupRect;
        public static float LastNavTime;

        /// <summary>
        /// Latched true once the ambient grid is being interacted with (any d-pad navigation
        /// or section switch). Cleared by <see cref="ClearAmbientFocus"/> when the user
        /// explicitly backs out (B), or when the selection / world view changes.
        /// </summary>
        public static bool AmbientFocused;
        private static bool _prevWorldOpen;

        /// <summary>Last frame-time at which the user touched the ambient grid with a
        /// d-pad press (even one that didn't actually navigate). Used to auto-expire
        /// <see cref="AmbientFocused"/> after a short idle window so world-camera zoom
        /// becomes available again without forcing the player to press B.</summary>
        public static float LastFocusActivityTime = -100f;
        private const float AmbientFocusIdleTimeout = 3f;

        public static void ClearAmbientFocus()
        {
            AmbientFocused = false;
            LastFocusActivityTime = -100f;
        }

        private static Dictionary<int, int> _gizmoIndexMemory = new Dictionary<int, int>();

        internal const int COLS = 4;
        internal const float CELL_SIZE = 64f;
        internal const float CELL_SPACING = 6f;
        internal const float PANEL_MARGIN = 12f;
        internal const float HEADER_HEIGHT = 36f;
        internal const float SECTION_HEADER_HEIGHT = 22f;
        internal const float PROMPT_HEIGHT = 32f;
        internal const float IDENTITY_HEIGHT = 118f;

        private const float CONTENT_PANEL_W_FRAC = 0.80f;
        private const float CONTENT_PANEL_H_FRAC = 0.80f;

        internal static Rect _lastPanelRect;

        // ---- public properties --------------------------------------------------

        public static bool IsActive => ConsoleHUD.ActivePanel == ConsolePanel.Inspect;
        public static bool InContentMode => IsActive && _mode == Mode.Content;

        /// <summary>Called by ReticleController when smart-X activates a gizmo, so the panel remembers it.</summary>
        public static void SetGizmoMemory(int pawnThingID, int gizmoIndex)
        {
            _gizmoIndexMemory[pawnThingID] = gizmoIndex;
        }

        public static bool HasAmbient
        {
            get
            {
                try
                {
                    if (Current.Game == null) return false;
                    if (Find.DesignatorManager?.SelectedDesignator != null) return false;
                    bool worldOpen = RimWorld.Planet.WorldRendererUtility.WorldSelected;
                    if (SelectionAdapter.NumSelected == 0 && !worldOpen) return false;
                    // Refresh lazily so the ambient bar knows whether to show
                    if (_gizmoActions.Count == 0 && _tabActions.Count == 0)
                        RefreshActions();
                    return _gizmoActions.Count > 0 || _tabActions.Count > 0;
                }
                catch { /* State probe may fail during world/map transitions */ return false; }
            }
        }

        public static bool CanOpen()
        {
            try
            {
                if (Current.Game == null) return false;
                if (Find.DesignatorManager?.SelectedDesignator != null) return false;
                if (SelectionAdapter.NumSelected == 0) return false;
                RefreshActions();
                return _gizmoActions.Count > 0 || _tabActions.Count > 0;
            }
            catch { /* State probe may fail during world/map transitions */ return false; }
        }

        // ---- open / close ------------------------------------------------------

        public static void Open()
        {
            InspectTabBridge.EnsureCached();
            _mode = Mode.Tabs;
            _focusedSection = Section.Gizmos;
            _gizmoNav.Reset();
            _tabNav.Reset();
            _contentScroll = Vector2.zero;
            _capturedSelection = CurrentSelection();
            _lastSelectionCount = SelectionAdapter.NumSelected;
            RefreshActions();

            // Prefer gizmos as initial focus; fall back to tabs
            if (_gizmoActions.Count == 0 && _tabActions.Count > 0)
                _focusedSection = Section.Tabs;

            if (_gizmoActions.Count == 0 && _tabActions.Count == 0) return;

            // If first focused item is a tab, open it eagerly
            if (_focusedSection == Section.Tabs && _tabActions.Count > 0)
            {
                var first = _tabActions[_tabNav.Index];
                if (first.Tab != null) try { first.Tab.OnOpen(); } catch { /* Tab initialization may fail on modded content */ }
            }

            ConsoleHUD.ActivePanel = ConsolePanel.Inspect;
        }

        public static void Close()
        {
            ConsoleHUD.ActivePanel = ConsolePanel.None;
            _gizmoActions.Clear();
            _tabActions.Clear();
            _capturedSelection = null;
            _mode = Mode.Tabs;
            AmbientFocused = false;
            LastFocusActivityTime = -100f;
            LastNavTime = -100f;
        }

        // ---- selection helpers -------------------------------------------------

        private static int _lastSelectionCount;
        internal static readonly object _worldAmbientToken = new object();

        private static object? CurrentSelection()
        {
            try
            {
                int n = SelectionAdapter.NumSelected;
                if (n == 0) return null;
                if (n == 1) return SelectionAdapter.Single();
                return SelectionAdapter.IsWorldMode ? (object?)Find.WorldSelector : (object?)Find.Selector;
            }
            catch { /* Selection probe may fail during game state transitions */ return null; }
        }

        private static List<InspectTabBase> ResolveTabs()
        {
            var result = new List<InspectTabBase>();
            try
            {
                IEnumerable<InspectTabBase>? tabs = null;

                if (SelectionAdapter.IsWorldViewOpen)
                {
                    var wo = Find.WorldSelector?.SingleSelectedObject;
                    if (wo != null)
                    {
                        tabs = wo.GetInspectTabs();
                    }
                    else
                    {
                        var ws = Find.WorldSelector;
                        if (ws != null && ws.SelectedTile.Valid)
                        {
                            var layerDef = RimWorld.Planet.PlanetLayer.Selected?.Def;
                            if (layerDef != null) tabs = layerDef.Tabs;
                        }
                    }
                }
                else
                {
                    Thing? t = Find.Selector?.SingleSelectedThing;
                    if (t != null) tabs = t.GetInspectTabs();
                    else if (Find.Selector?.SelectedZone is Zone z) tabs = z.GetInspectTabs();
                }

                if (tabs == null) return result;

                foreach (var tab in tabs)
                {
                    if (tab == null) continue;
                    try
                    {
                        if (tab.Hidden) continue;
                        if (!tab.IsVisible) continue;
                    }
                    catch { /* Tab property probe may fail on modded tab types */ continue; }
                    result.Add(tab);
                }
            }
            catch { /* Tab enumeration may fail on modded tab types */ }
            return result;
        }

        // ---- action population -------------------------------------------------

        private static void RefreshActions()
        {
            int numSelected = SelectionAdapter.NumSelected;
            bool worldOpen = RimWorld.Planet.WorldRendererUtility.WorldSelected;
            if (numSelected == 0 && !worldOpen)
            {
                _gizmoActions.Clear();
                _tabActions.Clear();
                return;
            }

            string prevGizmoLabel = (_gizmoNav.Index >= 0 && _gizmoNav.Index < _gizmoActions.Count)
                ? _gizmoActions[_gizmoNav.Index].Label : "";
            string prevTabLabel = (_tabNav.Index >= 0 && _tabNav.Index < _tabActions.Count)
                ? _tabActions[_tabNav.Index].Label : "";

            // --- Gizmos (Commands) ---
            _gizmoActions.Clear();
            GizmoController.RefreshFromSelection();
            foreach (var g in GizmoController.CurrentGizmos)
            {
                _gizmoActions.Add(new ActionItem
                {
                    Label = GetGizmoLabel(g),
                    Gizmo = g,
                    Icon = (g is Command c) ? (Texture2D)c.icon : null,
                    ThingDef = (g is Command_VerbTarget cvt && cvt.ownerThing != null) ? cvt.ownerThing.def : null,
                    IconColor = (g is Command c2) ? c2.IconDrawColor : Color.white,
                    Disabled = (g is Command c3) && c3.Disabled,
                    DisabledReason = (g is Command c4) ? c4.disabledReason : ""
                });
            }

            // --- Tabs (Info) ---
            _tabActions.Clear();
            bool wantTabs = (numSelected == 1)
                            || (worldOpen && Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid);
            if (wantTabs)
            {
                var tabs = ResolveTabs();
                foreach (var tab in tabs)
                {
                    _tabActions.Add(new ActionItem
                    {
                        Label = GetTabLabel(tab),
                        Tab = tab,
                        IconColor = Color.white
                    });
                }
            }

            // Preserve indices by label
            if (!string.IsNullOrEmpty(prevGizmoLabel))
            {
                int newIdx = _gizmoActions.FindIndex(a => a.Label == prevGizmoLabel);
                if (newIdx >= 0) _gizmoNav.Index = newIdx;
            }
            if (!string.IsNullOrEmpty(prevTabLabel))
            {
                int newIdx = _tabActions.FindIndex(a => a.Label == prevTabLabel);
                if (newIdx >= 0) _tabNav.Index = newIdx;
            }

            // Clamp indices
            _gizmoNav.Count = _gizmoActions.Count;
            _gizmoNav.Clamp();
            _tabNav.Count = _tabActions.Count;
            _tabNav.Clamp();

            // If focused section is empty, switch
            SanitiseFocus();
        }

        /// <summary>Ensure _focusedSection points to a non-empty section.</summary>
        internal static void SanitiseFocus()
        {
            if (_focusedSection == Section.Gizmos && _gizmoActions.Count == 0 && _tabActions.Count > 0)
                _focusedSection = Section.Tabs;
            else if (_focusedSection == Section.Tabs && _tabActions.Count == 0 && _gizmoActions.Count > 0)
                _focusedSection = Section.Gizmos;
        }

        // ---- selected-index helpers --------------------------------------------

        private static List<ActionItem> FocusedActions =>
            _focusedSection == Section.Gizmos ? _gizmoActions : _tabActions;

        internal static ActionItem? FocusedAction
        {
            get
            {
                var list = FocusedActions;
                int idx = _focusedSection == Section.Gizmos ? _gizmoNav.Index : _tabNav.Index;
                if (idx >= 0 && idx < list.Count) return list[idx];
                return null;
            }
        }

        // ---- label helpers -----------------------------------------------------

        private static string GetGizmoLabel(Gizmo g)
        {
            if (g is Command c && !c.LabelCap.NullOrEmpty()) return c.LabelCap;
            // Command_VerbTarget sometimes has no LabelCap set - pull from verb's equipment
            if (g is Command_VerbTarget cvt && cvt.verb != null)
            {
                if (cvt.verb.EquipmentSource != null)
                    return cvt.verb.EquipmentSource.LabelCap;
                if (!cvt.verb.verbProps.label.NullOrEmpty())
                    return cvt.verb.verbProps.label;
            }
            return g.GetType().Name;
        }

        private static string GetTabLabel(InspectTabBase tab)
        {
            try
            {
                if (!string.IsNullOrEmpty(tab.labelKey))
                    return tab.labelKey.Translate();
            }
            catch { /* Tab label translation may fail on modded tab types */ }
            string name = tab.GetType().Name;
            if (name.StartsWith("ITab_Pawn_")) name = name.Substring("ITab_Pawn_".Length);
            else if (name.StartsWith("ITab_")) name = name.Substring("ITab_".Length);
            return name;
        }

        // ---- refresh on selection change ---------------------------------------

        private static void RefreshForCurrentSelection()
        {
            InspectTabBridge.EnsureCached();
            var cur = CurrentSelection();
            int curCount = SelectionAdapter.NumSelected;
            bool worldOpen = RimWorld.Planet.WorldRendererUtility.WorldSelected;

            // Map ↔ World transitions invalidate any latched focus from the previous mode.
            if (worldOpen != _prevWorldOpen)
            {
                AmbientFocused = false;
                _prevWorldOpen = worldOpen;
            }

            // Auto-expire latched focus once the player stops interacting with the grid,
            // so world-camera zoom (d-pad up/down) naturally becomes available again.
            if (AmbientFocused && Time.unscaledTime - LastFocusActivityTime > AmbientFocusIdleTimeout)
            {
                AmbientFocused = false;
            }
            if (cur == null && !worldOpen)
            {
                if (IsActive) Close();
                _gizmoActions.Clear();
                _tabActions.Clear();
                _capturedSelection = null;
                _lastSelectionCount = 0;
                AmbientFocused = false;
                return;
            }
            // Synthetic stable token for "world view, nothing selected" so the panel still
            // refreshes when transitioning into / out of that state.
            if (cur == null) cur = _worldAmbientToken;

            bool changed = !ReferenceEquals(cur, _capturedSelection) || curCount != _lastSelectionCount;
            if (changed)
            {
                // Save gizmo index for old pawn
                if (_capturedSelection is Pawn oldPawn && _gizmoNav.Index > 0)
                    _gizmoIndexMemory[oldPawn.thingIDNumber] = _gizmoNav.Index;

                _capturedSelection = cur;
                _lastSelectionCount = curCount;
                _tabNav.Reset();
                _mode = Mode.Tabs;
                _focusedSection = Section.Gizmos;

                // Restore gizmo index for new pawn
                    if (cur is Pawn newPawn && _gizmoIndexMemory.TryGetValue(newPawn.thingIDNumber, out int savedIdx))
                        _gizmoNav.Index = savedIdx;
                    else
                        _gizmoNav.Reset();

                    RefreshActions();
                    SanitiseFocus();
                }
            else
            {
                RefreshActions();
            }
        }
    }
}
