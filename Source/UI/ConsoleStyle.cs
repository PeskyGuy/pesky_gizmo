using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [StaticConstructorOnStartup]
    public static class ConsoleStyle
    {
        public static readonly Color PanelBg = new Color(21f/255f, 25f/255f, 29f/255f, 0.95f);
        public static readonly Color PanelBgSolid = new Color(21f/255f, 25f/255f, 29f/255f, 1f);
        public static readonly Color PanelBorder = new Color(97f/255f, 108f/255f, 122f/255f, 1f);
        public static readonly Color SectionBg = new Color(42f/255f, 43f/255f, 44f/255f, 1f);
        public static readonly Color SectionBorder = new Color(135f/255f, 135f/255f, 135f/255f, 0.4f);

        public static readonly Color Teal = new Color(0.25f, 0.60f, 0.60f, 1f);
        public static readonly Color TealDark = new Color(0.15f, 0.40f, 0.40f, 1f);
        public static readonly Color TealBright = new Color(0.35f, 0.75f, 0.75f, 1f);

        public static readonly Color BtnGreen = new Color(0.35f, 0.75f, 0.35f, 1f);
        public static readonly Color BtnRed = new Color(0.85f, 0.30f, 0.30f, 1f);
        public static readonly Color BtnBlue = new Color(0.30f, 0.50f, 0.85f, 1f);
        public static readonly Color BtnYellow = new Color(0.85f, 0.75f, 0.25f, 1f);

        public static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color TextGray = new Color(0.60f, 0.60f, 0.65f, 1f);
        public static readonly Color TextMuted = new Color(0.40f, 0.40f, 0.45f, 1f);

        public static readonly Color HealthGreen = new Color(0.35f, 0.70f, 0.40f, 1f);
        public static readonly Color MoodBlue = new Color(0.35f, 0.55f, 0.75f, 1f);
        public static readonly Color BarBg = new Color(0.15f, 0.15f, 0.18f, 1f);

        public static readonly Color CellBg = new Color(42f/255f, 43f/255f, 44f/255f, 1f);
        public static readonly Color CellSelected = new Color(55f/255f, 70f/255f, 75f/255f, 1f);
        public static readonly Color CellHover = new Color(50f/255f, 55f/255f, 58f/255f, 1f);

        private static Texture2D? _solidTex;
        private static Texture2D? _roundedRectTex;

        public static Texture2D SolidTex
        {
            get
            {
                if (_solidTex == null)
                {
                    _solidTex = new Texture2D(1, 1);
                    _solidTex.SetPixel(0, 0, Color.white);
                    _solidTex.Apply();
                }
                return _solidTex;
            }
        }

        public static Texture2D RoundedRectTex
        {
            get
            {
                if (_roundedRectTex == null)
                {
                    int size = 32;
                    int radius = 6;
                    _roundedRectTex = new Texture2D(size, size);
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float alpha = 1f;
                            if (x < radius && y < radius)
                            {
                                float dist = Mathf.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius));
                                alpha = dist > radius ? 0f : dist > radius - 1.5f ? (radius - dist) / 1.5f : 1f;
                            }
                            else if (x >= size - radius && y < radius)
                            {
                                float dist = Mathf.Sqrt((x - (size - radius - 1)) * (x - (size - radius - 1)) + (y - radius) * (y - radius));
                                alpha = dist > radius ? 0f : dist > radius - 1.5f ? (radius - dist) / 1.5f : 1f;
                            }
                            else if (x < radius && y >= size - radius)
                            {
                                float dist = Mathf.Sqrt((x - radius) * (x - radius) + (y - (size - radius - 1)) * (y - (size - radius - 1)));
                                alpha = dist > radius ? 0f : dist > radius - 1.5f ? (radius - dist) / 1.5f : 1f;
                            }
                            else if (x >= size - radius && y >= size - radius)
                            {
                                float dist = Mathf.Sqrt((x - (size - radius - 1)) * (x - (size - radius - 1)) + (y - (size - radius - 1)) * (y - (size - radius - 1)));
                                alpha = dist > radius ? 0f : dist > radius - 1.5f ? (radius - dist) / 1.5f : 1f;
                            }
                            _roundedRectTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                        }
                    }
                    _roundedRectTex.Apply();
                }
                return _roundedRectTex;
            }
        }

        public static void DrawPanel(Rect rect)
        {
            Widgets.DrawWindowBackground(rect);
        }

        public static void DrawSolidPanel(Rect rect)
        {
            Widgets.DrawWindowBackground(rect);
        }

        public static void DrawSection(Rect rect)
        {
            GUI.color = SectionBg;
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = SectionBorder;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }

        public static void DrawRoundedRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = Color.white;
        }

        public static void DrawSolid(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = Color.white;
        }

        public static void DrawBar(Rect rect, float fill, Color barColor)
        {
            DrawRoundedRect(rect, BarBg);
            if (fill > 0f)
            {
                Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height);
                DrawRoundedRect(fillRect, barColor);
            }
        }

        public static void Label(Rect rect, string text, Color color, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;
            var prevColor = GUI.color;
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
            Widgets.Label(rect, text);
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }
    }
}
