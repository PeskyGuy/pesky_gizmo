using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class NavMenu
    {
        public static readonly INavPanel Instance = new NavPanelAdapter(
            isOpen:      () => IsOpen,
            hidesCursor: () => true,
            handleInput: () => HandleInput(),
            draw:        Draw
        );

        public static bool IsOpen { get; private set; }

        private enum View { Top, Colony, Nature, Planet }
        private static View _view = View.Top;
        private static GridNavState _nav = new GridNavState { Cols = 1, Wrap = true };

        // Pending-return state for re-opening NavMenu after a vanilla tab is closed
        private enum PendingState { None, Launching, Active }
        private static PendingState _pending = PendingState.None;
        private static View _pendingReturnView = View.Top;
        private static MainButtonDef? _pendingTabDef;
        private static bool _pendingArchitect;
        private static bool _pendingWorld;

        private struct Entry
        {
            public string label;
            public string? defName;     // null = group navigator
            public View? subView;       // non-null = open submenu
            public bool isWorld;        // requires CameraJumper.TryShowWorld
            public bool dlcOnly;        // entry only valid if def exists
        }

        private static readonly Entry[] TopLevel =
        {
            new Entry { label = "Architect",  defName = "Architect" },
            new Entry { label = "Colony",     subView = View.Colony },
            new Entry { label = "Nature",     subView = View.Nature },
            new Entry { label = "Planet",     subView = View.Planet },
            new Entry { label = "Colonists",  defName = "__colonists__" },
            new Entry { label = "Ideoligion", defName = "Ideos", dlcOnly = true },
        };

        private static readonly Entry[] ColonyEntries =
        {
            new Entry { label = "Work",     defName = "Work" },
            new Entry { label = "Schedule", defName = "Schedule" },
            new Entry { label = "Assign",   defName = "Assign" },
            new Entry { label = "Research", defName = "Research" },
        };

        private static readonly Entry[] NatureEntries =
        {
            new Entry { label = "Animals",  defName = "Animals" },
            new Entry { label = "Wildlife", defName = "Wildlife" },
        };

        private static readonly Entry[] PlanetEntries =
        {
            new Entry { label = "World",    defName = "World", isWorld = true },
            new Entry { label = "Quests",   defName = "Quests" },
            new Entry { label = "Factions", defName = "Factions" },
            new Entry { label = "History",  defName = "History" },
        };

        public static void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                _view = View.Top;
                _nav.Reset();
            }
        }

        public static void Open()
        {
            IsOpen = true;
            _view = View.Top;
            _nav.Reset();
        }

        public static void Close()
        {
            IsOpen = false;
            _view = View.Top;
            _nav.Reset();
        }

        private static List<Entry> CurrentEntries()
        {
            Entry[] src = _view switch
            {
                View.Colony => ColonyEntries,
                View.Nature => NatureEntries,
                View.Planet => PlanetEntries,
                _ => TopLevel,
            };
            var list = new List<Entry>(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].dlcOnly && !DefExists(src[i].defName)) continue;
                list.Add(src[i]);
            }
            return list;
        }

        private static string CurrentTitle => _view switch
        {
            View.Colony => "Colony",
            View.Nature => "Nature",
            View.Planet => "Planet",
            _ => "Tabs",
        };

        public static void Draw()
        {
            if (!IsOpen) return;

            var entries = CurrentEntries();
            if (entries.Count == 0) { IsOpen = false; return; }
            _nav.Count = entries.Count;
            _nav.Clamp();

            const float panelW = 260f;
            const float rowH = 40f;
            const float rowSpacing = 4f;
            const float padding = 14f;
            const float headerH = 28f;
            const float promptH = 28f;

            float panelH = padding * 2 + headerH + (entries.Count * (rowH + rowSpacing) - rowSpacing) + promptH + 12f;
            float x = 28f;
            float y = (Verse.UI.screenHeight - panelH) * 0.5f;

            Rect panel = new Rect(x, y, panelW, panelH);
            ConsoleStyle.DrawPanel(panel);

            // Header
            Rect header = new Rect(panel.x + padding, panel.y + padding, panel.width - padding * 2, headerH);
            if (_view != View.Top)
            {
                GlyphRenderer.Draw(header.x, header.y + (headerH - 22f) * 0.5f, "B", 22f);
                Rect titleRect = new Rect(header.x + 30f, header.y, header.width - 30f, headerH);
                ConsoleStyle.Label(titleRect, CurrentTitle, ConsoleStyle.Teal, GameFont.Medium, TextAnchor.MiddleLeft);
            }
            else
            {
                ConsoleStyle.Label(header, CurrentTitle, ConsoleStyle.Teal, GameFont.Medium, TextAnchor.MiddleLeft);
            }

            // Rows
            float rowY = header.yMax + 8f;
            for (int i = 0; i < entries.Count; i++)
            {
                Rect row = new Rect(panel.x + padding, rowY, panel.width - padding * 2, rowH);
                bool sel = (i == _nav.Index);

                if (sel)
                {
                    ConsoleStyle.DrawRoundedRect(row, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(row, 2);
                    GUI.color = Color.white;
                    
                    NavController.SnapCursorTo(row);
                }
                else
                {
                    ConsoleStyle.DrawRoundedRect(row, ConsoleStyle.SectionBg);
                }

                Texture2D? icon = TryGetIcon(entries[i]);
                float iconSize = 26f;
                Rect iconRect = new Rect(row.x + 8f, row.y + (rowH - iconSize) * 0.5f, iconSize, iconSize);
                if (icon != null)
                {
                    GUI.color = sel ? Color.white : new Color(1f, 1f, 1f, 0.85f);
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }

                Rect labelRect = new Rect(row.x + 8f + iconSize + 10f, row.y, row.width - iconSize - 50f, rowH);
                ConsoleStyle.Label(labelRect, entries[i].label, sel ? ConsoleStyle.TextWhite : ConsoleStyle.TextGray, GameFont.Small, TextAnchor.MiddleLeft);

                if (entries[i].subView.HasValue)
                {
                    Rect chevron = new Rect(row.xMax - 22f, row.y, 18f, rowH);
                    ConsoleStyle.Label(chevron, "\u203A", sel ? ConsoleStyle.Teal : ConsoleStyle.TextGray, GameFont.Medium, TextAnchor.MiddleCenter);
                }

                rowY += rowH + rowSpacing;
            }

            // Prompt
            Rect prompt = new Rect(panel.x + padding, panel.yMax - padding - promptH, panel.width - padding * 2, promptH);
            if (_view == View.Top)
                GlyphRenderer.DrawPromptBar(prompt, ("A", "Open"), ("LB", "Hide"));
            else
                GlyphRenderer.DrawPromptBar(prompt, ("A", "Open"), ("B", "Back"));
        }

        public static bool HandleInput()
        {
            // LB toggles open/close globally
            if (ControllerState.ButtonLBEdge)
            {
                Toggle();
                return true;
            }

            if (!IsOpen) return false;

            var entries = CurrentEntries();
            if (entries.Count == 0)
            {
                IsOpen = false;
                return true;
            }

            _nav.Count = entries.Count;
            if (_nav.HandleDpad()) return true;

            if (ControllerState.ButtonAEdge)
            {
                Activate(entries[Mathf.Clamp(_nav.Index, 0, entries.Count - 1)]);
                return true;
            }

            if (ControllerState.ButtonBEdge)
            {
                if (_view != View.Top)
                {
                    _view = View.Top;
                    _nav.Reset();
                }
                else
                {
                    Close();
                }
                return true;
            }

            // Consume D-pad left/right too while open to prevent leakage
            if (ControllerState.DpadLeftEdge || ControllerState.DpadRightEdge)
                return true;

            return false;
        }

        private static void Activate(Entry e)
        {
            if (e.subView.HasValue)
            {
                _view = e.subView.Value;
                _nav.Reset();
                return;
            }

            try
            {
                View originView = _view;

                if (e.label == "Architect")
                {
                    BeginPending(originView, architect: true);
                    Close();
                    ConsoleHUD.ActivePanel = ConsolePanel.Architect;
                    ArchitectPanel.Open();
                    return;
                }

                if (e.label == "Colonists")
                {
                    Close();
                    ColonistPanel.Toggle();
                    return;
                }

                if (e.isWorld)
                {
                    BeginPending(originView, world: true);
                    Close();
                    CameraJumper.TryShowWorld();
                    return;
                }

                if (string.IsNullOrEmpty(e.defName)) return;
                MainButtonDef? def = DefDatabase<MainButtonDef>.GetNamed(e.defName, false);
                if (def != null && def.Worker != null)
                {
                    BeginPending(originView, tabDef: def);
                    Close();
                    def.Worker.InterfaceTryActivate();
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] NavMenu activate failed for '{e.label}': {ex.GetBaseException().Message}", 778812);
            }
        }

        private static void BeginPending(View origin, MainButtonDef? tabDef = null, bool architect = false, bool world = false)
        {
            _pendingReturnView = origin;
            _pendingTabDef = tabDef;
            _pendingArchitect = architect;
            _pendingWorld = world;
            _pending = PendingState.Launching;
        }

        private static void ClearPending()
        {
            _pending = PendingState.None;
            _pendingTabDef = null;
            _pendingArchitect = false;
            _pendingWorld = false;
        }

        public static void Tick()
        {
            if (_pending == PendingState.None) return;
            if (IsOpen) { ClearPending(); return; }

            bool inTarget = false;
            try
            {
                if (_pendingArchitect)
                {
                    inTarget = ConsoleHUD.ActivePanel == ConsolePanel.Architect;
                }
                else if (_pendingWorld)
                {
                    inTarget = WorldRendererUtility.WorldSelected;
                }
                else if (_pendingTabDef != null)
                {
                    inTarget = Find.MainTabsRoot.OpenTab == _pendingTabDef;
                }
            }
            catch { }

            if (_pending == PendingState.Launching && inTarget)
            {
                _pending = PendingState.Active;
            }
            else if (_pending == PendingState.Active && !inTarget)
            {
                // The user has returned to the map -- re-open NavMenu at origin view
                IsOpen = true;
                _view = _pendingReturnView;
                _nav.Reset();
                ClearPending();
            }
        }

        private static bool DefExists(string? defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            try { return DefDatabase<MainButtonDef>.GetNamed(defName, false) != null; }
            catch { return false; }
        }

        private static Texture2D? TryGetIcon(Entry e)
        {
            try
            {
                if (e.subView.HasValue) return null;
                if (string.IsNullOrEmpty(e.defName)) return null;
                MainButtonDef? def = DefDatabase<MainButtonDef>.GetNamed(e.defName, false);
                return def?.Icon;
            }
            catch { return null; }
        }
    }
}
