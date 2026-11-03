using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public enum GamepadButton
    {
        A, B, X, Y,
        Dpad, DpadUp, DpadDown, DpadLeft, DpadRight,
        LB, RB, LBRB, LT, RT,
        L3, R3, Start, Back
    }

    [StaticConstructorOnStartup]
    public static class GamepadTex
    {
        // Generic D-Pad Textures
        public static readonly Texture2D Dpad = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Dpad_Alt");
        public static readonly Texture2D DpadUp = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Dpad_Up_Alt");
        public static readonly Texture2D DpadDown = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Dpad_Down_Alt");
        public static readonly Texture2D DpadLeft = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Dpad_Left_Alt");
        public static readonly Texture2D DpadRight = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Dpad_Right_Alt");

        // Xbox Textures
        public static readonly Texture2D XboxA = ContentFinder<Texture2D>.Get("UI/XB/T_X_A_White_Alt");
        public static readonly Texture2D XboxB = ContentFinder<Texture2D>.Get("UI/XB/T_X_B_White_Alt");
        public static readonly Texture2D XboxX = ContentFinder<Texture2D>.Get("UI/XB/T_X_X_White_Alt");
        public static readonly Texture2D XboxY = ContentFinder<Texture2D>.Get("UI/XB/T_X_Y_White_Alt");
        public static readonly Texture2D XboxLB = ContentFinder<Texture2D>.Get("UI/XB/T_X_LB_Alt");
        public static readonly Texture2D XboxRB = ContentFinder<Texture2D>.Get("UI/XB/T_X_RB_Alt");
        public static readonly Texture2D XboxLT = ContentFinder<Texture2D>.Get("UI/XB/T_X_LT_Alt");
        public static readonly Texture2D XboxRT = ContentFinder<Texture2D>.Get("UI/XB/T_X_RT_Alt");
        public static readonly Texture2D XboxL3 = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Left_Stick_Click_Alt");
        public static readonly Texture2D XboxR3 = ContentFinder<Texture2D>.Get("UI/Gen/T_X_Right_Stick_Click_Alt");
        public static readonly Texture2D XboxStart = ContentFinder<Texture2D>.Get("UI/XB/T_X_X_Alt");
        public static readonly Texture2D XboxBack = ContentFinder<Texture2D>.Get("UI/XB/T_X_Share_Alt");

        // PlayStation Textures
        public static readonly Texture2D PsCross = ContentFinder<Texture2D>.Get("UI/PS/T_P5_Cross_Alt");
        public static readonly Texture2D PsCircle = ContentFinder<Texture2D>.Get("UI/PS/T_P5_Circle_Alt");
        public static readonly Texture2D PsSquare = ContentFinder<Texture2D>.Get("UI/PS/T_P5_Square_Alt");
        public static readonly Texture2D PsTriangle = ContentFinder<Texture2D>.Get("UI/PS/T_P5_Triangle_Alt");
        public static readonly Texture2D PsL1 = ContentFinder<Texture2D>.Get("UI/PS/T_P5_L1_Alt");
        public static readonly Texture2D PsR1 = ContentFinder<Texture2D>.Get("UI/PS/T_P5_R1_Alt");
        public static readonly Texture2D PsL2 = ContentFinder<Texture2D>.Get("UI/PS/T_P5_L2_Alt");
        public static readonly Texture2D PsR2 = ContentFinder<Texture2D>.Get("UI/PS/T_P5_R2_Alt");
        public static readonly Texture2D PsL3 = ContentFinder<Texture2D>.Get("UI/Gen/T_P5_Left_Stick_3_Click_Alt_Alt-1");
        public static readonly Texture2D PsR3 = ContentFinder<Texture2D>.Get("UI/Gen/T_P5_Right_Stick_3_Click_Alt_Alt-1");
        public static readonly Texture2D PsStart = ContentFinder<Texture2D>.Get("UI/PS/T_P5_Options_Alt");
        public static readonly Texture2D PsBack = ContentFinder<Texture2D>.Get("UI/PS/T_P5_Share_Alt");

        public static Texture2D? GetTextureFor(GamepadButton btn)
        {
            bool isPS = ControllerState.CurrentGlyphSet == GlyphSet.PlayStation;

            switch (btn)
            {
                case GamepadButton.A: return isPS ? PsCross : XboxA;
                case GamepadButton.B: return isPS ? PsCircle : XboxB;
                case GamepadButton.X: return isPS ? PsSquare : XboxX;
                case GamepadButton.Y: return isPS ? PsTriangle : XboxY;

                case GamepadButton.Dpad: return Dpad;
                case GamepadButton.DpadUp: return DpadUp;
                case GamepadButton.DpadDown: return DpadDown;
                case GamepadButton.DpadLeft: return DpadLeft;
                case GamepadButton.DpadRight: return DpadRight;

                case GamepadButton.LB: return isPS ? PsL1 : XboxLB;
                case GamepadButton.RB: return isPS ? PsR1 : XboxRB;
                case GamepadButton.LT: return isPS ? PsL2 : XboxLT;
                case GamepadButton.RT: return isPS ? PsR2 : XboxRT;

                case GamepadButton.L3: return isPS ? PsL3 : XboxL3;
                case GamepadButton.R3: return isPS ? PsR3 : XboxR3;

                case GamepadButton.Start: return isPS ? PsStart : XboxStart;
                case GamepadButton.Back: return isPS ? PsBack : XboxBack;

                default: return null;
            }
        }
    }
}
