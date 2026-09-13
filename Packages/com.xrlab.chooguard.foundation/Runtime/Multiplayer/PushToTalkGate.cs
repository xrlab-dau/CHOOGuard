using System;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class PushToTalkGate
    {
        private int generation;
        private bool available;
        public bool Held { get; private set; }

        public void SetAvailability(bool authorized, bool connected, bool focused, bool inputEnabled)
        {
            available = authorized && connected && focused && inputEnabled;
            if (!available) Release();
        }
        public int Press()
        {
            generation++;
            Held = available;
            return generation;
        }
        public void Release() { generation++; Held = false; }
        public bool CanTransmit(int ticket) => available && Held && generation == ticket;

        public static void Stop(Action mute, Action stopCapture)
        {
            try { mute(); }
            finally { stopCapture(); }
        }
    }
}
