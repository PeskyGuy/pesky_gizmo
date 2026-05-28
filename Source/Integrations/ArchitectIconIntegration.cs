using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class ArchitectIconIntegration
    {
        private static readonly Dictionary<string, Texture2D?> _categoryTexCache = new Dictionary<string, Texture2D?>();

        public static Texture2D? GetCategoryTexture(string defName)
        {
            if (_categoryTexCache.TryGetValue(defName, out Texture2D? cached))
                return cached;

            Texture2D? tex = ContentFinder<Texture2D>.Get("UI/Console/Categories/" + defName, false);

            if (tex == null)
                tex = ContentFinder<Texture2D>.Get("UI/ArchitectIcons/" + defName, false);

            if (tex == null)
                tex = ContentFinder<Texture2D>.Get("UI/ArchitectIcons/Default/" + defName, false);

            _categoryTexCache[defName] = tex;
            return tex;
        }
    }
}
