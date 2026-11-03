using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld.Planet;

namespace PeskyGizmo
{
    public static class NotificationPanel
    {
        public static bool IsOpen { get; private set; }
        private static GridNavState _nav = new GridNavState { Cols = 1, Wrap = false };
        private static FieldInfo? _activeAlertsField;

        public static void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen) SoundDefOf.TabOpen.PlayOneShotOnCamera();
            else SoundDefOf.TabClose.PlayOneShotOnCamera();
        }

        public static void Close()
        {
            if (IsOpen) SoundDefOf.TabClose.PlayOneShotOnCamera();
            IsOpen = false;
        }

        public static void HandleInput()
        {
            if (!IsOpen) return;

            var alerts = GetActiveAlerts();
            _nav.Count = alerts.Count;
            
            if (_nav.HandleDpad())
            {
                ConsoleHUD.ConsumeInput();
                return;
            }

            if (ControllerState.ButtonBEdge || ControllerState.ButtonRBEdge)
            {
                Close();
                ConsoleHUD.ConsumeInput();
                return;
            }

            if (ControllerState.ButtonAEdge)
            {
                int idx = _nav.Index;
                if (idx >= 0 && idx < alerts.Count)
                {
                    var alert = alerts[idx];
                    var report = alert.GetReport();
                    if (report.AllCulprits != null)
                    {
                        var culprit = report.AllCulprits.FirstOrDefault();
                        if (culprit.IsValid)
                        {
                            CameraJumper.TryJump(culprit);
                        }
                    }
                }
                ConsoleHUD.ConsumeInput();
                return;
            }
        }

        private static List<Alert> GetActiveAlerts()
        {
            if (Find.UIRoot is UIRoot_Play playRoot && playRoot.alerts != null)
            {
                if (_activeAlertsField == null)
                    _activeAlertsField = AccessTools.Field(typeof(AlertsReadout), "activeAlerts");
                return _activeAlertsField?.GetValue(playRoot.alerts) as List<Alert> ?? new List<Alert>();
            }
            return new List<Alert>();
        }

        public static void Draw()
        {
            if (!IsOpen) return;

            var alerts = GetActiveAlerts();
            if (alerts.Count == 0) { Close(); return; }

            _nav.Count = alerts.Count;
            _nav.Clamp();

            float width = 300f;
            float rowH = 32f;
            float padding = 10f;
            float headerH = 24f;

            float height = padding * 2 + headerH + 4f + alerts.Count * rowH;
            
            // Draw top-right
            float x = Verse.UI.screenWidth - width - 16f;
            float y = 50f;

            Rect bgRect = new Rect(x, y, width, height);
            Widgets.DrawWindowBackground(bgRect);

            Rect header = new Rect(x + padding, y + padding, width - padding * 2, headerH);
            ConsoleStyle.Label(header, "Alerts", ConsoleStyle.Teal, GameFont.Small, TextAnchor.MiddleLeft);

            float cy = header.yMax + 4f;

            for (int i = 0; i < alerts.Count; i++)
            {
                Rect cell = new Rect(x + padding, cy, width - padding * 2, rowH);
                bool sel = (i == _nav.Index);

                if (sel)
                {
                    ConsoleStyle.DrawRoundedRect(cell, ConsoleStyle.CellSelected);
                    GUI.color = ConsoleStyle.Teal;
                    Widgets.DrawBox(cell, 2);
                    GUI.color = Color.white;
                }

                var alert = alerts[i];
                Color textColor = alert.Priority == AlertPriority.Critical ? Color.red : Color.white;
                ConsoleStyle.Label(cell, alert.GetLabel(), textColor, GameFont.Small, TextAnchor.MiddleLeft);

                cy += rowH;
            }
        }
    }
}
