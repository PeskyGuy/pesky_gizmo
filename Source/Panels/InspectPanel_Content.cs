using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    /// <summary>
    /// Content-mode drawing — the popup that appears when a tab is opened.
    /// </summary>
    public static partial class InspectPanel
    {
        // ---- content mode drawing ----------------------------------------------

        private static void DrawContent()
        {
            // Draw the ambient panel first so it stays visible underneath the popup.
            DrawSplitGrid();

            // Console-style detail view: larger than RimWorld's stock tabs so
            // vanilla controls stay legible and easier to hit with a controller.
            float maxW = Mathf.Min(Verse.UI.screenWidth - 40f, 800f);
            float maxH = Mathf.Min(Verse.UI.screenHeight - 32f, 720f);
            float popupW = Mathf.Clamp(Verse.UI.screenWidth * 0.54f, 540f, maxW);
            float popupH = Mathf.Clamp(Verse.UI.screenHeight * 0.76f, 540f, maxH);
            float popupX = Verse.UI.screenWidth - popupW - 20f;
            float popupY = Mathf.Max(12f, (Verse.UI.screenHeight - popupH) / 2f);

            Rect popupRect = new Rect(popupX, popupY, popupW, popupH);
            ConsoleStyle.DrawPanel(popupRect);
            _bioPopupRect = popupRect;

            string tabLabel = (_tabNav.Index >= 0 && _tabNav.Index < _tabActions.Count)
                ? _tabActions[_tabNav.Index].Label : "";

            Rect header = new Rect(popupRect.x + PANEL_MARGIN, popupRect.y + PANEL_MARGIN, popupRect.width - PANEL_MARGIN * 2, HEADER_HEIGHT);
            ConsoleStyle.DrawSection(header);
            ConsoleStyle.Label(new Rect(header.x + 8f, header.y, header.width * 0.5f, header.height),
                GetCategoryTitle(), ConsoleStyle.TextGray, GameFont.Small, TextAnchor.MiddleLeft);
            ConsoleStyle.Label(new Rect(header.x + 8f, header.y, header.width - 16f, header.height),
                tabLabel, ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleCenter);

            float contentY = header.yMax + 8f;
            float contentH = popupRect.height - (contentY - popupRect.y) - PROMPT_HEIGHT - PANEL_MARGIN * 2 - 4f;
            Rect contentBg = new Rect(popupRect.x + PANEL_MARGIN, contentY, popupRect.width - PANEL_MARGIN * 2, contentH);
            ConsoleStyle.DrawSection(contentBg);

            Rect contentInner = contentBg.ContractedBy(16f);
            _contentInnerHeight = contentInner.height;
            // Scroll view for all tab content (Bio, Health, World tabs, etc.)
            GUI.BeginGroup(contentInner);
            Rect scrolledRect = new Rect(0f, -_contentScroll.y, contentInner.width, 800f);
            DrawCurrentTabContent(scrolledRect, contentInner);
            GUI.EndGroup();
            // Bio-element info tooltip to the left
            DrawBioInfoTooltip(_bioPopupRect);

            Rect promptRect = new Rect(popupRect.x + PANEL_MARGIN, contentBg.yMax + 8f, popupRect.width - PANEL_MARGIN * 2, PROMPT_HEIGHT);
            GlyphRenderer.DrawPromptBar(promptRect, ("B", "Back"), ("LB/RB", "Tabs"), ("D-Pad", "Scroll"));
        }

        // ---- tab content via reflection ----------------------------------------

        private static void DrawCurrentTabContent(Rect rect, Rect absRect)
        {
            if (_tabNav.Index < 0 || _tabNav.Index >= _tabActions.Count) return;
            var action = _tabActions[_tabNav.Index];
            if (action.Tab == null) return;
            InspectTabBase tab = action.Tab;

            if (!IsSelectionStableForTab())
            {
                ConsoleStyle.Label(rect, "Selection changed", ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
                return;
            }

            // Console-native Bio tab with D-pad-navigable elements
            if (tab.GetType().Name == "ITab_Pawn_Bio" || action.Label == "Bio")
            {
                if (SelectionAdapter.Single() is Pawn pawn)
                {
                    DrawCompactBioTab(rect, pawn);
                    return;
                }
            }

            if ((tab.GetType().Name == "ITab_Pawn_Health" || action.Label == "Health")
                && SelectionAdapter.Single() is Pawn healthPawn)
            {
                DrawConsoleHealthTab(rect, healthPawn);
                return;
            }

            if ((tab.GetType().Name == "ITab_Pawn_Social" || action.Label == "Social")
                && SelectionAdapter.Single() is Pawn socialPawn)
            {
                DrawConsoleSocialTab(rect, socialPawn);
                return;
            }

            float scale = VanillaTabScale();
            Vector2 logicalSize = new Vector2(absRect.width / scale, absRect.height / scale);
            try { InspectTabBridge.SetSize(tab, logicalSize); } catch { /* Reflection probe; API may differ across versions */ }

            Find.WindowStack.ImmediateWindow(0x7E5C9A21, absRect, WindowLayer.GameUI, () =>
            {
                Matrix4x4 prevMatrix = GUI.matrix;
                try
                {
                    if (!IsSelectionStableForTab()) return;
                    GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);
                    InspectTabBridge.FillTab(tab);
                }
                catch
                {
                    ConsoleStyle.Label(new Rect(0, 0, logicalSize.x, 24f), "Tab unavailable for this selection",
                        ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
                }
                finally
                {
                    GUI.matrix = prevMatrix;
                }
            }, doBackground: false, absorbInputAroundWindow: false, shadowAlpha: 0f);
        }

        private static float VanillaTabScale()
        {
            return 1f;
        }

        // ---- content input -----------------------------------------------------

        private static void HandleContentInput()
        {
            if (_tabActions.Count == 0) { _mode = Mode.Tabs; return; }

            int tabIdx = _tabNav.Index;
            if (tabIdx < 0 || tabIdx >= _tabActions.Count) tabIdx = 0;

            bool isBio = (tabIdx >= 0 && tabIdx < _tabActions.Count)
                       && (_tabActions[tabIdx].Label == "Bio"
                        || _tabActions[tabIdx].Tab?.GetType().Name == "ITab_Pawn_Bio");
            bool isHealth = (tabIdx >= 0 && tabIdx < _tabActions.Count)
                       && (_tabActions[tabIdx].Label == "Health"
                        || _tabActions[tabIdx].Tab?.GetType().Name == "ITab_Pawn_Health");

            if (isHealth)
            {
                if (HandleConsoleHealthInput()) return;
            }
            else if (isBio && _bioFocusRects.Count > 0)
            {
                // Spatial d-pad nav — picks nearest focusable rect in the pressed direction.
                if (ControllerState.DpadUpEdge)    { NavigateBioSpatial(0, -1); ConsoleHUD.ConsumeInput(); return; }
                if (ControllerState.DpadDownEdge)  { NavigateBioSpatial(0,  1); ConsoleHUD.ConsumeInput(); return; }
                if (ControllerState.DpadLeftEdge)  { NavigateBioSpatial(-1, 0); ConsoleHUD.ConsumeInput(); return; }
                if (ControllerState.DpadRightEdge) { NavigateBioSpatial( 1, 0); ConsoleHUD.ConsumeInput(); return; }
                if (ControllerState.ButtonAEdge)
                {
                    ActivateBioFocus();
                    ConsoleHUD.ConsumeInput();
                    return;
                }
            }
            else
            {
                // D-pad up/down scrolls tab content (non-Bio tabs)
                const float scrollStep = 40f;
                if (ControllerState.DpadUpEdge)
                {
                    _contentScroll.y = Mathf.Max(0f, _contentScroll.y - scrollStep);
                    ConsoleHUD.ConsumeInput();
                    return;
                }
                if (ControllerState.DpadDownEdge)
                {
                    float maxScroll = Mathf.Max(0f, 2000f - _contentInnerHeight);
                    _contentScroll.y = Mathf.Min(_contentScroll.y + scrollStep, maxScroll);
                    ConsoleHUD.ConsumeInput();
                    return;
                }
            }

            if (ControllerState.ButtonLBEdge)
            {
                tabIdx = (tabIdx - 1 + _tabActions.Count) % _tabActions.Count;
                _tabNav.Index = tabIdx;
                _contentScroll = Vector2.zero;
                _bioFocusIndex = 0;
                try { _tabActions[tabIdx].Tab?.OnOpen(); } catch { /* Tab initialization may fail on modded content */ }
                ConsoleHUD.ConsumeInput();
            }
            else if (ControllerState.ButtonRBEdge)
            {
                tabIdx = (tabIdx + 1) % _tabActions.Count;
                _tabNav.Index = tabIdx;
                _contentScroll = Vector2.zero;
                _bioFocusIndex = 0;
                try { _tabActions[tabIdx].Tab?.OnOpen(); } catch { /* Tab initialization may fail on modded content */ }
                ConsoleHUD.ConsumeInput();
            }

            if (ControllerState.ButtonBEdge)
            {
                // Yield B to the global handler while a vanilla submenu (Dialog or
                // non-Inspect MainTab) is open, so chip-launched menus close first
                // and our content stays intact underneath.
                if (HasVanillaSubmenuOpen()) return;

                _mode = Mode.Tabs;
                ConsoleHUD.ActivePanel = ConsolePanel.None;
                ConsoleHUD.ConsumeInput();
            }
        }

        private static bool HasVanillaSubmenuOpen()
        {
            var stack = Find.WindowStack;
            if (stack != null)
            {
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    var w = stack[i];
                    if (w == null || w.IsDebug) continue;
                    if (w.layer == WindowLayer.Dialog) return true;
                }
            }
            var openTab = Find.MainTabsRoot?.OpenTab;
            if (openTab != null && openTab != MainButtonDefOf.Inspect) return true;
            return false;
        }

        /// <summary>
        /// Returns true when the captured selection hasn't changed since the tab was
        /// opened. Used to avoid drawing stale tab content after the pawn despawns
        /// or the player selects something else.
        /// </summary>
        private static bool IsSelectionStableForTab()
        {
            object? live = CurrentSelection();
            // Synthetic token means "world view with nothing selected" — always stable.
            if (_capturedSelection == _worldAmbientToken) return true;
            if (live == null) return false;
            if (live is PlanetTile pt && _capturedSelection is PlanetTile pt2)
                return pt.Valid && pt2.Valid && pt == pt2;
            return ReferenceEquals(live, _capturedSelection);
        }
    }
}
