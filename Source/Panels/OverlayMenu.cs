using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace PeskyGizmo
{
    public static class OverlayMenu
    {
        public static bool IsOpen { get; private set; }

        private static GridNavState _nav = new GridNavState { Cols = 5, Wrap = false };
        private static List<OverlayItem> _items = new List<OverlayItem>();

        private struct OverlayItem
        {
            public string label;
            public string tooltip;
            public System.Action toggle;
            public System.Func<bool> isActive;
            public Texture2D icon;
        }

        static OverlayMenu()
        {
            _items.Add(Make("Learning Helper", "ShowLearningHelperWhenEmptyToggleButton",      () => Find.PlaySettings.showLearningHelper,          v => Find.PlaySettings.showLearningHelper = v,           TexButton.ShowLearningHelper));
            _items.Add(Make("Zones",           "ZoneVisibilityToggleButton",                   () => Find.PlaySettings.showZones,                   v => Find.PlaySettings.showZones = v,                    TexButton.ShowZones));
            _items.Add(Make("Beauty",          "ShowBeautyToggleButton",                       () => Find.PlaySettings.showBeauty,                  v => Find.PlaySettings.showBeauty = v,                   TexButton.ShowBeauty));
            _items.Add(Make("Room Stats",      "ShowRoomStatsToggleButton",                    () => Find.PlaySettings.showRoomStats,               v => Find.PlaySettings.showRoomStats = v,                TexButton.ShowRoomStats));
            _items.Add(Make("Colonist Bar",    "ShowColonistBarToggleButton",                  () => Find.PlaySettings.showColonistBar,             v => Find.PlaySettings.showColonistBar = v,              TexButton.ShowColonistBar));
            _items.Add(Make("Roof",            "ShowRoofOverlayToggleButton",                  () => Find.PlaySettings.showRoofOverlay,             v => Find.PlaySettings.showRoofOverlay = v,              TexButton.ShowRoofOverlay));
            _items.Add(Make("Fertility",       "ShowFertilityOverlayToggleButton",             () => Find.PlaySettings.showFertilityOverlay,        v => Find.PlaySettings.showFertilityOverlay = v,         TexButton.ShowFertilityOverlay));
            _items.Add(Make("Terrain",         "ShowTerrainAffordanceOverlayToggleButton",     () => Find.PlaySettings.showTerrainAffordanceOverlay,v => Find.PlaySettings.showTerrainAffordanceOverlay = v, TexButton.ShowTerrainAffordanceOverlay));
            _items.Add(Make("Home Zone",       "AutoHomeAreaToggleButton",                     () => Find.PlaySettings.autoHomeArea,                v => Find.PlaySettings.autoHomeArea = v,                 TexButton.AutoHomeArea));
            _items.Add(Make("Auto-Rebuild",    "AutoRebuildButton",                            () => Find.PlaySettings.autoRebuild,                 v => Find.PlaySettings.autoRebuild = v,                  TexButton.AutoRebuild));
            _items.Add(Make("Temperature",     "ShowTemperatureOverlayToggleButton",           () => Find.PlaySettings.showTemperatureOverlay,      v => Find.PlaySettings.showTemperatureOverlay = v,       TexButton.ShowTemperatureOverlay));
            _items.Add(Make("Categorized",     "CategorizedResourceReadoutToggleButton",       () => Prefs.ResourceReadoutCategorized,              v => Prefs.ResourceReadoutCategorized = v,               TexButton.CategorizedResourceReadout));
            _items.Add(Make("Pollution",       "ShowPollutionOverlayToggleButton",             () => Find.PlaySettings.showPollutionOverlay,        v => Find.PlaySettings.showPollutionOverlay = v,         TexButton.ShowPollutionOverlay));
            _nav.Count = _items.Count;
        }

        private static OverlayItem Make(string label, string tooltipKey, System.Func<bool> getter, System.Action<bool> setter, Texture2D icon)
        {
            return new OverlayItem
            {
                label = label,
                tooltip = tooltipKey.Translate(),
                icon = icon,
                isActive = getter,
                toggle = () => setter(!getter()),
            };
        }

        public static void Toggle() { IsOpen = !IsOpen; if (IsOpen) _nav.Reset(); }
        public static void Open()  { IsOpen = true; _nav.Reset(); }
        public static void Close() { IsOpen = false; }

        public static void HandleInput()
        {
            if (!IsOpen) return;
            _nav.Count = _items.Count;
            if (_nav.HandleDpad()) { ConsoleHUD.ConsumeInput(); return; }

            if (ControllerState.ButtonAEdge)
            {
                int idx = _nav.Index;
                if (idx >= 0 && idx < _items.Count)
                    _items[idx].toggle?.Invoke();
                ConsoleHUD.ConsumeInput();
                return;
            }

            if (ControllerState.ButtonBEdge)
            {
                Close();
                ConsoleHUD.ConsumeInput();
                return;
            }
        }

        public static void Draw()
        {
            if (!IsOpen) return;
            int count = _items.Count;
            if (count == 0) { Close(); return; }
            _nav.Count = count;
            _nav.Clamp();

            const float cellSize = 48f;
            const float spacing = 4f;
            const float padding = 10f;
            const float headerH = 24f;
            const float tooltipH = 22f;

            int cols = _nav.Cols;
            int rows = Mathf.CeilToInt((float)count / cols);
            float gridW = cols * cellSize + (cols - 1) * spacing;
            float gridH = rows * cellSize + (rows - 1) * spacing;
            float panelW = gridW + padding * 2;
            float panelH = padding * 2 + headerH + gridH + tooltipH;
            float panelX = Verse.UI.screenWidth - panelW - 16f;
            float panelY = Verse.UI.screenHeight - 35f - panelH - 4f;

            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);
            Widgets.DrawWindowBackground(panelRect);

            Rect header = new Rect(panelX + padding, panelY + padding, panelW - padding * 2, headerH);
            ConsoleStyle.Label(header, "Overlays", ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleLeft);

            float startX = panelX + padding;
            float startY = header.yMax + 4f;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float cx = startX + col * (cellSize + spacing);
                float cy = startY + row * (cellSize + spacing);
                Rect cellRect = new Rect(cx, cy, cellSize, cellSize);

                var item = _items[i];
                bool sel = i == _nav.Index;
                bool active = item.isActive?.Invoke() ?? false;

                if (sel)
                {
                    ConsoleStyle.DrawRoundedRect(cellRect, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(cellRect, 2);
                    GUI.color = Color.white;
                }
                else
                {
                    ConsoleStyle.DrawRoundedRect(cellRect, ConsoleStyle.SectionBg);
                }

                if (item.icon != null)
                {
                    Rect iconRect = cellRect.ContractedBy(4f);
                    Color drawColor = active ? Color.white : ConsoleStyle.TextGray;
                    GUI.color = drawColor;
                    GUI.DrawTexture(iconRect, item.icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
            }

            Rect tooltipRect = new Rect(panelX + padding, panelRect.yMax - padding - tooltipH, panelW - padding * 2, tooltipH);
            int idx = _nav.Index;
            if (idx >= 0 && idx < _items.Count)
            {
                string tip = _items[idx].tooltip;
                if (!string.IsNullOrEmpty(tip))
                    ConsoleStyle.Label(tooltipRect, tip, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
        }
    }
}
