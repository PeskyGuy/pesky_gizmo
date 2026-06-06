using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static partial class InspectPanel
    {
        private const float ConsoleRowH = 32f;
        private const float ConsoleGap = 4f;
        private enum HealthSubtab { Overview, Operations }
        private static HealthSubtab _healthSubtab = HealthSubtab.Overview;
        private static readonly List<Rect> _healthFocusRects = new List<Rect>();
        private static readonly List<Action> _healthFocusActions = new List<Action>();
        private static int _healthFocusIndex;

        private struct OperationEntry
        {
            public string Label;
            public RecipeDef Recipe;
            public BodyPartRecord Part;
            public Texture2D Icon;
            public bool Disabled;
            public string DisabledReason;
        }

        private static readonly List<OperationEntry> _cachedOps = new List<OperationEntry>();
        private static int _opsCacheTick;
        private static Pawn? _opsCachePawn;
        private static Vector2 _opsScroll;

        private static void DrawConsoleHealthTab(Rect rect, Pawn pawn)
        {
            Pawn_HealthTracker health = pawn.health;
            if (health == null)
            {
                ConsoleStyle.Label(rect, "Health unavailable", ConsoleStyle.TextGray, GameFont.Small, TextAnchor.MiddleCenter);
                return;
            }

            _healthFocusRects.Clear();
            _healthFocusActions.Clear();

            GUI.BeginGroup(rect);
            float x = 0f;
            float y = 0f;
            float w = rect.width;

            DrawHealthSubtabs(new Rect(x, y, w, 26f));
            y += 30f;

            if (_healthSubtab == HealthSubtab.Operations)
            {
                DrawConsoleOperations(new Rect(x, y, w, rect.height - y), pawn);
                DrawHealthFocus();
                GUI.EndGroup();
                return;
            }

            float leftW = Mathf.Min(240f, w * 0.34f);
            float rightX = leftW + ConsoleGap;
            float rightW = w - rightX;

            Rect care = new Rect(x, y, leftW, Mathf.Max(280f, rect.height - y));
            DrawConsoleSection(care, "Overview");
            float cy = care.y + 24f;
            DrawClickableInfoLine(new Rect(care.x + 8f, cy, care.width - 16f, 20f), "Food", pawn.foodRestriction?.CurrentFoodPolicy?.label ?? "-", () => OpenFoodMenu(pawn));
            cy += 22f;
            DrawClickableInfoLine(new Rect(care.x + 8f, cy, care.width - 16f, 20f), "Medicine", pawn.playerSettings?.medCare.GetLabel() ?? "-", () => CycleMedicine(pawn));
            cy += 22f;
            DrawClickableInfoLine(new Rect(care.x + 8f, cy, care.width - 16f, 20f), "Self tend", pawn.playerSettings?.selfTend == true ? "On" : "Off", () => ToggleSelfTend(pawn));
            cy += 26f;
            DrawInfoLine(new Rect(care.x + 8f, cy, care.width - 16f, 18f), "Pain", health.hediffSet.PainTotal > 0f ? health.hediffSet.PainTotal.ToStringPercent("F0") : "None");
            cy += 22f;

            IEnumerable<PawnCapacityDef> capacities = GetVisibleCapacities(pawn);
            foreach (PawnCapacityDef def in capacities)
            {
                if (!PawnCapacityUtility.BodyCanEverDoCapacity(pawn.RaceProps.body, def)) continue;
                float pct = health.capacities.GetLevel(def);
                DrawCapacityRow(new Rect(care.x + 8f, cy, care.width - 16f, 18f), def.LabelCap, pct);
                cy += 19f;
            }

            Rect conditions = new Rect(rightX, y, rightW, Mathf.Max(280f, rect.height - y));
            DrawConsoleSection(conditions, "Conditions");
            float hy = conditions.y + 26f;
            var hediffs = health.hediffSet.hediffs
                .Where(h => h != null && h.Visible)
                .OrderByDescending(h => h.BleedRate)
                .ThenByDescending(h => h.Severity)
                .Take(12)
                .ToList();

            if (hediffs.Count == 0)
            {
                ConsoleStyle.Label(new Rect(conditions.x, conditions.y + conditions.height * 0.45f, conditions.width, 20f),
                    "No health conditions", ConsoleStyle.TextMuted, GameFont.Small, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (Hediff h in hediffs)
                {
                    DrawConditionRow(new Rect(conditions.x + 8f, hy, conditions.width - 16f, 26f), h);
                    hy += 28f;
                }
            }

            DrawHealthFocus();
            GUI.EndGroup();
        }

        private static IEnumerable<PawnCapacityDef> GetVisibleCapacities(Pawn pawn)
        {
            if (pawn.RaceProps.Humanlike)
                return DefDatabase<PawnCapacityDef>.AllDefs.Where(c => c.showOnHumanlikes).OrderBy(c => c.listOrder);
            if (pawn.RaceProps.Animal)
                return DefDatabase<PawnCapacityDef>.AllDefs.Where(c => c.showOnAnimals).OrderBy(c => c.listOrder);
            if (pawn.RaceProps.IsAnomalyEntity)
                return DefDatabase<PawnCapacityDef>.AllDefs.Where(c => c.showOnAnomalyEntities).OrderBy(c => c.listOrder);
            if (pawn.RaceProps.IsMechanoid)
                return DefDatabase<PawnCapacityDef>.AllDefs.Where(c => c.showOnMechanoids).OrderBy(c => c.listOrder);
            if (pawn.RaceProps.IsDrone)
                return DefDatabase<PawnCapacityDef>.AllDefs.Where(c => c.showOnDrones).OrderBy(c => c.listOrder);
            return DefDatabase<PawnCapacityDef>.AllDefs.Where(c => c.showOnHumanlikes).OrderBy(c => c.listOrder);
        }

        public static bool HandleConsoleHealthInput()
        {
            if (ControllerState.DpadLeftRepeat || ControllerState.DpadRightRepeat)
            {
                _healthSubtab = _healthSubtab == HealthSubtab.Overview ? HealthSubtab.Operations : HealthSubtab.Overview;
                _healthFocusIndex = 0;
                _opsScroll = Vector2.zero;
                ConsoleHUD.ConsumeInput();
                return true;
            }

            if (_healthFocusRects.Count > 0)
            {
                if (ControllerState.DpadUpRepeat)
                {
                    _healthFocusIndex = Mathf.Max(0, _healthFocusIndex - 1);
                    ConsoleHUD.ConsumeInput();
                    return true;
                }
                if (ControllerState.DpadDownRepeat)
                {
                    _healthFocusIndex = Mathf.Min(_healthFocusRects.Count - 1, _healthFocusIndex + 1);
                    ConsoleHUD.ConsumeInput();
                    return true;
                }
                if (ControllerState.ButtonAEdge)
                {
                    if (_healthFocusIndex >= 0 && _healthFocusIndex < _healthFocusActions.Count)
                        _healthFocusActions[_healthFocusIndex]?.Invoke();
                    ConsoleHUD.ConsumeInput();
                    return true;
                }
            }

            return false;
        }

        private static void DrawConsoleSocialTab(Rect rect, Pawn pawn)
        {
            GUI.BeginGroup(rect);
            float y = 0f;
            float w = rect.width;

            Rect identity = new Rect(0f, y, w, 92f);
            DrawConsoleSection(identity, "Ideology");
            float ix = identity.x + 10f;
            float iy = identity.y + 28f;
            string ideoName = ModsConfig.IdeologyActive && pawn.Ideo != null ? pawn.Ideo.name : pawn.Faction?.Name ?? "Unaffiliated";
            ConsoleStyle.Label(new Rect(ix, iy, identity.width * 0.45f, 24f), ideoName, ConsoleStyle.TextWhite, GameFont.Small);
            if (ModsConfig.IdeologyActive && pawn.ideo != null)
            {
                DrawBarWithLabel(new Rect(identity.x + identity.width * 0.52f, iy, identity.width * 0.38f, 22f),
                    pawn.ideo.Certainty, "Certainty");
                string role = pawn.Ideo?.GetRole(pawn)?.LabelForPawn(pawn) ?? "No role assigned";
                ConsoleStyle.Label(new Rect(ix, iy + 30f, identity.width - 20f, 22f), role, ConsoleStyle.TextGray, GameFont.Tiny);
            }
            y += 102f;

            float leftW = Mathf.Min(330f, w * 0.48f);
            float rightX = leftW + ConsoleGap;
            float rightW = w - rightX;

            Rect relations = new Rect(0f, y, leftW, rect.height - y);
            DrawConsoleSection(relations, "Relations");
            float ry = relations.y + 30f;
            var rels = BuildSocialRows(pawn).Take(10).ToList();
            if (rels.Count == 0)
            {
                ConsoleStyle.Label(new Rect(relations.x, relations.y + relations.height * 0.45f, relations.width, 24f),
                    "No known relations", ConsoleStyle.TextMuted, GameFont.Small, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (var row in rels)
                {
                    DrawSocialRelationRow(new Rect(relations.x + 10f, ry, relations.width - 20f, 34f), row);
                    ry += 38f;
                }
            }

            Rect log = new Rect(rightX, y, rightW, rect.height - y);
            DrawConsoleSection(log, "Recent");
            float ly = log.y + 30f;
            var entries = Find.PlayLog?.AllEntries?
                .Where(e => e != null && e.Concerns(pawn))
                .Take(7)
                .Select(e => e.ToGameStringFromPOV(pawn))
                .Where(s => !s.NullOrEmpty())
                .ToList() ?? new List<string>();

            if (entries.Count == 0)
            {
                ConsoleStyle.Label(new Rect(log.x, log.y + log.height * 0.45f, log.width, 24f),
                    "No recent social log", ConsoleStyle.TextMuted, GameFont.Small, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (string entry in entries)
                {
                    float h = Mathf.Min(58f, Text.CalcHeight(entry, log.width - 28f) + 8f);
                    Rect r = new Rect(log.x + 10f, ly, log.width - 20f, h);
                    Widgets.DrawBoxSolid(r, new Color(0.12f, 0.13f, 0.14f));
                    ConsoleStyle.Label(r.ContractedBy(6f), entry, ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.UpperLeft);
                    ly += h + 8f;
                    if (ly > log.yMax - 30f) break;
                }
            }

            GUI.EndGroup();
        }

        private static void DrawHealthSubtabs(Rect rect)
        {
            float tabW = Mathf.Min(120f, rect.width * 0.28f);
            DrawHealthSubtabButton(new Rect(rect.x, rect.y, tabW, rect.height), "Overview", HealthSubtab.Overview);
            DrawHealthSubtabButton(new Rect(rect.x + tabW + 2f, rect.y, tabW, rect.height), "Operations", HealthSubtab.Operations);
        }

        private static void DrawHealthSubtabButton(Rect rect, string label, HealthSubtab tab)
        {
            bool selected = _healthSubtab == tab;
            Widgets.DrawBoxSolid(rect, selected ? new Color(0.18f, 0.22f, 0.23f) : new Color(0.11f, 0.12f, 0.13f));
            Widgets.DrawBox(rect, selected ? 2 : 1);
            ConsoleStyle.Label(rect, label, selected ? ConsoleStyle.TextWhite : ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static void DrawConsoleOperations(Rect rect, Pawn pawn)
        {
            float y = rect.y;
            float w = rect.width;

            BillStack stack = pawn.BillStack;
            bool hasQueued = stack != null && stack.Count > 0;

            if (hasQueued)
            {
                Rect queuedSection = new Rect(rect.x, y, w, Mathf.Min(stack.Count * 28f + 28f, rect.height * 0.35f));
                DrawConsoleSection(queuedSection, $"Queued ({stack.Count})");
                float qy = queuedSection.y + 24f;

                for (int i = 0; i < stack.Count; i++)
                {
                    Bill bill = stack[i];
                    if (bill == null) continue;
                    Rect row = new Rect(queuedSection.x + 6f, qy, queuedSection.width - 12f, 24f);
                    if (qy + 24f > queuedSection.yMax) break;
                    int idx = i;

                    Widgets.DrawBoxSolid(row, new Color(0.14f, 0.15f, 0.16f));

                    Texture2D billIcon = null;
                    try { billIcon = bill.recipe?.UIIcon; } catch { /* recipe may not have icon */ }
                    if (billIcon != null)
                    {
                        GUI.color = Color.white;
                        Widgets.DrawTextureFitted(new Rect(row.x + 2f, row.y + 2f, 20f, 20f), billIcon, 1f);
                        GUI.color = Color.white;
                    }

                    float labelX = billIcon != null ? row.x + 24f : row.x + 4f;
                    ConsoleStyle.Label(new Rect(labelX, row.y, row.width - 80f - (labelX - row.x), row.height),
                        bill.LabelCap, ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.MiddleLeft);
                    ConsoleStyle.Label(new Rect(row.xMax - 70f, row.y, 66f, row.height),
                        "[A] Cancel", ConsoleStyle.TextMuted, GameFont.Tiny, TextAnchor.MiddleRight);

                    AddHealthFocus(row, () => ConfirmCancelOperation(pawn, stack[idx]));
                    qy += 26f;
                }

                y = queuedSection.yMax + 4f;
            }

            float availH = rect.yMax - y;
            Rect availSection = new Rect(rect.x, y, w, availH);
            DrawConsoleSection(availSection, "Available");

            RebuildOperationEntries(pawn);

            if (_cachedOps.Count == 0)
            {
                ConsoleStyle.Label(new Rect(availSection.x, availSection.y + availSection.height * 0.4f, availSection.width, 20f),
                    "No operations available", ConsoleStyle.TextMuted, GameFont.Tiny, TextAnchor.MiddleCenter);
                return;
            }

            float listY = availSection.y + 24f;
            float listH = availSection.height - 24f;
            float entryH = 24f;
            float stride = 26f;
            float totalH = _cachedOps.Count * stride;

            int focusInOps = _healthFocusIndex - (hasQueued ? (stack?.Count ?? 0) : 0);
            if (focusInOps >= 0 && focusInOps < _cachedOps.Count)
            {
                float focusedY = focusInOps * stride;
                if (focusedY < _opsScroll.y)
                    _opsScroll.y = focusedY;
                else if (focusedY + entryH > _opsScroll.y + listH)
                    _opsScroll.y = focusedY + entryH - listH;
            }
            _opsScroll.y = Mathf.Clamp(_opsScroll.y, 0f, Mathf.Max(0f, totalH - listH));

            GUI.BeginClip(new Rect(availSection.x, listY, availSection.width, listH));

            for (int i = 0; i < _cachedOps.Count; i++)
            {
                float ey = i * stride - _opsScroll.y;
                if (ey + entryH < 0f || ey > listH) continue;

                OperationEntry op = _cachedOps[i];
                Rect row = new Rect(6f, ey, availSection.width - 12f, entryH);
                Widgets.DrawBoxSolid(row, new Color(0.12f, 0.13f, 0.14f));

                if (op.Icon != null)
                {
                    GUI.color = op.Disabled ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : Color.white;
                    Widgets.DrawTextureFitted(new Rect(row.x + 2f, row.y + 2f, 20f, 20f), op.Icon, 1f);
                    GUI.color = Color.white;
                }

                float lx = op.Icon != null ? row.x + 24f : row.x + 4f;
                Color labelColor = op.Disabled ? ConsoleStyle.TextMuted : ConsoleStyle.TextWhite;
                ConsoleStyle.Label(new Rect(lx, row.y, row.width - (lx - row.x) - 4f, row.height),
                    op.Label, labelColor, GameFont.Tiny, TextAnchor.MiddleLeft);

                Rect absRow = new Rect(availSection.x + 6f, listY + ey, availSection.width - 12f, entryH);
                int opIdx = i;
                if (op.Disabled)
                    AddHealthFocus(absRow, null);
                else
                    AddHealthFocus(absRow, () => QueueOperation(pawn, _cachedOps[opIdx]));
            }

            GUI.EndClip();

            if (totalH > listH)
            {
                float scrollFrac = _opsScroll.y / (totalH - listH);
                float barH = Mathf.Max(20f, listH * (listH / totalH));
                float barY = listY + scrollFrac * (listH - barH);
                Widgets.DrawBoxSolid(new Rect(availSection.xMax - 4f, barY, 3f, barH), ConsoleStyle.TealDark);
            }
        }

        private static void RebuildOperationEntries(Pawn pawn)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (_opsCachePawn == pawn && Mathf.Abs(tick - _opsCacheTick) < 60 && _cachedOps.Count > 0) return;
            _opsCachePawn = pawn;
            _opsCacheTick = tick;
            _cachedOps.Clear();

            Thing thingForMedBills = pawn;

            foreach (RecipeDef recipe in thingForMedBills.def.AllRecipes)
            {
                if (recipe == null) continue;
                if (!recipe.AvailableNow) continue;

                AcceptanceReport report = recipe.Worker.AvailableReport(pawn);
                if (!report.Accepted && report.Reason.NullOrEmpty()) continue;

                IEnumerable<ThingDef>? missing = null;
                try { missing = recipe.PotentiallyMissingIngredients(null!, pawn.MapHeld); }
                catch { missing = Enumerable.Empty<ThingDef>(); }

                if (missing!.Any(td => td.isTechHediff) || missing.Any(td => td.IsDrug))
                    continue;
                if (missing.Any() && recipe.dontShowIfAnyIngredientMissing)
                    continue;

                bool disabled = !report.Accepted;
                string disabledReason = report.Reason ?? "";
                if (!disabled && missing.Any())
                {
                    disabled = true;
                    disabledReason = "Missing: " + missing.Select(td => td.label).ToCommaList();
                }

                if (recipe.targetsBodyPart)
                {
                    foreach (BodyPartRecord part in recipe.Worker.GetPartsToApplyOn(pawn, recipe))
                    {
                        if (!recipe.AvailableOnNow(pawn, part)) continue;

                        string label = recipe.Worker.GetLabelWhenUsedOn(pawn, part).CapitalizeFirst();
                        if (!recipe.hideBodyPartNames)
                            label = $"{label} ({part.Label})";

                        Texture2D icon = ResolveRecipeIcon(recipe, part);
                        _cachedOps.Add(new OperationEntry
                        {
                            Label = label, Recipe = recipe, Part = part,
                            Icon = icon, Disabled = disabled, DisabledReason = disabledReason
                        });
                    }
                }
                else
                {
                    if (recipe.addsHediff != null && pawn.health.hediffSet.HasHediff(recipe.addsHediff))
                        continue;

                    string label = recipe.Worker.GetLabelWhenUsedOn(pawn, null).CapitalizeFirst();
                    Texture2D icon = ResolveRecipeIcon(recipe, null);
                    _cachedOps.Add(new OperationEntry
                    {
                        Label = label, Recipe = recipe, Part = null,
                        Icon = icon, Disabled = disabled, DisabledReason = disabledReason
                    });
                }
            }
        }

        private static Texture2D? ResolveRecipeIcon(RecipeDef recipe, BodyPartRecord? part)
        {
            try
            {
                if (recipe.UIIcon != null) return recipe.UIIcon;
                if (recipe.UIIconThing != null)
                    return recipe.UIIconThing.uiIcon;
                if (recipe.Worker is Recipe_RemoveBodyPart && part?.def?.spawnThingOnRemoved?.uiIcon != null)
                    return part.def.spawnThingOnRemoved.uiIcon;
                if (recipe.Worker is Recipe_AdministerIngestible)
                {
                    var ingr = recipe.ingredients?.FirstOrDefault()?.FixedIngredient;
                    if (ingr?.uiIcon != null) return ingr.uiIcon;
                }
            }
            catch { }
            return null;
        }

        private static void QueueOperation(Pawn pawn, OperationEntry op)
        {
            if (op.Disabled) return;

            HediffDef hediff = op.Recipe.addsHediff ?? op.Recipe.changesHediffLevel;
            if (hediff != null)
            {
                TaggedString violation = CompRoyalImplant.CheckForViolations(pawn, hediff, op.Recipe.hediffLevelOffset);
                if (!violation.NullOrEmpty())
                {
                    Find.WindowStack.Add(new Dialog_MessageBox(violation,
                        "Yes".Translate(), () => HealthCardUtility.CreateSurgeryBill(pawn, op.Recipe, op.Part),
                        "No".Translate()));
                    return;
                }
            }

            TaggedString confirm = op.Recipe.Worker.GetConfirmation(pawn);
            if (!confirm.NullOrEmpty())
            {
                Find.WindowStack.Add(new Dialog_MessageBox(confirm,
                    "Yes".Translate(), () => HealthCardUtility.CreateSurgeryBill(pawn, op.Recipe, op.Part),
                    "No".Translate()));
                return;
            }

            HealthCardUtility.CreateSurgeryBill(pawn, op.Recipe, op.Part);
            _opsCacheTick = 0;
        }

        private static void DrawCommandRow(Rect rect, string label, Action action)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.15f, 0.16f));
            Widgets.DrawBox(rect, 1);
            ConsoleStyle.Label(rect.ContractedBy(6f), label, ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.MiddleLeft);
            AddHealthFocus(rect, action);
        }

        private static void DrawClickableInfoLine(Rect rect, string label, string value, Action action)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.13f, 0.14f));
            DrawInfoLine(rect.ContractedBy(4f, 0f), label, value);
            AddHealthFocus(rect, action);
        }

        private static void AddHealthFocus(Rect rect, Action action)
        {
            _healthFocusRects.Add(rect);
            _healthFocusActions.Add(action);
        }

        private static void DrawHealthFocus()
        {
            if (_healthFocusRects.Count == 0) return;
            _healthFocusIndex = Mathf.Clamp(_healthFocusIndex, 0, _healthFocusRects.Count - 1);
            GUI.color = ConsoleStyle.Teal;
            Widgets.DrawBox(_healthFocusRects[_healthFocusIndex].ExpandedBy(1f), 2);
            GUI.color = Color.white;
        }

        private static void OpenFoodMenu(Pawn pawn)
        {
            if (pawn.foodRestriction == null || Current.Game?.foodRestrictionDatabase == null) return;
            var options = new List<FloatMenuOption>();
            foreach (FoodPolicy policy in Current.Game.foodRestrictionDatabase.AllFoodRestrictions)
            {
                FoodPolicy local = policy;
                options.Add(new FloatMenuOption(local.label, () => pawn.foodRestriction.CurrentFoodPolicy = local));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void CycleMedicine(Pawn pawn)
        {
            if (pawn.playerSettings == null) return;
            int next = ((int)pawn.playerSettings.medCare + 1) % 5;
            pawn.playerSettings.medCare = (MedicalCareCategory)next;
        }

        private static void ToggleSelfTend(Pawn pawn)
        {
            if (pawn.playerSettings == null) return;
            pawn.playerSettings.selfTend = !pawn.playerSettings.selfTend;
        }

        private static void ConfirmCancelOperation(Pawn pawn, Bill bill)
        {
            if (pawn.BillStack == null || bill == null) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Cancel operation?".Translate() + "\n\n" + bill.LabelCap,
                () => { pawn.BillStack.Delete(bill); _opsCacheTick = 0; }));
        }

        private struct SocialRow
        {
            public Pawn Pawn;
            public string Relation;
            public int Opinion;
            public int TheirOpinion;
        }

        private static IEnumerable<SocialRow> BuildSocialRows(Pawn pawn)
        {
            IEnumerable<Pawn> candidates = PawnsFinder.AllMapsWorldAndTemporary_Alive
                .Where(p => p != pawn && p.RaceProps?.Humanlike == true);

            foreach (Pawn other in candidates)
            {
                int opinion = 0;
                int theirOpinion = 0;
                try { opinion = pawn.relations?.OpinionOf(other) ?? 0; } catch { }
                try { theirOpinion = other.relations?.OpinionOf(pawn) ?? 0; } catch { }
                string relation = "";
                try { relation = pawn.GetRelations(other).Select(r => r.GetGenderSpecificLabel(other)).FirstOrDefault() ?? ""; } catch { }
                if (string.IsNullOrEmpty(relation) && Mathf.Abs(opinion) < 10 && Mathf.Abs(theirOpinion) < 10) continue;
                yield return new SocialRow { Pawn = other, Relation = string.IsNullOrEmpty(relation) ? "Acquaintance" : relation.CapitalizeFirst(), Opinion = opinion, TheirOpinion = theirOpinion };
            }
        }

        private static void DrawPawnStatusHeader(Rect rect, Pawn pawn)
        {
            DrawConsoleSection(rect, pawn.LabelShortCap);
            string sub = pawn.Downed ? "Downed" : pawn.Dead ? "Dead" : pawn.CurJobDef?.label?.CapitalizeFirst() ?? "Active";
            ConsoleStyle.Label(new Rect(rect.x + 10f, rect.y + 28f, rect.width - 20f, 24f), sub,
                pawn.Downed ? new Color(1f, 0.55f, 0.35f) : ConsoleStyle.TextGray, GameFont.Small);
        }

        private static void DrawConsoleSection(Rect rect, string title)
        {
            ConsoleStyle.DrawSection(rect);
            ConsoleStyle.DrawSolid(new Rect(rect.x, rect.y, rect.width, 22f), new Color(0.13f, 0.16f, 0.17f));
            ConsoleStyle.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, 22f), title, ConsoleStyle.Teal, GameFont.Tiny, TextAnchor.MiddleLeft);
        }

        private static void DrawInfoLine(Rect rect, string label, string value)
        {
            ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width * 0.45f, rect.height), label, ConsoleStyle.TextGray, GameFont.Tiny);
            ConsoleStyle.Label(new Rect(rect.x + rect.width * 0.45f, rect.y, rect.width * 0.55f, rect.height), value, ConsoleStyle.TextWhite, GameFont.Tiny);
        }

        private static void DrawMetricCard(Rect rect, string label, float pct, bool invert = false)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.11f, 0.12f, 0.13f));
            float shown = Mathf.Clamp01(pct);
            Color c = invert ? MetricColor(1f - shown) : MetricColor(shown);
            ConsoleStyle.Label(new Rect(rect.x, rect.y + 3f, rect.width, 16f), label, ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleCenter);
            ConsoleStyle.Label(new Rect(rect.x, rect.y + 19f, rect.width, 20f), shown.ToStringPercent("F0"), c, GameFont.Small, TextAnchor.MiddleCenter);
        }

        private static void DrawCapacityRow(Rect rect, string label, float pct)
        {
            ConsoleStyle.Label(new Rect(rect.x, rect.y, rect.width * 0.45f, rect.height), label, ConsoleStyle.TextWhite, GameFont.Tiny);
            DrawBarWithLabel(new Rect(rect.x + rect.width * 0.45f, rect.y + 3f, rect.width * 0.55f, 12f), pct, pct.ToStringPercent("F0"));
        }

        private static void DrawConditionRow(Rect rect, Hediff h)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.13f, 0.14f));
            ConsoleStyle.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width * 0.60f, 15f), h.LabelCap, ConsoleStyle.TextWhite, GameFont.Tiny);
            string part = h.Part?.LabelCap ?? "";
            ConsoleStyle.Label(new Rect(rect.x + 4f, rect.y + 15f, rect.width * 0.60f, 15f), part, ConsoleStyle.TextMuted, GameFont.Tiny);
            string sev = !h.SeverityLabel.NullOrEmpty() ? h.SeverityLabel : h.Severity > 0f ? h.Severity.ToString("0.##") : "";
            ConsoleStyle.Label(new Rect(rect.x + rect.width * 0.62f, rect.y, rect.width * 0.36f, rect.height), sev, h.BleedRate > 0f ? new Color(1f, 0.45f, 0.35f) : ConsoleStyle.TextGray, GameFont.Tiny, TextAnchor.MiddleRight);
        }

        private static void DrawSocialRelationRow(Rect rect, SocialRow row)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.13f, 0.14f));
            ConsoleStyle.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width * 0.42f, 15f), row.Relation, ConsoleStyle.TextGray, GameFont.Tiny);
            ConsoleStyle.Label(new Rect(rect.x + 6f, rect.y + 15f, rect.width * 0.55f, 15f), row.Pawn.LabelShortCap, ConsoleStyle.TextWhite, GameFont.Tiny);
            ConsoleStyle.Label(new Rect(rect.x + rect.width * 0.58f, rect.y, rect.width * 0.18f, rect.height), Signed(row.Opinion), OpinionColor(row.Opinion), GameFont.Tiny, TextAnchor.MiddleRight);
            ConsoleStyle.Label(new Rect(rect.x + rect.width * 0.78f, rect.y, rect.width * 0.20f, rect.height), Signed(row.TheirOpinion), OpinionColor(row.TheirOpinion), GameFont.Tiny, TextAnchor.MiddleRight);
        }

        private static void DrawBarWithLabel(Rect rect, float fill, string label)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.05f, 0.05f, 0.055f));
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), MetricColor(fill));
            ConsoleStyle.Label(rect, label, ConsoleStyle.TextWhite, GameFont.Tiny, TextAnchor.MiddleCenter);
        }

        private static Color MetricColor(float pct)
        {
            if (pct >= 0.70f) return new Color(0.38f, 0.72f, 0.32f);
            if (pct >= 0.35f) return new Color(0.78f, 0.65f, 0.22f);
            return new Color(0.85f, 0.25f, 0.22f);
        }

        private static Color OpinionColor(int value)
        {
            if (value > 10) return new Color(0.35f, 0.95f, 0.35f);
            if (value < -10) return new Color(1f, 0.35f, 0.30f);
            return ConsoleStyle.TextGray;
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }
}
