using System;

namespace PeskyGizmo
{
    public class NavPanelAdapter : INavPanel
    {
        private readonly Func<bool> _isOpen;
        private readonly Func<bool> _hidesCursor;
        private readonly Action     _handleInput;
        private readonly Action     _draw;

        public NavPanelAdapter(Func<bool> isOpen, Func<bool> hidesCursor,
                                Action handleInput, Action draw)
        {
            _isOpen       = isOpen;
            _hidesCursor  = hidesCursor;
            _handleInput  = handleInput;
            _draw         = draw;
        }

        public bool IsOpen      => _isOpen();
        public bool HidesCursor => _hidesCursor();
        public void HandleInput() => _handleInput();
        public void Draw()        => _draw();
    }
}
