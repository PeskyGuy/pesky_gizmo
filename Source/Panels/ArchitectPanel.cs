using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PeskyGizmo
{
    public static class ArchitectPanel
    {
        public static readonly INavPanel Instance = new NavPanelAdapter(
            isOpen:      () => ConsoleHUD.ActivePanel == ConsolePanel.Architect,
            hidesCursor: () => true,
            handleInput: HandleInput,
            draw:        Draw
        );

        static ArchitectPanel()
        {
            ConsoleHUD.RegisterBlockingException(() => ConsoleHUD.ActivePanel == ConsolePanel.Architect && Find.DesignatorManager?.SelectedDesignator != null);
        }

        private enum Mode { Categories, Subcategories, Items }
        private static Mode _mode = Mode.Categories;

        private static List<DesignationCategoryDef>? _categories;
        private static List<DesignationCategoryDef>? _subcategories;
        private static List<Designator>? _currentDesignators;

        private static GridNavState _nav = new GridNavState { Cols = COLS, Wrap = false };

        private static int _selectedCategoryIndex;
        private static int _selectedSubcategoryIndex;

        private static Vector2 _catScrollPos;
        private static Vector2 _subScrollPos;
        private static Vector2 _itemsScrollPos;

        private static Rect _lastPanelRect;

        // Designator right-click options overlay
        private static bool _showingDesignatorOptions;
        private static List<FloatMenuOption> _designatorOptions = new List<FloatMenuOption>();
        private static int _designatorOptionIndex;

        private const float CELL_SIZE = 64f;
        private const float CELL_SPACING = 6f;
        private const int COLS = 4;
        private const float PANEL_MARGIN = 12f;
        private const float HEADER_HEIGHT = 36f;
        private const float PROMPT_HEIGHT = 32f;
        private const float DESC_WIDTH = 220f;
        private const float SCROLLBAR_WIDTH = 18f;

        public static void Open()
        {
            _mode = Mode.Categories;
            _nav.Reset();
            _selectedCategoryIndex = 0;
            _selectedSubcategoryIndex = 0;
            _catScrollPos = Vector2.zero;
            _subScrollPos = Vector2.zero;
            _itemsScrollPos = Vector2.zero;
            _subcategories = null;
            _currentDesignators = null;
            _showingDesignatorOptions = false;
            _designatorOptions.Clear();
            CacheCategories();
            DesignatorBridge.TryCloseVanillaArchitect();
        }

        public static void Close()
        {
            ConsoleHUD.ActivePanel = ConsolePanel.None;
            Find.DesignatorManager.Deselect();
            _showingDesignatorOptions = false;
            _designatorOptions.Clear();
            DesignatorBridge.TryCloseVanillaArchitect();
        }

        private static void CacheCategories()
        {
            _categories = DesignatorBridge.GetCachedPanelDefs();

            if (_categories == null || _categories.Count == 0)
            {
                _categories = DefDatabase<DesignationCategoryDef>.AllDefsListForReading
                    .Where(d => d.Visible)
                    .OrderBy(d => d.order)
                    .ToList();
            }
        }

        private static void OpenCategory(int index)
        {
            if (_categories == null || index < 0 || index >= _categories.Count) return;
            _selectedCategoryIndex = index;
            var parentDef = _categories[index];

            _subcategories = BAMIntegration.GetSubcategoriesFor(parentDef);

            if (_subcategories != null && _subcategories.Count > 0)
            {
                _mode = Mode.Subcategories;
                _selectedSubcategoryIndex = 0;
                _nav.Reset();
                _subScrollPos = Vector2.zero;
            }
            else
            {
                _currentDesignators = parentDef.ResolvedAllowedDesignators.Where(d => d.Visible).ToList();
                if (_currentDesignators.Count == 0)
                {
                    _currentDesignators = parentDef.ResolvedAllowedDesignators.ToList();
                }

                if (_currentDesignators.Count > 0)
                {
                    _mode = Mode.Items;
                    _nav.Reset();
                    _itemsScrollPos = Vector2.zero;
                }
            }
        }

        private static void OpenSubcategory(int index)
        {
            if (_subcategories == null || index < 0 || index >= _subcategories.Count) return;
            _selectedSubcategoryIndex = index;

            _currentDesignators = _subcategories[index].ResolvedAllowedDesignators.Where(d => d.Visible).ToList();
            if (_currentDesignators.Count == 0)
            {
                _currentDesignators = _subcategories[index].ResolvedAllowedDesignators.ToList();
            }

            if (_currentDesignators.Count > 0)
            {
                _mode = Mode.Items;
                _nav.Reset();
                _itemsScrollPos = Vector2.zero;
            }
        }

        public static void Draw()
        {
            if (_mode == Mode.Categories) DrawCategories();
            else if (_mode == Mode.Subcategories) DrawSubcategories();
            else DrawItems();

            DrawShapesOverlay();
            DrawRotationOverlay();
            DrawDesignatorOptionsOverlay();
        }

        public static void HandleInput()
        {
            bool up = ControllerState.DpadUpEdge;
            bool down = ControllerState.DpadDownEdge;
            bool left = ControllerState.DpadLeftEdge;
            bool right = ControllerState.DpadRightEdge;
            bool aPress = ControllerState.ButtonAEdge;
            bool bPress = ControllerState.ButtonBEdge;
            bool xPress = ControllerState.ButtonXEdge;
            bool lbPress = ControllerState.ButtonLBEdge;
            bool rbPress = ControllerState.ButtonRBEdge;

            // X button: show right-click options for selected designator
            if (xPress && _mode == Mode.Items && !_showingDesignatorOptions)
            {
                var des = _currentDesignators?[_nav.Index];
                if (des != null)
                {
                    var options = des.RightClickFloatMenuOptions?.ToList();
                    if (options != null && options.Count > 0)
                    {
                        _designatorOptions = options;
                        _designatorOptionIndex = 0;
                        _showingDesignatorOptions = true;
                        ConsoleHUD.ConsumeInput();
                        return;
                    }
                }
            }

            // Handle options overlay navigation
            if (_showingDesignatorOptions)
            {
                if (up)
                {
                    _designatorOptionIndex = Mathf.Max(0, _designatorOptionIndex - 1);
                    ConsoleHUD.ConsumeInput();
                }
                if (down)
                {
                    _designatorOptionIndex = Mathf.Min(_designatorOptions.Count - 1, _designatorOptionIndex + 1);
                    ConsoleHUD.ConsumeInput();
                }
                if (aPress)
                {
                    var opt = _designatorOptions[_designatorOptionIndex];
                    if (!opt.Disabled && opt.action != null)
                    {
                        try { opt.action(); } catch (System.Exception ex)
                        {
                            GizmoLog.WarningOnce($"Designator option action failed: {ex.GetBaseException().Message}", 552002);
                        }
                    }
                    else
                    {
                        SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    }
                    _showingDesignatorOptions = false;
                    ConsoleHUD.ConsumeInput();
                }
                if (bPress)
                {
                    _showingDesignatorOptions = false;
                    ConsoleHUD.ConsumeInput();
                }
                return;
            }

            // Handle Shapes cycling (LB/RB) — only when designator has draw-style shapes
            Designator? activeDes = Find.DesignatorManager.SelectedDesignator;
            if (activeDes != null && activeDes.DrawStyleCategory != null)
            {
                if (lbPress || rbPress)
                {
                    CycleShape(activeDes, lbPress ? -1 : 1);
                    ConsoleHUD.ConsumeInput();
                }
            }
            // Handle placement rotation (LB/RB) — for buildings without shape variants
            else if (activeDes is Designator_Place placeDes && DesignatorBridge.IsPlacingDefRotatable(placeDes))
            {
                if (lbPress || rbPress)
                {
                    DesignatorBridge.TryRotate(placeDes, lbPress ? RotationDirection.Counterclockwise : RotationDirection.Clockwise);
                    ConsoleHUD.ConsumeInput();
                }
            }

            int count = 0;
            if (_mode == Mode.Categories) count = _categories?.Count ?? 0;
            else if (_mode == Mode.Subcategories) count = _subcategories?.Count ?? 0;
            else count = _currentDesignators?.Count ?? 0;

            if (count == 0) return;

            // Always consume D-pad when the panel is active — prevents edge
            // presses from leaking through to other systems.
            _nav.Count = count;
            bool dpadPressed = ControllerState.DpadUpEdge || ControllerState.DpadDownEdge
                            || ControllerState.DpadLeftEdge || ControllerState.DpadRightEdge;
            if (_nav.HandleDpad())
            {
                InspectPanel.LastNavTime = Time.unscaledTime;
            }
            if (dpadPressed)
            {
                ConsoleHUD.ConsumeInput();
            }

            if (aPress)
            {
                // Convert physical bottom-up coordinates to logical top-down GUI coordinates for Rect checking
                Vector2 guiMouse = new Vector2(
                    ControllerState.CursorScreenPos.x / Prefs.UIScale,
                    Verse.UI.screenHeight - (ControllerState.CursorScreenPos.y / Prefs.UIScale)
                );

                // Only consume A if we are over the menu or no designator is selected
                if (activeDes == null || _lastPanelRect.Contains(guiMouse))
                {
                    if (_mode == Mode.Categories) OpenCategory(_nav.Index);
                    else if (_mode == Mode.Subcategories) OpenSubcategory(_nav.Index);
                    else ActivateDesignator();
                    ConsoleHUD.ConsumeInput();
                }
            }

            if (bPress)
            {
                if (activeDes != null)
                {
                    Find.DesignatorManager.Deselect();
                    ConsoleHUD.ConsumeInput();
                }
                else if (_mode == Mode.Items)
                {
                    if (_subcategories != null && _subcategories.Count > 0)
                    {
                        _mode = Mode.Subcategories;
                        _nav.Index = _selectedSubcategoryIndex;
                    }
                    else
                    {
                        _mode = Mode.Categories;
                        _nav.Index = _selectedCategoryIndex;
                    }
                    ConsoleHUD.ConsumeInput();
                }
                else if (_mode == Mode.Subcategories)
                {
                    _mode = Mode.Categories;
                    _nav.Index = _selectedCategoryIndex;
                    ConsoleHUD.ConsumeInput();
                }
                else
                {
                    Close();
                    ConsoleHUD.ConsumeInput();
                }
            }
        }

        private static void CycleShape(Designator des, int direction)
        {
            var cat = des.DrawStyleCategory;
            if (cat == null || cat.styles.NullOrEmpty()) return;

            var current = Find.DesignatorManager.SelectedStyle;
            int idx = cat.styles.IndexOf(current);
            if (idx < 0) idx = 0;

            int newIdx = (idx + direction + cat.styles.Count) % cat.styles.Count;
            Find.DesignatorManager.SelectedStyle = cat.styles[newIdx];
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        private static void ActivateDesignator()
        {
            if (_currentDesignators == null || _nav.Index < 0 || _nav.Index >= _currentDesignators.Count) return;
            Designator des = _currentDesignators[_nav.Index];
            if (des is DesignatorWithEyedropper)
            {
                Find.DesignatorManager.Select(des);
                des.ProcessInput(Event.current);
            }
            else
            {
                Find.DesignatorManager.Select(des);
            }
            // MODIFIED: No longer close the menu automatically.
        }

        private static Texture2D? GetCategoryTexture(string defName)
        {
            return ArchitectIconIntegration.GetCategoryTexture(defName);
        }

        private static void DrawGrid(List<DesignationCategoryDef> list, ref Vector2 scrollPos, string titleLeft, string titleRight, string aPrompt)
        {
            int count = list.Count;
            if (count == 0) return;

            int rows = Mathf.CeilToInt((float)count / COLS);
            float gridW = COLS * (CELL_SIZE + CELL_SPACING) - CELL_SPACING;
            float gridH = rows * (CELL_SIZE + CELL_SPACING) - CELL_SPACING;

            float maxPanelH = Verse.UI.screenHeight - 35f - 150f; // Extra space for shapes overlay

            float fixedHeight = HEADER_HEIGHT + PROMPT_HEIGHT + (PANEL_MARGIN * 3) + 10f;
            float desiredPanelH = fixedHeight + gridH;
            float panelH = Mathf.Min(desiredPanelH, maxPanelH);

            bool needsScrollbar = desiredPanelH > maxPanelH;
            float currentScrollbarWidth = needsScrollbar ? SCROLLBAR_WIDTH : 0f;

            float panelW = gridW + PANEL_MARGIN * 2 + currentScrollbarWidth;
            float panelX = 0f;
            float panelY = Verse.UI.screenHeight - 35f - panelH;

            _lastPanelRect = new Rect(panelX, panelY, panelW, panelH);
            ConsoleStyle.DrawPanel(_lastPanelRect);

            Rect headerRect = new Rect(panelX + PANEL_MARGIN, panelY + PANEL_MARGIN, gridW + currentScrollbarWidth, HEADER_HEIGHT);
            ConsoleStyle.DrawSection(headerRect);

            ConsoleStyle.Label(new Rect(headerRect.x + 8f, headerRect.y, headerRect.width * 0.5f, HEADER_HEIGHT), titleLeft, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleLeft);
            ConsoleStyle.Label(new Rect(headerRect.x + headerRect.width * 0.5f, headerRect.y, headerRect.width * 0.5f - 8f, HEADER_HEIGHT), titleRight, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleRight);

            float viewX = panelX + PANEL_MARGIN;
            float viewY = panelY + PANEL_MARGIN + HEADER_HEIGHT + 6f;
            float viewW = gridW + currentScrollbarWidth;
            float viewH = panelH - (fixedHeight - 4f);

            Rect viewRect = new Rect(viewX, viewY, viewW, viewH);
            Rect contentRect = new Rect(0, 0, gridW, gridH);

            Widgets.BeginScrollView(viewRect, ref scrollPos, contentRect);

            for (int i = 0; i < count; i++)
            {
                int col = i % COLS;
                int row = i / COLS;
                Rect cellRect = new Rect(
                    col * (CELL_SIZE + CELL_SPACING),
                    row * (CELL_SIZE + CELL_SPACING),
                    CELL_SIZE, CELL_SIZE);

                bool selected = i == _nav.Index;
                ConsoleStyle.DrawSection(cellRect);

                if (selected)
                {
                    ConsoleStyle.DrawRoundedRect(cellRect, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(cellRect, 2);
                    GUI.color = Color.white;
                }

                var cat = list[i];
                Texture2D? catIcon = GetCategoryTexture(cat.defName);
                if (catIcon != null)
                {
                    Rect iconRect = cellRect.ContractedBy(10f);
                    GUI.color = selected ? Color.white : ConsoleStyle.TextGray;
                    GUI.DrawTexture(iconRect, catIcon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
                else
                {
                    string catLabel = cat.LabelCap;
                    if (_mode == Mode.Subcategories && _categories != null && cat == _categories[_selectedCategoryIndex])
                    {
                        catLabel = "More";
                    }
                    ConsoleStyle.Label(cellRect, catLabel, selected ? ConsoleStyle.TextWhite : ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
                }
            }

            Widgets.EndScrollView();

            if (needsScrollbar)
            {
                float selY = (_nav.Index / COLS) * (CELL_SIZE + CELL_SPACING);
                if (selY < scrollPos.y)
                    scrollPos.y = selY;
                else if (selY + CELL_SIZE > scrollPos.y + viewH)
                    scrollPos.y = selY + CELL_SIZE - viewH;
            }

            Rect promptRect = new Rect(panelX, panelY + panelH - PROMPT_HEIGHT - 4f, panelW, PROMPT_HEIGHT);
            GlyphRenderer.DrawPromptBar(promptRect, (GamepadButton.B, _mode == Mode.Categories ? "Close" : "Back"), (GamepadButton.A, aPrompt));
        }

        private static void DrawCategories()
        {
            if (_categories == null) CacheCategories();
            if (_categories == null || _categories.Count == 0) return;
            string selCatName = (_nav.Index >= 0 && _nav.Index < _categories.Count) ? _categories[_nav.Index].LabelCap : "";
            DrawGrid(_categories, ref _catScrollPos, selCatName, "Architect", "Select");
        }

        private static void DrawSubcategories()
        {
            if (_subcategories == null || _subcategories.Count == 0) return;
            string parentName = _categories?[_selectedCategoryIndex].LabelCap ?? "Architect";

            var selCat = _subcategories[_nav.Index];
            string selCatName = selCat.LabelCap;
            if (_categories != null && selCat == _categories[_selectedCategoryIndex]) selCatName = "More";

            DrawGrid(_subcategories, ref _subScrollPos, selCatName, parentName, "Open");
        }

        private static void DrawItems()
        {
            if (_currentDesignators == null || _categories == null) return;
            int count = _currentDesignators.Count;
            if (count == 0) { _mode = Mode.Categories; return; }

            int rows = Mathf.CeilToInt((float)count / COLS);
            float gridW = COLS * (CELL_SIZE + CELL_SPACING) - CELL_SPACING;
            float gridH = rows * (CELL_SIZE + CELL_SPACING) - CELL_SPACING;

            float maxPanelH = Verse.UI.screenHeight - 35f - 150f;

            float fixedHeight = HEADER_HEIGHT + PROMPT_HEIGHT + (PANEL_MARGIN * 3) + 10f;
            float desiredPanelH = fixedHeight + Mathf.Max(gridH, 120f);
            float panelH = Mathf.Min(desiredPanelH, maxPanelH);

            bool needsScrollbar = desiredPanelH > maxPanelH;
            float currentScrollbarWidth = needsScrollbar ? SCROLLBAR_WIDTH : 0f;

            float panelW = gridW + PANEL_MARGIN * 2 + DESC_WIDTH + PANEL_MARGIN + currentScrollbarWidth;
            float panelX = 0f;
            float panelY = Verse.UI.screenHeight - 35f - panelH;

            _lastPanelRect = new Rect(panelX, panelY, panelW, panelH);
            ConsoleStyle.DrawPanel(_lastPanelRect);

            string catName = _categories[_selectedCategoryIndex].LabelCap;
            Rect headerRect = new Rect(panelX + PANEL_MARGIN, panelY + PANEL_MARGIN, gridW + currentScrollbarWidth, HEADER_HEIGHT);
            ConsoleStyle.DrawSection(headerRect);

            if (_subcategories != null && _subcategories.Count > 0)
            {
                var selCat = _subcategories[_selectedSubcategoryIndex];
                string subLabel = selCat.LabelCap;
                if (selCat == _categories[_selectedCategoryIndex]) subLabel = "More";

                ConsoleStyle.Label(new Rect(headerRect.x + 8f, headerRect.y, headerRect.width * 0.45f, HEADER_HEIGHT),
                    catName, ConsoleStyle.TextGray, GameFont.Small, TextAnchor.MiddleLeft);

                ConsoleStyle.Label(new Rect(headerRect.x + 8f, headerRect.y, headerRect.width - 16f, HEADER_HEIGHT),
                    subLabel, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleCenter);
            }
            else
            {
                ConsoleStyle.Label(new Rect(headerRect.x + 8f, headerRect.y, headerRect.width - 16f, HEADER_HEIGHT),
                    catName, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleLeft);
            }

            float viewX = panelX + PANEL_MARGIN;
            float viewY = panelY + PANEL_MARGIN + HEADER_HEIGHT + 6f;
            float viewW = gridW + currentScrollbarWidth;
            float viewH = panelH - (fixedHeight - 4f);

            Rect viewRect = new Rect(viewX, viewY, viewW, viewH);
            Rect contentRect = new Rect(0, 0, gridW, gridH);

            Widgets.BeginScrollView(viewRect, ref _itemsScrollPos, contentRect);

            for (int i = 0; i < count; i++)
            {
                int col = i % COLS;
                int row = i / COLS;
                Rect cellRect = new Rect(
                    col * (CELL_SIZE + CELL_SPACING),
                    row * (CELL_SIZE + CELL_SPACING),
                    CELL_SIZE, CELL_SIZE);

                bool selected = i == _nav.Index;
                ConsoleStyle.DrawSection(cellRect);

                Designator des = _currentDesignators[i];
                bool active = Find.DesignatorManager.SelectedDesignator == des;

                if (selected || active)
                {
                    ConsoleStyle.DrawRoundedRect(cellRect, ConsoleStyle.CellSelected);
                    GUI.color = active ? Color.white : ConsoleStyle.Teal;
                    Widgets.DrawBox(cellRect, selected ? 2 : 1);
                    GUI.color = Color.white;
                }

                if (des.icon != null)
                {
                    Rect iconRect = cellRect.ContractedBy(4f);
                    Color drawColor = des.IconDrawColor;
                    if (!selected && !active) drawColor *= ConsoleStyle.TextGray;
                    GUI.color = drawColor;
                    Widgets.DrawTextureFitted(iconRect, des.icon, des.iconDrawScale * 0.85f, des.iconProportions, des.iconTexCoords, des.iconAngle);
                    GUI.color = Color.white;
                    DrawDesignatorOverlay(des, iconRect);
                }
            }

            Widgets.EndScrollView();

            if (needsScrollbar)
            {
                float selY = (_nav.Index / COLS) * (CELL_SIZE + CELL_SPACING);
                if (selY < _itemsScrollPos.y)
                    _itemsScrollPos.y = selY;
                else if (selY + CELL_SIZE > _itemsScrollPos.y + viewH)
                    _itemsScrollPos.y = selY + CELL_SIZE - viewH;
            }

            if (_nav.Index >= 0 && _nav.Index < count)
            {
                Designator selDes = _currentDesignators[_nav.Index];
                float descX = viewX + viewW + PANEL_MARGIN;
                float descY = viewY;
                float descH = viewH;
                Rect descBg = new Rect(descX, descY, DESC_WIDTH, descH);
                ConsoleStyle.DrawSection(descBg);

                Rect nameRect = new Rect(descX + 10f, descY + 8f, DESC_WIDTH - 20f, 24f);
                ConsoleStyle.Label(nameRect, selDes.LabelCap, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.UpperLeft);

                Rect sepRect = new Rect(descX + 10f, descY + 34f, DESC_WIDTH - 20f, 1f);
                ConsoleStyle.DrawSolid(sepRect, ConsoleStyle.SectionBorder);

                Rect bodyRect = new Rect(descX + 10f, descY + 40f, DESC_WIDTH - 20f, descH - 50f);
                ConsoleStyle.Label(bodyRect, selDes.Desc, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.UpperLeft);
            }

            Rect promptRect = new Rect(panelX, panelY + panelH - PROMPT_HEIGHT - 4f, panelW, PROMPT_HEIGHT);
            GlyphRenderer.DrawPromptBar(promptRect, (GamepadButton.B, "Back"), (GamepadButton.X, "Options"), (GamepadButton.A, "Select"));
        }

        private static void DrawShapesOverlay() => DrawShapesOverlayAt(_lastPanelRect);

        private static void DrawShapesOverlayAt(Rect anchor)
        {
            Designator? des = Find.DesignatorManager.SelectedDesignator;
            if (des == null || des.DrawStyleCategory == null) return;

            var cat = des.DrawStyleCategory;
            var current = Find.DesignatorManager.SelectedStyle;

            float overlayW = 200f;
            float overlayH = 100f;
            float x = anchor.x;
            float y = anchor.y - overlayH - 6f;

            Rect rect = new Rect(x, y, overlayW, overlayH);
            ConsoleStyle.DrawPanel(rect);

            Rect header = new Rect(x + 10f, y + 10f, overlayW - 20f, 24f);
            ConsoleStyle.Label(header, des.LabelCap, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);

            Rect styleRect = new Rect(x + 10f, y + 40f, overlayW - 20f, 30f);
            ConsoleStyle.DrawSection(styleRect);

            string styleLabel = current?.LabelCap ?? "Default";
            ConsoleStyle.Label(styleRect, styleLabel, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleCenter);

            Rect hints = new Rect(x + 10f, y + 75f, overlayW - 20f, 20f);
            GlyphRenderer.DrawWithLabel(hints.x, hints.y, GamepadButton.LB, "", 16f);
            GlyphRenderer.DrawWithLabel(hints.x + 35f, hints.y, GamepadButton.RB, "Shape", 16f);
        }

        private static void DrawRotationOverlay() => DrawRotationOverlayAt(_lastPanelRect);

        private static void DrawRotationOverlayAt(Rect anchor)
        {
            Designator? des = Find.DesignatorManager.SelectedDesignator;
            if (des is not Designator_Place placeDes) return;
            if (placeDes.DrawStyleCategory != null) return; // shapes overlay already shown
            if (!DesignatorBridge.IsPlacingDefRotatable(placeDes)) return;

            float overlayW = 200f;
            float overlayH = 100f;
            float x = anchor.x;
            float y = anchor.y - overlayH - 6f;

            Rect rect = new Rect(x, y, overlayW, overlayH);
            ConsoleStyle.DrawPanel(rect);

            Rect header = new Rect(x + 10f, y + 10f, overlayW - 20f, 24f);
            ConsoleStyle.Label(header, des.LabelCap, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);

            Rot4? curRot = DesignatorBridge.GetPlacingRot(placeDes);
            string rotLabel = curRot.HasValue ? RotName(curRot.Value) : "?";

            Rect styleRect = new Rect(x + 10f, y + 40f, overlayW - 20f, 30f);
            ConsoleStyle.DrawSection(styleRect);
            ConsoleStyle.Label(styleRect, rotLabel, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleCenter);

            Rect hints = new Rect(x + 10f, y + 75f, overlayW - 20f, 20f);
            GlyphRenderer.DrawWithLabel(hints.x, hints.y, GamepadButton.LB, "", 16f);
            GlyphRenderer.DrawWithLabel(hints.x + 35f, hints.y, GamepadButton.RB, "Rotate", 16f);
        }

        private static void DrawDesignatorOptionsOverlay()
        {
            if (!_showingDesignatorOptions || _designatorOptions == null || _designatorOptions.Count == 0) return;

            float overlayW = 280f;
            float rowH = 28f;
            float rowSpacing = 2f;
            float headerH = 28f;
            float padding = 10f;
            float overlayH = padding * 2 + headerH + _designatorOptions.Count * (rowH + rowSpacing) - rowSpacing;

            float x = _lastPanelRect.x;
            float y = _lastPanelRect.y - overlayH - 6f;
            if (y < 10f) y = 10f;

            Rect rect = new Rect(x, y, overlayW, overlayH);
            ConsoleStyle.DrawPanel(rect);

            Rect header = new Rect(x + padding, y + padding, overlayW - padding * 2, headerH);
            ConsoleStyle.Label(header, "Options", ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);

            float rowY = header.yMax + 4f;
            for (int i = 0; i < _designatorOptions.Count; i++)
            {
                var opt = _designatorOptions[i];
                Rect row = new Rect(x + padding, rowY, overlayW - padding * 2, rowH);
                bool sel = i == _designatorOptionIndex;

                if (sel)
                {
                    ConsoleStyle.DrawRoundedRect(row, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(row, 2);
                    GUI.color = Color.white;
                }
                else
                {
                    ConsoleStyle.DrawRoundedRect(row, ConsoleStyle.SectionBg);
                }

                GUI.color = opt.Disabled ? ConsoleStyle.TextGray : (sel ? ConsoleStyle.TextWhite : ConsoleStyle.TextGray);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(row.ContractedBy(4f), opt.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                rowY += rowH + rowSpacing;
            }
        }

        private static string RotName(Rot4 r)
        {
            if (r == Rot4.North) return "North";
            if (r == Rot4.East) return "East";
            if (r == Rot4.South) return "South";
            if (r == Rot4.West) return "West";
            return r.ToString();
        }

        /// <summary>
        /// Draws the shapes/rotation overlay for an active designator when the ArchitectPanel is NOT open.
        /// Positions the overlay at the bottom-left of the screen above the bottom bar.
        /// </summary>
        public static void DrawActiveDesignatorOverlays()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.DesignatorManager?.SelectedDesignator == null) return;
            float anchorY = Verse.UI.screenHeight - 35f;
            // Shift right when the inspect ambient panel is visible to avoid overlap
            float anchorX = InspectPanel.AmbientPanelRight;
            Rect anchor = new Rect(anchorX, anchorY, 200f, 0f);
            DrawShapesOverlayAt(anchor);
            DrawRotationOverlayAt(anchor);
        }

        /// <summary>
        /// Handles LB/RB for shape cycling or rotation when a designator is active,
        /// regardless of whether the ArchitectPanel is open.
        /// Returns true if LB or RB was consumed.
        /// </summary>
        public static bool HandleActiveDesignatorInput()
        {
            if (Current.ProgramState != ProgramState.Playing) return false;
            Designator? activeDes = Find.DesignatorManager?.SelectedDesignator;
            if (activeDes == null) return false;

            bool lbPress = ControllerState.ButtonLBEdge;
            bool rbPress = ControllerState.ButtonRBEdge;
            if (!lbPress && !rbPress) return false;

            if (activeDes.DrawStyleCategory != null)
            {
                CycleShape(activeDes, lbPress ? -1 : 1);
                return true;
            }

            if (activeDes is Designator_Place placeDes && DesignatorBridge.IsPlacingDefRotatable(placeDes))
            {
                DesignatorBridge.TryRotate(placeDes, lbPress ? RotationDirection.Counterclockwise : RotationDirection.Clockwise);
                return true;
            }

            return false;
        }

        private static void DrawDesignatorOverlay(Designator des, Rect iconRect)
        {
            Texture2D? topTex = DesignatorBridge.GetIconTopTex(des);
            if (topTex == null) return;
            GUI.color = Color.white;
            Widgets.DrawTextureFitted(iconRect, topTex, des.iconDrawScale * 0.85f, des.iconProportions, des.iconTexCoords, des.iconAngle);
        }
    }
}
