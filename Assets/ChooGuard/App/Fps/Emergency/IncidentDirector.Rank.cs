using System.Collections.Generic;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Stable ranks for candidate lists. Candidates are listed again and again while the world runs, and JEV's rating
    /// belongs to a candidate as long as it stays the same one, so which people, places and things are picked must not be
    /// re-rolled on every listing. Every subject gets one rank per shift, drawn when it is first met from a random stream
    /// of its own (seeded from the world seed): lists sort and pick by rank, so the same subjects stay candidates while
    /// they qualify and a list changes only when the world does. Listing candidates never touches StationWorld.Random.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private System.Random rankRandom;
        private readonly Dictionary<string, int> thingRanks = new Dictionary<string, int>();
        private readonly Dictionary<int, int> personRanks = new Dictionary<int, int>();

        /// <summary>This shift's rank of a place, thing or zone (by its id), drawn once from the stream of its own.</summary>
        internal int Rank(string id)
        {
            rankRandom = rankRandom ?? new System.Random(world.Seed ^ 0x5eed1e);
            if (!thingRanks.TryGetValue(id, out int rank)) thingRanks[id] = rank = rankRandom.Next();
            return rank;
        }

        /// <summary>This shift's rank of a passenger, drawn once from the stream of its own.</summary>
        internal int Rank(Passenger person)
        {
            rankRandom = rankRandom ?? new System.Random(world.Seed ^ 0x5eed1e);
            if (!personRanks.TryGetValue(person.Number, out int rank)) personRanks[person.Number] = rank = rankRandom.Next();
            return rank;
        }
    }
}
