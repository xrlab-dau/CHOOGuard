// ARCHITECTURE CONTRACT SKETCH ONLY. Not compiled in Unity in this delivery.
// C# 7.3 subset; DTOs are defined by schemas and mapped to existing Foundation types.
// No UnityEngine, SQLite, worker vendor SDK or LLM SDK dependency.
namespace ChooGuard.Architecture.Contracts
{
    public interface ICommandPort
    {
        // Application thread; implementations enqueue to the single session owner.
        // Cancellation of caller waiting is NOT domain task cancellation.
        System.Threading.Tasks.Task<string> PreviewAsync(string intentJson);
        System.Threading.Tasks.Task<string> SubmitAsync(string intentJson);
        System.Threading.Tasks.Task<string> QueryReceiptAsync(string runId, string intentId);
    }
    public interface IOperationsReadPort
    {
        // Authenticated query scope; never return unrestricted WorldState.
        System.Threading.Tasks.Task<string> QueryAsync(string runId, string scope, long afterSequence);
    }
    public interface IAtomicCommitPort
    {
        // Must commit events + receipts + reservations + outbox before return.
        // Throws without publication on failure. No distributed transaction promise.
        void Commit(string transactionEnvelopeJson);
    }
    public interface IWorkerPort
    {
        System.Threading.Tasks.Task<string> GetCapabilitiesAsync();
        System.Threading.Tasks.Task<string> SubmitJobAsync(string envelopeJson);
        System.Threading.Tasks.Task<string> RequestCancelAsync(string jobId);
        System.Threading.Tasks.Task<string> QueryJobAsync(string jobId);
    }
    public interface IBranchPort
    {
        System.Threading.Tasks.Task<string> PrepareCheckpointAsync(string runId);
        System.Threading.Tasks.Task<string> ForkAsync(string checkpointId, string planRevision);
    }
    public interface IAuthoringPort
    {
        // Generates untrusted proposal only. No simulation or approval mutation.
        System.Threading.Tasks.Task<string> ProposeAsync(string evidencePackJson);
    }
}
