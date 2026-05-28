using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    internal static class DesignatorBridge
    {
        private static FieldInfo? _placingRotField;
        private static MethodInfo? _handleRotationMethod;
        private static FieldInfo? _desPanelsCachedField;

        private const string ICON_TOP_TEX = "IconTopTex";

        public static bool TryRotate(Designator_Place place, RotationDirection dir)
        {
            try
            {
                if (_handleRotationMethod == null)
                {
                    _handleRotationMethod = typeof(Designator_Place).GetMethod("HandleRotation",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }

                if (_handleRotationMethod != null)
                {
                    _handleRotationMethod.Invoke(place, new object[] { dir });
                    return true;
                }

                if (_placingRotField == null)
                {
                    _placingRotField = typeof(Designator_Place).GetField("placingRot",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }

                if (_placingRotField != null)
                {
                    Rot4 current = (Rot4)_placingRotField.GetValue(place);
                    Rot4 next = dir == RotationDirection.Clockwise
                        ? current.Rotated(RotationDirection.Clockwise)
                        : current.Rotated(RotationDirection.Counterclockwise);
                    _placingRotField.SetValue(place, next);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                GizmoLog.WarningOnce($"DesignatorBridge.TryRotate: {ex.GetBaseException().Message}", 554001);
            }

            return false;
        }

        public static Rot4? GetPlacingRot(Designator_Place place)
        {
            if (_placingRotField == null)
            {
                _placingRotField = typeof(Designator_Place).GetField("placingRot",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (_placingRotField != null)
                return (Rot4)_placingRotField.GetValue(place);
            return null;
        }

        public static Texture2D? GetIconTopTex(Designator des)
        {
            var prop = des.GetType().GetProperty(ICON_TOP_TEX,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (prop == null) return null;
            return prop.GetValue(des) as Texture2D;
        }

        public static bool IsPlacingDefRotatable(Designator_Place place)
        {
            if (place.PlacingDef is ThingDef td) return td.rotatable;
            return false;
        }

        public static List<DesignationCategoryDef>? GetCachedPanelDefs()
        {
            if (_desPanelsCachedField == null)
            {
                _desPanelsCachedField = typeof(MainTabWindow_Architect)
                    .GetField("desPanelsCached", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            if (_desPanelsCachedField == null) return null;

            var archWindow = MainButtonDefOf.Architect?.TabWindow;
            if (archWindow == null) return null;

            return (_desPanelsCachedField.GetValue(archWindow) as List<ArchitectCategoryTab>)?
                .ConvertAll(t => t.def);
        }

        public static bool TryCloseVanillaArchitect()
        {
            var archDef = MainButtonDefOf.Architect;
            if (archDef?.TabWindow != null && Find.WindowStack.IsOpen(archDef.TabWindow))
                return Find.WindowStack.TryRemove(archDef.TabWindow, true);
            return false;
        }
    }
}
