using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class CombatHUD
    {
        private const float BAR_W = 110f;
        private const float ROW_H = 32f;
        private const float PAD = 4f;
        private const float CARD_W = 52f;
        private const float CARD_H = 64f;

        public static bool IsCombatMode
        {
            get
            {
                if (Current.Game == null || Find.CurrentMap == null) return false;
                var sel = Find.Selector;
                if (sel == null || sel.NumSelected == 0) return false;
                return sel.SelectedObjectsListForReading.OfType<Pawn>()
                    .Any(p => p.IsColonistPlayerControlled && p.drafter != null
                              && p.drafter.Drafted && !p.Dead && !p.Downed);
            }
        }

        public static void Draw()
        {
            if (!IsCombatMode) return;
            var pawns = DraftedSelected();
            if (pawns.Count == 0) return;
            Pawn lead = pawns[0];

            float barH = 3f * ROW_H + PAD * 2f;
            Rect bar = new Rect(PAD, 200f, BAR_W, barH);
            DrawActionBar(bar, lead);

            float cardX = bar.xMax + PAD;
            for (int i = 0; i < pawns.Count; i++)
                DrawPawnCard(new Rect(cardX, bar.y + i * (CARD_H + PAD), CARD_W, CARD_H), pawns[i]);
        }

        private static List<Pawn> DraftedSelected()
        {
            return Find.Selector.SelectedObjectsListForReading
                .OfType<Pawn>()
                .Where(p => p.IsColonistPlayerControlled && p.drafter != null
                            && p.drafter.Drafted && !p.Dead && !p.Downed)
                .ToList();
        }

        private static void DrawActionBar(Rect r, Pawn lead)
        {
            GUI.BeginGroup(r);
            GUI.DrawTexture(new Rect(0, 0, r.width, r.height), TexUI.GrayBg);
            bool ltHeld = ControllerState.IsButtonDown(GizmoMod.Settings.btnLT);

            string moveLabel = ltHeld ? "Cover move" : "Move";
            DrawActionRow(new Rect(0, PAD, r.width, ROW_H), "A", moveLabel, MoveTargetLabel());

            string atkLabel = CombatCommands.ForceMelee ? "Attack [Melee]" : "Attack";
            DrawActionRow(new Rect(0, PAD + ROW_H, r.width, ROW_H), "X", atkLabel, AttackTargetLabel());

            string abLabel = CombatCommands.CurrentAbilityLabel(lead);
            DrawActionRow(new Rect(0, PAD + ROW_H * 2f, r.width, ROW_H), "RB", "Ability", abLabel);
            GUI.EndGroup();
        }

        private static void DrawActionRow(Rect r, string glyph, string label, string detail)
        {
            float gW = 22f;
            Rect gRect = new Rect(r.x + 2f, r.y + (r.height - 20f) / 2f, gW, 20f);
            Rect tRect = new Rect(gRect.xMax + 3f, r.y + 2f, r.width - gRect.xMax - 5f, r.height - 4f);

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(gRect, glyph);
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = Color.white;
            string full = detail.NullOrEmpty() ? label : (label + "\n" + detail);
            Widgets.Label(tRect, full);
            Text.Font = GameFont.Small;
        }

        private static string MoveTargetLabel()
        {
            var cell = ReticleController.CurrentCell;
            var map = Find.CurrentMap;
            if (map == null || !cell.InBounds(map)) return "";
            return cell.GetTerrain(map).label.CapitalizeFirst();
        }

        private static string AttackTargetLabel()
        {
            Thing? t = ReticleController.HoveredThing;
            if (t == null) return "No target";
            return t.LabelCapNoCount;
        }

        private static void DrawPawnCard(Rect r, Pawn p)
        {
            GUI.BeginGroup(r);
            GUI.DrawTexture(new Rect(0, 0, r.width, r.height), TexUI.GrayBg);
            if (CombatCommands.IsHolding(p))
            {
                GUI.color = new Color(0.15f, 0.35f, 0.15f, 0.25f);
                GUI.DrawTexture(new Rect(0, 0, r.width, r.height), BaseContent.WhiteTex);
                GUI.color = Color.white;
            }

            var head = PortraitsCache.Get(p, new Vector2(r.width - 2f, r.width - 2f),
                Rot4.South, default, 1f, true, true, false, false);
            if (head != null)
                GUI.DrawTexture(new Rect(1f, 1f, r.width - 2f, r.width - 2f), head);

            float hpPct = p.health.summaryHealth.SummaryHealthPercent;
            Color hpCol = hpPct < 0.33f ? Color.red : (hpPct < 0.66f ? Color.yellow : new Color(0.15f, 0.75f, 0.15f));
            float barY = r.width;
            GUI.color = hpCol;
            Widgets.FillableBar(new Rect(2f, barY, r.width - 4f, 4f), hpPct, BaseContent.WhiteTex, TexUI.GrayBg, false);
            GUI.color = Color.white;

            string wep = p.equipment?.Primary?.LabelCapNoCount ?? "Unarmed";
            Thing? curTarget = p.CurJob?.targetA.Thing;
            string target = curTarget != null ? ("→ " + curTarget.LabelCapNoCount) : "";
            string line = target.Length > 0 ? (wep + " " + target) : wep;
            float avail = r.width - 4f;
            float h = Text.CalcHeight(line, avail);
            Rect textR = new Rect(2f, barY + 5f, avail, r.height - barY - 5f);
            if (h > textR.height)
            {
                int len = line.Length;
                while (len > 3 && Text.CalcHeight(line.Substring(0, len) + "…", avail) > textR.height)
                    len--;
                line = line.Substring(0, len) + "…";
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(textR, line);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.EndGroup();
        }
    }
}
