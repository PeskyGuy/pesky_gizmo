using System;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public interface INavPanel
    {
        bool IsOpen { get; }
        bool HidesCursor { get; }
        void HandleInput();
        void Draw();
    }

    public class NavPanelAdapter : INavPanel
    {
        private readonly Func<bool> _isOpen;
        private readonly Func<bool> _hidesCursor;
        private readonly Action _handleInput;
        private readonly Action _draw;

        public NavPanelAdapter(Func<bool> isOpen, Func<bool> hidesCursor, Action handleInput, Action draw)
        {
            _isOpen = isOpen;
            _hidesCursor = hidesCursor;
            _handleInput = handleInput;
            _draw = draw;
        }

        public bool IsOpen => _isOpen();
        public bool HidesCursor => _hidesCursor();
        public void HandleInput() => _handleInput();
        public void Draw() => _draw();
    }

    public static class NavController
    {
        private static readonly INavPanel[] _panels =
        {
            ColonistPanel.Instance,
            ArchitectPanel.Instance,
            InspectPanel.Instance,
            NavHelper.Instance,
        };

        public static bool IsCustomUIActive =>
            ConsoleHUD.ActivePanel != ConsolePanel.None
            || NavMenu.IsOpen
            || OverlayMenu.IsOpen
            || InspectPanel.AmbientFocused;

        public static bool AnyCursorHidingPanelOpen
        {
            get
            {
                foreach (var p in _panels)
                    if (p.IsOpen && p.HidesCursor) return true;
                return false;
            }
        }

        public static void Tick()
        {
            foreach (var p in _panels)
            {
                if (!p.IsOpen) continue;
                p.HandleInput();
                if (ConsoleHUD.InputConsumed) return;
            }
        }

        public static void Draw()
        {
            foreach (var p in _panels)
            {
                if (p.IsOpen) p.Draw();
            }
        }

        public static void CloseAll()
        {
            if (NavMenu.IsOpen) NavMenu.Close();
            if (ColonistPanel.IsOpen) ColonistPanel.Close();
            if (ConsoleHUD.ActivePanel != ConsolePanel.None)
                ConsoleHUD.ActivePanel = ConsolePanel.None;
        }

        public static void SnapCursorTo(Rect localGuiRect)
        {
            if (Event.current?.type != EventType.Repaint) return;
            if (!ControllerState.UsingController) return;

            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(localGuiRect.center); }
            catch { return; }

            Vector2 unityPos = new Vector2(screenCenter.x, Screen.height - screenCenter.y);
            if (Vector2.Distance(ControllerState.CursorScreenPos, unityPos) < 0.5f) return;

            ControllerState.CursorScreenPos = unityPos;

            if (!AnyCursorHidingPanelOpen)
                MouseSimulation.SyncWindowsCursor();
        }
    }
}
