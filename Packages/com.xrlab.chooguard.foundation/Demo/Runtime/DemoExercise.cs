using System;
using System.Collections.Generic;
using System.Linq;
namespace ChooGuard.Foundation.Demo
{
    [Serializable] public sealed class DemoDrillCatalog { public string disclaimer; public DemoDrill[] drills; }
    [Serializable] public sealed class DemoDrill { public string id; public string title; public string[] steps; }
    // Extra steps rehearse synthetic scene operations; never mutate role scoring or teammate work.
    public sealed class DemoExercise
    {
        private readonly string[] steps;
        public int Index { get; private set; }
        public IReadOnlyList<string> Steps { get { return Array.AsReadOnly(steps); } }
        public string Current { get { return ReadyForAssembly ? null : steps[Index]; } }
        public bool ReadyForAssembly { get { return Index == steps.Length; } }
        public DemoExercise(DemoDrill drill, string roleAnchor)
        {
            if (drill == null || string.IsNullOrWhiteSpace(drill.id) || string.IsNullOrWhiteSpace(roleAnchor) ||
                drill.steps == null || drill.steps.Length == 0 || drill.steps.Any(string.IsNullOrWhiteSpace) ||
                drill.steps.Distinct(StringComparer.Ordinal).Count() != drill.steps.Length || drill.steps.Last() != "assembly-register")
                throw new ArgumentException("A named drill with unique ordered steps ending in assembly-register is required.");
            steps = new[] { roleAnchor }.Concat(drill.steps.Where(x => x != roleAnchor)).ToArray();
        }
        public bool TryAdvance(string anchor)
        {
            if (ReadyForAssembly || anchor != Current) return false;
            Index++; return true;
        }
    }
}
