using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    internal sealed class NavItemTracker
    {
        private List<TrackedItem> _currentFrame = new List<TrackedItem>();
        private List<TrackedItem> _snapshot = new List<TrackedItem>();
        private int _lastSeenFrame = -1;

        public IReadOnlyList<TrackedItem> Snapshot => _snapshot;
        public int SnapshotCount => _snapshot.Count;
        public int CurrentFrameCount => _currentFrame.Count;

        public TrackedItem GetFromCurrentFrame(int index) => _currentFrame[index];

        public bool CurrentFrameContainsNearby(Vector2 screenCenter, float epsilon)
        {
            for (int i = 0; i < _currentFrame.Count; i++)
                if (Vector2.Distance(_currentFrame[i].screenCenter, screenCenter) < epsilon)
                    return true;
            return false;
        }

        public void Record(TrackedItem item)
        {
            _currentFrame.Add(item);
        }

        public void SwapIfNewFrame(float dedupEpsilon)
        {
            int f = Time.frameCount;
            if (f == _lastSeenFrame) return;
            _lastSeenFrame = f;
            (_snapshot, _currentFrame) = (_currentFrame, _snapshot);
            _currentFrame.Clear();
            Deduplicate(dedupEpsilon);
            MarkObscuredItems();
        }

        private void MarkObscuredItems()
        {
            for (int i = 0; i < _snapshot.Count; i++)
            {
                var item = _snapshot[i];
                for (int j = i + 1; j < _snapshot.Count; j++)
                {
                    var laterItem = _snapshot[j];
                    if (laterItem.rect.Contains(item.screenCenter))
                    {
                        item.isObscured = true;
                        _snapshot[i] = item;
                        break;
                    }
                }
            }
        }

        private void Deduplicate(float epsilon)
        {
            if (_snapshot.Count <= 1) return;
            for (int i = _snapshot.Count - 1; i >= 1; i--)
            {
                bool dupe = false;
                for (int j = 0; j < i; j++)
                {
                    if (Vector2.Distance(_snapshot[i].screenCenter, _snapshot[j].screenCenter) < epsilon)
                    {
                        dupe = true;
                        break;
                    }
                }
                if (dupe) _snapshot.RemoveAt(i);
            }
        }

        public void Reset()
        {
            _currentFrame.Clear();
            _snapshot.Clear();
        }
    }
}
