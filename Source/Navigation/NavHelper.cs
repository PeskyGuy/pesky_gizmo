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
        public Rect? parentScrollOutRect;
        public bool isObscured;
    }

    public static class NavHelper
    {
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

        private static float RowClusterEpsilonPx = 6f;
        private static float ScreenCenterMatchEps = 0.5f;

        private static Window? _lastSeenTopWindow = null;
        private static ProgramState? _lastProgramState;
        private static readonly NavItemTracker _tracker = new NavItemTracker();
        private static int _lastSnapshotCount = 0;

        public static readonly INavPanel Instance = new NavPanelAdapter(
            isOpen: () => CurrentTopWindow() != null || Current.ProgramState == ProgramState.Entry,
            hidesCursor: () => true,
            handleInput: HandleInput,
            draw: () => {}
        );

        private static Window? CurrentTopWindow()
        {
            var ws = Find.WindowStack;
            if (ws == null) return null;
            for (int i = ws.Count - 1; i >= 0; i--)
            {
                var w = ws[i];
                if (w == null || w is ImmediateWindow || w.IsDebug) continue;
                if (w.layer == WindowLayer.Dialog
                    || w.layer == WindowLayer.Super
                    || w.layer == WindowLayer.SubSuper
                    || (w.layer == WindowLayer.GameUI && w is RimWorld.MainTabWindow))
                {
                    if (w is RimWorld.MainTabWindow_Inspect) continue;
                    if (w is RimWorld.MainTabWindow_Architect) continue;
                    return w;
                }
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

        private static bool IsOccludedByWindow(Vector2 screenCenter)
        {
            var ws = Find.WindowStack;
            if (ws == null) return false;
            
            var top = CurrentTopWindow();
            if (top == null) return false;

            bool foundTop = false;
            var windows = ws.Windows;
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w == top) 
                {
                    foundTop = true;
                    continue;
                }
                
                if (foundTop)
                {
                    // Ignore tooltips which usually don't have backgrounds and don't absorb input
                    if (w is ImmediateWindow iw && !iw.doWindowBackground && !iw.absorbInputAroundWindow)
                        continue;

                    if (w.windowRect.Contains(screenCenter))
                        return true;
                }
            }
            return false;
        }

        public static bool IsActive => Instance.IsOpen && _tracker.SnapshotCount > 0;
        public static bool HasSelection => _hasSelection;

        private static Vector2 ComputeScreenCenter(Rect localRect)
        {
            try { return GUIUtility.GUIToScreenPoint(localRect.center); }
            catch
            {
                // GUIUtility matrix not set during Layout events; fall back to local coords
                return localRect.center;
            }
        }

        private static void SwapIfNewFrame()
        {
            _tracker.SwapIfNewFrame(ScreenCenterMatchEps);
        }


        public static void RecordButton(Rect rect, string label, bool active)
        {
            if (!Instance.IsOpen || !ShouldRecord()) return;
            SwapIfNewFrame();
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (IsOccludedByWindow(sc)) return;
            if (_tracker.CurrentFrameContainsNearby(sc, ScreenCenterMatchEps)) return;
            _tracker.Record(new TrackedItem { rect = rect, screenCenter = sc, label = label, active = active, kind = ItemKind.Brown, parentScrollOutRect = NavScrollHelper.ActiveOutRect });
        }

        public static void RecordInvisibleButton(Rect rect)
        {
            if (!Instance.IsOpen || !ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (IsOccludedByWindow(sc)) return;
            SwapIfNewFrame();
            if (_tracker.CurrentFrameContainsNearby(sc, ScreenCenterMatchEps)) return;
            _tracker.Record(new TrackedItem { rect = rect, screenCenter = sc, kind = ItemKind.Invisible, active = true, parentScrollOutRect = NavScrollHelper.ActiveOutRect });
        }

        public static void RecordRadioButton(Rect rect, bool chosen)
        {
            if (!Instance.IsOpen || !ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (IsOccludedByWindow(sc)) return;
            SwapIfNewFrame();
            if (_tracker.CurrentFrameContainsNearby(sc, ScreenCenterMatchEps)) return;
            _tracker.Record(new TrackedItem { rect = rect, screenCenter = sc, kind = ItemKind.Radio, active = !chosen, parentScrollOutRect = NavScrollHelper.ActiveOutRect });
        }

        private static readonly Dictionary<long, Action> _customActions = new Dictionary<long, Action>();

        public static void RecordCustomTarget(Rect rect, string label, Action onActivate)
        {
            if (!Instance.IsOpen || !ShouldRecord()) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (IsOccludedByWindow(sc)) return;
            SwapIfNewFrame();
            if (_tracker.CurrentFrameContainsNearby(sc, ScreenCenterMatchEps)) return;
            long hash = (long)(sc.x * 100) * 1000000L + (long)(sc.y * 100);
            _customActions[hash] = onActivate;
            _tracker.Record(new TrackedItem { rect = rect, screenCenter = sc, label = label, active = true, kind = ItemKind.Brown });
        }

        public static bool TryInvokeCustomAction(Vector2 screenCenter)
        {
            long hash = (long)(screenCenter.x * 100) * 1000000L + (long)(screenCenter.y * 100);
            if (_customActions.TryGetValue(hash, out var action))
            {
                try { action(); } catch (System.Exception ex) { GizmoLog.ErrorOnce($"Custom action failed: {ex.Message}", 555001); }
                return true;
            }
            return false;
        }

        public static void RecordSlider(Rect rect, float value, float min, float max, float roundTo)
        {
            if (!Instance.IsOpen || !ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (IsOccludedByWindow(sc)) return;
            SwapIfNewFrame();
            if (_tracker.CurrentFrameContainsNearby(sc, ScreenCenterMatchEps)) return;
            _tracker.Record(new TrackedItem
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
            if (!Instance.IsOpen || !ShouldRecord()) return;
            if (rect.width < 4f || rect.height < 4f) return;
            var sc = ComputeScreenCenter(rect);
            if (!IsScreenPosOnScreen(sc)) return;
            if (IsOccludedByWindow(sc)) return;
            SwapIfNewFrame();
            if (_tracker.CurrentFrameContainsNearby(sc, ScreenCenterMatchEps)) return;
            _tracker.Record(new TrackedItem
            {
                rect = rect,
                screenCenter = sc,
                kind = ItemKind.TextField,
                active = true
            });
        }

        public static bool ShouldSimulateClick(Rect rect)
        {
            if (!Instance.IsOpen || !ShouldRecord()) return false;
            if (_simulatedClickFrame != Time.frameCount) return false;
            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(rect.center); }
            catch
            {
                // GUIUtility matrix not set during Layout events; fall back to local coords
                screenCenter = rect.center;
            }
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
            catch
            {
                // GUIUtility matrix not set during Layout events; fall back to local coords
                screenCenter = rect.center;
            }
            return Vector2.Distance(screenCenter, _textFieldFocusScreenCenter) < ScreenCenterMatchEps;
        }

        private static bool IsTextFieldSelected()
        {
            if (!_hasSelection) return false;
            for (int i = 0; i < _tracker.SnapshotCount; i++)
            {
                var item = _tracker.Snapshot[i];
                if (item.isObscured) continue;
                if (!item.active) continue;
                if (!RectApproxEquals(item.rect, _selectedRect) &&
                    Vector2.Distance(item.screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps)
                    continue;
                if (item.kind == ItemKind.TextField)
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
            if (_tracker.SnapshotCount == 0)
            {
                _hasSelection = false;
                return;
            }

            Vector2 guiCursorPos = new Vector2(ControllerState.CursorScreenPos.x, Screen.height - ControllerState.CursorScreenPos.y);

            TrackedItem bestItem = _tracker.Snapshot[0];
            float bestDistSq = float.MaxValue;

            foreach (var item in _tracker.Snapshot)
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
        }




        public static void HandleInput()
        {
            if (_tracker.SnapshotCount == 0)
            {
                _hasSelection = false;
                return;
            }

            var curTop = CurrentTopWindow();
            bool justEnteredEntry = Current.ProgramState == ProgramState.Entry && _lastProgramState != ProgramState.Entry;
            _lastProgramState = Current.ProgramState;

            int currentSnapCount = _tracker.SnapshotCount;
            bool justGainedItems = currentSnapCount > 0 && _lastSnapshotCount == 0;
            _lastSnapshotCount = currentSnapCount;

            bool forceMainMenuReset = Current.ProgramState == ProgramState.Entry && curTop == null && justGainedItems;

            if (curTop != _lastSeenTopWindow || justEnteredEntry || forceMainMenuReset)
            {
                _lastSeenTopWindow = curTop;
                _hasSelection = false;
            }

            if (!_hasSelection && _tracker.SnapshotCount > 0)
            {
                InitializeSelection();
            }

            float stickMag = Mathf.Sqrt(ControllerState.CursorAxisX * ControllerState.CursorAxisX
                                       + ControllerState.CursorAxisY * ControllerState.CursorAxisY);
            if (stickMag > 0.5f)
            {
                _hasSelection = false;
            }

            bool up = ControllerState.DpadUpEdge;
            bool down = ControllerState.DpadDownEdge;
            bool left = ControllerState.DpadLeftEdge;
            bool right = ControllerState.DpadRightEdge;

            if (_hasSelection && !ContainsScreenCenter(_tracker.Snapshot, _selectedScreenCenter))
                _hasSelection = false;

            if (_hasSelection && TryGetSliderUnderSelection(out int sliderIdx))
            {
                var s = _tracker.Snapshot[sliderIdx];
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

                    try { SoundDefOf.DragSlider?.PlayOneShotOnCamera(); }
                    catch { /* Best-effort audio; camera may not be available */ }
                    left = right = false;
                }
            }

            if (up || down || left || right)
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

                if (_hasSelection && !IsTextFieldSelected())
                {
                    GUIUtility.keyboardControl = 0;
                }
            }

            if (ControllerState.ButtonAEdge)
            {
                if (_hasSelection)
                {
                    if (!TryInvokeCustomAction(_selectedScreenCenter))
                    {
                        _simulatedClickRect = _selectedRect;
                        _simulatedClickScreenCenter = _selectedScreenCenter;
                        _simulatedClickFrame = Time.frameCount;
                    }
                    try { SoundDefOf.Click?.PlayOneShotOnCamera(); }
                    catch { /* Best-effort audio; camera may not be available */ }

                    if (IsTextFieldSelected())
                    {
                        _textFieldFocusRect = _selectedRect;
                        _textFieldFocusScreenCenter = _selectedScreenCenter;
                        _textFieldFocusFrame = Time.frameCount;
                    }
                }
            }

            if (up || down || left || right || ControllerState.ButtonAEdge || ControllerState.ButtonXEdge || ControllerState.ButtonYEdge)
            {
                if (_hasSelection || Instance.IsOpen)
                {
                    ConsoleHUD.ConsumeInput();
                }
            }
        }

        public static bool IsSelectedAndActive(Rect rect)
        {
            if (!ShouldRecord()) return false;
            if (!IsActive || !_hasSelection) return false;
            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(rect.center); }
            catch
            {
                // GUIUtility matrix not set during Layout events; fall back to local coords
                screenCenter = rect.center;
            }
            return Vector2.Distance(screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps;
        }

        public static bool IsNearSelected(Rect rect)
        {
            if (!_hasSelection) return false;
            Vector2 screenCenter;
            try { screenCenter = GUIUtility.GUIToScreenPoint(rect.center); }
            catch
            {
                // GUIUtility matrix not set during Layout events; fall back to local coords
                screenCenter = rect.center;
            }
            return Vector2.Distance(screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps;
        }

        public static void DrawSelectionHighlight(Rect rect)
        {
            if (Event.current?.type != EventType.Repaint) return;
            Rect highlightRect = rect.ExpandedBy(-1f);
            Color prevCol = GUI.color;
            
            float pulse = (Mathf.Sin(Time.realtimeSinceStartup * 8f) + 1f) / 2f; // 0.0 to 1.0
            float alphaFill = Mathf.Lerp(0.15f, 0.3f, pulse);
            float alphaBorder = Mathf.Lerp(0.6f, 1f, pulse);

            GUI.color = new Color(0f, 1f, 1f, alphaFill);
            GUI.DrawTexture(highlightRect, BaseContent.WhiteTex);
            GUI.color = new Color(0f, 1f, 1f, alphaBorder);
            Widgets.DrawBox(highlightRect, 2);
            GUI.color = prevCol;
        }

        private static bool TryGetSliderUnderSelection(out int idx)
        {
            for (int i = 0; i < _tracker.SnapshotCount; i++)
            {
                if (_tracker.Snapshot[i].kind == ItemKind.Slider
                    && Vector2.Distance(_tracker.Snapshot[i].screenCenter, _selectedScreenCenter) < ScreenCenterMatchEps)
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
            foreach (var item in _tracker.Snapshot)
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
            foreach (var item in _tracker.Snapshot)
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

            foreach (var b in _tracker.Snapshot)
            {
                if (b.isObscured) continue;
                if (!b.active) continue;
                if (Vector2.Distance(b.screenCenter, curCenter) < ScreenCenterMatchEps) continue;

                float dxTo = b.screenCenter.x - curCenter.x;
                float dyTo = b.screenCenter.y - curCenter.y;

                if (dy != 0)
                {
                    if (Mathf.Sign(dyTo) != Mathf.Sign(dy)) continue;
                    if (Mathf.Abs(dyTo) < RowClusterEpsilonPx) continue;

                    // Cone check: reject items that are too far off-axis (more than 45 degrees)
                    if (Mathf.Abs(dxTo) > Mathf.Abs(dyTo) * 1.5f) continue;

                    // Massive penalty for off-axis drift to prioritize same column
                    float score = Mathf.Abs(dyTo) + Mathf.Abs(dxTo) * 10f;
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

                    // Cone check: reject items that are too far off-axis (more than 45 degrees)
                    if (Mathf.Abs(dyTo) > Mathf.Abs(dxTo) * 1.5f) continue;

                    // Massive penalty for off-axis drift to prioritize same row
                    float score = Mathf.Abs(dxTo) + Mathf.Abs(dyTo) * 10f;
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
                HapticManager.UI_DpadNav();

                if (bestItem.parentScrollOutRect.HasValue)
                {
                    NavScrollHelper.RequestScroll(bestItem.parentScrollOutRect.Value, bestItem.rect);
                }
            }
            else if (GizmoMod.Settings.navWrapAround)
            {
                var wrapped = FindFarthest(dx * -1, dy * -1);
                if (wrapped.rect.width > 0f)
                {
                    _selectedRect      = wrapped.rect;
                    _selectedScreenCenter = wrapped.screenCenter;
                }
            }
        }

        private static TrackedItem FindStarter()
        {
            TrackedItem best = default;
            bool found = false;

            foreach (var b in _tracker.Snapshot)
            {
                if (b.isObscured) continue;
                if (!b.active) continue;

                if (!found || b.screenCenter.y < best.screenCenter.y - 0.5f
                    || (Mathf.Abs(b.screenCenter.y - best.screenCenter.y) < 0.5f && b.screenCenter.x < best.screenCenter.x))
                {
                    best = b;
                    found = true;
                }
            }
            return found ? best : (_tracker.SnapshotCount > 0 ? _tracker.Snapshot[0] : default);
        }

        private static TrackedItem FindFarthest(int dx, int dy)
        {
            var curCenter = _selectedScreenCenter;
            TrackedItem best = default;
            float bestScore  = float.MinValue;

            foreach (var b in _tracker.Snapshot)
            {
                if (b.isObscured) continue;
                if (!b.active) continue;
                if (Vector2.Distance(b.screenCenter, curCenter) < ScreenCenterMatchEps) continue;
                float dxTo = b.screenCenter.x - curCenter.x;
                float dyTo = b.screenCenter.y - curCenter.y;

                float score = dy != 0
                    ? Mathf.Abs(dyTo) + Mathf.Abs(dxTo) * 2f
                    : Mathf.Abs(dxTo) + Mathf.Abs(dyTo) * 2f;

                if (score > bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        private static bool ContainsScreenCenter(IReadOnlyList<TrackedItem> list, Vector2 target)
        {
            for (int i = 0; i < list.Count; i++)
                if (Vector2.Distance(list[i].screenCenter, target) < ScreenCenterMatchEps) return true;
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
