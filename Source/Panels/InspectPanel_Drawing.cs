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
    /// Drawing — ambient panel, identity area, tab strip, gizmo grid, category title.
    /// </summary>
    public static partial class InspectPanel
    {
        // ---- draw --------------------------------------------------------------

        public static void Draw()
        {
            RefreshForCurrentSelection();
            if (_gizmoActions.Count == 0 && _tabActions.Count == 0) return;

            if (_mode == Mode.Tabs) DrawSplitGrid();
            else DrawContent();
        }

        public static void DrawAmbient()
        {
            RefreshForCurrentSelection();
            if (_gizmoActions.Count == 0 && _tabActions.Count == 0) return;
            DrawSplitGrid(isAmbient: true);
        }

        // ── Split-grid drawing ─────────────────────────────────────────────────

        private static void DrawSplitGrid(bool isAmbient = false)
        {
            int gizmoCount = _gizmoActions.Count;
            int tabCount = _tabActions.Count;
            int gizmoRows = gizmoCount > 0 ? Mathf.CeilToInt((float)gizmoCount / COLS) : 0;

            float gridW = COLS * CELL_SIZE + (COLS - 1) * CELL_SPACING;
            float gizmoGridH = gizmoRows * CELL_SIZE + (gizmoRows - 1) * CELL_SPACING;

            // World tiles have more lines in the identity area (biome, lat/long,
            // hilliness, road, movement difficulty, temperature) so grow the header.
            float identityHeight = IDENTITY_HEIGHT;
            if (SelectionAdapter.IsWorldViewOpen)
            {
                var tile = Find.WorldSelector?.SelectedTile ?? PlanetTile.Invalid;
                if (tile.Valid) identityHeight = 132f;
            }

            // Compact vertical tab strip on the left when tabs exist.
            float tabStripW = tabCount > 0 ? 82f : 0f;
            float tabCellH = 28f;
            float tabSpacing = 4f;
            float tabStripH = tabCount > 0 ? tabCount * tabCellH + (tabCount - 1) * tabSpacing : 0f;

            float gizmoSectionH = gizmoCount > 0 ? SECTION_HEADER_HEIGHT + 4f + gizmoGridH : 0f;
            float sharedH = Mathf.Max(tabStripH, gizmoSectionH);
            float sectionsH = sharedH + (gizmoCount > 0 || tabCount > 0 ? 4f : 0f);

            float panelW = (tabCount > 0 ? tabStripW + 8f : 0f) + gridW + PANEL_MARGIN * 2;
            float panelH = HEADER_HEIGHT + 8f + identityHeight + 8f
                         + sectionsH
                         + PROMPT_HEIGHT + PANEL_MARGIN * 2;
            float panelX = 0f;
            float panelY = Verse.UI.screenHeight - panelH;

            _lastPanelRect = new Rect(panelX, panelY, panelW, panelH);
            ConsoleStyle.DrawPanel(_lastPanelRect);

            // Header
            string selLabel = FocusedAction?.Label ?? "";
            string title = GetCategoryTitle();

            Rect header = new Rect(panelX + PANEL_MARGIN, panelY + PANEL_MARGIN, panelW - PANEL_MARGIN * 2, HEADER_HEIGHT);
            ConsoleStyle.DrawSection(header);
            ConsoleStyle.Label(new Rect(header.x + 8f, header.y, header.width * 0.5f, header.height),
                title, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleLeft);
            ConsoleStyle.Label(new Rect(header.x + header.width * 0.5f, header.y, header.width * 0.5f - 8f, header.height),
                selLabel, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleRight);

            // Identity (full width)
            float idY = header.yMax + 8f;
            Rect idRect = new Rect(panelX + PANEL_MARGIN, idY, panelW - PANEL_MARGIN * 2, identityHeight);
            ConsoleStyle.DrawSection(idRect);
            DrawIdentity(idRect.ContractedBy(10f));

            float curY = idRect.yMax + 8f;

            // --- Left tab strip + right gizmo area ---
            float rightX = panelX + PANEL_MARGIN + (tabCount > 0 ? tabStripW + 8f : 0f);
            float rightW = panelW - PANEL_MARGIN * 2 - (tabCount > 0 ? tabStripW + 8f : 0f);

            if (tabCount > 0)
            {
                DrawTabStrip(panelX + PANEL_MARGIN, curY, tabStripW, tabStripH);
            }

            if (gizmoCount > 0)
            {
                DrawSectionHeader(rightX, curY, rightW,
                    "Commands", _focusedSection == Section.Gizmos);
                float gy = curY + SECTION_HEADER_HEIGHT + 4f;
                DrawActionGrid(_gizmoActions, _gizmoNav.Index, _focusedSection == Section.Gizmos,
                    rightX, gy, gridW, gizmoGridH);
            }

            curY += sharedH + 4f;

            // Prompt bar
            Rect promptRect = new Rect(panelX + PANEL_MARGIN, curY, panelW - PANEL_MARGIN * 2, PROMPT_HEIGHT);
            if (isAmbient)
            {
                if (AmbientFocused)
                    GlyphRenderer.DrawPromptBar(promptRect, ("A", "Confirm"), ("B", "Back"));
                else
                    GlyphRenderer.DrawPromptBar(promptRect, ("A", "Select"), ("D-Pad", "Actions"));
            }
            else
            {
                GlyphRenderer.DrawPromptBar(promptRect, ("A", "Open"), ("B", "Close"));
            }

            // Disabled reason
            var fa = FocusedAction;
            if (fa != null && fa.Value.Disabled)
            {
                var reason = fa.Value.DisabledReason;
                if (!reason.NullOrEmpty())
                {
                    Rect rRect = new Rect(panelX + PANEL_MARGIN, promptRect.yMax + 2f, panelW - PANEL_MARGIN * 2, 16f);
                    ConsoleStyle.Label(rRect, reason, new Color(1f, 0.4f, 0.4f), GameFont.Tiny, TextAnchor.MiddleCenter);
                }
            }
        }

        private static void DrawSectionHeader(float x, float y, float width, string text, bool focused)
        {
            Rect rect = new Rect(x, y, width, SECTION_HEADER_HEIGHT);
            Color bg = focused ? new Color(0.18f, 0.32f, 0.32f, 0.85f) : ConsoleStyle.SectionBg;
            ConsoleStyle.DrawSolid(rect, bg);
            // Left accent bar when focused
            if (focused)
            {
                Rect accent = new Rect(x, y, 3f, SECTION_HEADER_HEIGHT);
                ConsoleStyle.DrawSolid(accent, ConsoleStyle.Teal);
            }
            ConsoleStyle.Label(new Rect(x + 10f, y, width - 10f, SECTION_HEADER_HEIGHT),
                text, focused ? ConsoleStyle.TealBright : ConsoleStyle.TextGray,
                GameFont.Tiny, TextAnchor.MiddleLeft);
        }

        /// <summary>Compact vertical tab strip drawn on the left side of the panel.</summary>
        private static void DrawTabStrip(float x, float y, float width, float height)
        {
            int count = _tabActions.Count;
            float cellH = 28f;
            float spacing = 4f;
            for (int i = 0; i < count; i++)
            {
                float cy = y + i * (cellH + spacing);
                Rect cell = new Rect(x, cy, width, cellH);
                bool isSel = i == _tabNav.Index && _focusedSection == Section.Tabs;

                ConsoleStyle.DrawSection(cell);
                if (isSel)
                {
                    ConsoleStyle.DrawRoundedRect(cell, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(cell, 2);
                    GUI.color = Color.white;
                }

                ConsoleStyle.Label(cell.ContractedBy(4f), _tabActions[i].Label,
                    isSel ? ConsoleStyle.TextWhite : ConsoleStyle.TextGray,
                    GameFont.Tiny, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Draw a single grid of ActionItems at (x, y).</summary>
        private static void DrawActionGrid(List<ActionItem> actions, int selectedIdx, bool focused,
            float gridX, float gridY, float gridW, float gridH)
        {
            int count = actions.Count;
            for (int i = 0; i < count; i++)
            {
                int col = i % COLS;
                int row = i / COLS;
                Rect cell = new Rect(gridX + col * (CELL_SIZE + CELL_SPACING),
                                     gridY + row * (CELL_SIZE + CELL_SPACING),
                                     CELL_SIZE, CELL_SIZE);
                bool isSel = i == selectedIdx && focused;
                var action = actions[i];

                ConsoleStyle.DrawSection(cell);
                if (isSel)
                {
                    ConsoleStyle.DrawRoundedRect(cell, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(cell, 2);
                    GUI.color = Color.white;
                }

                if (action.Icon != null)
                {
                    GUI.color = action.Disabled ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : action.IconColor;
                    Widgets.DrawTextureFitted(cell.ContractedBy(8f), action.Icon, 1f);
                    GUI.color = Color.white;
                }
                else if (action.ThingDef != null)
                {
                    GUI.color = action.Disabled ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : Color.white;
                    Widgets.ThingIcon(cell.ContractedBy(6f), action.ThingDef);
                    GUI.color = Color.white;
                }
                else
                {
                    ConsoleStyle.Label(cell, action.Label,
                        isSel ? ConsoleStyle.TextWhite : ConsoleStyle.TextGray,
                        GameFont.Tiny, TextAnchor.MiddleCenter);
                }
            }
        }

        // ---- category / identity -----------------------------------------------

        private static string GetCategoryTitle()
        {
            try
            {
                int n = SelectionAdapter.NumSelected;
                if (n == 0) return "Inspect";
                if (n > 1) return $"{n} Selected";

                object? obj = SelectionAdapter.Single();
                if (obj is Pawn p)
                {
                    if (p.RaceProps?.Humanlike == true) return "Character";
                    if (p.RaceProps?.Animal == true) return "Animal";
                    return "Pawn";
                }
                if (obj is Thing t) return t.def?.label?.CapitalizeFirst() ?? "Inspect";
                if (obj is Zone) return "Zone";
                if (obj is RimWorld.Planet.WorldObject wo) return wo.def?.label?.CapitalizeFirst() ?? "World";

                // World view, no objects — fall back to selected tile's biome name.
                if (SelectionAdapter.IsWorldViewOpen)
                {
                    var t2 = Find.WorldSelector?.SelectedTile ?? RimWorld.Planet.PlanetTile.Invalid;
                    if (t2.Valid)
                    {
                        var biome = Find.WorldGrid?[t2]?.PrimaryBiome;
                        if (biome != null) return biome.LabelCap;
                    }
                    return "World";
                }
            }
            catch { /* Selection label probe may fail during state transitions */ }
            return "Inspect";
        }

        private static void DrawIdentity(Rect rect)
        {
            try
            {
                int n = SelectionAdapter.NumSelected;
                if (n == 0)
                {
                    if (SelectionAdapter.IsWorldViewOpen) DrawWorldTileIdentity(rect);
                    return;
                }
                if (n > 1)
                {
                    ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width, rect.height),
                        $"{n} objects selected", ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleCenter);
                    return;
                }

                object? obj = SelectionAdapter.Single();
                if (obj is Pawn pawn)
                {
                    DrawPawnIdentity(rect, pawn);
                    return;
                }

                if (obj is Thing t)
                {
                    ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width, 22f),
                        t.LabelCapNoCount, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);
                    string sub = t.def?.label ?? "";
                    ConsoleStyle.Label(new Rect(rect.x, rect.y + 22f, rect.width, 18f),
                        sub.CapitalizeFirst(), ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleLeft);
                    return;
                }

                if (obj is Zone z)
                {
                    ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width, 22f),
                        z.label, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);
                    return;
                }

                if (obj is RimWorld.Planet.WorldObject wo)
                {
                    ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width, 22f),
                        wo.LabelCap, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);
                    string sub = wo.def?.label ?? "";
                    ConsoleStyle.Label(new Rect(rect.x, rect.y + 22f, rect.width, 18f),
                        sub.CapitalizeFirst(), ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleLeft);
                    return;
                }

                // No selection but world view is open — describe the currently picked tile.
                if (SelectionAdapter.IsWorldViewOpen)
                {
                    DrawWorldTileIdentity(rect);
                }
            }
            catch { /* Identity rendering may fail during state transitions */ }
        }

        private static void DrawWorldTileIdentity(Rect rect)
        {
            var sel = Find.WorldSelector;
            if (sel == null) return;
            var tile = sel.SelectedTile;
            if (!tile.Valid) return;

            var grid = Find.WorldGrid;
            if (grid == null) return;

            var tileData = grid[tile];
            string biomeLabel = tileData?.PrimaryBiome?.LabelCap ?? "World";

            ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width, 22f),
                biomeLabel, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);

            string body = BuildTileInspectString(tile, tileData);
            if (!string.IsNullOrEmpty(body))
            {
                ConsoleStyle.Label(new Rect(rect.x, rect.y + 22f, rect.width, rect.height - 22f),
                    body, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.UpperLeft);
            }
        }

        private static string BuildTileInspectString(RimWorld.Planet.PlanetTile tile, RimWorld.Planet.Tile? tileData)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                Vector2 ll = Find.WorldGrid.LongLatOf(tile);
                sb.Append(ll.y.ToStringLatitude());
                sb.Append(" ");
                sb.Append(ll.x.ToStringLongitude());

                if (tileData != null && tileData.PrimaryBiome != null
                    && !tileData.PrimaryBiome.impassable
                    && tileData.hilliness != Hilliness.Undefined)
                {
                    sb.AppendLine();
                    sb.Append(tileData.hilliness.GetLabelCap());
                }

                if (tileData is RimWorld.Planet.SurfaceTile surface)
                {
                    if (surface.Roads != null)
                    {
                        var road = surface.Roads
                            .Select(rl => rl.road)
                            .OrderByDescending(r => r.priority)
                            .FirstOrDefault();
                        if (road != null)
                        {
                            sb.AppendLine();
                            sb.Append(road.LabelCap);
                        }
                    }
                    if (Find.World != null && !Find.World.Impassable(tile))
                    {
                        try
                        {
                            float diff = RimWorld.Planet.WorldPathGrid.CalculatedMovementDifficultyAt(tile, false)
                                * Find.WorldGrid.GetRoadMovementDifficultyMultiplier(tile, RimWorld.Planet.PlanetTile.Invalid);
                            sb.AppendLine();
                            sb.Append($"{"MovementDifficulty".Translate()}: {diff:0.#}");
                        }
                        catch { /* World movement calculation may fail on edge tiles */ }
                    }
                }

                try
                {
                    sb.AppendLine();
                    sb.Append($"{"AvgTemp".Translate()}: {GenTemperature.GetAverageTemperatureLabel(tile)}");
                }
                catch { /* Temperature calculation may fail on ocean or edge tiles */ }

                return sb.ToString();
            }
            catch { /* Tile info may be unavailable during world transitions */ return ""; }
        }

        private static void DrawPawnIdentity(Rect rect, Pawn pawn)
        {
            string name = pawn.LabelCapNoCount;
            string title = pawn.story?.TitleShortCap ?? "";
            string line1 = string.IsNullOrEmpty(title) ? name : $"{name}, {title}";
            ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width, 22f),
                line1, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);

            string line2 = "";
            if (pawn.ageTracker != null)
            {
                string g = pawn.gender == Gender.Male ? "Male" : pawn.gender == Gender.Female ? "Female" : "";
                int bio = Mathf.FloorToInt(pawn.ageTracker.AgeBiologicalYearsFloat);
                int chrono = Mathf.FloorToInt(pawn.ageTracker.AgeChronologicalYearsFloat);
                line2 = string.IsNullOrEmpty(g) ? $"age {bio} ({chrono})" : $"{g}, age {bio} ({chrono})";
            }
            ConsoleStyle.Label(new Rect(rect.x, rect.y + 18f, rect.width, 18f),
                line2, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleLeft);

            float barsY = rect.y + 38f;
            float barsW = (rect.width - 8f) * 0.5f;
            DrawHealthBar(new Rect(rect.x, barsY, barsW, 18f), pawn);
            DrawMoodBar(new Rect(rect.x + barsW + 8f, barsY, barsW, 18f), pawn);

            // Schedule and Area — full width, stacked vertically.
            float schedY = barsY + 20f;
            float areaY = schedY + 20f;
            string sched = GetScheduleText(pawn);
            string area = GetAreaText(pawn);
            if (!string.IsNullOrEmpty(sched))
            {
                ConsoleStyle.Label(new Rect(rect.x, schedY, rect.width, 20f),
                    sched, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleLeft);
            }
            if (!string.IsNullOrEmpty(area))
            {
                ConsoleStyle.Label(new Rect(rect.x, areaY, rect.width, 20f),
                    area, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleLeft);
            }
        }

        private static void DrawHealthBar(Rect rect, Pawn pawn)
        {
            float fill = 1f;
            string label = "Healthy";
            try
            {
                if (pawn.health?.summaryHealth != null)
                    fill = Mathf.Clamp01(pawn.health.summaryHealth.SummaryHealthPercent);
                if (pawn.health != null && pawn.health.hediffSet != null)
                    label = HealthUtility.GetGeneralConditionLabel(pawn, true).CapitalizeFirst();
            }
            catch { /* Health data may be unavailable for dead/despawned pawns */ }
            Color c = fill > 0.7f ? new Color(0.22f, 0.45f, 0.22f) : fill > 0.35f ? new Color(0.55f, 0.50f, 0.18f) : new Color(0.55f, 0.20f, 0.20f);
            ConsoleStyle.DrawBar(rect, fill, c);
            ConsoleStyle.Label(rect, label, ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static void DrawMoodBar(Rect rect, Pawn pawn)
        {
            float fill = 0.5f;
            string label = "—";
            try
            {
                if (pawn.needs?.mood != null)
                {
                    fill = Mathf.Clamp01(pawn.needs.mood.CurLevelPercentage);
                    label = pawn.MentalState != null && pawn.MentalState.def != null
                        ? pawn.MentalState.def.LabelCap.ToString()
                        : pawn.needs.mood.MoodString.CapitalizeFirst();
                }
            }
            catch { /* Mood data may be unavailable for dead/despawned pawns */ }
            Color c = fill > 0.6f ? new Color(0.28f, 0.45f, 0.55f) : fill > 0.3f ? new Color(0.55f, 0.50f, 0.18f) : new Color(0.55f, 0.22f, 0.22f);
            ConsoleStyle.DrawBar(rect, fill, c);
            ConsoleStyle.Label(rect, label, ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static string GetScheduleText(Pawn pawn)
        {
            try
            {
                if (pawn.timetable == null) return "";
                var assign = pawn.timetable.CurrentAssignment;
                return assign != null ? $"Sched: {assign.LabelCap}" : "";
            }
            catch { /* Schedule data may be unavailable for non-colonist pawns */ return ""; }
        }

        private static string GetAreaText(Pawn pawn)
        {
            try
            {
                var area = pawn.playerSettings?.AreaRestrictionInPawnCurrentMap;
                if (area != null) return $"Area: {area.Label}";
                return "Area: Unrestricted";
            }
            catch { /* Area data may be unavailable for non-colonist pawns */ return ""; }
        }
    }
}
