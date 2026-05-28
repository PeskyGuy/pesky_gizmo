using System.Reflection;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    internal static class InspectTabBridge
    {
        private static MethodInfo? _fillTabMethod;
        private static MethodInfo? _updateSizeMethod;
        private static FieldInfo? _sizeField;

        public static void EnsureCached()
        {
            if (_fillTabMethod == null)
                _fillTabMethod = typeof(InspectTabBase).GetMethod("FillTab",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (_updateSizeMethod == null)
                _updateSizeMethod = typeof(InspectTabBase).GetMethod("UpdateSize",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (_sizeField == null)
                _sizeField = typeof(InspectTabBase).GetField("size",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public static void FillTab(InspectTabBase tab)
        {
            if (_fillTabMethod == null) EnsureCached();
            _fillTabMethod?.Invoke(tab, null);
        }

        public static void UpdateSize(InspectTabBase tab)
        {
            if (_updateSizeMethod == null) EnsureCached();
            _updateSizeMethod?.Invoke(tab, null);
        }

        public static void SetSize(InspectTabBase tab, Vector2 size)
        {
            if (_sizeField == null) EnsureCached();
            _sizeField?.SetValue(tab, size);
        }
    }
}
