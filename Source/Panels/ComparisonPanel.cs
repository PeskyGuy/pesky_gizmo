using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PeskyGizmo
{
    public static class ComparisonPanel
    {
        public static bool IsOpen { get; private set; }

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
            
            if (ControllerState.ButtonBEdge || ControllerState.DpadDownEdge)
            {
                Close();
                ConsoleHUD.ConsumeInput();
            }
        }

        private static readonly SkillDef[] SkillsToCompare = new[]
        {
            SkillDefOf.Shooting,
            SkillDefOf.Melee,
            SkillDefOf.Medicine,
            SkillDefOf.Cooking,
            SkillDefOf.Plants,
            SkillDefOf.Animals,
            SkillDefOf.Crafting,
            SkillDefOf.Construction
        };

        public static void Draw()
        {
            if (!IsOpen) return;

            var map = Find.CurrentMap;
            if (map == null) { Close(); return; }

            var pawns = map.mapPawns.FreeColonistsSpawned.ToList();
            if (pawns.Count == 0) { Close(); return; }

            float colW = 60f;
            float rowH = 28f;
            float labelW = 100f;
            float padding = 16f;

            float width = labelW + pawns.Count * colW + padding * 2;
            float height = (SkillsToCompare.Length + 1) * rowH + padding * 2;

            float x = (Verse.UI.screenWidth - width) / 2f;
            float y = (Verse.UI.screenHeight - height) / 2f;

            Rect bgRect = new Rect(x, y, width, height);
            Widgets.DrawWindowBackground(bgRect);

            // Draw header
            float cx = x + padding + labelW;
            float cy = y + padding;
            foreach (var p in pawns)
            {
                Rect cell = new Rect(cx, cy, colW, rowH);
                ConsoleStyle.Label(cell, p.LabelShort, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
                cx += colW;
            }

            cy += rowH;

            // Draw rows
            foreach (var skillDef in SkillsToCompare)
            {
                Rect rowLabel = new Rect(x + padding, cy, labelW, rowH);
                ConsoleStyle.Label(rowLabel, skillDef.label.CapitalizeFirst(), ConsoleStyle.TextGray, GameFont.Small, TextAnchor.MiddleLeft);

                cx = x + padding + labelW;

                int maxLevel = -1;
                foreach (var p in pawns)
                {
                    var sr = p.skills?.GetSkill(skillDef);
                    if (sr != null && !sr.TotallyDisabled && sr.Level > maxLevel)
                        maxLevel = sr.Level;
                }

                foreach (var p in pawns)
                {
                    Rect cell = new Rect(cx, cy, colW, rowH);
                    var sr = p.skills?.GetSkill(skillDef);
                    if (sr == null || sr.TotallyDisabled)
                    {
                        ConsoleStyle.Label(cell, "-", ConsoleStyle.TextGray, GameFont.Small, TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        Color c = (sr.Level == maxLevel && maxLevel > 0) ? ConsoleStyle.Teal : Color.white;
                        ConsoleStyle.Label(cell, sr.Level.ToString(), c, GameFont.Small, TextAnchor.MiddleCenter);
                    }
                    cx += colW;
                }

                cy += rowH;
            }
        }
    }
}
