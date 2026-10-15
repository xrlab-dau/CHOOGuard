using System;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>A handover is a review of a specific observed snapshot. New information invalidates earlier acknowledgements.</summary>
    public sealed class HandoverChecklist
    {
        private string[] sections = Array.Empty<string>();
        private bool[] acknowledged = Array.Empty<bool>();
        public int Count => sections.Length;
        public bool Ready
        {
            get { if (Count == 0) return false; foreach (bool value in acknowledged) if (!value) return false; return true; }
        }
        public string Section(int index) => sections[index];
        public bool Reviewed(int index) => acknowledged[index];
        public bool Update(string[] next)
        {
            if (next == null || next.Length == 0) throw new ArgumentException("A handover needs review sections.");
            bool changed = sections.Length != next.Length;
            for (int i = 0; i < next.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(next[i])) throw new ArgumentException("Empty review section.");
                if (i >= sections.Length || !string.Equals(sections[i], next[i], StringComparison.Ordinal)) changed = true;
            }
            if (!changed) return false;
            sections = (string[])next.Clone();
            acknowledged = new bool[sections.Length];
            return true;
        }
        public void Review(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            acknowledged[index] = true;
        }
    }
}
