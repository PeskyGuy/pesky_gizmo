using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public class CalibrationWindow : Window
    {
        private enum Step
        {
            Start,
            PressA, PressB, PressX, PressY,
            PressLB, PressRB,
            PressBack, PressStart,
            PressL3, PressR3,
            PushLS_Up, PushLS_Right,
            PushRS_Up, PushRS_Right,
            PullLT, PullRT,
            Done
        }

        private Step _currentStep = Step.Start;
        private float _debounce;
        private const float DEBOUNCE_TIME = 0.4f;
        private string _lastSeen = "";

        private readonly HashSet<int> _usedButtons = new HashSet<int>();

        public override Vector2 InitialSize => new Vector2(520f, 380f);

        public CalibrationWindow()
        {
            doCloseButton = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Block the Patch_RootUpdate B-button cancel logic while calibrating.
            if (ControllerState.ControllerModeEnabled && ControllerState.ButtonBEdge)
            {
                Event.current.Use();
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 36), "Controller Calibration");
            Text.Font = GameFont.Small;

            string prompt = GetPrompt();
            Widgets.Label(new Rect(0, 50, inRect.width, 60), prompt);

            if (!string.IsNullOrEmpty(_lastSeen))
                Widgets.Label(new Rect(0, 120, inRect.width, 24), $"Currently seeing: {_lastSeen}");

            int stepNum = (int)_currentStep;
            int total = (int)Step.Done;
            if (stepNum > 0 && stepNum < total)
                Widgets.Label(new Rect(0, 145, inRect.width, 24), $"Step {stepNum} of {total - 1}");

            if (_currentStep == Step.Start)
            {
                if (Widgets.ButtonText(new Rect(0, inRect.height - 40, 200, 35), "Begin"))
                {
                    _usedButtons.Clear();
                    _currentStep = Step.PressA;
                }
            }
            else if (_currentStep == Step.Done)
            {
                Widgets.Label(new Rect(0, 170, inRect.width, 60), "Calibration complete!");
                if (Widgets.ButtonText(new Rect(0, inRect.height - 40, 200, 35), "Save & Close"))
                {
                    CommandableMod.Settings.Write();
                    Close();
                }
            }
            else
            {
                HandleInput();
            }
        }

        private void HandleInput()
        {
            if (Time.realtimeSinceStartup < _debounce) return;
            NativeInput.Poll();

            if (_currentStep >= Step.PressA && _currentStep <= Step.PressR3)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (NativeInput.IsButtonDown(i) && !_usedButtons.Contains(i))
                    {
                        _lastSeen = $"Button {i}";
                        SaveButton(i);
                        _usedButtons.Add(i);
                        Advance();
                        return;
                    }
                }
            }
            else if (_currentStep >= Step.PushLS_Up && _currentStep <= Step.PushRS_Right)
            {
                CheckStickAxis("X", NativeInput.RawX);
                CheckStickAxis("Y", NativeInput.RawY);
                CheckStickAxis("Z", NativeInput.RawZ);
                CheckStickAxis("R", NativeInput.RawR);
            }
            else if (_currentStep == Step.PullLT || _currentStep == Step.PullRT)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (NativeInput.IsButtonDown(i) && !_usedButtons.Contains(i))
                    {
                        _lastSeen = $"Button {i}";
                        SaveTriggerButton(i);
                        _usedButtons.Add(i);
                        Advance();
                        return;
                    }
                }

                CheckTriggerAxis("U", NativeInput.RawU);
                CheckTriggerAxis("V", NativeInput.RawV);
            }
        }

        private void CheckStickAxis(string name, float val)
        {
            float abs = Mathf.Abs(val);
            if (abs > 0.7f)
            {
                _lastSeen = $"Axis {name} = {val:F2}";
                SaveAxis(name);
                Advance();
            }
        }

        private void CheckTriggerAxis(string name, float val)
        {
            if (Mathf.Abs(val) < 0.3f)
            {
                _lastSeen = $"Axis {name} = {val:F2}";
                SaveTriggerAxis(name);
                Advance();
            }
        }

        private void SaveButton(int idx)
        {
            var s = CommandableMod.Settings;
            switch (_currentStep)
            {
                case Step.PressA:     s.btnA = idx; break;
                case Step.PressB:     s.btnB = idx; break;
                case Step.PressX:     s.btnX = idx; break;
                case Step.PressY:     s.btnY = idx; break;
                case Step.PressLB:    s.btnLB = idx; break;
                case Step.PressRB:    s.btnRB = idx; break;
                case Step.PressBack:  s.btnBack = idx; break;
                case Step.PressStart: s.btnStart = idx; break;
                case Step.PressL3:    s.btnL3 = idx; break;
                case Step.PressR3:    s.btnR3 = idx; break;
            }
        }

        private void SaveAxis(string name)
        {
            var s = CommandableMod.Settings;
            switch (_currentStep)
            {
                case Step.PushLS_Up:    s.axisLS_Y = name; break;
                case Step.PushLS_Right: s.axisLS_X = name; break;
                case Step.PushRS_Up:    s.axisRS_Y = name; break;
                case Step.PushRS_Right: s.axisRS_X = name; break;
            }
        }

        private void SaveTriggerButton(int idx)
        {
            var s = CommandableMod.Settings;
            if (_currentStep == Step.PullLT) s.btnLT = idx;
            else s.btnRT = idx;
        }

        private void SaveTriggerAxis(string name)
        {
            var s = CommandableMod.Settings;
            if (_currentStep == Step.PullLT) s.axisTR_L = name;
            else s.axisTR_R = name;
        }

        private void Advance()
        {
            _debounce = Time.realtimeSinceStartup + DEBOUNCE_TIME;
            _currentStep++;
            _lastSeen = "";
        }

        private string GetPrompt()
        {
            return _currentStep switch
            {
                Step.Start      => "This wizard will map your controller.\nMake sure it is connected.",
                Step.PressA     => "Press the A / Cross button.",
                Step.PressB     => "Press the B / Circle button.",
                Step.PressX     => "Press the X / Square button.",
                Step.PressY     => "Press the Y / Triangle button.",
                Step.PressLB    => "Press the LEFT BUMPER (LB / L1).",
                Step.PressRB    => "Press the RIGHT BUMPER (RB / R1).",
                Step.PressBack  => "Press BACK / SELECT / Share.",
                Step.PressStart => "Press START / Options / Menu.",
                Step.PressL3    => "Click the LEFT STICK (L3).",
                Step.PressR3    => "Click the RIGHT STICK (R3).",
                Step.PushLS_Up    => "Push the LEFT STICK all the way UP.",
                Step.PushLS_Right => "Push the LEFT STICK all the way RIGHT.",
                Step.PushRS_Up    => "Push the RIGHT STICK all the way UP.",
                Step.PushRS_Right => "Push the RIGHT STICK all the way RIGHT.",
                Step.PullLT => "Pull the LEFT TRIGGER (or press LT button).",
                Step.PullRT => "Pull the RIGHT TRIGGER (or press RT button).",
                Step.Done => "",
                _ => ""
            };
        }
    }
}
