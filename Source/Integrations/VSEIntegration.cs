using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [StaticConstructorOnStartup]
    public static class VSEIntegration
    {
        private static Type? _passionManagerType;
        private static MethodInfo? _passionToDefMethod;
        private static PropertyInfo? _passionDefIconProp;
        private static PropertyInfo? _passionDefLabelCapProp;
        private static bool _checked;

        public static Texture2D? GetPassionIcon(Passion passion)
        {
            if (passion == Passion.Major) return SkillUI.PassionMajorIcon;
            if (passion == Passion.Minor) return SkillUI.PassionMinorIcon;
            if (passion == Passion.None) return null;

            EnsureCached();
            var def = GetPassionDef(passion);
            if (def != null && _passionDefIconProp != null)
            {
                try { return _passionDefIconProp.GetValue(def) as Texture2D; }
                catch { /* VSE reflection probe — optional mod may not be loaded */ }
            }

            return SkillUI.PassionMajorIcon;
        }

        public static string GetPassionLabel(Passion passion)
        {
            if (passion == Passion.None) return "PassionNone".Translate();
            if (passion == Passion.Minor) return "PassionMinor".Translate();
            if (passion == Passion.Major) return "PassionMajor".Translate();

            EnsureCached();
            var def = GetPassionDef(passion);
            if (def != null && _passionDefLabelCapProp != null)
            {
                try { return _passionDefLabelCapProp.GetValue(def) as string ?? passion.ToString(); }
                catch { /* VSE reflection probe — optional mod may not be loaded */ }
            }

            return passion.ToString();
        }

        private static object? GetPassionDef(Passion passion)
        {
            if (_passionToDefMethod == null) return null;
            try { return _passionToDefMethod.Invoke(null, new object[] { passion }); }
            catch { /* VSE reflection probe — optional mod may not be loaded */ return null; }
        }

        private static void EnsureCached()
        {
            if (_checked) return;
            _checked = true;
            try
            {
                _passionManagerType = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionManager");
                if (_passionManagerType != null)
                {
                    _passionToDefMethod = _passionManagerType.GetMethod("PassionToDef",
                        BindingFlags.Public | BindingFlags.Static);
                    var passionDefType = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionDef");
                    if (passionDefType != null)
                    {
                        _passionDefIconProp = passionDefType.GetProperty("Icon",
                            BindingFlags.Instance | BindingFlags.Public);
                        _passionDefLabelCapProp = passionDefType.GetProperty("LabelCap",
                            BindingFlags.Instance | BindingFlags.Public);
                    }
                }
            }
            catch { /* VSE type resolution failed — optional mod not present */ }
        }
    }
}
