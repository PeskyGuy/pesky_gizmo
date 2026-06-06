using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class NavScrollHelper
    {
        private static readonly Stack<Rect> _activeScrollViews = new Stack<Rect>();
        private static readonly Dictionary<Rect, Rect> _pendingScrolls = new Dictionary<Rect, Rect>();

        public static Rect? ActiveOutRect
        {
            get
            {
                if (_activeScrollViews.Count > 0)
                    return _activeScrollViews.Peek();
                return null;
            }
        }

        public static void Push(Rect outRect)
        {
            _activeScrollViews.Push(outRect);
        }

        public static void Pop()
        {
            if (_activeScrollViews.Count > 0)
                _activeScrollViews.Pop();
        }

        public static void RequestScroll(Rect outRect, Rect localTarget)
        {
            _pendingScrolls[outRect] = localTarget;
        }

        public static bool TryConsumePendingScroll(Rect outRect, out Rect localTarget)
        {
            if (_pendingScrolls.TryGetValue(outRect, out localTarget))
            {
                _pendingScrolls.Remove(outRect);
                return true;
            }
            return false;
        }
    }
}
