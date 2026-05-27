using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class FloatMenuNavHelper
    {
        private static List<FloatMenuOption> _options = new List<FloatMenuOption>();
        private static List<Rect>            _optionRects = new List<Rect>();
        private static int                   _index;
        private static FloatMenu?            _trackedMenu;

        public static bool IsTracking => _trackedMenu != null && Find.WindowStack.IsOpen(_trackedMenu);

        public static void HandleInput()
        {
            if (!IsTracking) { _trackedMenu = null; return; }
            if (_options.Count == 0) return;

            var nav = new GridNavState { Count = _options.Count, Cols = 1, Wrap = true };
            nav.Index = _index;

            if (nav.HandleDpad()) { _index = nav.Index; ConsoleHUD.ConsumeInput(); HapticManager.UI_DpadNav(); }
            if (ControllerState.ButtonAEdge) { ActivateCurrent(); ConsoleHUD.ConsumeInput(); }
            if (ControllerState.ButtonBEdge) { _trackedMenu?.Close(); _trackedMenu = null; ConsoleHUD.ConsumeInput(); }

            if (ControllerState.DpadLeftEdge || ControllerState.DpadRightEdge) ConsoleHUD.ConsumeInput();
        }

        public static void Draw()
        {
            if (!IsTracking || _index < 0 || _index >= _optionRects.Count) return;
            if (Event.current?.type != EventType.Repaint) return;
            NavHelper.DrawSelectionHighlight(_optionRects[_index]);
        }

        private static void ActivateCurrent()
        {
            if (_index < 0 || _index >= _options.Count) return;
            var opt = _options[_index];
            if (opt.Disabled) { HapticManager.UI_InvalidAction(); return; }
            try { opt.action?.Invoke(); } catch { }
            _trackedMenu?.Close();
            _trackedMenu = null;
            HapticManager.UI_Confirm();
        }

        [HarmonyPatch(typeof(FloatMenu), MethodType.Constructor)]
        public static class Patch_FloatMenu_Ctor
        {
            public static void Postfix(FloatMenu __instance, List<FloatMenuOption> options)
            {
                if (!ControllerState.ControllerModeEnabled) return;
                _trackedMenu = __instance;
                _options     = options ?? new List<FloatMenuOption>();
                _index       = 0;
            }
        }
    }
}
