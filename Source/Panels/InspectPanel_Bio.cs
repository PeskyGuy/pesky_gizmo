using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    /// <summary>
    /// Console-native Bio tab — skills, traits, xenotype, backstory, faction
    /// are individually selectable via D-pad. A "clicks" the focused element.
    /// Focused element info appears in a small panel to the left of the stats.
    /// </summary>
    public static partial class InspectPanel
    {
        private const float SKILL_BAR_WIDTH = 140f;
        private const float SKILL_BAR_HEIGHT = 9f;
        private const float SKILL_ROW_H = 24f;
        private const float SKILL_MAX = 20f;
        private const float CHIP_H = 22f;

        // Bio section ids — up/down moves between sections, left/right within.
        private const int SEC_IDENTITY  = 0; // xenotype / ideo / faction chips
        private const int SEC_BACKSTORY = 1; // childhood / adulthood
        private const int SEC_SKILLS    = 2; // skill rows
        private const int SEC_TRAITS    = 3; // trait chips

        // Info tooltip shown to the left of the stats popup for focused element
        private static string _bioInfoText = "";
        private static float _bioInfoTime;

        private const float BIO_X_PAD = 4f;

        private static void DrawCompactBioTab(Rect rect, Pawn pawn)
        {
            _bioFocusRects.Clear();
            _bioFocusActions.Clear();
            _bioFocusInfos.Clear();
            _bioFocusSections.Clear();

            GUI.BeginGroup(rect);
            float y = 0f;
            float maxW = rect.width - BIO_X_PAD * 2f;

            // ── Toolbar (top-right): Info card + Select-next-in-cell ───────────────
            DrawBioToolbar(rect.width, pawn);

            // ── Portrait + name ──────────────────────────────────────
            const float portraitW = 56f;
            const float portraitH = 70f;
            float infoX = BIO_X_PAD + portraitW + 8f;
            float infoW = maxW - portraitW - 8f;

            Rect portraitRect = new Rect(BIO_X_PAD, y, portraitW, portraitH);
            Widgets.DrawBox(portraitRect, 1);
            try
            {
                RenderTexture portraitTex = PortraitsCache.Get(pawn, new Vector2(portraitW, portraitH),
                    Rot4.South, new Vector3(0f, 0f, 0.3f), 1.28205f,
                    supersample: false, compensateForUIScale: true);
                GUI.DrawTexture(portraitRect.ContractedBy(1f), (Texture)portraitTex);
            }
            catch { /* Portrait rendering may fail on invalid pawn state */ }

            ConsoleStyle.Label(new Rect(infoX, y, infoW, 20f), pawn.LabelNoCount,
                ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.UpperLeft);
            y += 20f;

            // ── Chips row 1: Xenotype, Ideoligion, Faction ──────────────────
            float chipX = infoX;
            float chipY = y;
            const float chipH = CHIP_H;
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;

            // Xenotype (Biotech)
            if (ModsConfig.BiotechActive)
            {
                string? xeno = null;
                Texture2D? xenoIcon = null;
                try { xeno = pawn.genes?.XenotypeLabel; xenoIcon = pawn.genes?.XenotypeIcon; } catch { /* Optional Biotech data; best-effort probe */ }
                if (!string.IsNullOrEmpty(xeno))
                {
                    float cw = Mathf.Min(Text.CalcSize(xeno).x + 40f, infoW);
                    if (chipX + cw > maxW && chipX > infoX) { chipX = infoX; chipY += chipH + 3f; }
                    Rect xr = new Rect(chipX, chipY, cw, chipH);
                    string xenoInfo = BuildXenotypeInfo(pawn);
                    var pawnRefX = pawn;
                    AddFocusRect(xr, () =>
                    {
                        ShowBioInfo(xenoInfo);
                        if (ModsConfig.BiotechActive && pawnRefX.genes != null)
                            Find.WindowStack.Add(new Dialog_ViewGenes(pawnRefX));
                    }, xenoInfo, SEC_IDENTITY);
                    DrawChip(xr, xeno!, true, xenoIcon, null);
                    chipX += cw + 4f;
                }
            }

            // Ideoligion (Ideology)
            if (ModsConfig.IdeologyActive)
            {
                string? ideoName = null;
                Texture2D? ideoIcon = null;
                Color? ideoColor = null;
                try { ideoName = pawn.Ideo?.name; ideoIcon = pawn.Ideo?.Icon; ideoColor = pawn.Ideo?.Color; } catch { /* Optional Ideology data; best-effort probe */ }
                if (!string.IsNullOrEmpty(ideoName))
                {
                    float cw = Mathf.Min(Text.CalcSize(ideoName).x + 40f, infoW);
                    if (chipX + cw > maxW && chipX > infoX) { chipX = infoX; chipY += chipH + 3f; }
                    Rect ir = new Rect(chipX, chipY, cw, chipH);
                    string ideoInfo = BuildIdeoInfo(pawn);
                    AddFocusRect(ir, () => { ShowBioInfo(ideoInfo); Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Ideos); }, ideoInfo, SEC_IDENTITY);
                    DrawChip(ir, ideoName!, false, ideoIcon, ideoColor);
                    chipX += cw + 4f;
                }
            }

            // Faction
            if (pawn.Faction != null)
            {
                string fac = pawn.Faction.Name;
                Texture2D? facIcon = pawn.Faction.def?.FactionIcon;
                Color? facColor = pawn.Faction.Color;
                float cw = Mathf.Min(Text.CalcSize(fac).x + 40f, infoW);
                if (chipX + cw > maxW + BIO_X_PAD && chipX > infoX) { chipX = infoX; chipY += chipH + 3f; }
                Rect fr = new Rect(chipX, chipY, cw, chipH);
                string facInfo = BuildFactionInfo(pawn.Faction);
                AddFocusRect(fr, () => { ShowBioInfo(facInfo); Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Factions); }, facInfo, SEC_IDENTITY);
                DrawChip(fr, fac, false, facIcon, facColor);
            }
            y = chipY + chipH + 4f;

            // Equipped weapon
            string equipped = GetEquippedString(pawn);
            if (!string.IsNullOrEmpty(equipped))
            {
                ConsoleStyle.Label(new Rect(infoX, y, infoW, 15f),
                    equipped, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.UpperLeft);
                y += 17f;
            }

            if (y < portraitH + 2f) y = portraitH + 2f;
            y += 2f;

            // ── Backstory chips ─────────────────────────────────────────────
            if (pawn.story?.Childhood != null || pawn.story?.Adulthood != null)
            {
                float bsX = 0f;
                float bsY = y;
                if (pawn.story.Childhood != null)
                {
                    string label = pawn.story.Childhood.title.CapitalizeFirst();
                    float cw = Mathf.Min(Text.CalcSize(label).x + 22f, maxW * 0.55f);
                    Rect cr = new Rect(BIO_X_PAD + bsX, bsY, cw, chipH);
                    var childRef = pawn.story.Childhood;
                    string childInfo = childRef.FullDescriptionFor(pawn).Resolve();
                    AddFocusRect(cr, () => ShowBioInfo(childInfo), childInfo, SEC_BACKSTORY);
                    DrawChip(cr, label, false);
                    bsX += cw + 4f;
                }
                if (pawn.story.Adulthood != null)
                {
                    string label = pawn.story.Adulthood.title.CapitalizeFirst();
                    float cw = Mathf.Min(Text.CalcSize(label).x + 22f, maxW - bsX - 4f);
                    Rect cr = new Rect(BIO_X_PAD + bsX, bsY, cw, chipH);
                    var adultRef = pawn.story.Adulthood;
                    string adultInfo = adultRef.FullDescriptionFor(pawn).Resolve();
                    AddFocusRect(cr, () => ShowBioInfo(adultInfo), adultInfo, SEC_BACKSTORY);
                    DrawChip(cr, label, false);
                }
                y = bsY + chipH + 6f;
            }

            // ── Skills ──────────────────────────────────────────────────────
            var skills = pawn.skills?.skills;
            if (skills != null && skills.Count > 0)
            {
                ConsoleStyle.Label(new Rect(BIO_X_PAD, y, maxW, 18f), "Skills",
                    ConsoleStyle.Teal, GameFont.Tiny, TextAnchor.UpperLeft);
                y += 20f;

                foreach (var sk in skills)
                {
                    if (sk == null) continue;
                    Rect rowRect = new Rect(BIO_X_PAD, y, maxW, SKILL_ROW_H);
                    string info = SkillInfoText(sk);
                    AddFocusRect(rowRect, () => ShowBioInfo(info), info, SEC_SKILLS);
                    DrawSkillRow(rowRect, sk);
                    y += SKILL_ROW_H + 1f;
                }
                y += 4f;
            }

            // ── Traits ──────────────────────────────────────────────────────
            var traits = pawn.story?.traits?.allTraits;
            if (traits != null && traits.Count > 0)
            {
                ConsoleStyle.Label(new Rect(BIO_X_PAD, y, maxW, 18f), "Traits",
                    ConsoleStyle.Teal, GameFont.Tiny, TextAnchor.UpperLeft);
                y += 20f;

                float cX = 0f, cY = y;
                foreach (var t in traits)
                {
                    if (t == null) continue;
                    string label = t.LabelCap;
                    float tw = Text.CalcSize(label).x + 18f;
                    if (cX + tw > maxW && cX > 0f) { cX = 0f; cY += chipH + 3f; }
                    Rect cr = new Rect(BIO_X_PAD + cX, cY, tw, chipH);
                    var traitRef = t;
                    var pawnRef = pawn;
                    AddFocusRect(cr, () => ShowBioInfo(traitRef.TipString(pawnRef)), traitRef.TipString(pawn), SEC_TRAITS);
                    DrawChip(cr, label, false);
                    cX += tw + 4f;
                }
                y = cY + chipH + 8f;
            }

            // ── Disabled Work Types ─────────────────────────────────────────
            var disabled = GetDisabledWorkLabels(pawn);
            if (disabled.Count > 0)
            {
                ConsoleStyle.Label(new Rect(BIO_X_PAD, y, maxW, 18f), "Incapable of",
                    ConsoleStyle.Teal, GameFont.Tiny, TextAnchor.UpperLeft);
                y += 20f;

                float dwX = 0f, dwY = y;
                foreach (var dw in disabled)
                {
                    float dw2 = Text.CalcSize(dw).x + 16f;
                    if (dwX + dw2 > maxW && dwX > 0f) { dwX = 0f; dwY += chipH + 3f; }
                    Rect dwRect = new Rect(BIO_X_PAD + dwX, dwY, dw2, chipH);
                    Widgets.DrawBoxSolid(dwRect, new Color(0.25f, 0.12f, 0.12f));
                    ConsoleStyle.Label(dwRect, dw, new Color(0.9f, 0.5f, 0.5f),
                        GameFont.Tiny, TextAnchor.MiddleCenter);
                    dwX += dw2 + 4f;
                }
                y = dwY + chipH + 8f;
            }

            Text.Font = prevFont;

            // ── Selection highlight ─────────────────────────────────────────
            if (_bioFocusIndex >= 0 && _bioFocusIndex < _bioFocusRects.Count)
            {
                Rect fr = _bioFocusRects[_bioFocusIndex];
                GUI.color = ConsoleStyle.Teal;
                Widgets.DrawBox(fr.ExpandedBy(2f), 2);
                GUI.color = Color.white;
            }

            GUI.EndGroup();
        }

        // ── Toolbar ──────────────────────────────────────────────

        private const int SEC_TOOLBAR = -1; // toolbar buttons live above all sections

        private static void DrawBioToolbar(float groupWidth, Pawn pawn)
        {
            const float BTN = 24f;
            const float GAP = 4f;
            float bx = groupWidth - BIO_X_PAD - BTN;
            float by = 0f;

            // Select next thing in same square (only when single-selected and applicable).
            var inspectTab = MainButtonDefOf.Inspect.TabWindow as MainTabWindow_Inspect;
            if (inspectTab != null && inspectTab.ShouldShowSelectNextInCellButton)
            {
                Rect nr = new Rect(bx, by, BTN, BTN);
                if (Widgets.ButtonImage(nr, TexButton.SelectOverlappingNext))
                    inspectTab.SelectNextInCell();
                AddFocusRect(nr, () => { inspectTab.SelectNextInCell(); }, "Select next thing in same square", SEC_TOOLBAR);
                bx -= BTN + GAP;
            }

            // Info card.
            {
                Rect ir = new Rect(bx, by, BTN, BTN);
                if (Widgets.ButtonImage(ir, TexButton.Info))
                    Find.WindowStack.Add(new Dialog_InfoCard(pawn));
                var pawnRef = pawn;
                AddFocusRect(ir, () => { Find.WindowStack.Add(new Dialog_InfoCard(pawnRef)); }, "View information", SEC_TOOLBAR);
            }
        }

        // ── Focus helpers ──────────────────────────────────────

        private static void AddFocusRect(Rect r, Action action, string? info, int section)
        {
            _bioFocusRects.Add(r);
            _bioFocusActions.Add(() => { action?.Invoke(); });
            _bioFocusInfos.Add(info ?? "");
            _bioFocusSections.Add(section);
        }

        /// <summary>Spatial 2D nav: pick the nearest focusable rect in the given d-pad
        /// direction (dx, dy). Uses center-to-center distance with a perpendicular-axis
        /// penalty so movement feels axis-aligned but degrades gracefully when no
        /// candidate sits directly along the axis.</summary>
        public static void NavigateBioSpatial(int dx, int dy)
        {
            int n = _bioFocusRects.Count;
            if (n == 0) { _bioFocusIndex = -1; return; }
            if (_bioFocusIndex < 0 || _bioFocusIndex >= n) { _bioFocusIndex = 0; UpdateBioInfo(); return; }

            Rect cur = _bioFocusRects[_bioFocusIndex];

            int best = -1;
            float bestCost = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                if (i == _bioFocusIndex) continue;
                Rect r = _bioFocusRects[i];

                // Primary-axis edge gap — must be strictly positive in pressed direction.
                float primary;
                if (dy > 0)      primary = r.yMin - cur.yMax;
                else if (dy < 0) primary = cur.yMin - r.yMax;
                else if (dx > 0) primary = r.xMin - cur.xMax;
                else             primary = cur.xMin - r.xMax;
                if (primary <= 0f) continue;

                // Perpendicular-axis gap: 0 if the rects overlap on that axis,
                // otherwise the distance between nearest edges. Overlap means the
                // candidate is "directly above/below/beside" the current rect and
                // should win against narrower-aligned but more distant candidates.
                float perp;
                if (dy != 0)
                {
                    float overlap = Mathf.Min(cur.xMax, r.xMax) - Mathf.Max(cur.xMin, r.xMin);
                    perp = overlap >= 0f ? 0f : -overlap;
                }
                else
                {
                    float overlap = Mathf.Min(cur.yMax, r.yMax) - Mathf.Max(cur.yMin, r.yMin);
                    perp = overlap >= 0f ? 0f : -overlap;
                }

                // Heavy perpendicular penalty so we never skip a directly-aligned item
                // for a closer-but-misaligned one.
                float cost = primary + perp * 4f;

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }

            if (best >= 0)
            {
                _bioFocusIndex = best;
                UpdateBioInfo();
                EnsureBioFocusVisible();
            }
        }

        /// <summary>Adjust the content scroll so the currently focused rect is fully visible.</summary>
        private static void EnsureBioFocusVisible()
        {
            if (_bioFocusIndex < 0 || _bioFocusIndex >= _bioFocusRects.Count) return;
            Rect r = _bioFocusRects[_bioFocusIndex];
            // Bio is drawn inside a scroll group of size ~contentInner. The visible
            // band in local coords is [_contentScroll.y, _contentScroll.y + viewportH].
            // viewportH is approximated by _bioPopupRect height — close enough for clamping.
            float viewportH = _bioPopupRect.height - 120f;
            if (viewportH < 80f) viewportH = 400f;
            float top = r.y;
            float bot = r.yMax;
            if (top < _contentScroll.y) _contentScroll.y = Mathf.Max(0f, top - 8f);
            else if (bot > _contentScroll.y + viewportH) _contentScroll.y = bot - viewportH + 8f;
        }

        private static void UpdateBioInfo()
        {
            if (_bioFocusIndex >= 0 && _bioFocusIndex < _bioFocusInfos.Count)
                ShowBioInfo(_bioFocusInfos[_bioFocusIndex]);
        }

        public static void ActivateBioFocus()
        {
            if (_bioFocusIndex < 0 || _bioFocusIndex >= _bioFocusActions.Count) return;
            try { _bioFocusActions[_bioFocusIndex]?.Invoke(); }
            catch (Exception ex) { Log.Error($"[Gizmo] Bio focus activate failed: {ex.Message}"); }
        }

        private static void ShowBioInfo(string info)
        {
            _bioInfoText = info;
            _bioInfoTime = Time.unscaledTime;
        }

        /// <summary>Draw info tooltip to the left of the stats popup.</summary>
        public static void DrawBioInfoTooltip(Rect statsPopupRect)
        {
            if (string.IsNullOrEmpty(_bioInfoText)) return;
            if (Time.unscaledTime - _bioInfoTime > 8f) { _bioInfoText = ""; return; }

            float w = 260f;
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float textH = Text.CalcHeight(_bioInfoText, w - 20f);
            Text.Font = prevFont;
            float h = Mathf.Clamp(textH + 24f, 48f, 400f);
            Rect infoRect = new Rect(statsPopupRect.x - w - 12f, statsPopupRect.y + 40f, w, h);
            if (infoRect.x < 4f) infoRect.x = 4f;
            Widgets.DrawWindowBackground(infoRect);
            Widgets.DrawBox(infoRect, 1);
            ConsoleStyle.Label(infoRect.ContractedBy(10f), _bioInfoText,
                ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.UpperLeft);
        }

        // ── Tooltip builders ───────────────────────────────────────────────

        private static string BuildXenotypeInfo(Pawn pawn)
        {
            try
            {
                var genes = pawn.genes;
                if (genes == null) return "";
                string label = genes.XenotypeLabelCap;
                string desc = genes.XenotypeDescShort ?? "";
                return $"{label}\n\n{desc}";
            }
            catch { /* Optional Biotech data; best-effort probe */ return ""; }
        }

        private static string BuildIdeoInfo(Pawn pawn)
        {
            try
            {
                var ideo = pawn.Ideo;
                if (ideo == null) return "";
                string desc = ideo.description ?? "";
                var role = ideo.GetRole(pawn);
                string roleStr = role != null ? $"\nRole: {role.LabelForPawn(pawn)}" : "";
                return $"{ideo.name}{roleStr}\n\n{desc}";
            }
            catch { /* Optional Ideology data; best-effort probe */ return ""; }
        }

        private static string BuildFactionInfo(Faction faction)
        {
            try
            {
                if (faction == null) return "";
                if (faction.IsPlayer)
                    return $"{faction.Name}\n{faction.def.LabelCap}";
                string rel = faction.PlayerRelationKind.GetLabelCap();
                int goodwill = faction.PlayerGoodwill;
                return $"{faction.Name}\n{faction.def.LabelCap}\n\nRelation: {rel} ({goodwill})";
            }
            catch { /* Faction data may be incomplete for world-object pawns */ return ""; }
        }

        private static string SkillInfoText(SkillRecord sk)
        {
            return $"{sk.def.skillLabel.CapitalizeFirst()}\n" +
                   $"Level: {sk.Level}  ·  {sk.LevelDescriptor}\n" +
                   $"Passion: {sk.passion.GetLabel()}\n" +
                   $"XP: {sk.xpSinceLastLevel:F0} / {sk.XpRequiredForLevelUp}\n" +
                   (sk.LearningSaturatedToday ? "Learning saturated today\n" : "");
        }

        // ── Drawing ────────────────────────────────────────────────────────

        private static void DrawChip(Rect r, string label, bool accent, Texture2D? icon = null, Color? iconColor = null)
        {
            Color bg = accent ? new Color(0.122f, 0.435f, 0.435f) : new Color(0.15f, 0.15f, 0.18f);
            Widgets.DrawBoxSolid(r, bg);
            float iconOffset = 0f;
            if (icon != null)
            {
                float iconSize = r.height - 4f;
                Rect iconRect = new Rect(r.x + 3f, r.y + 2f, iconSize, iconSize);
                Color prev = GUI.color;
                GUI.color = iconColor ?? Color.white;
                GUI.DrawTexture(iconRect, (Texture)icon);
                GUI.color = prev;
                iconOffset = iconSize + 2f;
            }
            Rect labelRect = new Rect(r.x + iconOffset, r.y, r.width - iconOffset, r.height);
            ConsoleStyle.Label(labelRect, label, accent ? Color.white : ConsoleStyle.TextWhite,
                GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static void DrawSkillRow(Rect row, SkillRecord sk)
        {
            float labelW = 110f;
            float barX = row.x + labelW;
            float barW = SKILL_BAR_WIDTH;
            float valX = barX + barW + 10f;

            ConsoleStyle.Label(new Rect(row.x, row.y, labelW - 4f, row.height),
                sk.def.skillLabel.CapitalizeFirst(), ConsoleStyle.TextWhite,
                GameFont.Small, TextAnchor.MiddleLeft);

            Rect trackRect = new Rect(barX, row.y + (row.height - SKILL_BAR_HEIGHT) / 2f, barW, SKILL_BAR_HEIGHT);
            Widgets.DrawBoxSolid(trackRect, new Color(0.2f, 0.2f, 0.2f));

            float fill = Mathf.Clamp01(sk.Level / SKILL_MAX);
            if (fill > 0f)
            {
                Rect fillRect = new Rect(trackRect.x + 1f, trackRect.y + 1f,
                    (trackRect.width - 2f) * fill, trackRect.height - 2f);
                Widgets.DrawBoxSolid(fillRect, new Color(0.165f, 0.616f, 0.561f));
            }

            ConsoleStyle.Label(new Rect(valX, row.y, 28f, row.height), sk.Level.ToString(),
                ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);

            Texture2D? passionTex = VSEIntegration.GetPassionIcon(sk.passion);
            if (passionTex != null)
            {
                Rect flameRect = new Rect(valX + 26f, row.y + 2f, 20f, 20f);
                GUI.DrawTexture(flameRect, (Texture)passionTex);
            }
        }

        // ── Data helpers ───────────────────────────────────────────────────

        private static string GetEquippedString(Pawn pawn)
        {
            try
            {
                if (pawn.equipment?.Primary == null) return "";
                var eq = pawn.equipment.Primary;
                string label = "Equipped: " + eq.LabelCapNoCount;
                string qual = eq.GetComp<CompQuality>()?.Quality.GetLabel() ?? "";
                return string.IsNullOrEmpty(qual) ? label : $"{label} ({qual})";
            }
            catch { /* Equipment data may be stale on despawned pawns */ return ""; }
        }

        private static List<string> GetDisabledWorkLabels(Pawn pawn)
        {
            var list = new List<string>();
            try
            {
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefs)
                    if (pawn.WorkTypeIsDisabled(wt))
                        list.Add(wt.LabelCap);
            }
            catch { /* Work-type probe may fail on modded work defs */ }
            list.RemoveAll(s => string.IsNullOrWhiteSpace(s));
            return list;
        }
    }
}
