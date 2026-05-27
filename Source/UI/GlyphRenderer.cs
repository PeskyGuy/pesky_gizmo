using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [StaticConstructorOnStartup]
    public static class GlyphRenderer
    {
        private const float GLYPH_SIZE = 22f;

        private static readonly Color GlyphBg = new Color(0.35f, 0.35f, 0.38f, 1f);
        private static readonly Color GlyphBorder = new Color(0.55f, 0.55f, 0.58f, 1f);
        private static readonly Color GlyphText = new Color(0.95f, 0.95f, 0.95f, 1f);

        private static Texture2D? _circleTex;

        private static Texture2D CircleTex
        {
            get
            {
                if (_circleTex == null)
                {
                    int res = 64;
                    _circleTex = new Texture2D(res, res, TextureFormat.ARGB32, false);
                    float center = (res - 1) * 0.5f;
                    float outerR = center;
                    float innerR = center - 2f;
                    for (int py = 0; py < res; py++)
                    {
                        for (int px = 0; px < res; px++)
                        {
                            float dx = px - center;
                            float dy = py - center;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist <= innerR)
                                _circleTex.SetPixel(px, py, Color.white);
                            else if (dist <= outerR)
                                _circleTex.SetPixel(px, py, new Color(0.7f, 0.7f, 0.7f, 1f));
                            else
                                _circleTex.SetPixel(px, py, Color.clear);
                        }
                    }
                    _circleTex.Apply();
                    _circleTex.filterMode = FilterMode.Bilinear;
                }
                return _circleTex;
            }
        }

        public static float Draw(float x, float y, string button, float size = GLYPH_SIZE)
        {
            // Measure text to decide width
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float textW = Text.CalcSize(button).x;
            Text.Font = prevFont;

            float padding = 8f;
            float width = Mathf.Max(size, textW + padding);

            Rect glyphRect = new Rect(x, y, width, size);

            GUI.color = GlyphBg;
            if (width <= size + 2f)
            {
                // Circle fits for short labels (A, B, X, Y, etc.)
                GUI.DrawTexture(glyphRect, CircleTex);
            }
            else
            {
                // Pill / rounded rect for long labels (D-Pad, LB/RB, etc.)
                GUI.DrawTexture(glyphRect, ConsoleStyle.RoundedRectTex, ScaleMode.StretchToFill);
                GUI.color = GlyphBorder;
                Widgets.DrawBox(glyphRect, 1);
                GUI.color = Color.white;
            }

            var prevAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = GlyphText;
            Widgets.Label(glyphRect, button);
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = Color.white;

            return x + width;
        }

        public static float DrawWithLabel(float x, float y, string button, string label, float glyphSize = GLYPH_SIZE)
        {
            float right = Draw(x, y, button, glyphSize);
            if (string.IsNullOrEmpty(label)) return right + 4f;
            Rect labelRect = new Rect(right + 4f, y, 200f, glyphSize);
            ConsoleStyle.Label(labelRect, label, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);
            return right + 4f + Text.CalcSize(label).x;
        }

        public static void DrawPromptBar(Rect container, params (string button, string label)[] prompts)
        {
            float spacing = 16f;
            float totalWidth = 0f;
            foreach (var (btn, lbl) in prompts)
            {
                totalWidth += MeasureGlyphWidth(btn);
                if (!string.IsNullOrEmpty(lbl))
                    totalWidth += 4f + Text.CalcSize(lbl).x;
                totalWidth += spacing;
            }
            totalWidth -= spacing;

            float x = container.x + (container.width - totalWidth) * 0.5f;
            float y = container.y + (container.height - GLYPH_SIZE) * 0.5f;

            foreach (var (btn, lbl) in prompts)
            {
                float right = Draw(x, y, btn);
                if (!string.IsNullOrEmpty(lbl))
                {
                    Rect labelRect = new Rect(right + 4f, y, 200f, GLYPH_SIZE);
                    ConsoleStyle.Label(labelRect, lbl, ConsoleStyle.TextWhite, GameFont.Small, TextAnchor.MiddleLeft);
                    x = right + 4f + Text.CalcSize(lbl).x + spacing;
                }
                else
                {
                    x = right + 4f + spacing;
                }
            }
        }

        private static float MeasureGlyphWidth(string button)
        {
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float textW = Text.CalcSize(button).x;
            Text.Font = prevFont;
            float padding = 8f;
            return Mathf.Max(GLYPH_SIZE, textW + padding);
        }
    }
}
