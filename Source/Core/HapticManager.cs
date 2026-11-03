using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public enum HapticTier { TinyTick, ShortPulse, MediumRumble, HeavyRumble, DualPulse }

    public static class HapticManager
    {
        private static readonly float[] _cooldowns = new float[5];
        private static readonly float[] _durations  = { 0.04f, 0.08f, 0.15f, 0.30f, 0.10f };
        private static readonly float[] _minGaps    = { 0.06f, 0.12f, 0.20f, 0.40f, 0.15f };

        private static readonly (float L, float R)[] _strengths =
        {
            (0.10f, 0.10f),
            (0.30f, 0.20f),
            (0.55f, 0.35f),
            (0.90f, 0.70f),
            (0.40f, 0.80f),
        };

        public static void Trigger(HapticTier tier)
        {
            if (!GizmoMod.Settings.hapticsEnabled) return;
            int t = (int)tier;
            if (Time.unscaledTime < _cooldowns[t]) return;

            _cooldowns[t] = Time.unscaledTime + _minGaps[t];

            float thisStrength = _strengths[t].L + _strengths[t].R;
            for (int i = 0; i < 5; i++)
            {
                if (i != t)
                {
                    float otherStrength = _strengths[i].L + _strengths[i].R;
                    if (thisStrength >= otherStrength)
                    {
                        _cooldowns[i] = Mathf.Max(_cooldowns[i], Time.unscaledTime + _durations[t]);
                    }
                }
            }

            try
            {
                NativeInput.SetVibration(_strengths[t].L, _strengths[t].R, _durations[t]);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce($"[Gizmo] HapticManager failed: {ex.Message}", 881234);
            }
        }

        public static void UI_DpadNav()       => Trigger(HapticTier.TinyTick);
        public static void UI_Confirm()       => Trigger(HapticTier.ShortPulse);
        public static void UI_InvalidAction() => Trigger(HapticTier.DualPulse);
        public static void Pawn_Selected()    => Trigger(HapticTier.ShortPulse);
        public static void Pawn_Drafted()     => Trigger(HapticTier.MediumRumble);
        public static void Combat_Melee()     => Trigger(HapticTier.MediumRumble);
        public static void Combat_Ranged()    => Trigger(HapticTier.MediumRumble);
        public static void Combat_Downed()    => Trigger(HapticTier.HeavyRumble);
    }
}
