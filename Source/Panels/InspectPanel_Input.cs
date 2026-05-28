using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static partial class InspectPanel
    {
        public static void HandleInput()
        {
            RefreshForCurrentSelection();
            if (_gizmoActions.Count == 0 && _tabActions.Count == 0) return;

            SyncNavCounts();

            if (_mode == Mode.Tabs) HandleGridInput();
            else HandleContentInput();
        }

        public static void HandleAmbientInput()
        {
            RefreshForCurrentSelection();
            int total = _gizmoActions.Count + _tabActions.Count;
            if (total == 0) return;

            // Don't navigate ambient grid while content popup is open.
            if (InContentMode) return;

            SyncNavCounts();

            bool anyDpad = ControllerState.DpadLeftRepeat || ControllerState.DpadRightRepeat
                        || ControllerState.DpadUpRepeat || ControllerState.DpadDownRepeat;
            if (anyDpad)
            {
                bool wasFocused = AmbientFocused;
                AmbientFocused = true;
                LastFocusActivityTime = Time.unscaledTime;
                if (!wasFocused) LastNavTime = Time.unscaledTime;
            }

            SanitiseFocus();

            if (_focusedSection == Section.Tabs)
                HandleTabStripInput();
            else
                HandleGizmoGridInput();

            // Always consume D-pad while ambient is focused to prevent edge
            // presses from leaking to time-speed or other ambient handlers.
            if (anyDpad)
                ConsoleHUD.ConsumeInput();

            if (ControllerState.ButtonAEdge && AmbientFocused)
            {
                ActivateSelected();
                LastNavTime = 0;
                ConsoleHUD.ConsumeInput();
            }
        }

        private static void SyncNavCounts()
        {
            _gizmoNav.Count = _gizmoActions.Count;
            _tabNav.Count = _tabActions.Count;
        }

        private static void HandleTabStripInput()
        {
            if (ControllerState.DpadUpRepeat)
            {
                if (_tabNav.Up())
                    LastNavTime = Time.unscaledTime;
            }
            else if (ControllerState.DpadDownRepeat)
            {
                if (_tabNav.Down())
                    LastNavTime = Time.unscaledTime;
            }
            else if (ControllerState.DpadRightRepeat)
            {
                if (TrySwitchSectionDown())
                    LastNavTime = Time.unscaledTime;
            }
        }

        private static void HandleGizmoGridInput()
        {
            if (ControllerState.DpadLeftRepeat)
            {
                int col = _gizmoNav.Index % COLS;
                if (col == 0)
                {
                    if (TrySwitchSectionUp())
                        LastNavTime = Time.unscaledTime;
                }
                else if (_gizmoNav.Left())
                {
                    LastNavTime = Time.unscaledTime;
                }
            }
            else if (ControllerState.DpadRightRepeat)
            {
                if (_gizmoNav.Right())
                    LastNavTime = Time.unscaledTime;
            }
            else if (ControllerState.DpadUpRepeat)
            {
                if (_gizmoNav.Up())
                    LastNavTime = Time.unscaledTime;
            }
            else if (ControllerState.DpadDownRepeat)
            {
                if (_gizmoNav.Down())
                    LastNavTime = Time.unscaledTime;
            }
        }

        private static void ActivateSelected()
        {
            var action = FocusedAction;
            if (action == null) return;

            if (action.Value.Gizmo != null)
            {
                GizmoController.RequestActivation(action.Value.Gizmo);
                if (IsActive) Close();
            }
            else if (action.Value.Tab != null)
            {
                _capturedSelection = CurrentSelection();
                _lastSelectionCount = SelectionAdapter.NumSelected;
                try { action.Value.Tab.OnOpen(); } catch { }
                _mode = Mode.Content;
                _contentScroll = Vector2.zero;
                ConsoleHUD.ActivePanel = ConsolePanel.Inspect;
            }
        }

        private static void HandleGridInput()
        {
            bool anyDpad = ControllerState.DpadUpRepeat || ControllerState.DpadDownRepeat
                        || ControllerState.DpadLeftRepeat || ControllerState.DpadRightRepeat;

            if (_focusedSection == Section.Tabs)
            {
                if (ControllerState.DpadUpRepeat)
                {
                    _tabNav.Up();
                }
                else if (ControllerState.DpadDownRepeat)
                {
                    _tabNav.Down();
                }
                else if (ControllerState.DpadRightRepeat)
                {
                    TrySwitchSectionDown();
                }
            }
            else
            {
                if (ControllerState.DpadLeftRepeat)
                {
                    int col = _gizmoNav.Index % COLS;
                    if (col == 0)
                    {
                        TrySwitchSectionUp();
                    }
                    else
                    {
                        _gizmoNav.Left();
                    }
                }
                else if (ControllerState.DpadRightRepeat)
                {
                    _gizmoNav.Right();
                }
                else if (ControllerState.DpadUpRepeat)
                {
                    _gizmoNav.Up();
                }
                else if (ControllerState.DpadDownRepeat)
                {
                    _gizmoNav.Down();
                }
            }

            if (anyDpad)
            {
                LastNavTime = Time.unscaledTime;
                ConsoleHUD.ConsumeInput();
            }

            if (ControllerState.ButtonAEdge)
            {
                ActivateSelected();
                ConsoleHUD.ConsumeInput();
                return;
            }

            if (ControllerState.ButtonBEdge)
            {
                Close();
                ConsoleHUD.ConsumeInput();
            }
        }

        private static bool TrySwitchSectionDown()
        {
            if (_focusedSection == Section.Tabs && _gizmoActions.Count > 0)
            {
                _focusedSection = Section.Gizmos;
                int row = _tabNav.Index;
                _gizmoNav.Index = Mathf.Min(row * COLS, _gizmoActions.Count - 1);
                return true;
            }
            return false;
        }

        private static bool TrySwitchSectionUp()
        {
            if (_focusedSection == Section.Gizmos && _tabActions.Count > 0)
            {
                _focusedSection = Section.Tabs;
                int row = _gizmoNav.Index / COLS;
                _tabNav.Index = Mathf.Min(row, _tabActions.Count - 1);
                return true;
            }
            return false;
        }

        public static bool CursorInsidePanel()
        {
            Vector2 guiMouse = new Vector2(
                ControllerState.CursorScreenPos.x / Prefs.UIScale,
                Verse.UI.screenHeight - (ControllerState.CursorScreenPos.y / Prefs.UIScale));
            return _lastPanelRect.Contains(guiMouse);
        }

        public static float AmbientPanelRight =>
            HasAmbient
                ? (COLS * CELL_SIZE + (COLS - 1) * CELL_SPACING + PANEL_MARGIN * 2 + 8f)
                : 8f;

        public static bool IsOverAmbientPanel(Vector2 guiPos) =>
            HasAmbient && _lastPanelRect.width > 0 && _lastPanelRect.Contains(guiPos);
    }
}
