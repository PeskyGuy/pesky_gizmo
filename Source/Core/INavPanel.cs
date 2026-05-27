namespace PeskyGizmo
{
    public interface INavPanel
    {
        bool IsOpen { get; }
        bool HidesCursor { get; }
        void HandleInput();
        void Draw();
    }
}
