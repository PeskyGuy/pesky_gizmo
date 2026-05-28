using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [StaticConstructorOnStartup]
    public static class ReticleDrawer
    {
        private static readonly Texture2D HexOutlineTex;

        private const float HEX_DRAW_SIZE = 32f;
        private const float HALF_HEX = HEX_DRAW_SIZE * 0.5f;
        private const float GAP = 4f;

        private static readonly Color ReticleColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color ReticleHotColor = new Color(0.95f, 0.85f, 0.30f, 1f);
        private static readonly Color PromptBg = new Color(0.08f, 0.08f, 0.08f, 0.75f);

        static ReticleDrawer()
        {
            var rawTex = ContentFinder<Texture2D>.Get("UI/HexCursor", true);
            if (rawTex != null)
            {
                rawTex.filterMode = FilterMode.Bilinear;
                rawTex.wrapMode = TextureWrapMode.Clamp;
            }
            HexOutlineTex = rawTex ?? new Texture2D(1, 1);
        }

        // INVARIANT: This file never checks ActivePanel, NavMenu.IsOpen, or any other
        // panel state directly. All cursor visibility decisions go through
        // NavController.AnyCursorHidingPanelOpen.
        public static void Draw(bool drawReticle = true)
        {
            try
            {
                bool cursorHidden = NavController.AnyCursorHidingPanelOpen || NavHelper.MenuNavModeActive;

                Vector2 rawPos = ControllerState.UsingController
                    ? ControllerState.CursorScreenPos
                    : (Vector2)Input.mousePosition;
                Vector2 pos = new Vector2(rawPos.x / Prefs.UIScale, (Screen.height - rawPos.y) / Prefs.UIScale);

                if (drawReticle && !cursorHidden)
                    DrawReticleAt(pos);

                if (!cursorHidden && ControllerDetector.IsMapSafeForInput())
                {
                    DrawHoverInfo(pos);
                    DrawContextPrompts(pos);
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] ReticleDrawer crash: {ex}", 772134);
            }
        }

        public static void DrawReticleAt(Vector2 pos)
        {
            bool hot = InspectPanel.IsActive;
            Color mainColor = hot ? ReticleHotColor : ReticleColor;

            float w = HexOutlineTex.width * 0.5f;
            float h = HexOutlineTex.height * 0.5f;
            Rect hexRect = new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h);

            Color prev = GUI.color;
            GUI.color = mainColor;
            GUI.DrawTexture(hexRect, HexOutlineTex);
            GUI.color = prev;
        }

        private static void DrawHoverInfo(Vector2 pos)
        {
            if (ReticleController.HoveredThing == null) return;

            string label = ReticleController.HoveredThing.LabelCapNoCount;

            Text.Font = GameFont.Small;
            Vector2 textSize = Text.CalcSize(label);
            float padX = 8f, padY = 4f;
            float boxW = textSize.x + padX * 2;
            float boxH = textSize.y + padY * 2;

            Rect bgRect = new Rect(pos.x - boxW / 2f, pos.y - HALF_HEX - GAP - boxH - 50f, boxW, boxH);
            Widgets.DrawBoxSolid(bgRect, PromptBg);

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(bgRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawContextPrompts(Vector2 pos)
        {
            if (Find.CurrentMap == null || Find.Selector == null) return;
            if (InspectPanel.IsActive) return;

            bool hasSelection = Find.Selector.NumSelected > 0;
            bool hasTarget = ReticleController.HasTarget;

            if (!hasSelection && !hasTarget) return;

            if (hasTarget)
            {
                float dist = HALF_HEX + GAP + 14f;
                DrawPrompt(pos + new Vector2(dist + 4f, -8f), "A", "Select");

                if (hasSelection)
                {
                    var sel = Find.Selector;
                    var selectedPawn = sel.SelectedObjectsListForReading.OfType<Pawn>().FirstOrDefault();
                    var target = ReticleController.HoveredPawn ?? ReticleController.HoveredThing;
                    if (selectedPawn != null && target != null
                        && ReticleController.IsAttackable(target, selectedPawn))
                    {
                        bool hostile = target.HostileTo(selectedPawn);
                        string label = hostile ? "Attack" : "Interact";
                        DrawPrompt(pos + new Vector2(dist + 4f, 14f), "X", label);
                    }
                }
            }

            if (hasSelection && !hasTarget)
            {
                var sel = Find.Selector;
                var draftedPawn = sel.SelectedObjectsListForReading
                    .OfType<Pawn>()
                    .FirstOrDefault(p => p.drafter != null && p.drafter.Drafted);
                if (draftedPawn != null)
                {
                    float dist = HALF_HEX + GAP + 14f;
                    DrawPrompt(pos + new Vector2(dist + 4f, -8f), "X", "Move");
                }
            }

            if (hasSelection)
                DrawSelectionHintBar();
        }

        private static void DrawSelectionHintBar()
        {
            try
            {
                var sel = Find.Selector;
                if (sel == null || sel.NumSelected == 0) return;

                var hints = new List<(string button, string label)>();

                hints.Add(("B", "Deselect"));

                var colonists = sel.SelectedObjectsListForReading
                    .OfType<Pawn>()
                    .Where(p => p.IsColonistPlayerControlled && p.drafter != null)
                    .ToList();
                if (colonists.Count > 0)
                {
                    bool allDrafted = colonists.All(p => p.drafter.Drafted);
                    hints.Add(("Y", allDrafted ? "Undraft" : "Draft"));
                }

                if (hints.Count == 0) return;

                const float barH = 28f;
                const float itemW = 90f;
                float totalW = hints.Count * itemW;
                float barX = (Verse.UI.screenWidth - totalW) * 0.5f;
                float barY = Verse.UI.screenHeight - 35f - barH - 6f;

                Rect bgRect = new Rect(barX - 8f, barY - 4f, totalW + 16f, barH + 8f);
                Widgets.DrawBoxSolid(bgRect, new Color(0.06f, 0.06f, 0.08f, 0.72f));

                GlyphRenderer.DrawPromptBar(new Rect(barX, barY, totalW, barH), hints.ToArray());
            }
            catch { }
        }

        private static void DrawPrompt(Vector2 pos, string button, string action)
        {
            string text = $"[{button}] {action}";
            Text.Font = GameFont.Tiny;
            Vector2 size = Text.CalcSize(text);
            float pad = 4f;

            Rect bg = new Rect(pos.x, pos.y, size.x + pad * 2, size.y + pad * 2);
            Widgets.DrawBoxSolid(bg, PromptBg);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.9f, 0.9f, 0.7f, 1f);
            Widgets.Label(bg, text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
