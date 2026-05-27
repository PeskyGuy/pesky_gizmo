using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class UIAimAssist
    {
        private const float AssistRadiusGui = 70f;
        private const float MaxPullSpeedGui = 900f;
        private const float PullFollowFactor = 8f;
        private const float MinRectSize = 14f;
        private const float MaxRectSize = 600f;

        private static readonly List<Rect> _rects = new List<Rect>(256);
        private static int _frameStamp = -1;

        public static void RecordRect(Rect r)
        {
            if (Time.frameCount != _frameStamp)
            {
                _rects.Clear();
                _frameStamp = Time.frameCount;
            }
            if (r.width < MinRectSize || r.height < MinRectSize) return;
            if (r.width > MaxRectSize || r.height > MaxRectSize) return;
            _rects.Add(r);
        }

        public static Vector2 ComputePullScreen(Vector2 cursorScreen, float stickX, float stickY)
        {
            if (_rects.Count == 0) return Vector2.zero;
            float magSq = stickX * stickX + stickY * stickY;
            if (magSq < 0.04f) return Vector2.zero;

            float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
            float guiX = cursorScreen.x / scale;
            float guiY = (Screen.height - cursorScreen.y) / scale;
            Vector2 guiPos = new Vector2(guiX, guiY);

            // Pick rect closest to cursor that lies roughly in the direction of stick travel.
            Vector2 stickDir = new Vector2(stickX, -stickY).normalized;
            float bestScore = float.MaxValue;
            Vector2 bestCenter = Vector2.zero;
            bool found = false;
            float radiusSq = AssistRadiusGui * AssistRadiusGui;

            for (int i = 0; i < _rects.Count; i++)
            {
                Vector2 c = _rects[i].center;
                Vector2 toC = c - guiPos;
                float dSq = toC.sqrMagnitude;
                if (dSq > radiusSq) continue;

                float dirBias = 1f;
                if (dSq > 1f)
                {
                    float dot = Vector2.Dot(toC.normalized, stickDir);
                    if (dot < -0.2f) continue;
                    dirBias = Mathf.Lerp(1.6f, 0.7f, (dot + 1f) * 0.5f);
                }

                float score = dSq * dirBias;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCenter = c;
                    found = true;
                }
            }

            if (!found) return Vector2.zero;

            Vector2 toTarget = bestCenter - guiPos;
            float pullStrength = Mathf.Sqrt(magSq);
            Vector2 pullGui = toTarget * PullFollowFactor * pullStrength * Time.unscaledDeltaTime;
            float maxStep = MaxPullSpeedGui * pullStrength * Time.unscaledDeltaTime;
            if (pullGui.sqrMagnitude > maxStep * maxStep)
                pullGui = pullGui.normalized * maxStep;

            return new Vector2(pullGui.x * scale, -pullGui.y * scale);
        }
    }
}
