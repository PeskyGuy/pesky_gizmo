using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace PeskyGizmo
{
    public enum ItemKind { Brown, Invisible, Radio, Slider, TextField }

    public struct TrackedItem
    {
        public Rect rect;
        public Vector2 screenCenter;
        public string label;
        public bool active;
        public ItemKind kind;
        public float sliderValue;
        public float sliderMin;
        public float sliderMax;
        public float sliderRoundTo;
    }

    public static class NavHelper
    {
        private static List<TrackedItem> _currentFrame = new List<TrackedItem>();
        private static List<TrackedItem> _snapshot = new List<TrackedItem>();
        private static int _lastSeenFrame = -1;

        private static Rect _selectedRect;
        private static Vector2 _selectedScreenCenter;
        private static bool _hasSelection = false;

        private static Rect _simulatedClickRect;
        private static Vector2 _simulatedClickScreenCenter;
        private static int _simulatedClickFrame = -1;

        private static Rect _sliderOverrideRect;
        private static float _sliderOverrideValue;
        private static int _sliderOverrideFrame = -1;

        private static Rect _textFieldFocusRect;
        private static Vector2 _textFieldFocusScreenCenter;
        private static int _textFieldFocusFrame = -1;

        private static Rect _currentRect;
        private static int _currentRectFrame = -1;

        private const float RowClusterEpsilonPx = 6f;
        private const float ScreenCenterMatchEps = 2f;

        private static bool _menuNavModeActive = false;
        private static Window _lastSeenTopWindow = null;

        private static Window CurrentTopWindow()
        {
            var ws = Find.WindowStack;
            if (ws == null) return null;
            for (int i = ws.Count - 1; i >= 0; i--)
            {
                var w = ws[i];
                if (w == null || w is ImmediateWindow || w.IsDebug) continue;
                if (w.layer == WindowLayer.Dialog
                    || w.layer == WindowLayer.Super
                    || w.layer == WindowLayer.SubSuper)
                    return w;
            }
            return null;
        }

        private static bool ShouldRecord()
        {
            var ws = Find.WindowStack;
            if (ws == null) return true;
            var top = CurrentTopWindow();
            return ws.currentlyDrawnWindow == top;
        }

        public static bool HasSelection => _hasSelection;

        public static bool MenuNavModeActive
        {
            get => _menuNavModeActive;
            set
            {
                if (_menuNavModeActive == value) return;
                _menuNavModeActive = value;
                if (!_menuNavModeActive)
                {
                    _hasSelection = false;
                }
                else
                {
                    InitializeSelection();
                    ZoomMode.Exit();
                }
            }
        }

        public static bool IsActive => ControllerState.ControllerModeEnabled && _menuNavModeActive && _snapshot.Count > 0;

        private static Vector2 ComputeScreenCenter(Rect localRect)
        {
            try { return GUIUtility.GUIToScreenPoint(localRect.center); }
            catch { return localRect.center; }
        }

        private static bool HasNearbyScreenCenter(List<TrackedItem> list, Vector2 sc)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Vector2.Distance(list[i].screenCenter, sc) < ScreenCenterMatchEps)
                    return true;
            }
            return false;
        }

        private static void SwapIfNewFrame()
        {
            int f = Time.frameCount;
            if (f != _lastSeenFrame)
            {
                _lastSeenFrame = f;
                var tmp = _snapshot;
                _snapshot = _currentFrame;
                _currentFrame = tmp;
                _currentFrame.Clear();

                DeduplicateSnapshot();
            }
        }

        private static void DeduplicateSnapshot()
        {
            if (_snapshot.Count <= 1) return;

            for (int i = _snapshot.Count - 1; i >= 1; i--)
            {
                bool dupe = false;
                for (int j = 0; j < i; j++)
                {
                    if (Vector2.Distance(_snapshot[i].screenCenter, _snapshot[j].screenCenter) < ScreenCenterMatchEps)
                    {
                        dupe = true;
                        break;
                    }
                }
                if (dupe)
                {
                    _snapshot.RemoveAt(i);
                }
            }
        }

        public static void RecordButton(Rect rect, string label, bool active)
        {
            if (!ShouldRecord()) return;
            SwapIfNewFrame();
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (HasNearbyScreenCenter(_currentFrame, sc)) return;
            _currentFrame.Add(new TrackedItem { rect = rect, screenCenter = sc, label = label, active = active, kind = ItemKind.Brown });
        }

        public static void RecordInvisibleButton(Rect rect)
        {
            if (!ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            SwapIfNewFrame();
            if (HasNearbyScreenCenter(_currentFrame, sc)) return;
            _currentFrame.Add(new TrackedItem { rect = rect, screenCenter = sc, kind = ItemKind.Invisible, active = true });
        }

        public static void RecordRadioButton(Rect rect, bool chosen)
        {
            if (!ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            SwapIfNewFrame();
            if (HasNearbyScreenCenter(_currentFrame, sc)) return;
            _currentFrame.Add(new TrackedItem { rect = rect, screenCenter = sc, kind = ItemKind.Radio, active = !chosen });
        }

        public static void RecordSlider(Rect rect, float value, float min, float max, float roundTo)
        {
            if (!ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            SwapIfNewFrame();
            if (HasNearbyScreenCenter(_currentFrame, sc)) return;
            _currentFrame.Add(new TrackedItem
            {
                rect = rect,
                screenCenter = sc,
                kind = ItemKind.Slider,
                active = true,
                sliderValue = value,
                sliderMin = min,
                sliderMax = max,
                sliderRoundTo = roundTo
            });
        }

        public static void RecordTextField(Rect rect)
        {
            if (!ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            SwapIfNewFrame();
            if (HasNearbyScreenCenter(_currentFrame, sc)) return;
            _currentFrame.Add(new TrackedItem
            {
                rect = rect,
                screenCenter = sc,
                kind = ItemKind.TextField,
                active = true
            });
        }

        public static bool ShouldSimulateClick(Rect rect)
        {
            if (!ShouldRecord()) return false;
            if (_simulatedClickFrame != Time.frameCount) return false;
            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(rect.center); }
            catch { screenCenter = rect.center; }
            if (Vector2.Distance(screenCenter, _simulatedClickScreenCenter) > ScreenCenterMatchEps) return false;
            _simulatedClickFrame = -1;
            return true;
        }

        public static bool TryGetSliderOverride(Rect rect, out float newValue)
        {
            if (!ShouldRecord())
            {
                newValue = 0f;
                return false;
            }
            if (_sliderOverrideFrame == Time.frameCount && RectApproxEquals(rect, _sliderOverrideRect))
            {
                newValue = _sliderOverrideValue;
                return true;
            }
            newValue = 0f;
            return false;
        }

        public static bool ShouldFocusTextField(Rect rect)
        {
            if (!ShouldRecord()) return false;
            if (_textFieldFocusFrame != Time.frameCount) return false;
            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(rect.center); }
            catch { screenCenter = rect.center; }
            return Vector2.Distance(screenCenter, _textFieldFocusScreenCenter) < ScreenCenterMatchEps;
        }

        private static bool IsTextFieldSelected()
        {
            if (!_hasSelection) return false;
            for (int i = 0; i < _snapshot.Count; i++)
            {
                if (_snapshot[i].kind == ItemKind.TextField
                    && Vector2.Distance(_snapshot[i].screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps)
                    return true;
            }
            return false;
        }

        public static void MarkCurrent(Rect rect)
        {
            _currentRect = rect;
            _currentRectFrame = Time.frameCount;
        }

        private static void InitializeSelection()
        {
            if (_snapshot.Count == 0)
            {
                _hasSelection = false;
                return;
            }

            Vector2 guiCursorPos = new Vector2(ControllerState.CursorScreenPos.x, Screen.height - ControllerState.CursorScreenPos.y);

            TrackedItem bestItem = _snapshot[0];
            float bestDistSq = float.MaxValue;

            foreach (var item in _snapshot)
            {
                float distSq = (item.screenCenter - guiCursorPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestItem = item;
                }
            }

            _selectedRect = bestItem.rect;
            _selectedScreenCenter = bestItem.screenCenter;
            _hasSelection = true;

            SnapCursorToSelected();
        }

        private static void SnapCursorToSelected()
        {
            if (!_hasSelection) return;
            Vector2 unityPos = new Vector2(_selectedScreenCenter.x, Screen.height - _selectedScreenCenter.y);
            ControllerState.CursorScreenPos = unityPos;
            if (!NavController.AnyCursorHidingPanelOpen)
                MouseSimulation.SyncWindowsCursor();
        }

        public static void Tick()
        {
            if (NavController.IsCustomUIActive
                || ZoomMode.Active
                || (Current.ProgramState == ProgramState.Playing && (Find.DesignatorManager?.SelectedDesignator != null || Find.Targeter?.IsTargeting == true)))
            {
                if (_menuNavModeActive) MenuNavModeActive = false;
                return;
            }

            var curTop = CurrentTopWindow();
            if (curTop != _lastSeenTopWindow)
            {
                _lastSeenTopWindow = curTop;
                _hasSelection = false;
                if (curTop != null)
                {
                    MenuNavModeActive = true;
                }
            }

            if (_menuNavModeActive && !_hasSelection && _snapshot.Count > 0)
            {
                InitializeSelection();
            }

            if (_snapshot.Count == 0)
            {
                _hasSelection = false;
                return;
            }

            if (ControllerState.ButtonRBEdge)
            {
                MenuNavModeActive = !MenuNavModeActive;
                SoundDefOf.Tick_Tiny?.PlayOneShotOnCamera();
            }

            if (_menuNavModeActive)
            {
                float stickMag = Mathf.Sqrt(ControllerState.CursorAxisX * ControllerState.CursorAxisX
                                           + ControllerState.CursorAxisY * ControllerState.CursorAxisY);
                if (stickMag > 0.5f)
                {
                    MenuNavModeActive = false;
                }
            }

            bool up = ControllerState.DpadUpEdge;
            bool down = ControllerState.DpadDownEdge;
            bool left = ControllerState.DpadLeftEdge;
            bool right = ControllerState.DpadRightEdge;

            bool initializedThisFrame = false;
            if (up || down || left || right)
            {
                if (!_menuNavModeActive)
                {
                    MenuNavModeActive = true;
                    initializedThisFrame = true;
                }
            }

            if (!_menuNavModeActive)
            {
                return;
            }

            if (_hasSelection && !ContainsScreenCenter(_snapshot, _selectedScreenCenter))
                _hasSelection = false;

            if (_hasSelection && TryGetSliderUnderSelection(out int sliderIdx))
            {
                var s = _snapshot[sliderIdx];
                bool lb = ControllerState.ButtonLBEdge;
                bool rb = ControllerState.ButtonRBEdge;

                if (left) lb = true;
                if (right) rb = true;

                if (lb || rb)
                {
                    float step = s.sliderRoundTo > 0f ? s.sliderRoundTo : Mathf.Max(0.01f, (s.sliderMax - s.sliderMin) / 50f);
                    float dv = 0f;
                    if (lb) dv -= step;
                    if (rb) dv += step;

                    float newVal = Mathf.Clamp(s.sliderValue + dv, s.sliderMin, s.sliderMax);
                    if (s.sliderRoundTo > 0f) newVal = Mathf.Round(newVal / s.sliderRoundTo) * s.sliderRoundTo;

                    _sliderOverrideRect = s.rect;
                    _sliderOverrideValue = newVal;
                    _sliderOverrideFrame = Time.frameCount;

                    try { SoundDefOf.DragSlider?.PlayOneShotOnCamera(); } catch { }
                    left = right = false;
                }
            }

            if (!initializedThisFrame && (up || down || left || right))
            {
                if (!_hasSelection)
                {
                    if (_currentRectFrame >= Time.frameCount - 1 && TryGetItemMatchingRect(_currentRect, out var cur))
                    {
                        _selectedRect = cur.rect;
                        _selectedScreenCenter = cur.screenCenter;
                    }
                    else
                    {
                        var s = FindStarter();
                        _selectedRect = s.rect;
                        _selectedScreenCenter = s.screenCenter;
                    }
                    _hasSelection = true;
                }
                else
                {
                    if (up) Move(0, -1);
                    else if (down) Move(0, +1);
                    else if (left) Move(-1, 0);
                    else if (right) Move(+1, 0);
                }

                if (_hasSelection)
                {
                    SnapCursorToSelected();
                }

                if (_hasSelection && !IsTextFieldSelected())
                {
                    GUIUtility.keyboardControl = 0;
                }
            }

            if (ControllerState.ButtonAEdge)
            {
                if (_hasSelection)
                {
                    _simulatedClickRect = _selectedRect;
                    _simulatedClickScreenCenter = _selectedScreenCenter;
                    _simulatedClickFrame = Time.frameCount;
                    try { SoundDefOf.Click?.PlayOneShotOnCamera(); } catch { }

                    if (IsTextFieldSelected())
                    {
                        _textFieldFocusRect = _selectedRect;
                        _textFieldFocusScreenCenter = _selectedScreenCenter;
                        _textFieldFocusFrame = Time.frameCount;
                    }
                }
            }
        }

        public static bool IsSelectedAndActive(Rect rect)
        {
            if (!ShouldRecord()) return false;
            if (!IsActive || !_hasSelection) return false;
            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(rect.center); }
            catch { screenCenter = rect.center; }
            return Vector2.Distance(screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps;
        }

        public static void DrawSelectionHighlight(Rect rect)
        {
            if (Event.current?.type != EventType.Repaint) return;
            Rect highlightRect = rect.ExpandedBy(-1f);
            Color prevCol = GUI.color;
            GUI.color = new Color(0f, 1f, 1f, 0.15f);
            GUI.DrawTexture(highlightRect, BaseContent.WhiteTex);
            GUI.color = new Color(0f, 1f, 1f, 0.8f);
            Widgets.DrawBox(highlightRect, 2);
            GUI.color = prevCol;
        }

        private static bool TryGetSliderUnderSelection(out int idx)
        {
            for (int i = 0; i < _snapshot.Count; i++)
            {
                if (_snapshot[i].kind == ItemKind.Slider
                    && Vector2.Distance(_snapshot[i].screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps)
                {
                    idx = i;
                    return true;
                }
            }
            idx = -1;
            return false;
        }

        private static bool IsScreenPosOnScreen(Vector2 screenCenter)
        {
            return screenCenter.x >= 0f && screenCenter.x <= Screen.width
                && screenCenter.y >= 0f && screenCenter.y <= Screen.height;
        }

        private static bool TryGetItemUnderMouse(Vector2 guiMousePos, out TrackedItem hit)
        {
            foreach (var item in _snapshot)
            {
                var delta = item.screenCenter - item.rect.center;
                var screenRect = new Rect(item.rect.x + delta.x, item.rect.y + delta.y, item.rect.width, item.rect.height);
                if (screenRect.Contains(guiMousePos))
                {
                    hit = item;
                    return true;
                }
            }
            hit = default;
            return false;
        }

        private static bool TryGetItemMatchingRect(Rect rect, out TrackedItem hit)
        {
            foreach (var item in _snapshot)
            {
                if (RectApproxEquals(item.rect, rect))
                {
                    hit = item;
                    return true;
                }
            }
            hit = default;
            return false;
        }

        private static void Move(int dx, int dy)
        {
            var curCenter = _selectedScreenCenter;
            TrackedItem bestItem = default;
            float bestScore = float.MaxValue;
            bool found = false;

            foreach (var b in _snapshot)
            {
                if (Vector2.Distance(b.screenCenter, curCenter) < ScreenCenterMatchEps) continue;

                float dxTo = b.screenCenter.x - curCenter.x;
                float dyTo = b.screenCenter.y - curCenter.y;

                if (dy != 0)
                {
                    if (Mathf.Sign(dyTo) != Mathf.Sign(dy)) continue;
                    if (Mathf.Abs(dyTo) < RowClusterEpsilonPx) continue;

                    float score = Mathf.Abs(dyTo) + Mathf.Abs(dxTo) * 2f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestItem = b;
                        found = true;
                    }
                }
                else if (dx != 0)
                {
                    if (Mathf.Sign(dxTo) != Mathf.Sign(dx)) continue;
                    if (Mathf.Abs(dxTo) < RowClusterEpsilonPx) continue;

                    float score = Mathf.Abs(dxTo) + Mathf.Abs(dyTo) * 2f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestItem = b;
                        found = true;
                    }
                }
            }

            if (found)
            {
                _selectedRect = bestItem.rect;
                _selectedScreenCenter = bestItem.screenCenter;
            }
        }

        private static TrackedItem FindStarter()
        {
            TrackedItem best = _snapshot[0];
            foreach (var b in _snapshot)
            {
                if (b.screenCenter.y < best.screenCenter.y - 0.5f
                    || (Mathf.Abs(b.screenCenter.y - best.screenCenter.y) < 0.5f && b.screenCenter.x < best.screenCenter.x))
                {
                    best = b;
                }
            }
            return best;
        }

        private static bool ContainsScreenCenter(List<TrackedItem> list, Vector2 target)
        {
            foreach (var b in list)
                if (Vector2.Distance(b.screenCenter, target) < ScreenCenterMatchEps) return true;
            return false;
        }

        private static bool RectApproxEquals(Rect a, Rect b)
        {
            const float eps = 1f;
            return Mathf.Abs(a.x - b.x) < eps
                && Mathf.Abs(a.y - b.y) < eps
                && Mathf.Abs(a.width - b.width) < eps
                && Mathf.Abs(a.height - b.height) < eps;
        }
    }
}
