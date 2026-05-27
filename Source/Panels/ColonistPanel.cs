using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmoCommandable
{
    /// <summary>
    /// Colonist navigation panel — appears on the left side when the Stats panel
    /// is open for a colonist. Lists all player-controlled colonists with D-pad
    /// navigation. A selects; B closes.
    /// </summary>
    public static class ColonistPanel
    {
        public static readonly INavPanel Instance = new NavPanelAdapter(
            isOpen:      () => IsOpen,
            hidesCursor: () => true,
            handleInput: HandleInput,
            draw:        Draw
        );

        public static bool IsOpen { get; private set; }
        private static GridNavState _nav = new GridNavState { Cols = 1, Wrap = true };
        private static List<Pawn> _colonists = new List<Pawn>();

        private const float PANEL_W = 220f;
        private const float ROW_H = 28f;
        private const float ROW_SPACING = 2f;
        private const float PADDING = 10f;
        private const float HEADER_H = 28f;
        private const float PROMPT_H = 26f;

        public static void Open()
        {
            IsOpen = true;
            _nav.Reset();
            RefreshList();
        }

        public static void Close()
        {
            IsOpen = false;
            _colonists.Clear();
        }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private static void RefreshList()
        {
            _colonists.Clear();
            if (Current.Game == null) return;
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                if (pawn == null) continue;
                _colonists.Add(pawn);
            }
            _nav.Count = _colonists.Count;
            _nav.Clamp();
        }

        public static void HandleInput()
        {
            if (!IsOpen) return;
            if (_colonists.Count == 0) { Close(); return; }

            _nav.Count = _colonists.Count;
            bool dpadPressed = ControllerState.DpadUpEdge || ControllerState.DpadDownEdge
                            || ControllerState.DpadLeftEdge || ControllerState.DpadRightEdge;
            if (_nav.HandleDpad()) { }
            if (dpadPressed) ConsoleHUD.ConsumeInput();
            else if (ControllerState.ButtonAEdge)
            {
                SelectCurrent();
                ConsoleHUD.ConsumeInput();
            }
            else if (ControllerState.ButtonBEdge)
            {
                Close();
                ConsoleHUD.ConsumeInput();
            }
        }

        private static void SelectCurrent()
        {
            if (_nav.Index < 0 || _nav.Index >= _colonists.Count) return;
                        var pawn = _colonists[_nav.Index];
            if (pawn == null) return;
            Find.Selector?.ClearSelection();
            Find.Selector?.Select(pawn);
            CameraJumper.TryJump(pawn);
            Close();
        }

        public static void Draw()
        {
            if (!IsOpen) return;
            RefreshList();
            if (_colonists.Count == 0) { Close(); return; }
            _nav.Count = _colonists.Count;
            _nav.Clamp();

            int count = _colonists.Count;
            float panelH = PADDING * 2 + HEADER_H + (count * (ROW_H + ROW_SPACING) - ROW_SPACING) + PROMPT_H + 4f;
            float panelX = 28f;
            float panelY = (Verse.UI.screenHeight - panelH) / 2f;

            Rect panelRect = new Rect(panelX, panelY, PANEL_W, panelH);
            Widgets.DrawWindowBackground(panelRect);

            // Header
            Rect header = new Rect(panelX + PADDING, panelY + PADDING, PANEL_W - PADDING * 2, HEADER_H);
            Widgets.DrawBoxSolid(header, new Color(0.122f, 0.435f, 0.435f)); // teal #1f6f6f
            ConsoleStyle.Label(header, "Colonists", Color.white, GameFont.Small, TextAnchor.MiddleLeft);

            // Rows
            float rowY = header.yMax + 4f;
            for (int i = 0; i < count; i++)
            {
                var pawn = _colonists[i];
                if (pawn == null) continue;

                Rect row = new Rect(panelX + PADDING, rowY, PANEL_W - PADDING * 2, ROW_H);
                bool sel = i == _nav.Index;

                if (sel)
                {
                    Widgets.DrawBoxSolid(row, new Color(0.118f, 0.118f, 0.118f)); // #1e1e1e
                    GUI.color = new Color(0.784f, 0.510f, 0.165f); // amber #c8822a
                    Widgets.DrawBox(row, 2);
                    GUI.color = Color.white;

                    // Placeholder for cursor snap call - will be refined when NavController is implemented
                    NavController.SnapCursorTo(row);
                }
                else
                {
                    Widgets.DrawBoxSolid(row, new Color(0.102f, 0.102f, 0.102f)); // #1a1a1a
                }

                string label = pawn.LabelShortCap; // "Lastname, Title" format
                ConsoleStyle.Label(new Rect(row.x + 6f, row.y, row.width - 12f, row.height),
                    label, Color.white, GameFont.Tiny, TextAnchor.MiddleLeft);

                rowY += ROW_H + ROW_SPACING;
            }

            // Prompt bar
            Rect prompt = new Rect(panelX + PADDING, panelRect.yMax - PADDING - PROMPT_H, PANEL_W - PADDING * 2, PROMPT_H);
            GlyphRenderer.DrawPromptBar(prompt, ("B", "Back"), ("A", "Select"));
        }
    }
}
