using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ChooGuard.Foundation
{
    // A deterministic display-state simulator; it does not perform another role's quest or procedure.
    public sealed class VirtualTeamSimulator : ITeamStateProvider
    {
        private readonly SortedDictionary<string, string> states;

        internal VirtualTeamSimulator(IEnumerable<string> roleIds)
        {
            states = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var roleId in roleIds) states.Add(roleId, "idle");
        }

        public IReadOnlyList<TeamRoleState> States
        {
            get
            {
                return new ReadOnlyCollection<TeamRoleState>(states
                    .Select(item => new TeamRoleState(item.Key, item.Value)).ToList());
            }
        }

        internal void ApplyValidated(IReadOnlyList<VirtualTeamEvent> events)
        {
            // Resolve all references before changing any state, even for an internal caller.
            foreach (var item in events)
                if (!states.ContainsKey(item.RoleId)) throw new ArgumentException("Unknown virtual team role.", nameof(events));
            foreach (var item in events) states[item.RoleId] = item.State;
        }
    }
}
