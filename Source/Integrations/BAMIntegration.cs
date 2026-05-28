using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace PeskyGizmo
{
    public static class BAMIntegration
    {
        private static Type? _nestedCatExtType;
        private static Type? _specialCatExtType;
        private static FieldInfo? _parentCatField;
        private static bool _bamChecked;
        private static bool _bamLoaded;

        public static bool IsLoaded
        {
            get
            {
                DetectBAM();
                return _bamLoaded;
            }
        }

        private static void DetectBAM()
        {
            if (_bamChecked) return;
            _bamChecked = true;
            _bamLoaded = false;

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (_nestedCatExtType == null)
                        _nestedCatExtType = asm.GetType("BetterArchitect.NestedCategoryExtension");

                    if (_specialCatExtType == null)
                        _specialCatExtType = asm.GetType("BetterArchitect.SpecialCategoryExtension");

                    if (_nestedCatExtType != null && _parentCatField == null)
                    {
                        _parentCatField = _nestedCatExtType.GetField("parentCategory",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    if (_nestedCatExtType != null && _parentCatField != null)
                    {
                        _bamLoaded = true;
                        break;
                    }
                }
            }
            catch
            {
                _bamLoaded = false;
            }
        }

        public static bool IsSpecialCategory(DesignationCategoryDef def)
        {
            if (def == null) return false;
            if (def.defName == "Orders" || def.defName == "Zone" || def.defName == "Blueprints") return true;

            DetectBAM();
            if (_bamLoaded && _specialCatExtType != null && def.modExtensions != null)
            {
                if (def.modExtensions.Any(ext => _specialCatExtType.IsInstanceOfType(ext)))
                    return true;
            }
            return false;
        }

        public static List<DesignationCategoryDef>? GetSubcategoriesFor(DesignationCategoryDef parent)
        {
            DetectBAM();
            if (!_bamLoaded || _nestedCatExtType == null || _parentCatField == null) return null;

            var subs = new List<DesignationCategoryDef>();

            foreach (var def in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                if (def.modExtensions == null) continue;
                foreach (var ext in def.modExtensions)
                {
                    if (!_nestedCatExtType.IsInstanceOfType(ext)) continue;

                    var parentVal = _parentCatField.GetValue(ext) as DesignationCategoryDef;
                    if (parentVal == parent)
                    {
                        var designators = def.ResolvedAllowedDesignators?.Where(d => d.Visible);
                        if (designators != null && (IsSpecialCategory(def) || HasVisibleBuildables(designators)))
                        {
                            subs.Add(def);
                        }
                    }
                }
            }

            if (subs.Count == 0) return null;

            subs = subs.OrderByDescending(c => c.order).ToList();

            var parentDesignators = parent.ResolvedAllowedDesignators?.Where(d => d.Visible);
            if (parentDesignators != null && HasVisibleBuildables(parentDesignators))
            {
                subs.Add(parent);
            }

            return subs;
        }

        private static bool HasVisibleBuildables(IEnumerable<Designator> designators)
        {
            if (designators == null) return false;
            return designators.Any(d => d.Visible && (
                d is Designator_Build ||
                (d is Designator_Dropdown dd && dd.Elements.Any(e => e is Designator_Build))
            ));
        }
    }
}
