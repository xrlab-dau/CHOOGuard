using System;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;

namespace ChooGuard.Application
{
    /// <summary>One bounded delivery attempt. Failure/cancellation leaves the durable lease for retry.
    /// Acknowledgement means transport acceptance only, never atomic simulation effect application.</summary>
    public sealed class OutboxDispatcher
    {
        private readonly IOutboxStore store;
        private readonly IOutboxTransport transport;
        public OutboxDispatcher(IOutboxStore store, IOutboxTransport transport)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }
        public async Task<bool> DispatchAsync(StableId runId, StableId ownerId, TimeSpan lease, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var delivery = await store.ClaimAsync(runId, ownerId, lease, cancellation).ConfigureAwait(false);
            if (delivery == null) return false;
            cancellation.ThrowIfCancellationRequested();
            var acknowledgement = await transport.DeliverAsync(delivery, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (!delivery.Identity.Matches(acknowledgement)) return false;
            return await store.AcknowledgeAsync(acknowledgement, cancellation).ConfigureAwait(false);
        }
    }
}
