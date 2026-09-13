namespace ChooGuard.Foundation.Simulation
{
    /// <summary>Immutable encoding issued only from an already validated, privately owned crowd model.
    /// It grants no permission to accept another string or a mutable public snapshot.</summary>
    public sealed class CrowdCheckpointProof
    {
        public string Encoded { get; }
        internal CrowdCheckpointProof(string encoded) { Encoded = encoded; }
    }
}
