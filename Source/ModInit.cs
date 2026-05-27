using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace PeskyGizmo
{
    [StaticConstructorOnStartup]
    public static class CursorHelper
    {
        private static readonly Texture2D ReticleCursor;
        private static readonly Vector2 Hotspot;
        private static readonly Texture2D EmptyCursor;

        static CursorHelper()
        {
            const int size = 32;
            ReticleCursor = LoadCursorTexture("UI/HexCursor", size) ?? new Texture2D(size, size, TextureFormat.ARGB32, false);
            Hotspot = new Vector2(size * 0.5f, size * 0.5f);

            EmptyCursor = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            EmptyCursor.SetPixel(0, 0, new Color(0, 0, 0, 0));
            EmptyCursor.Apply();
        }

        private static Texture2D? LoadCursorTexture(string path, int size)
        {
            var src = ContentFinder<Texture2D>.Get(path, true);
            if (src == null) return null;

            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                var dst = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                dst.Apply();
                return dst;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static void ApplyReticleCursor()
        {
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.SetCursor(ReticleCursor, Hotspot, CursorMode.ForceSoftware);
        }

        public static void SetInvisible()
        {
            UnityEngine.Cursor.SetCursor(EmptyCursor, Vector2.zero, CursorMode.ForceSoftware);
        }

        public static void SetVisible()
        {
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    public class GizmoMod : Mod
    {
        public static GizmoMod? Instance { get; private set; }
        public static GizmoSettings Settings => Instance!._settings;

        private readonly GizmoSettings _settings;

        public GizmoMod(ModContentPack content) : base(content)
        {
            Instance  = this;
            _settings = GetSettings<GizmoSettings>();

            var harmony = new Harmony("Pesky.Gizmo");
            harmony.PatchAll();

            if (!_settings.calibrationComplete)
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    if (ControllerState.ControllerConnected)
                        Find.WindowStack?.Add(new CalibrationWindow());
                });
            }

            Log.Message("[Gizmo] Initialised.");
        }

        public static void SetInvisibleCursor() => CursorHelper.ApplyReticleCursor();
        public static void SetVisibleCursor() => CursorHelper.SetVisible();

        public override string SettingsCategory() => "Pesky's Gizmo";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Camera Sensitivity: {Settings.cameraSensitivity:F0}");
            Settings.cameraSensitivity = listing.Slider(Settings.cameraSensitivity, 5f, 200f);

            listing.Label($"Cursor Sensitivity: {Settings.cursorSensitivity:F0} px/s");
            Settings.cursorSensitivity = listing.Slider(Settings.cursorSensitivity, 200f, 3000f);

            listing.Gap();
            listing.Label($"Aim Assist Strength: {Settings.aimAssistStrength:P0}");
            Settings.aimAssistStrength = listing.Slider(Settings.aimAssistStrength, 0f, 0.9f);
            listing.Label("  Slows the reticle near world targets and softly pulls it toward UI buttons.");

            listing.CheckboxLabeled("UI Magnetism (snap to buttons & widgets)", ref Settings.enableUIAimAssist,
                "When on, the cursor is gently pulled toward the nearest UI button while you move the stick. Disable for raw cursor control.");

            listing.Gap();
            listing.Label($"Stick Response Curve: {Settings.stickCurve:F1}");
            string curveLabel = Settings.stickCurve <= 1.3f ? "Linear"
                : Settings.stickCurve <= 2.0f ? "Curved" : "Aggressive";
            listing.Label($"  ({curveLabel})");
            Settings.stickCurve = listing.Slider(Settings.stickCurve, 1.0f, 3.0f);

            listing.Gap();
            listing.Label($"Inner Deadzone: {Settings.innerDeadzone:P0}");
            Settings.innerDeadzone = listing.Slider(Settings.innerDeadzone, 0.05f, 0.40f);

            listing.Gap();
            listing.CheckboxLabeled("Swap Left/Right Sticks", ref Settings.swapSticks);
            listing.CheckboxLabeled("Wrap Navigation at Edges", ref Settings.navWrapAround);
            listing.CheckboxLabeled("Haptic Feedback", ref Settings.hapticsEnabled);

            if (listing.ButtonText("Test Vibration"))
                HapticManager.Trigger(HapticTier.MediumRumble);

            listing.CheckboxLabeled("Show Debug Overlay", ref Settings.showDebugOverlay);

            listing.GapLine();
            if (listing.ButtonText("Open Calibration Wizard"))
            {
                Find.WindowStack.Add(new CalibrationWindow());
            }

            listing.End();
        }
    }
}
