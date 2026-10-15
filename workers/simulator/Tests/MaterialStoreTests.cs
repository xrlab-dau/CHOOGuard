using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class MaterialStoreTests
    {
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Material store: "+reason);}
        static ProviderSelection Binding(string provider="alpha",string account="account-1",string lease="lease-1")=>ProviderSelection.Parse(new JObject{["provider_id"]=provider,["account_id"]=account,["model_id"]="same/model",["session_generation"]=lease,["catalog_version"]=new string('a',64),["selection_version"]=ProviderSelection.Version,["billing"]="user_subscription",["registration"]="own_app_public_oauth"});
        static World World(){var world=new World{Seed=17};var grid=new Entity{Id="grid",Kind="electrical"};grid.Tags.Add("electrical");grid.Set("power",1);world.Add(grid);return world;}
        static MaterialStoreKey Key(World world,ProviderSelection binding,string prompt="prompt-v1",string revision="pinned-v1")=>new MaterialStoreKey(InitialConditionMaterial.Applicability(world),binding.LogicalFingerprint,prompt,"initial-priors-v2",Stable.Hash("explicit-world-only-coverage-v1"),Stable.Hash("fixture-exact-wire-body"),revision);
        static MaterialStoreEntry Entry(World world,ProviderSelection binding,double lower=.1,string prompt="prompt-v1",string revision="pinned-v1")
        {return new MaterialStoreEntry(Key(world,binding,prompt,revision),InitialConditionMaterial.Create(world,new[]{new InitialConditionPrior("grid","insulation",lower,.9)},binding.LogicalFingerprint),binding.ToBindingRecord(),Stable.Hash("full-context"),Stable.Hash("source-reply"));}
        static MaterialStoreResult Await(Task<MaterialStoreResult> task)=>task.GetAwaiter().GetResult();
        static void Refuse(Action call,string reason){bool refused=false;try{call();}catch(ArgumentException){refused=true;}catch(InvalidOperationException){refused=true;}catch(OperationCanceledException){refused=true;}Check(refused,reason);}
        sealed class BoundProvider:IMaterialBoundProvider
        {
            readonly ProviderSelection binding=Binding(account:"fixture-"+Guid.NewGuid().ToString("N"));
            public int Calls;public bool Current=true;public EmergenceRequest Request;public TaskCompletionSource<EmergenceReply> Pending;
            public string Version=>binding.CacheVersion+":initial_world";public bool Available=>true;public long MaximumOutputTokens=>100;
            public string LogicalBindingFingerprint=>binding.LogicalFingerprint;public JObject LogicalBindingRecord=>binding.ToBindingRecord();public bool IsLeaseCurrent=>Current;
            public string ModelInputFingerprint(EmergenceRequest request)=>Stable.Hash(new JArray("bound-fixture-prompt-v1",Version,binding.ModelId,MaximumOutputTokens,request.Json).ToString(Newtonsoft.Json.Formatting.None));
            public long EstimateInputTokens(EmergenceRequest request)=>100;
            public Task<EmergenceReply> Generate(EmergenceRequest request,CancellationToken cancellation){Calls++;Request=request;return Pending==null?Task.FromResult(Reply()):Pending.Task;}
            public EmergenceReply Reply()=>new EmergenceReply{BasisDigest=Request.BasisDigest,Seed=Request.Seed,ReturnedModel=binding.ModelId,InputTokens=40,OutputTokens=10,Priors=new List<InitialConditionPrior>{new InitialConditionPrior("grid","insulation",.1,.9)},Raw=new JObject{["basis_digest"]=Request.BasisDigest,["seed"]=Request.Seed.ToString(),["priors"]=new JArray(new JObject{["entity_id"]="grid",["property"]="insulation",["min"]=.1,["max"]=.9})}.ToString()};
        }
        public static void Run()
        {
            var world=World();var binding=Binding();var entry=Entry(world,binding);var store=new InitialConditionMaterialStore();int calls=0;var budget=new DecisionBudget();
            Func<Task<MaterialStoreEntry>> factory=()=>{calls++;DecisionBudget.Reservation reservation;string reason;Check(budget.TryReserve(100,20,out reservation,out reason),"factory budget admitted once");budget.Settle(reservation,40,10);return Task.FromResult(entry);};
            var created=Await(store.GetOrCreateAsync(entry.Key,factory,()=>true,CancellationToken.None));var cached=Await(store.GetOrCreateAsync(entry.Key,factory,()=>true,CancellationToken.None));
            Check(!created.CacheHit&&cached.CacheHit&&!cached.JoinedInFlight&&calls==1&&budget.Requests==1&&budget.MeasuredInputTokens==40,"same pinned material reuses with zero additional upstream calls or budget reservations");
            var restored=MaterialStoreEntry.Parse(entry.Serialize());Check(restored.EntrySha256==entry.EntrySha256&&restored.Material.ContentSha256==entry.Material.ContentSha256&&!entry.Serialize().Contains("account-1")&&!entry.Serialize().Contains("lease-1"),"immutable export restores exact material and redacted logical provenance");
            var secondStore=new InitialConditionMaterialStore();secondStore.PutRecorded(restored);Check(secondStore.ReadRecorded(entry.Key,entry.EntrySha256).Material.ContentSha256==entry.Material.ContentSha256,"explicit saved ledger reuse requires exact key and entry hash");
            Refuse(()=>secondStore.ReadRecorded(entry.Key,new string('f',64)),"wrong recorded content hash fails closed");
            Refuse(()=>secondStore.PutRecorded(Entry(world,binding,.2)),"a new proposal cannot overwrite a pinned key");
            var revision=Entry(world,binding,.2,revision:"explicit-new-material-v2");secondStore.PutRecorded(revision);Check(secondStore.Count==2,"explicit revision permits a distinct immutable prior artifact");
            var renewed=Binding(lease:"lease-2");Check(Key(world,renewed).Fingerprint==entry.Key.Fingerprint,"auth lease renewal does not change logical material identity");
            world.Seed=18;Check(Key(world,binding).Fingerprint==entry.Key.Fingerprint,"structural applicability excludes seed while actual model input is independently required in the key");
            Check(Key(world,Binding("beta")).Fingerprint!=entry.Key.Fingerprint&&Key(world,Binding(account:"account-2")).Fingerprint!=entry.Key.Fingerprint&&Key(world,binding,"prompt-v2").Fingerprint!=entry.Key.Fingerprint,"provider account and prompt changes never alias");
            Check(new MaterialStoreKey(entry.Key.ApplicabilityFingerprint,entry.Key.BindingFingerprint,entry.Key.PromptVersion,entry.Key.ProposalContract,Stable.Hash("different-external-coverage"),entry.Key.ModelContextFingerprint).Fingerprint!=entry.Key.Fingerprint,"external visual identity/capability coverage changes never alias");
            Check(new MaterialStoreKey(entry.Key.ApplicabilityFingerprint,entry.Key.BindingFingerprint,entry.Key.PromptVersion,entry.Key.ProposalContract,entry.Key.CoverageFingerprint,Stable.Hash("different-exact-wire-body")).Fingerprint!=entry.Key.Fingerprint,"any actual full model input change is a material cache miss");
            var wrongWorld=World();wrongWorld.Entities["grid"].Tags.Add("source-input");Check(Key(wrongWorld,binding).Fingerprint!=entry.Key.Fingerprint,"source coverage/capability epoch change creates a distinct key");
            Refuse(()=>new MaterialStoreEntry(Key(wrongWorld,binding),entry.Material,binding.ToBindingRecord(),Stable.Hash("c"),Stable.Hash("r")),"material from stale context cannot be bound to a new source epoch");
            Refuse(()=>new MaterialStoreEntry(entry.Key,entry.Material,Binding("beta").ToBindingRecord(),Stable.Hash("c"),Stable.Hash("r")),"wrong provider provenance fails closed");
            var corrupt=JObject.Parse(entry.Serialize());corrupt["source_reply_sha256"]=new string('e',64);Refuse(()=>MaterialStoreEntry.Parse(corrupt.ToString()),"ledger mutation without matching entry hash rejected");
            corrupt=JObject.Parse(entry.Serialize());corrupt["binding"]["access_token"]="forbidden";Refuse(()=>MaterialStoreEntry.Parse(corrupt.ToString()),"secret fields are not admitted in material provenance");
            int before=calls;Refuse(()=>Await(store.GetOrCreateAsync(entry.Key,factory,()=>false,CancellationToken.None)),"cached material never bypasses revoked live selection consent");Check(calls==before,"revoked cached request makes zero upstream calls");
            store=new InitialConditionMaterialStore(maximumEntries:1);store.PutRecorded(entry);before=calls;Refuse(()=>Await(store.GetOrCreateAsync(revision.Key,factory,()=>true,CancellationToken.None)),"store cap checked before new provider factory");Check(calls==before,"store cap makes zero provider calls");
            SingleFlightAndLateResults(entry);
            ComposerMaterialReuse();
            BoundWireInputFingerprint();
        }
        static void BoundWireInputFingerprint()
        {
            var choice=new JObject{["provider_id"]="alpha",["account_id"]="wire-"+Guid.NewGuid().ToString("N"),["model_id"]="same/model",["session_generation"]="wire-lease-1",["catalog_version"]=new string('a',64),["selection_version"]=ProviderSelection.Version,["billing"]="user_subscription",["registration"]="own_app_public_oauth"};
            int calls=0;JObject sent=null;
            SubscriptionTransport transport=(method,url,capability,generation,body,cancellation)=>
            {
                if(method=="GET")return Task.FromResult(new ModelHttpReply{Status=200,Body=new JObject{["provider"]="alpha",["model"]="same/model",["session_generation"]=choice["session_generation"],["connected"]=true,["cloud_enabled"]=true,["selection"]=choice.DeepClone(),["inference_contracts"]=new JObject{["alpha"]=new string('a',64)},["providers"]=new JArray(new JObject{["id"]="alpha",["registered"]=true,["billing"]="user_subscription",["auth_modes"]=new JArray("browser_oauth"),["capabilities"]=new JArray("initial_world","npc_plan")})}.ToString()});
                calls++;sent=JObject.Parse(body);var source=JObject.Parse((string)sent["messages"][1]["content"]);
                var answer=new JObject{["basis_digest"]=source["basis_digest"],["seed"]=source["seed"],["priors"]=new JArray(new JObject{["entity_id"]="grid",["property"]="insulation",["min"]=.1,["max"]=.9})};
                return Task.FromResult(new ModelHttpReply{Status=200,Body=new JObject{["model"]="same/model",["usage"]=new JObject{["prompt_tokens"]=40,["completion_tokens"]=10},["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=answer.ToString()}})}.ToString()});
            };
            var descriptor=new JObject{["schema"]=1,["endpoint"]="http://127.0.0.1:18953",["capability"]=new string('c',48),["browser_url"]="http://127.0.0.1:18953/#"+new string('b',48)};
            var connection=SubscriptionConnection.ParseDescriptor(descriptor.ToString(),transport);var status=connection.Refresh(CancellationToken.None).GetAwaiter().GetResult();var session=connection.LockSelection(status);var provider=(IMaterialBoundProvider)session.CreateEmergenceProvider(100);
            var world=World();var request=EmergenceComposer.CreateRequest(world);var inputHash=provider.ModelInputFingerprint(request);var changed=World();changed.Seed++;
            Check(provider.ModelInputFingerprint(EmergenceComposer.CreateRequest(changed))!=inputHash,"actual initial prompt seed/basis change produces a different model input hash");
            changed=World();changed.Entities["grid"].Set("power",.7);Check(provider.ModelInputFingerprint(EmergenceComposer.CreateRequest(changed))!=inputHash,"actual numeric world input changes are never hidden to obtain a cache hit");
            var record=new EmergenceComposer(provider).GenerateAndApplyAsync(world,false,authorized:true).GetAwaiter().GetResult();
            var plain=(JObject)sent.DeepClone();plain.Remove("chooguard_selection");Check(Stable.Hash(plain.ToString(Newtonsoft.Json.Formatting.None))==inputHash,"cache model input fingerprint matches exact captured client prompt/output/model body before volatile lease injection");
            var key=MaterialStoreEntry.Parse(record.MaterialStoreEntryJson).Key;Check(key.ModelContextFingerprint==inputHash&&calls==1,"bound production generator stores that actual wire-input hash in the immutable key");
            choice["session_generation"]="wire-lease-2";status=connection.Refresh(CancellationToken.None).GetAwaiter().GetResult();var renewed=(IMaterialBoundProvider)connection.LockSelection(status).CreateEmergenceProvider(100);
            var renewedComposer=new EmergenceComposer(renewed);var cached=renewedComposer.GenerateAndApplyAsync(World(),false,authorized:true).GetAwaiter().GetResult();
            Check(cached.MaterialCacheHit&&calls==1&&renewedComposer.Budget.Requests==0&&!provider.IsLeaseCurrent&&renewed.IsLeaseCurrent,"renewed exact lease uses same logical prompt cache without reauthorizing the old lease or consuming model tokens");
            Check(renewed.ModelInputFingerprint(request)!=((IMaterialBoundProvider)connection.LockSelection(status).CreateEmergenceProvider(128)).ModelInputFingerprint(request),"changed output limit is part of actual model-input policy identity");
        }
        static void ComposerMaterialReuse()
        {
            var provider=new BoundProvider();var one=World();var firstComposer=new EmergenceComposer(provider);
            var first=firstComposer.GenerateAndApplyAsync(one,false,authorized:true).GetAwaiter().GetResult();
            Check(first.Disposition=="accepted"&&provider.Calls==1&&firstComposer.Budget.Requests==1&&!first.MaterialCacheHit&&!string.IsNullOrEmpty(first.MaterialStoreEntryJson),"bound production composer obtains and records one immutable model material");
            var same=World();var cacheComposer=new EmergenceComposer(provider);var reused=cacheComposer.GenerateAndApplyAsync(same,false,authorized:true).GetAwaiter().GetResult();
            Check(reused.Disposition=="accepted"&&reused.MaterialCacheHit&&provider.Calls==1&&cacheComposer.Budget.Requests==0&&one.Digest()==same.Digest(),"same seed/applicability/provider material composer reuse performs zero model calls or budget admissions");
            var another=World();another.Seed=18;cacheComposer=new EmergenceComposer(provider);var sampled=cacheComposer.GenerateAndApplyAsync(another,false,authorized:true).GetAwaiter().GetResult();
            Check(!sampled.MaterialCacheHit&&provider.Calls==2&&cacheComposer.Budget.Requests==1&&another.Entities["grid"].Get("insulation")!=one.Entities["grid"].Get("insulation"),"different seed changes the full prompt and creates a new source entry instead of an unsafe cache hit");
            var offline=World();offline.Seed=19;var savedMaterial=InitialConditionMaterial.Parse(first.MaterialJson);var localOnly=EmergenceComposer.MaterializeAndApply(offline,savedMaterial);
            Check(localOnly.Disposition=="accepted"&&provider.Calls==2&&localOnly.MaterialSha256==first.MaterialSha256,"explicit saved immutable material samples another eligible seed locally with zero new model calls");
            var replay=World();string reason;Check(EmergenceComposer.ValidateAndApplyRecorded(replay,reused,false,out reason)&&replay.Digest()==same.Digest(),"cached initial state record replays its saved material exactly without provider access");
            provider.Current=false;var revoked=World();string before=revoked.Digest();var blocked=new EmergenceComposer(provider).GenerateAndApplyAsync(revoked,false,authorized:true).GetAwaiter().GetResult();
            Check(blocked.Disposition=="blocked_provider_session_changed"&&revoked.Digest()==before&&provider.Calls==2,"cached composer material cannot bypass a revoked live account/model lease");
            provider=new BoundProvider{Pending=new TaskCompletionSource<EmergenceReply>()};var late=World();before=late.Digest();var composer=new EmergenceComposer(provider);var pending=composer.GenerateAndApplyAsync(late,false,authorized:true);provider.Current=false;provider.Pending.SetResult(provider.Reply());
            blocked=pending.GetAwaiter().GetResult();Check(blocked.Disposition.StartsWith("blocked_")&&late.Digest()==before&&composer.Budget.InFlight==0&&provider.Calls==1,"late selected-session change cannot commit an initial world material");
            provider=new BoundProvider{Pending=new TaskCompletionSource<EmergenceReply>()};late=World();before=late.Digest();composer=new EmergenceComposer(provider);
            using(var cancellation=new CancellationTokenSource())
            {
                pending=composer.GenerateAndApplyAsync(late,false,cancellation:cancellation.Token,authorized:true);cancellation.Cancel();blocked=pending.GetAwaiter().GetResult();provider.Pending.SetResult(provider.Reply());
                SpinWait.SpinUntil(()=>composer.Budget.InFlight==0,1000);Check(blocked.Disposition=="cancelled"&&late.Digest()==before&&composer.Budget.InFlight==0&&provider.Calls==1,"cancelled composer leaves its source reservation closed and world untouched after a late provider response");
            }
        }
        static void SingleFlightAndLateResults(MaterialStoreEntry entry)
        {
            var store=new InitialConditionMaterialStore();var pending=new TaskCompletionSource<MaterialStoreEntry>();int calls=0;
            Func<Task<MaterialStoreEntry>> factory=()=>{calls++;return pending.Task;};
            var first=store.GetOrCreateAsync(entry.Key,factory,()=>true,CancellationToken.None);var second=store.GetOrCreateAsync(entry.Key,factory,()=>true,CancellationToken.None);
            Check(calls==1&&store.InFlight==1,"concurrent same-key misses make exactly one upstream factory");pending.SetResult(entry);
            Check(!Await(first).CacheHit&&Await(second).JoinedInFlight&&store.Count==1&&store.InFlight==0,"singleflight waiters share only the validated immutable result");
            store=new InitialConditionMaterialStore();pending=new TaskCompletionSource<MaterialStoreEntry>();bool current=true;
            first=store.GetOrCreateAsync(entry.Key,factory,()=>current,CancellationToken.None);current=false;pending.SetResult(entry);Refuse(()=>Await(first),"late selected-model change refuses before material commit");Check(store.Count==0&&store.InFlight==0,"stale provider reply leaves no cached material");
            store=new InitialConditionMaterialStore();pending=new TaskCompletionSource<MaterialStoreEntry>();
            using(var cancellation=new CancellationTokenSource())
            {
                first=store.GetOrCreateAsync(entry.Key,factory,()=>true,cancellation.Token);cancellation.Cancel();Refuse(()=>Await(first),"owner cancellation refuses uncooperative pending factory");
                int before=calls;Check(store.InFlight==1,"cancelled uncooperative source remains counted until actual completion");
                using(var secondCancellation=new CancellationTokenSource())
                {var join=store.GetOrCreateAsync(entry.Key,factory,()=>true,secondCancellation.Token);secondCancellation.Cancel();Refuse(()=>Await(join),"another waiter can cancel without launching a second source factory");}
                Check(calls==before,"cancellation never frees a slot for duplicate concurrent upstream work");pending.SetResult(entry);
                SpinWait.SpinUntil(()=>store.InFlight==0,1000);Check(store.Count==0&&store.InFlight==0,"cancelled late completion closes source slot without populating ledger");
            }
            store=new InitialConditionMaterialStore();int failed=0;Refuse(()=>Await(store.GetOrCreateAsync(entry.Key,()=>{failed++;throw new InvalidOperationException("quota_stopped");},()=>true,CancellationToken.None)),"provider failure remains explicit without fallback");Check(store.Count==0&&store.InFlight==0&&failed==1,"failed singleflight releases slot without manufacturing successful cached material");
        }
    }
}
