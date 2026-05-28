namespace PeskyGizmo
{
    public struct GridNavState
    {
        public int Index;
        public int Count;
        public int Cols;
        public bool Wrap;

        public bool Up()    => Step(0, -1);
        public bool Down()  => Step(0, +1);
        public bool Left()  => Step(-1, 0);
        public bool Right() => Step(+1, 0);

        public bool HandleDpad()
        {
            if (ControllerState.DpadUpEdge)    return Up();
            if (ControllerState.DpadDownEdge)  return Down();
            if (ControllerState.DpadLeftEdge)  return Left();
            if (ControllerState.DpadRightEdge) return Right();
            return false;
        }

        public void Reset() => Index = 0;
        public void Clamp() => Index = UnityEngine.Mathf.Clamp(Index, 0, UnityEngine.Mathf.Max(0, Count - 1));

        private bool Step(int dx, int dy)
        {
            if (Count <= 0) return false;

            int rows = UnityEngine.Mathf.CeilToInt((float)Count / Cols);
            int row  = Index / Cols;
            int col  = Index % Cols;

            int newRow = row + dy;
            int newCol = col + dx;

            if (Wrap)
            {
                newRow = ((newRow % rows) + rows) % rows;
                newCol = ((newCol % Cols) + Cols) % Cols;
            }
            else
            {
                newRow = UnityEngine.Mathf.Clamp(newRow, 0, rows - 1);
                newCol = UnityEngine.Mathf.Clamp(newCol, 0, Cols - 1);
            }

            int newIndex = UnityEngine.Mathf.Clamp(newRow * Cols + newCol, 0, Count - 1);
            if (newIndex == Index) return false;
            Index = newIndex;
            HapticManager.UI_DpadNav();
            return true;
        }
    }
}
