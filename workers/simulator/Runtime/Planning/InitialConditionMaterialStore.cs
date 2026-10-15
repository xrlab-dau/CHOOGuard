using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public interface IMaterialBoundProvider:IEmergenceProvider
    {
        string LogicalBindingFingerprint {get;}
        JObject LogicalBindingRecord {get;}
        bool IsLeaseCurrent {get;}
        string ModelInputFingerprint(EmergenceRequest request);
    }
    public sealed class MaterialStoreKey
    {
        public const string Schema="initial-material-key-v3";
        public string ApplicabilityFingerprint {get;}
        public string BindingFingerprint {get;}
        public string PromptVersion {get;}
        public string ProposalContract {get;}
        public string CoverageFingerprint {get;}
        public string ModelContextFingerprint {get;}
        public string Revision {get;}
        public string Fingerprint {get;}
        public MaterialStoreKey(string applicability,string binding,string prompt,string proposalContract,string coverageFingerprint,string modelContextFingerprint,string revision="pinned-v1")
        {
            RequireHash(applicability);RequireHash(binding);RequireHash(coverageFingerprint);RequireHash(modelContextFingerprint);
            foreach(string value in new[]{prompt,proposalContract,revision})if(string.IsNullOrEmpty(value)||value.Length>160||value.Any(char.IsControl))throw new ArgumentException("invalid_material_key_version");
            ApplicabilityFingerprint=applicability;BindingFingerprint=binding;PromptVersion=prompt;ProposalContract=proposalContract;CoverageFingerprint=coverageFingerprint;ModelContextFingerprint=modelContextFingerprint;Revision=revision;
            Fingerprint=Stable.Hash(ToToken().ToString(Formatting.None));
        }
        internal static void RequireHash(string value)
        {if(value==null||value.Length!=64||value.Any(c=>!((c>='0'&&c<='9')||(c>='a'&&c<='f'))))throw new ArgumentException("invalid_material_hash");}
        public JObject ToToken()=>new JObject{["schema"]=Schema,["applicability"]=ApplicabilityFingerprint,["binding"]=BindingFingerprint,["prompt"]=PromptVersion,["proposal_contract"]=ProposalContract,["coverage"]=CoverageFingerprint,["model_input"]=ModelContextFingerprint,["revision"]=Revision};
        public static MaterialStoreKey Parse(JToken token)
        {
            var value=token as JObject;string[] fields={"schema","applicability","binding","prompt","proposal_contract","coverage","model_input","revision"};
            if(value==null||value.Properties().Count()!=fields.Length||fields.Any(k=>value[k]?.Type!=JTokenType.String)||(string)value["schema"]!=Schema)throw new ArgumentException("invalid_material_store_key");
            return new MaterialStoreKey((string)value["applicability"],(string)value["binding"],(string)value["prompt"],(string)value["proposal_contract"],(string)value["coverage"],(string)value["model_input"],(string)value["revision"]);
        }
    }

    /// <summary>Immutable material plus credential-free provenance. Context/reply hashes are evidence,
    /// not reproducibility claims about a fresh external model response.</summary>
    public sealed class MaterialStoreEntry
    {
        public const string Schema="initial-material-entry-v1";
        public MaterialStoreKey Key {get;}
        public InitialConditionMaterial Material {get;}
        public string SourceContextSha256 {get;}
        public string SourceReplySha256 {get;}
        public string EntrySha256 {get;}
        readonly JObject binding;
        public JObject BindingRecord=>(JObject)binding.DeepClone();
        public MaterialStoreEntry(MaterialStoreKey key,InitialConditionMaterial material,JObject providerBinding,string contextSha,string replySha)
        {
            if(key==null||material==null)throw new ArgumentException("material_entry_required");
            binding=ProviderSelection.ValidateBindingRecord(providerBinding);MaterialStoreKey.RequireHash(contextSha);MaterialStoreKey.RequireHash(replySha);
            if(Stable.Hash(binding.ToString(Formatting.None))!=key.BindingFingerprint||material.ProviderBindingFingerprint!=key.BindingFingerprint||material.ApplicabilityFingerprint!=key.ApplicabilityFingerprint)throw new ArgumentException("material_provenance_mismatch");
            Key=key;Material=InitialConditionMaterial.Parse(material.Serialize());SourceContextSha256=contextSha;SourceReplySha256=replySha;
            EntrySha256=Stable.Hash(Payload().ToString(Formatting.None));
        }
        JObject Payload()=>new JObject{["schema"]=Schema,["key"]=Key.ToToken(),["material"]=Material.Serialize(),["binding"]=binding.DeepClone(),["source_context_sha256"]=SourceContextSha256,["source_reply_sha256"]=SourceReplySha256};
        public string Serialize(){var payload=Payload();payload["entry_sha256"]=EntrySha256;return payload.ToString(Formatting.None);}
        public static MaterialStoreEntry Parse(string json)
        {
            var value=JObject.Parse(json);string[] fields={"schema","key","material","binding","source_context_sha256","source_reply_sha256","entry_sha256"};
            if(value.Properties().Count()!=fields.Length||fields.Any(k=>value[k]==null)||(string)value["schema"]!=Schema||value["material"].Type!=JTokenType.String||value["source_context_sha256"].Type!=JTokenType.String||value["source_reply_sha256"].Type!=JTokenType.String||value["entry_sha256"].Type!=JTokenType.String)throw new ArgumentException("invalid_material_entry");
            var entry=new MaterialStoreEntry(MaterialStoreKey.Parse(value["key"]),InitialConditionMaterial.Parse((string)value["material"]),value["binding"] as JObject,(string)value["source_context_sha256"],(string)value["source_reply_sha256"]);
            if(entry.EntrySha256!=(string)value["entry_sha256"])throw new ArgumentException("material_entry_hash_mismatch");return entry;
        }
    }

    public sealed class MaterialStoreResult
    {
        public MaterialStoreEntry Entry {get;}
        public bool CacheHit {get;}
        public bool JoinedInFlight {get;}
        internal MaterialStoreResult(MaterialStoreEntry entry,bool hit,bool joined){Entry=entry;CacheHit=hit;JoinedInFlight=joined;}
    }

    /// <summary>Bounded process-local pinned material ledger. Exported immutable entries may be saved
    /// in a run record and explicitly restored; this class never reads credentials or files.
    /// Live cache hits still require the caller's current account/model consent lease.</summary>
    public sealed class InitialConditionMaterialStore
    {
        public static readonly InitialConditionMaterialStore Shared=new InitialConditionMaterialStore();
        readonly object gate=new object();
        readonly Dictionary<string,MaterialStoreEntry> entries=new Dictionary<string,MaterialStoreEntry>(StringComparer.Ordinal);
        readonly Dictionary<string,TaskCompletionSource<MaterialStoreEntry>> flights=new Dictionary<string,TaskCompletionSource<MaterialStoreEntry>>(StringComparer.Ordinal);
        readonly int maximumEntries,maximumInFlight;
        readonly long maximumBytes;long bytes;
        public int Count {get{lock(gate)return entries.Count;}}
        public int InFlight {get{lock(gate)return flights.Count;}}
        public InitialConditionMaterialStore(int maximumEntries=64,long maximumBytes=8388608,int maximumInFlight=1)
        {
            if(maximumEntries<1||maximumEntries>4096||maximumBytes<1||maximumBytes>67108864||maximumInFlight<1||maximumInFlight>8)throw new ArgumentException("invalid_material_store_limits");
            this.maximumEntries=maximumEntries;this.maximumBytes=maximumBytes;this.maximumInFlight=maximumInFlight;
        }
        public void PutRecorded(MaterialStoreEntry entry)
        {
            if(entry==null)throw new ArgumentException("material_entry_required");
            lock(gate)Put(entry);
        }
        void Put(MaterialStoreEntry entry)
        {
            MaterialStoreEntry existing;
            if(entries.TryGetValue(entry.Key.Fingerprint,out existing))
            {if(existing.EntrySha256!=entry.EntrySha256)throw new InvalidOperationException("immutable_material_key_collision");return;}
            long size=Encoding.UTF8.GetByteCount(entry.Serialize());
            if(size>524288||entries.Count>=maximumEntries||size>maximumBytes-bytes)throw new InvalidOperationException("material_store_budget_exceeded");
            entries.Add(entry.Key.Fingerprint,entry);bytes+=size;
        }
        public MaterialStoreEntry ReadRecorded(MaterialStoreKey key,string expectedEntrySha256)
        {
            if(key==null)throw new ArgumentException("material_store_key_required");MaterialStoreKey.RequireHash(expectedEntrySha256);
            lock(gate){MaterialStoreEntry entry;if(!entries.TryGetValue(key.Fingerprint,out entry)||entry.EntrySha256!=expectedEntrySha256)throw new InvalidOperationException("recorded_material_missing_or_mismatched");return entry;}
        }
        public async Task<MaterialStoreResult> GetOrCreateAsync(MaterialStoreKey key,Func<Task<MaterialStoreEntry>> factory,Func<bool> currentLease,CancellationToken cancellation)
        {
            if(key==null||factory==null||currentLease==null)throw new ArgumentException("material_admission_contract_required");
            CheckLease(currentLease,cancellation);TaskCompletionSource<MaterialStoreEntry> flight;bool owner=false;
            lock(gate)
            {
                CheckLease(currentLease,cancellation);MaterialStoreEntry cached;
                if(entries.TryGetValue(key.Fingerprint,out cached))return new MaterialStoreResult(cached,true,false);
                if(!flights.TryGetValue(key.Fingerprint,out flight))
                {
                    if(flights.Count>=maximumInFlight||entries.Count>=maximumEntries||bytes>=maximumBytes)throw new InvalidOperationException("material_store_budget_exceeded");
                    flight=new TaskCompletionSource<MaterialStoreEntry>(TaskCreationOptions.RunContinuationsAsynchronously);Observe(flight.Task);flights.Add(key.Fingerprint,flight);owner=true;
                }
            }
            if(owner){var production=Produce(key,flight,factory,currentLease,cancellation);Observe(production);}
            var entry=await WithCancellation(flight.Task,cancellation).ConfigureAwait(false);CheckLease(currentLease,cancellation);
            return new MaterialStoreResult(entry,!owner,!owner);
        }
        async Task Produce(MaterialStoreKey key,TaskCompletionSource<MaterialStoreEntry> flight,Func<Task<MaterialStoreEntry>> factory,Func<bool> currentLease,CancellationToken cancellation)
        {
            try
            {
                CheckLease(currentLease,cancellation);
                var task=factory();if(task==null)throw new InvalidOperationException("material_factory_task_required");
                // A cancelled caller returns promptly, but the source factory still owns its slot
                // until it actually completes. Never undercount an uncooperative upstream task.
                var entry=await task.ConfigureAwait(false);
                if(entry==null||entry.Key.Fingerprint!=key.Fingerprint)throw new InvalidOperationException("material_factory_key_mismatch");
                lock(gate){CheckLease(currentLease,cancellation);Put(entry);}
                flight.TrySetResult(entry);
            }
            catch(Exception error){flight.TrySetException(error);}
            finally{lock(gate){TaskCompletionSource<MaterialStoreEntry> current;if(flights.TryGetValue(key.Fingerprint,out current)&&ReferenceEquals(current,flight))flights.Remove(key.Fingerprint);}}
        }
        static void CheckLease(Func<bool> currentLease,CancellationToken cancellation)
        {cancellation.ThrowIfCancellationRequested();if(!currentLease())throw new InvalidOperationException("provider_session_changed");}
        static async Task<T> WithCancellation<T>(Task<T> task,CancellationToken cancellation)
        {
            if(task==null)throw new InvalidOperationException("material_factory_task_required");
            var cancelled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using(cancellation.Register(()=>cancelled.TrySetResult(true)))
            {if(await Task.WhenAny(task,cancelled.Task).ConfigureAwait(false)!=task){Observe(task);throw new OperationCanceledException(cancellation);}var result=await task.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return result;}
        }
        static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);}
    }
}
