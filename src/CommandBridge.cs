namespace ShortcutWindow
{
    public class CommandBridge
    {
        public bool IsPlaying { get; private set; } = true;

        public void Play()
        {
            IsPlaying = true;
            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            IsPlaying = false;
            PlayStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler PlayStateChanged;
    }
}
